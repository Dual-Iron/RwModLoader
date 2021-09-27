using Realm.AssemblyLoading;
using Realm.Logging;

namespace Realm.ModLoading;

public sealed class ModLoader
{
    private RwmodFile[] rwmods = new RwmodFile[0];

    public IEnumerable<RwmodFile> AllRwmods => rwmods;
    public AssemblyPool? AssemblyPool { get; private set; }
    public LoadedAssemblyPool? LoadedAssemblyPool { get; private set; }

    public void Refresh(IProgressable progressable)
    {
        progressable.Message(MessageType.Info, "Fetching mods");

        foreach (var mod in rwmods) {
            mod.Stream.Dispose();
        }

        rwmods = RwmodFile.GetRwmodFiles();
    }

    public void Unload(IProgressable progressable)
    {
        progressable.Message(MessageType.Info, "Unloading assemblies");

        LoadedAssemblyPool?.Unload(progressable);
        LoadedAssemblyPool = null;

        AssemblyPool?.Dispose();
        AssemblyPool = null;

        foreach (var mod in rwmods) {
            mod.Stream.Dispose();
        }

        rwmods = new RwmodFile[0];
    }

    public void Reload(IProgressable progressable)
    {
        Unload(progressable);
        Refresh(progressable);

        List<RwmodFile> plugins = new();

        foreach (var rwmod in rwmods) {
            if ((rwmod.Header.Flags & RwmodFlags.Mod) != 0 && ProgramState.Current.Prefs.EnabledMods.Contains(rwmod.Header.Name)) {
                plugins.Add(rwmod);
            }
        }

        progressable.Message(MessageType.Info, "Reading assemblies");
        AssemblyPool = AssemblyPool.Read(progressable, plugins);

        progressable.Message(MessageType.Info, "Loading assemblies");
        LoadedAssemblyPool = LoadedAssemblyPool.Load(progressable, AssemblyPool);
    }
}
