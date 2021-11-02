using BepInEx;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using Partiality.Modloader;
using Realm.Gui;
using Realm.Logging;
using Realm.AssemblyLoading;
using BepInEx.Configuration;
using Realm.Gui.Installation;

namespace Realm;

static class Program
{
    public static ManualLogSource Logger { get; } = BepInEx.Logging.Logger.CreateLogSource("Realm");

    // Called just after the Chainloader starts and likely before the game runs
    // Perfect place to load plugins and add hooks
    internal static void Main(List<string> earlyWrappedAsms, bool extraPatchers)
    {
        GuiFix.Fix();

        if (!File.Exists(RealmPaths.MutatorPath)) {
            Logger.LogFatal("Mutator not present. Please reinstall Realm!");
            ReinstallNotif.ApplyHooks();
            return;
        }

        State.Instance.NoHotReloading = extraPatchers;

        Logger.LogDebug("Debug logging enabled.");

        ConfigFile file = new(configPath: Path.Combine(Paths.ConfigPath, "Realm.cfg"), saveOnInit: true);
        State.Instance.DeveloperMode = file.Bind("General", "HotReloading", false, "While enabled, Realm will allow hot reloading assemblies in-game. This feature is unstable.").Value;
        bool skip = file.Bind("General", "SkipLoading", false, "While enabled, Realm won't self-update or load mods when starting the game.").Value;

        if (!skip) CheckForSelfUpdate();
        LoadEmbeddedAssemblies();
        NeuterPartiality();
        StaticFixes.Hook();

        if (!skip) {
            State.Instance.Prefs.Load();
            State.Instance.Prefs.Enable(earlyWrappedAsms);
            State.Instance.Prefs.Save();
            State.Instance.Mods.Reload(new Progressable());
        }

        GuiHandler.Hook();
    }

    private static void CheckForSelfUpdate()
    {
        MutatorProcess proc = MutatorProcess.Execute("-q", 1000);

        if (proc.ExitCode == 0) {
            if (proc.Output == "y") {
                Logger.LogError("Realm is not up to date.");

                UpdateNotif.ApplyHooks();
            } else {
                Logger.LogInfo("Realm is up to date.");
            }
        } else {
            Logger.LogError("Couldn't determine if Realm is up to date.");
            Logger.LogDebug(proc);
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
