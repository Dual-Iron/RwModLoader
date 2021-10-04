using Partiality;
using Partiality.Modloader;
using System.Reflection;

namespace Realm.AssemblyLoading;

public abstract partial class ModDescriptor
{
    public sealed class PartMod : ModDescriptor
    {
        private readonly string typeName;

        private PartialityMod? instance;

        public PartMod(string typeName)
        {
            this.typeName = typeName;
        }

        public override bool IsPartiality => true;

        public override void Initialize(Assembly assembly)
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
        
        public override void Unload()
        {
            if (instance != null) {
                // Remove mod before custom code. Ensures the mod is removed even if it fails to unload.
                var loadedMods = PartialityManager.Instance.modManager.loadedMods;

                for (int i = loadedMods.Count - 1; i >= 0; i--) {
                    if (ReferenceEquals(loadedMods[i], instance)) {
                        loadedMods.RemoveAt(i);
                    }
                }

                // Disable mod
                instance.OnDisable();
            }
        }
    }
}
