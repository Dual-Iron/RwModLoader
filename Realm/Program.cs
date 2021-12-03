global using Rwml.IO;
global using System;
global using System.IO;
global using System.Text;
global using System.Linq;
global using System.Collections.Generic;

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

    // Start doing stuff here.
    internal static void Main(List<string> earlyWrappedAsms, bool extraPatchers)
    {
        Logger.LogDebug("Debug logging enabled.");

        GuiFix.Fix();

        if (!File.Exists(RealmPaths.MutatorPath)) {
            Logger.LogFatal("Mutator not present. Please reinstall Realm!");
            ReinstallNotif.ApplyHooks();
            return;
        }

        State.PatchMods = GetPatchMods();

        State.NoHotReloading = extraPatchers || State.PatchMods.Count > 0;

        ConfigFile file = new(configPath: Path.Combine(Paths.ConfigPath, "Realm.cfg"), saveOnInit: true);
        State.DeveloperMode = file.Bind("General", "HotReloading", false, "While enabled, Realm will allow hot reloading assemblies in-game. This feature is unstable.").Value;
        bool skip = file.Bind("General", "SkipLoading", false, "While enabled, Realm won't self-update or load mods when starting the game.").Value;

        if (!skip) CheckForSelfUpdate();
        NeuterPartiality();
        StaticFixes.Hook();

        if (!skip) {
            State.Prefs.Load();
            State.Prefs.Enable(earlyWrappedAsms);
            State.Prefs.Save();
            State.Mods.Reload(new Progressable());
        }

        GuiHandler.Hook();
    }

    public static List<string> GetPatchMods()
    {
        const string prefix = "Assembly-CSharp.";
        const string suffix = ".mm.dll";

        string mmPath = Path.Combine(Paths.BepInExRootPath, "monomod");
        string[] patchMods = Directory.Exists(mmPath) ? Directory.GetFiles(mmPath) : new string[0];

        List<string> ret = new(capacity: patchMods.Length);

        foreach (string patchMod in patchMods) {
            var fileName = Path.GetFileName(patchMod);

            if (fileName.StartsWith(prefix) && fileName.EndsWith(suffix)) {
                ret.Add(fileName.Substring(prefix.Length, fileName.Length - prefix.Length - suffix.Length));
            }
        }

        return ret;
    }

    private static void CheckForSelfUpdate()
    {
        MutatorProcess proc = MutatorProcess.Execute("-q", 1000);

        if (proc.ExitCode == 0) {
            if (proc.Output == "y") {
                Logger.LogError("Realm is not up to date.");

                UpdateNotif.ApplyHooks();
            }
            else {
                Logger.LogInfo("Realm is up to date.");
            }
        }
        else {
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
}
