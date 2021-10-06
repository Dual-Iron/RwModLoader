using BepInEx;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using Partiality.Modloader;
using System.Diagnostics;
using Realm.Gui;
using Realm.Logging;
using Realm.AssemblyLoading;
using BepInEx.Configuration;

namespace Realm; // TODO NEXT: GUI and proper RWML integration support.

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
        ProgramState.Instance.DeveloperMode = file.Bind("General", "HotReloading", false, "If enabled, Realm will allow hot reloading assemblies in-game. This feature is unstable.").Value;
        bool skip = file.Bind("General", "SkipLoading", false, "If enabled, Realm won't load mods when starting the game.").Value;

        if (!skip) TrySelfUpdate();
        LoadEmbeddedAssemblies();
        NeuterPartiality();
        StaticFixes.Hook();

        if (!skip) {
            ProgramState.Instance.Prefs.Load();
            ProgramState.Instance.Mods.Reload(new ProgressMessagingProgressable());
        }

        GuiHandler.Hook();
        DebugHandler.Hook();
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
