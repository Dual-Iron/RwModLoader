using BepInEx.Preloader.Patching;
using Realm.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Realm.ModLoading
{
    public sealed class LoadedAssemblyPool
    {
        public static LoadedAssemblyPool Load(IProgressable progressable, AssemblyPool asmPool)
        {
            LoadedAssemblyPool ret = new(asmPool);

            int tasksComplete = 0;

            void SetTaskProgress(float percent)
            {
                const float totalTasks = 3;

                progressable.Progress = Mathf.Lerp(tasksComplete / totalTasks, (tasksComplete + 1) / totalTasks, percent);
            }

            ret.RunPatchers(progressable, SetTaskProgress);

            if (progressable.ProgressState == ProgressStateType.Failed) return ret;

            ret.LoadAssemblies(progressable, SetTaskProgress);

            if (progressable.ProgressState == ProgressStateType.Failed) return ret;

            ret.InitializeMods(progressable, SetTaskProgress);

            return ret;
        }

        private readonly Dictionary<string, Assembly> loadedAssembliesByName = new();

        public AssemblyPool AssemblyPool { get; }

        private LoadedAssemblyPool(AssemblyPool assemblies)
        {
            AssemblyPool = assemblies;
        }

        public IEnumerable<string> LoadedAssemblyNames => loadedAssembliesByName.Keys;

        public bool TryGetLoadedAssembly(string name, [MaybeNullWhen(false)] out Assembly asm)
        {
            return loadedAssembliesByName.TryGetValue(name, out asm);
        }

        private void RunPatchers(IProgressable progressable, Action<float> setTaskProgress)
        {
            List<PatcherPlugin> patchers = AssemblyPatcher.PatcherPlugins.ToList();

            if (patchers.Count == 0) {
                setTaskProgress(1);
                return;
            }

            // Remove ChainLoader patcher
            patchers.RemoveAt(0);

            // Remove self
            patchers.RemoveAll(pp => pp.TypeName == typeof(EntryPoint).FullName);

            // Initialize()
            foreach (var patcher in patchers) {
                try {
                    patcher.Initializer?.Invoke();
                } catch (Exception e) {
                    progressable.Message(MessageType.Fatal, $"Patcher {patcher.TypeName} failed to initialize: {e.Message}");
                }
            }

            setTaskProgress(1 / 3f);

            var assembliesByFile = AssemblyPool.Assemblies.ToDictionary(asm => asm.FileName);

            // Patch(ref AssemblyDefinition)
            foreach (var patcher in patchers) {
                // TargetDLLs
                IEnumerable<string> targetDLLs;
                try {
                    targetDLLs = patcher.TargetDLLs();
                } catch (Exception e) {
                    progressable.Message(MessageType.Fatal, $"Patcher {patcher.TypeName} failed in TargetDLLS: {e.Message}");
                    continue;
                }

                if (targetDLLs == null) continue;

                foreach (var targetDLL in targetDLLs)
                    if (assembliesByFile.TryGetValue(targetDLL, out var modAsm)) {
                        try {
                            patcher.Patcher?.Invoke(ref modAsm.AsmDef);
                        } catch (Exception e) {
                            progressable.Message(MessageType.Fatal, $"Patcher {patcher.TypeName} failed to patch {targetDLL}: {e.Message}");
                        }
                    }
            }

            setTaskProgress(2 / 3f);

            // Finish()
            foreach (var patcher in patchers) {
                try {
                    patcher.Finalizer?.Invoke();
                } catch (Exception e) {
                    progressable.Message(MessageType.Fatal, $"Patcher {patcher.TypeName} failed to finalize: {e.Message}");
                }
            }

            setTaskProgress(3 / 3f);

            // TODO MEDIUM: dump assemblies
        }

        private void LoadAssemblies(IProgressable progressable, Action<float> setTaskProgress)
        {
            IEnumerable<ModAssembly> GetDependencies(ModAssembly asm)
            {
                foreach (var module in asm.AsmDef.Modules)
                    foreach (var reference in module.AssemblyReferences)
                        if (AssemblyPool.TryGetAssembly(reference.Name, out var item))
                            yield return item;
            }

            // Sort assemblies by their dependencies
            IEnumerable<ModAssembly> sortedAssemblies = AssemblyPool.Assemblies.TopologicalSort(GetDependencies);

            int total = AssemblyPool.Count;
            int finished = 0;

            foreach (var asm in sortedAssemblies) {

                // Update assembly references
                foreach (var module in asm.AsmDef.Modules)
                    foreach (var reference in module.AssemblyReferences)
                        if (AssemblyPool.TryGetAssembly(reference.FullName, out var asmRefAsm)) {
                            reference.Name = asmRefAsm.AsmDef.Name.Name;
                        }

                // Load assemblies
                using MemoryStream ms = new();
                asm.AsmDef.Write(ms);
                asm.AsmDef.Dispose();

                try {
                    loadedAssembliesByName.Add(asm.AsmName, Assembly.Load(ms.ToArray()));
                } catch (Exception e) {
                    progressable.Message(MessageType.Fatal, $"Assembly {asm.AsmName} failed to load: {e}");
                }

                // TODO LOW: find some way to reduce the memory load here

                setTaskProgress(++finished / (float)total);
            }
        }

        private void InitializeMods(IProgressable progressable, Action<float> setTaskProgress)
        {
            // EnumExtender dependency fix
            VirtualEnums.VirtualEnumApi.ReloadWith(loadedAssembliesByName.Values, Program.Logger.LogError);

            StaticFixes.PreLoad();

            int total = loadedAssembliesByName.Count;
            int finished = 0;

            // Load mods one-by-one
            foreach (var loadedAssemblyKvp in loadedAssembliesByName) {
                ModAssembly modAssembly = AssemblyPool[loadedAssemblyKvp.Key];
                Assembly loadedModAssembly = loadedAssemblyKvp.Value;

                try {
                    modAssembly.Descriptor.Initialize(loadedModAssembly);
                } catch (Exception e) {
                    progressable.Message(MessageType.Fatal, $"Mod {modAssembly.AsmName} failed to initialize: {e}");
                }

                setTaskProgress(++finished / (float)total);
            }

            StaticFixes.PostLoad();
        }
    }
}
