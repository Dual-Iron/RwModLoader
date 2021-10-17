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
    internal static void Main(List<string> earlyWrappedAsms)
    {
        if (!File.Exists(RealmPaths.MutatorPath)) {
            Logger.LogFatal("Mutator not present. Please reinstall Realm!");
            ReinstallNotif.ApplyHooks();
            return;
        }

        Logger.LogDebug("Debug logging enabled.");

        ConfigFile file = new(configPath: Path.Combine(Paths.ConfigPath, "Realm.cfg"), saveOnInit: true);
        State.Instance.DeveloperMode = file.Bind("General", "HotReloading", false, "While enabled, Realm will allow hot reloading assemblies in-game. This feature is unstable.").Value;
        bool skip = file.Bind("General", "SkipLoading", false, "While enabled, Realm won't self-update or load mods when starting the game.").Value;

        if (!skip) TrySelfUpdate();
        LoadEmbeddedAssemblies();
        NeuterPartiality();
        StaticFixes.Hook();

        if (!skip) {
            State.Instance.Prefs.Load();
            State.Instance.Prefs.EnableThenSave(earlyWrappedAsms);
            State.Instance.Mods.Reload(new MessagingProgressable());
        }

        GuiHandler.Hook();
        DebugHandler.Hook();
    }

    private static void TrySelfUpdate()
    {
        Execution result = Execution.Run(RealmPaths.MutatorPath, "--needs-self-update", 1000);

        if (result.ExitCode == 0) {
            bool needsToUpdate = result.Output == "y";
            if (needsToUpdate) {
                Logger.LogInfo("Updating Realm.");

                using var self = Process.GetCurrentProcess();
                Execution.Run(RealmPaths.MutatorPath, $"--kill {self.Id} --self-update --install --runrw");
                return;
            }
            Logger.LogInfo("Realm is up to date.");
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
        PastebinMachine.EnumExtender.EnumExtender.Test();
    }
}
