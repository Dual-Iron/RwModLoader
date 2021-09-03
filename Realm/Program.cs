using System;
using BepInEx;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using Partiality.Modloader;
using System.Diagnostics;
using System.IO;
using Realm.Gui;
using Realm.Logging;
using Realm.AssemblyLoading;
using BepInEx.Configuration;

namespace Realm
{
    public static class Program
    {
        public static ManualLogSource Logger { get; } = BepInEx.Logging.Logger.CreateLogSource("Realm");

        // Called just after the Chainloader starts and likely before the game runs
        // Perfect place to load plugins and add hooks
        internal static void Main()
        {
            if (!File.Exists(Extensions.MutatorPath)) {
                throw new InvalidOperationException("MUTATOR NOT PRESENT. REINSTALL REALM!");
            }

            Logger.LogDebug("Debug logging enabled.");

            ConfigFile file = new(configPath: Path.Combine(Paths.ConfigPath, "Realm.cfg"), saveOnInit: true);

            var skip = file.Bind("General", "SkipLoading", false, "If enabled, Realm won't reload mods when starting the game.").Value;
            var reset = file.Bind("General", "ResetMods", false, "If enabled, Realm will reset all mods and user preferences when starting the game.").Value;
            ProgramState.Current.DeveloperMode = file.Bind("General", "HotReloading", false, "If enabled, Realm will allow hot reloading assemblies in-game. This feature is volatile.").Value;

            if (!skip) TrySelfUpdate();
            LoadEmbeddedAssemblies();
            NeuterPartiality();
            StaticFixes.Hook();

            if (reset) {
                ProgramState.Current.Prefs.Save();
                ProgramState.Current.Mods.Unload(new ProgressMessagingProgressable());
                File.Delete(Path.Combine(Paths.GameRootPath, "reset"));
            } else {
                ProgramState.Current.Prefs.Load();
            }

            if (!skip) ProgramState.Current.Mods.Reload(new ProgressMessagingProgressable());

            GuiHandler.Hook(ProgramState.Current);
        }

        private static void TrySelfUpdate()
        {
            if (Environment.GetEnvironmentVariable("LAUNCHED_FROM_MUTATOR", EnvironmentVariableTarget.Process) == "true") {
                return;
            }

            Execution result = Execution.Run(Extensions.MutatorPath, "--needs-self-update", 1000);

            if (result.ExitCode == 0) {
                bool needsToUpdate = result.Output == "y";
                if (needsToUpdate) {
                    using var self = Process.GetCurrentProcess();
                    Execution.Run(Extensions.MutatorPath, $"--kill {self.Id} --self-update --uninstall --install --run \"{Path.Combine(Paths.GameRootPath, "RainWorld.exe")}\"");
                    return;
                }
                Logger.LogInfo("Realm is up to date!");
            } else {
                Logger.LogWarning("Couldn't determine if Realm is up to date or not.");
                Logger.LogDebug($"{result.ExitMessage}: {result.Error}");
            }
        }

        private static void NeuterPartiality()
        {
            static void LoadAllModsIL(ILContext il)
            {
                il.Instrs.Clear();
                il.Instrs.Add(Instruction.Create(OpCodes.Ret));
            }

            _ = new ILHook(typeof(ModManager).GetMethod("LoadAllMods"), LoadAllModsIL);
        }

        private static void LoadEmbeddedAssemblies()
        {
            // Eagerly load these assemblies because it can't hurt
            PastebinMachine.EnumExtender.EnumExtender.DoNothing();
        }
    }
}
