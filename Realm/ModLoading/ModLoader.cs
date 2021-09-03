using BepInEx;
using Realm.AssemblyLoading;
using Realm.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Realm.ModLoading
{
    public sealed class ModLoader
    {
        private static string[] PluginFiles => Directory.GetFiles(Directory.CreateDirectory(Paths.PluginPath).FullName, "*.dll", SearchOption.TopDirectoryOnly);

        private static void Patch(IProgressable progressable, string[] files)
        {
            StringBuilder args = new("--parallel");

            foreach (var file in files) {
                args.Append($" --patchup \"{file}\"");
            }

            Execution result = Execution.Run(Extensions.MutatorPath, args.ToString());

            if (result.ExitCode != 0) {
                progressable.Message(MessageType.Diagnostic, result.Error);
                progressable.Message(MessageType.Fatal, $"Couldn't patch. {result.ExitMessage}: {result.Output}");
            }
        }

        private RwmodFile[] mods = new RwmodFile[0];

        public IEnumerable<RwmodFile> AllRwmods => mods;
        public AssemblyPool? AssemblyPool { get; private set; }
        public LoadedAssemblyPool? LoadedAssemblyPool { get; private set; }

        public void Refresh(IProgressable progressable)
        {
            progressable.Message(MessageType.Info, "Patching assemblies");
            Patch(progressable, PluginFiles);

            progressable.Message(MessageType.Info, "Fetching mods");
            mods = RwmodFile.FetchAll();
        }

        public void Unload(IProgressable progressable)
        {
            progressable.Message(MessageType.Info, "Unloading assemblies");

            LoadedAssemblyPool?.Unload(progressable);
            LoadedAssemblyPool = null;

            AssemblyPool?.Dispose();
            AssemblyPool = null;

            progressable.Message(MessageType.Info, "Restoring mods");

            StringBuilder args = new();

            foreach (RwmodFile rwmodFile in mods) {
                args.Append($" --restore \"{rwmodFile.FilePath}\"");
            }

            Execution pr = Execution.Run(Extensions.MutatorPath, args.ToString());

            if (pr.ExitCode != 0) {
                progressable.Message(MessageType.Diagnostic, pr.Error);
                progressable.Message(MessageType.Fatal, $"Couldn't unload. {pr.ExitMessage}: {pr.Output}");
                return;
            }

            mods = new RwmodFile[0];
        }

        public void Reload(IProgressable progressable)
        {
            Unload(progressable);
            Refresh(progressable);

            IEnumerable<RwmodFile> enabledRwmods = mods.Where(rwmf => ProgramState.Current.Prefs.EnabledMods.Contains(rwmf.Name));

            bool safeDependencies = true;

            // TODO HIGH: prevent circular dependencies
            HashSet<string> modNames = new(enabledRwmods.Select(m => m.Name));

            foreach (var mod in enabledRwmods) {
                foreach (var dep in mod.ModDependencies.Dependencies) {
                    if (!modNames.Contains(dep)) {
                        progressable.Message(MessageType.Fatal, $"{mod.Name} is missing a dependency: {dep}.");
                        safeDependencies = false;
                    }
                }
            }

            if (!safeDependencies) {
                return;
            }

            progressable.Message(MessageType.Info, "Unwrapping mods");

            StringBuilder args = new();

            foreach (RwmodFile rwmodFile in enabledRwmods) {
                args.Append($" --unwrap \"{rwmodFile.FilePath}\"");
            }

            if (args.Length == 0) {
                return;
            }

            Execution pr = Execution.Run(Extensions.MutatorPath, args.ToString());

            if (pr.ExitCode != 0) {
                progressable.Message(MessageType.Diagnostic, pr.Error);
                progressable.Message(MessageType.Fatal, $"Couldn't load. {pr.ExitMessage}: {pr.Output}");
                return;
            }

            progressable.Message(MessageType.Info, "Reading assemblies");
            AssemblyPool = AssemblyPool.Read(progressable, PluginFiles);

            progressable.Message(MessageType.Info, "Loading assemblies");
            LoadedAssemblyPool = LoadedAssemblyPool.Load(progressable, AssemblyPool);
        }

        public void ReloadJustAssemblies(IProgressable progressable)
        {
            progressable.Message(MessageType.Info, "Unloading assemblies");

            LoadedAssemblyPool?.Unload(progressable);
            LoadedAssemblyPool = null;

            AssemblyPool?.Dispose();
            AssemblyPool = null;

            progressable.Message(MessageType.Info, "Patching assemblies");
            Patch(progressable, PluginFiles);

            progressable.Message(MessageType.Info, "Reading assemblies");
            AssemblyPool = AssemblyPool.Read(progressable, PluginFiles);

            progressable.Message(MessageType.Info, "Loading assemblies");
            LoadedAssemblyPool = LoadedAssemblyPool.Load(progressable, AssemblyPool);
        }
    }
}