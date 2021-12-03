using BepInEx.Preloader;
using Realm.Logging;

namespace Realm.ModLoading;

static class PluginWrapper
{
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
                MutatorProcess proc = MutatorProcess.Execute($"-w \"{pluginFile}\"");

                if (proc.ExitCode == 0) {
                    if (proc.Output.Length > 0)
                        wrappedMods.Add(proc.Output);

                    progressable.Message(MessageType.Info, $"Wrapped {Path.GetFileName(pluginFile)}.");
                }
                else {
                    progressable.Message(MessageType.Fatal, $"Failed to wrap {Path.GetFileName(pluginFile)}. {proc}");
                }
            }
            catch { }
        }

        if (progressable.ProgressState == ProgressStateType.Failed) {
            return;
        }

        foreach (var pluginFile in pluginFiles) {
            File.Delete(pluginFile);
        }
    }
}
