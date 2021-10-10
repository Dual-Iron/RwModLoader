using BepInEx.Preloader;
using Realm.Logging;
using System.Reflection;

namespace Realm.ModLoading;

public static class PluginWrapper
{
    public static void WrapPluginsThenSave(IProgressable progressable)
    {
        WrapPlugins(progressable, out var wrappedAsms);

        if (progressable.ProgressState == ProgressStateType.Failed) return;

        State.Instance.Prefs.EnableThenSave(wrappedAsms);
    }

    public static void WrapPlugins(IProgressable progressable, out List<string> wrappedMods)
    {
        // IMPORTANT: Do not reference BepInEx, Assembly-CSharp, or UnityEngine in this method. Otherwise, BepInEx won't run the chainloader and Realm won't start.
        // This is why we can't use BepInEx.Paths.PluginPath and why RealmUtils and RealmPaths are separate types.

        wrappedMods = new();

        FileInfo preloaderFile = new(Path.GetFullPath(EnvVars.DOORSTOP_INVOKE_DLL_PATH));

        string pluginPath = preloaderFile.Directory.Parent.CreateSubdirectory("plugins").FullName;

        if (!Directory.Exists(pluginPath)) {
            return;
        }

        string[] pluginFiles = Directory.GetFiles(pluginPath, "*.dll", SearchOption.TopDirectoryOnly);

        if (pluginFiles.Length == 0) {
            return;
        }

        foreach (string pluginFile in pluginFiles) {
            try {
                string name = AssemblyName.GetAssemblyName(pluginFile).Name;

                Execution exec = Execution.Run(RealmPaths.MutatorPath, $"--wrap \"{name}\" \"{pluginFile}\"");

                if (exec.ExitCode == 0) {
                    wrappedMods.Add(name);

                    progressable.Message(MessageType.Info, $"Wrapped {Path.GetFileName(pluginFile)}.");
                } else {
                    progressable.Message(MessageType.Fatal, $"Failed to wrap {Path.GetFileName(pluginFile)}. {exec.ExitMessage}: {exec.Error}.");
                }
            } catch { }
        }

        if (progressable.ProgressState == ProgressStateType.Failed) {
            return;
        }

        foreach (var pluginFile in pluginFiles) {
            File.Delete(pluginFile);
        }
    }
}
