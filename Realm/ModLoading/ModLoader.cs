using Realm.AssemblyLoading;
using Realm.Logging;
using System.Threading;

namespace Realm.ModLoading;

sealed class ModLoader
{
    private readonly List<WeakReference> hangingMods = new();

    public LoadedAssemblyPool? LoadedAssemblyPool { get; private set; }

    public void WarnHangingMods(IProgressable progressable)
    {
        Thread.MemoryBarrier();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        List<string> names = new();

        foreach (var weakRef in hangingMods) {
            if (weakRef.Target is object mod) {
                names.Add(mod.GetType().ToString());
            }
        }

        if (names.Count > 0) {
            progressable.Message(MessageType.Warning, $"The following mods aren't fully unloaded: [{string.Join(", ", names.ToArray())}]");
        }
    }

    public void Reload(IProgressable progressable)
    {
        List<ModReloadState> reloadState = GetReloadState(progressable);

        Unload(progressable);

        if (progressable.ProgressState == ProgressStateType.Failed) return;

        Load(progressable, reloadState);
    }

    private void Unload(IProgressable progressable)
    {
        progressable.Message(MessageType.Info, "Disabling mods");

        if (LoadedAssemblyPool == null) return;

        hangingMods.Clear();

        foreach (var asm in LoadedAssemblyPool.Pool.Assemblies) {
            hangingMods.AddRange(asm.Descriptor.GetModObjects().Select(m => new WeakReference(m)));
        }

        LoadedAssemblyPool.Unload(progressable);
        LoadedAssemblyPool = null;
    }

    private void Load(IProgressable progressable, List<ModReloadState> reloadState)
    {
        PluginWrapper.WrapPlugins(progressable, out var wrappedAsms);

        if (progressable.ProgressState == ProgressStateType.Failed) return;

        if (wrappedAsms.Count > 0) {
            State.Instance.Prefs.Enable(wrappedAsms);
            State.Instance.Prefs.Save();
        }

        progressable.Message(MessageType.Info, "Reading assemblies");

        List<RwmodFile> plugins = new();

        // DO NOT inline this variable.
        RwmodFile[] rwmods = RwmodFile.GetRwmodFiles();

        using Disposable disposeStreams = new(() => { foreach (var r in rwmods) r.Stream.Dispose(); });

        foreach (var rwmod in rwmods) {
            if (State.Instance.Prefs.EnabledMods.Contains(rwmod.Header.Name)) {
                plugins.Add(rwmod);
            }
        }

        AssemblyPool assemblyPool = AssemblyPool.Read(progressable, plugins);

        if (progressable.ProgressState == ProgressStateType.Failed) return;
        progressable.Message(MessageType.Info, "Loading assemblies");
        progressable.Progress = 0;

        LoadedAssemblyPool = LoadedAssemblyPool.Load(progressable, assemblyPool);

        if (progressable.ProgressState == ProgressStateType.Failed) return;
        progressable.Message(MessageType.Info, "Enabling mods");
        progressable.Progress = 0;

        LoadedAssemblyPool.InitializeMods(progressable);

        if (progressable.ProgressState == ProgressStateType.Failed) return;

        // Call Reload after the mods are loaded
        // This ensures they have an instance to pass the state to
        CallReload(progressable, reloadState);
    }

    private void CallReload(IProgressable progressable, List<ModReloadState> reloadState)
    {
        foreach (var state in reloadState) {
            LoadedModAssembly? newMod = LoadedAssemblyPool!.LoadedAssemblies.FirstOrDefault(m => m.AsmName == state.AsmName);

            if (newMod != null) {
                try {
                    LoadedAssemblyPool.Pool[state.AsmName].Descriptor.SetUnloadState(state.ModData);
                } catch (Exception e) {
                    progressable.Message(MessageType.Fatal, $"An uncaught exception was thrown in {newMod.AsmName}. {e}");
                }
            }
        }
    }

    private List<ModReloadState> GetReloadState(IProgressable progressable)
    {
        if (LoadedAssemblyPool == null) {
            return new();
        }

        List<ModReloadState> ret = new();

        foreach (var asm in LoadedAssemblyPool.LoadedAssemblies) {
            try {
                var state = LoadedAssemblyPool.Pool[asm.AsmName].Descriptor.GetReloadState();
                if (state is not null) {
                    ret.Add(new() { AsmName = asm.AsmName, ModData = state.Value });
                }
            } catch (Exception e) {
                progressable.Message(MessageType.Fatal, $"An uncaught exception was thrown in {asm.AsmName}. {e}");
            }
        }

        return ret;
    }
}

struct ModReloadState
{
    public string AsmName;
    public object? ModData;
}
