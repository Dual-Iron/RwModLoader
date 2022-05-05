global using Rwml.IO;
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Mono.Cecil.Cil;
using MonoMod.RuntimeDetour;
using Partiality.Modloader;
using Realm.AssemblyLoading;
using Realm.ModLoading;
using Realm.Gui.Warnings;
using Realm.Logging;

namespace Realm;

static class Program
{
    public static ManualLogSource Logger { get; } = BepInEx.Logging.Logger.CreateLogSource("Realm");

    // Start doing stuff here.
    internal static void Main(List<string> earlyWrappedAsms)
    {
        Logger.LogDebug("Debug logging enabled.");

        if (!File.Exists(RealmPaths.BackendPath)) {
            Logger.LogFatal("Backend.exe was not found. Please reinstall Realm!");
            Reinstall.Hook();
            return;
        }

        State.PatchMods = GetPatchMods();

        NeuterPartiality();
        DeleteOldLogs();
        ReloadFixes.Hook();
        VanillaFixes.Hook();
        Gui.Gui.Hook();
        State.Prefs.Load();

        ConfigFile file = new(configPath: Path.Combine(Paths.ConfigPath, "Realm.cfg"), saveOnInit: true);
        State.DeveloperMode = file.Bind("General", "DeveloperMode", false, "Enables reloading mods in the pause menu. This feature is prone to breaking and best suited for mod development.").Value;
        bool load = file.Bind("General", "LoadOnStart", true, "Enables loading mods before the main menu appears.").Value;

        if (load) {
            State.Prefs.Enable(earlyWrappedAsms);
            State.Prefs.Save();

            Progressable prog = new();

            State.Mods.Reload(prog);

            if (prog.ProgressState == ProgressStateType.Failed) {
                FailedLoad.Hook();
            }
        }

        CheckForSelfUpdate();

        Job.Start(AudbEntry.PopulateAudb);
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

    private static void NeuterPartiality()
    {
        new ILHook(typeof(ModManager).GetMethod("LoadAllMods"), il => {
            il.Instrs.Clear();
            il.Instrs.Add(Instruction.Create(OpCodes.Ret));
        });
    }

    private static void DeleteOldLogs()
    {
        File.Delete(Path.Combine(Paths.GameRootPath, "consoleLog.txt"));
        File.Delete(Path.Combine(Paths.GameRootPath, "exceptionLog.txt"));
    }

    private static void CheckForSelfUpdate()
    {
        BackendProcess proc = BackendProcess.Execute("-q", 1000);

        if (proc.ExitCode == 0) {
            if (proc.Output == "y") {
                Logger.LogError("Realm is not up to date.");

                Update.Hook();
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
}
