using System.Reflection;
using UnityEngine;
using BepInEx;
using Mono.Cecil;
using BepInEx.Bootstrap;

namespace Realm.ModLoading
{
    public abstract partial class ModDescriptor
    {
        public sealed class BepMod : ModDescriptor
        {
            private static readonly GameObject pluginManager = new("IDoNotThinkThatTheNameOfThisObjectMattersWhatsoeverLol");

            private readonly string typeName;
            private readonly PluginInfo pluginInfo;

            private BaseUnityPlugin? plugin;

            public BepMod(TypeDefinition typeDef)
            {
                typeName = typeDef.FullName;
                pluginInfo = Chainloader.ToPluginInfo(typeDef);
            }

            public override bool IsPartiality => false;

            public override void Initialize(Assembly assembly)
            {
                Chainloader.PluginInfos[pluginInfo.Metadata.GUID] = pluginInfo;
                plugin = (BaseUnityPlugin)pluginManager.AddComponent(assembly.GetType(typeName));
            }

            public override void Unload()
            {
                UnityEngine.Object.Destroy(plugin);
                Chainloader.PluginInfos.Remove(pluginInfo.Metadata.GUID);
            }
        }
    }
}
