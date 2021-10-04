using BepInEx;
using Realm.AssemblyLoading;
using Realm.Logging;
using System.Reflection;

namespace Realm.ModLoading;

public sealed class ModLoader
{
    public LoadedAssemblyPool? LoadedAssemblyPool { get; private set; }

    public void Unload(IProgressable progressable)
    {
        progressable.Message(MessageType.Info, "Unloading assemblies");

        LoadedAssemblyPool?.Unload(progressable);
        LoadedAssemblyPool = null;
    }

    public void Reload(IProgressable progressable)
    {
        Unload(progressable);

        if (Directory.Exists(Paths.PluginPath)) {
            bool wrapped = false;

            string[] pluginFiles = Directory.GetFiles(Paths.PluginPath, "*.dll", SearchOption.TopDirectoryOnly);

            foreach (string pluginFile in pluginFiles) {
                try {
                    AssemblyName asmName = AssemblyName.GetAssemblyName(pluginFile);
                    Execution exec = Execution.Run(Extensions.MutatorPath, $"--wrap \"\" \"{pluginFile}\"");

                    if (exec.ExitCode == 0) {
                        wrapped = true;
                        ProgramState.Instance.Prefs.EnabledMods.Add(asmName.Name);
                        progressable.Message(MessageType.Info, $"Wrapped {Path.GetFileName(pluginFile)}.");
                    } else {
                        progressable.Message(MessageType.Fatal, $"Failed to wrap {Path.GetFileName(pluginFile)}. {exec.ExitMessage}: {exec.Error}.");
                    }
                } catch { }
            }

            if (progressable.ProgressState == ProgressStateType.Failed) return;

            if (wrapped) {
                ProgramState.Instance.Prefs.Save();

                foreach (var pluginFile in pluginFiles) {
                    File.Delete(pluginFile);
                }
            }
        }

        RwmodFile[] rwmods = RwmodFile.GetRwmodFiles();

        List<RwmodFile> plugins = new();

        foreach (var rwmod in rwmods) {
            if (ProgramState.Instance.Prefs.EnabledMods.Contains(rwmod.Header.Name)) {
                plugins.Add(rwmod);
            }
        }

        progressable.Message(MessageType.Info, "Reading assemblies");
        
        AssemblyPool assemblyPool = AssemblyPool.Read(progressable, plugins);

        if (progressable.ProgressState == ProgressStateType.Failed) goto Ret;
        progressable.Message(MessageType.Info, "Loading assemblies");
        progressable.Progress = 0;

        LoadedAssemblyPool = LoadedAssemblyPool.Load(progressable, assemblyPool);

        if (progressable.ProgressState == ProgressStateType.Failed) goto Ret;
        progressable.Message(MessageType.Info, "Initializing mods");
        progressable.Progress = 0;

        LoadedAssemblyPool.InitializeMods(progressable);

        Ret:
        foreach (var rwmod in rwmods) {
            rwmod.Stream.Dispose();
        }
    }
}
