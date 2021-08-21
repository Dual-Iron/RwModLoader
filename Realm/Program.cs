using BepInEx;
using BepInEx.Logging;
using BepInEx.Preloader.Patching;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Partiality.Modloader;
using Realm.Logging;
using Realm.ModLoading;
using System;

namespace Realm
{
    public static class Program
    {
        public static ManualLogSource Logger { get; } = BepInEx.Logging.Logger.CreateLogSource("Realm");

        // Called just after the Chainloader starts and likely before the game runs
        // Perfect place to load plugins and add hooks
        internal static void Main()
        {
            try {
                LoadEmbeddedAssemblies();
            } catch (MissingMethodException) {
                Logger.LogWarning("EnumExtender in plugins folder.");
                // TODO LOW: overwrite BepInEx's assembly resolving with our own to prevent old built-in dependencies from even being loaded
                // To do so, we would unsubscribe https://github.com/BepInEx/BepInEx/blob/v5-lts/BepInEx.Preloader/Entrypoint.cs#L68 this method and subscribe our own
            }

            PreventBepPatcherDisposal();

            NeuterPartiality();

            // TODO NEXT: load plugins on startup and reload on demand
            Progressable progressable = new();

            AssemblyPool pool = AssemblyPool.ReadMods(progressable, Paths.PluginPath);
            LoadedAssemblyPool loadedPool = LoadedAssemblyPool.Load(progressable, pool);
        }

        private static void PreventBepPatcherDisposal()
        {
            static void BeforeDispose(Action orig) { }

            new Hook(typeof(AssemblyPatcher).GetMethod("DisposePatchers"), (Action<Action>)BeforeDispose);
        }

        private static void NeuterPartiality()
        {
            static void NeuterPartiality(ILContext il)
            {
                il.Instrs.Clear();
                il.Instrs.Add(Instruction.Create(OpCodes.Ret));
            }

            new ILHook(typeof(ModManager).GetMethod("LoadAllMods"), NeuterPartiality);
        }

        private static void LoadEmbeddedAssemblies()
        {
            // For now, the only embedded assemblies are EnumExtender
            PastebinMachine.EnumExtender.EnumExtender.DoNothing();
        }
    }
}
