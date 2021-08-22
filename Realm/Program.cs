using Realm.Logging;
using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Preloader.Patching;
using MonoMod.RuntimeDetour;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using Partiality.Modloader;
using System.Diagnostics;
using System.IO;
using Realm.ModLoading;

namespace Realm
{
    public static class Program
    {
        public static ManualLogSource Logger { get; } = BepInEx.Logging.Logger.CreateLogSource("Realm");

        // Called just after the Chainloader starts and likely before the game runs
        // Perfect place to load plugins and add hooks
        internal static void Main()
        {
            TrySelfUpdate();

            try {
                LoadEmbeddedAssemblies();
            } catch (MissingMethodException) {
                Logger.LogWarning("EnumExtender in plugins folder.");
                // TODO LOW: overwrite BepInEx's assembly resolving with our own to prevent old built-in dependencies (like EnumExtender.dll in the plugins folder) from even being loaded
                // To do so, we would unsubscribe https://github.com/BepInEx/BepInEx/blob/v5-lts/BepInEx.Preloader/Entrypoint.cs#L68 this method and subscribe our own
            }

            PreventBepPatcherDisposal();

            NeuterPartiality();

            // TODO NEXT: GUI rwmod listing
            new ModLoader().Load(new ProgressMessagingProgressable());
        }

        private static void TrySelfUpdate()
        {
            if (Environment.GetEnvironmentVariable("LAUNCHED_FROM_MUTATOR", EnvironmentVariableTarget.Process) == "true") {
                return;
            }

            ProcessResult result = ProcessResult.From(Extensions.MutatorPath, "--needs-self-update", 1000);

            if (result.ExitCode == 0) {
                bool needsToUpdate = result.Output == "y";
                if (needsToUpdate) {
                    using var self = Process.GetCurrentProcess();
                    ProcessResult.From(Extensions.MutatorPath, $"--kill {self.Id} --self-update --uninstall --install --run \"{Path.Combine(Paths.GameRootPath, "RainWorld.exe")}\"");
                    return;
                }
                Logger.LogInfo("Realm is up to date!");
            } else {
                Logger.LogWarning("Couldn't determine if Realm is up to date or not.");
                Logger.LogDebug($"{result.ExitMessage}: {result.Error}");
            }
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
