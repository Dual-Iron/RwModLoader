using Realm.AssemblyLoading;
using Realm.Logging;

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

        RwmodFile[] rwmods = RwmodFile.GetRwmodFiles();

        List<RwmodFile> plugins = new();

        foreach (var rwmod in rwmods) {
            if (ProgramState.Instance.Prefs.EnabledMods.Contains(rwmod.Header.Name)) {
                plugins.Add(rwmod);
            }
        }

        progressable.Message(MessageType.Info, "Reading assemblies");
        AssemblyPool assemblyPool = AssemblyPool.Read(progressable, plugins);

        progressable.Message(MessageType.Info, "Loading assemblies");
        LoadedAssemblyPool = LoadedAssemblyPool.Load(progressable, assemblyPool);

        foreach (var rwmod in rwmods) {
            rwmod.Stream.Dispose();
        }
    }
}
