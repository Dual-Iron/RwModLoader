using Realm.Logging;
using System.Collections.ObjectModel;
using System.Reflection;
using UnityEngine;
using BepInEx.Preloader.Patching;
using MonoMod.RuntimeDetour;

namespace Realm.AssemblyLoading;

sealed class LoadedAssemblyPool
{
    private static readonly DetourModManager monomod = new();

    /// <summary>
    /// Loads the assemblies in <paramref name="asmPool"/>. Call <see cref="InitializeMods(IProgressable, Action{float})"/> to initialize them. Never calls <see cref="IDisposable.Dispose"/> on the assembly streams.
    /// </summary>
    public static LoadedAssemblyPool Load(IProgressable progressable, AssemblyPool asmPool)
    {
        LoadedAssemblyPool ret = new(asmPool);

        int tasksComplete = 0;

        void SetTaskProgress(float percent)
        {
            const float totalTasks = 3;

            progressable.Progress = Mathf.Lerp(tasksComplete / totalTasks, (tasksComplete + 1) / totalTasks, percent);
        }

        tasksComplete++;
        if (progressable.ProgressState == ProgressStateType.Failed) {
            return ret;
        }

        ret.LoadAssemblies(progressable, SetTaskProgress);

        return ret;
    }

    private readonly List<LoadedModAssembly> loadedAssemblies = new();

    public AssemblyPool Pool { get; }
    public ReadOnlyCollection<LoadedModAssembly> LoadedAssemblies { get; }

    private LoadedAssemblyPool(AssemblyPool assemblies)
    {
        Pool = assemblies;
        LoadedAssemblies = new(loadedAssemblies);
    }

    public void Unload(IProgressable progressable)
    {
        int complete = 0;
        int count = loadedAssemblies.Count;

        foreach (var loadedAsmKvp in loadedAssemblies) {
            try {
                Pool[loadedAsmKvp.AsmName].Descriptor.Unload();
            } catch (Exception e) {
                progressable.Message(MessageType.Fatal, $"Failed to unload {loadedAsmKvp.AsmName}\n{e}");
            } finally {
                monomod.Unload(loadedAsmKvp.Asm);
            }

            progressable.Progress = ++complete / (float)count;
        }

        loadedAssemblies.Clear();
        VirtualEnums.VirtualEnumApi.Clear();
    }

    private void LoadAssemblies(IProgressable progressable, Action<float> setTaskProgress)
    {
        IEnumerable<ModAssembly> GetDependencies(ModAssembly asm)
        {
            foreach (var module in asm.AsmDef.Modules)
                foreach (var reference in module.AssemblyReferences)
                    if (Pool.TryGetAssembly(reference.Name, out var item))
                        yield return item;
        }

        // Sort assemblies by their dependencies
        IEnumerable<ModAssembly> sortedAssemblies = Pool.Assemblies.TopologicalSort(GetDependencies);

        int total = Pool.Count;
        int finished = 0;

        foreach (var asm in sortedAssemblies) {

            // Update assembly references
            foreach (var module in asm.AsmDef.Modules)
                foreach (var reference in module.AssemblyReferences)
                    if (Pool.TryGetAssembly(reference.Name, out var asmRefAsm)) {
                        reference.Name = asmRefAsm.AsmDef.Name.Name;
                    }

            string name = asm.OriginalAssemblyName;

            // Load assemblies
            using MemoryStream ms = new();
            asm.AsmDef.Write(ms);

            try {
                loadedAssemblies.Add(new(Assembly.Load(ms.ToArray()), name, asm.FileName));
            } catch (Exception e) {
                progressable.Message(MessageType.Fatal, $"Failed to load {name}\n{e}");
            }

            setTaskProgress(++finished / (float)total);
        }
    }

    public void InitializeMods(IProgressable progressable)
    {
        foreach (var loadedAsm in loadedAssemblies) {
            VirtualEnums.VirtualEnumApi.UseAssembly(loadedAsm.Asm, out var err);

            if (err != null) {
                foreach (var e in err.LoaderExceptions) {
                    progressable.Message(MessageType.Debug, e.ToString());
                }
                progressable.Message(MessageType.Fatal, $"Failed to register enums for {loadedAsm.AsmName}, exception details logged\n\nThis is usually because the mod is missing a dependency. Did you forget to download or enable a mod?");
            }
        }

        if (progressable.ProgressState == ProgressStateType.Failed) return;

        StaticFixes.PreLoad();

        int total = loadedAssemblies.Count;
        int finished = 0;

        // Load mods one-by-one
        foreach (var lasm in loadedAssemblies) {
            ModAssembly modAssembly = Pool[lasm.AsmName];
            Assembly loadedModAssembly = lasm.Asm;

            try {
                modAssembly.Descriptor.Initialize(loadedModAssembly);
                progressable.Message(MessageType.Debug, $"Finished loading {lasm.AsmName}");
            } catch (FileNotFoundException e) when (BepInEx.Utility.TryParseAssemblyName(e.FileName, out AssemblyName name)) {
                progressable.Message(MessageType.Fatal, $"The mod {lasm.AsmName} v{lasm.Asm.GetName().Version.ToString(2)} is missing a dependency: {name.Name} v{name.Version.ToString(2)}");
            } catch (Exception e) {
                progressable.Message(MessageType.Fatal, $"Failed to initialize {lasm.AsmName}\n{e}");
            }

            progressable.Progress = ++finished / (float)total;
        }

        StaticFixes.PostLoad();
    }
}
