using BepInEx;
using BepInEx.Bootstrap;
using Mono.Cecil;
using Partiality;
using Partiality.Modloader;
using System.Reflection;
using UnityEngine;

namespace Realm.AssemblyLoading;

sealed class AssemblyDescriptor
{
    interface IModDescriptor
    {
        object? ModObject { get; }

        void Initialize(Assembly assembly);
        void Unload();
    }

    sealed class Lib : IModDescriptor
    {
        object? IModDescriptor.ModObject => null;

        void IModDescriptor.Initialize(Assembly assembly) { }
        void IModDescriptor.Unload() { }
    }

    sealed class Plugin : IModDescriptor
    {
        private static readonly GameObject pluginManager = new("IDoNotThinkThatTheNameOfThisObjectMattersWhatsoeverLol");

        private readonly PluginInfo pluginInfo;
        private readonly string typeName;

        private BaseUnityPlugin? plugin;

        public Plugin(PluginInfo pluginInfo, string typeName)
        {
            this.pluginInfo = pluginInfo;
            this.typeName = typeName;
        }

        object? IModDescriptor.ModObject => plugin;

        void IModDescriptor.Initialize(Assembly assembly)
        {
            Chainloader.PluginInfos[pluginInfo.Metadata.GUID] = pluginInfo;
            plugin = (BaseUnityPlugin)pluginManager.AddComponent(assembly.GetType(typeName));
        }

        void IModDescriptor.Unload()
        {
            UnityEngine.Object.Destroy(plugin);
            Chainloader.PluginInfos.Remove(pluginInfo.Metadata.GUID);
        }
    }

    sealed class PartMod : IModDescriptor
    {
        private readonly string typeName;

        private PartialityMod? instance;

        public PartMod(string typeName)
        {
            this.typeName = typeName;
        }

        object? IModDescriptor.ModObject => instance;

        void IModDescriptor.Initialize(Assembly assembly)
        {
            instance = (PartialityMod)Activator.CreateInstance(assembly.GetType(typeName));

            if (!instance.isEnabled) {
                instance.isEnabled = true;
                instance.Init();
                instance.OnLoad();
                instance.OnEnable();
            }

            // Add mod after custom code. Forces the mod to catch its own exceptions.
            PartialityManager.Instance.modManager.loadedMods.Add(instance);
        }

        void IModDescriptor.Unload()
        {
            // Remove mod before custom code. Ensures the mod is removed even if it fails to unload.
            var loadedMods = PartialityManager.Instance.modManager.loadedMods;

            for (int i = loadedMods.Count - 1; i >= 0; i--) {
                if (ReferenceEquals(loadedMods[i], instance)) {
                    loadedMods.RemoveAt(i);
                }
            }

            // Disable mod
            instance!.OnDisable();
        }
    }

    private static IModDescriptor GetModDescriptor(AssemblyDefinition definition, string modType)
    {
        if (string.IsNullOrEmpty(modType)) {
            return new Lib();
        }

        TypeDefinition type = definition.MainModule.GetType(modType);

        if (type is not null && !type.IsAbstract && !type.IsInterface && !type.IsValueType) {
            if (type.IsSubtypeOf(typeof(BaseUnityPlugin))) {
                return new Plugin(Chainloader.ToPluginInfo(type), type.FullName);
            }
            if (type.IsSubtypeOf(typeof(PartialityMod))) {
                return new PartMod(type.FullName);
            }
        }

        Program.Logger.LogError($"Mutator's patcher provided a type name that wasn't a plugin or a partmod: {modType} from {definition.Name}.");
        return new Lib();
    }

    private readonly List<IModDescriptor> mods = new();

    public AssemblyDescriptor(AssemblyDefinition definition, IEnumerable<string> modTypes)
    {
        foreach (var modType in modTypes) {
            mods.Add(GetModDescriptor(definition, modType));
        }
    }

    public void Initialize(Assembly assembly)
    {
        foreach (var mod in mods) {
            mod.Initialize(assembly);
        }
    }
    
    public void Unload()
    {
        foreach (var mod in mods) {
            mod.Unload();
        }
    }

    public Ref<object?>? GetReloadState()
    {
        foreach (var mod in mods) {
            if (mod.ModObject is not object o) {
                continue;
            }

            Type type = o.GetType();

            MethodInfo? get = type.GetMethod("GetReloadState", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);

            if (get == null) continue;

            if (get.ReturnType == typeof(void)) {
                Program.Logger.LogWarning($"The return type of {type.FullName}.GetReloadState() is void.");
                continue;
            }

            return new(get.Invoke(o, null));
        }
        return null;
    }

    public void SetUnloadState(object? modData)
    {
        foreach (var mod in mods) {
            if (mod.ModObject is not object o) {
                continue;
            }

            Type type = o.GetType();

            MethodInfo? set = type.GetMethod("Reload", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(object) }, null);

            if (set == null) {
                Program.Logger.LogWarning($"The mod {type.FullName} has reload data but no Reload(object) method.");
                continue;
            }

            set.Invoke(o, new[] { modData });

            return;
        }
    }
}
