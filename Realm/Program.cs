using Realm.Logging;
using Realm.ModLoading;
using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Preloader.Patching;
using MonoMod.RuntimeDetour;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using Partiality.Modloader;

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
                // TODO LOW: overwrite BepInEx's assembly resolving with our own to prevent old built-in dependencies (like EnumExtender.dll in the plugins folder) from even being loaded
                // To do so, we would unsubscribe https://github.com/BepInEx/BepInEx/blob/v5-lts/BepInEx.Preloader/Entrypoint.cs#L68 this method and subscribe our own
            }

            PreventBepPatcherDisposal();

            NeuterPartiality();

            // TODO NEXT: reload plugins on demand
            ProgressMessagingProgressable progressable = new();

            progressable.Message(MessageType.Info, "Getting assemblies");

            AssemblyPool pool = AssemblyPool.ReadMods(progressable, Paths.PluginPath);

            progressable.Message(MessageType.Info, "Loading assemblies");

            LoadedAssemblyPool.Load(progressable, pool);
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
