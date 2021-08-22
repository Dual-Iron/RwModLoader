using BepInEx;
using Realm.AssemblyLoading;
using Realm.Logging;
using System.IO;
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

            ProcessResult result = ProcessResult.From(Extensions.MutatorPath, args.ToString());

            if (result.ExitCode != 0) {
                progressable.Message(MessageType.Diagnostic, result.Error);
                progressable.Message(MessageType.Fatal, $"{result.ExitMessage}: {result.Output}");
            }
        }

        private RwmodPool? rwmods;
        private LoadedAssemblyPool? loadedAsms;

        public void Refresh(IProgressable progressable)
        {
            progressable.Message(MessageType.Info, "Patching assemblies");
            Patch(progressable, PluginFiles);

            progressable.Message(MessageType.Info, "Fetching mods");
            rwmods = RwmodPool.Fetch();
        }

        public void Unload(IProgressable progressable)
        {
            if (loadedAsms != null) {
                loadedAsms.Unload(progressable);
                loadedAsms = null;
            }

            rwmods?.RestoreAll();
            rwmods = null;
        }

        public void Load(IProgressable progressable)
        {
            Unload(progressable);
            Refresh(progressable);

            progressable.Message(MessageType.Info, "Unwrapping mods");

            ProcessResult pr = rwmods!.UnwrapAll();

            if (pr.ExitCode != 0) {
                progressable.Message(MessageType.Diagnostic, pr.Error);
                progressable.Message(MessageType.Fatal, $"{pr.ExitMessage}: {pr.Output}");
                return;
            }

            progressable.Message(MessageType.Info, "Reading assemblies");
            AssemblyPool asms = AssemblyPool.Read(progressable, PluginFiles);

            progressable.Message(MessageType.Info, "Loading assemblies");
            loadedAsms = LoadedAssemblyPool.Load(progressable, asms);
        }
    }
}