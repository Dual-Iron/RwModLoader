using Menu;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Realm.Jobs;
using Realm.Logging;
using System.Linq;
using UnityEngine;

namespace Realm.Gui;

public static class GuiHandler
{
    public static MenuScene.SceneID TimedScene => DateTime.Now.DayOfWeek switch {
        DayOfWeek.Sunday => MenuScene.SceneID.Intro_1_Tree,
        DayOfWeek.Monday => MenuScene.SceneID.Intro_2_Branch,
        DayOfWeek.Tuesday => MenuScene.SceneID.Intro_3_In_Tree,
        DayOfWeek.Wednesday => MenuScene.SceneID.Intro_4_Walking,
        DayOfWeek.Thursday => SlugcatSelectMenu.CheckUnlockRed() ? MenuScene.SceneID.Void_Slugcat_Upright : MenuScene.SceneID.Intro_5_Hunting,
        DayOfWeek.Friday => SlugcatSelectMenu.CheckUnlockRed() ? MenuScene.SceneID.Void_Slugcat_Down : MenuScene.SceneID.Intro_6_7_Rain_Drop,
        _ => SlugcatSelectMenu.CheckUnlockRed() ? MenuScene.SceneID.Outro_2_Up_Swim : MenuScene.SceneID.SleepScreen
    };

    public static string TimedSong => DateTime.Now.Hour switch {
        < 4 => "NA_39 - Cracked Earth",
        < 8 => "NA_04 - Silicon",
        < 12 => "NA_30 - Distance",
        < 16 => "NA_24 - Emotion Thread",
        < 20 => "NA_09 - Interest Pad",
        _ => "RW_16 - Shoreline",
    };

    private const string HOT_RELOAD = "H";

    private static Job? reloadingJob;

    public static void Hook()
    {
        ModsMenuMusic.Hook();

        if (ProgramState.Instance.DeveloperMode) {
            On.Menu.PauseMenu.ctor += AddReloadAsmsButtonToPauseMenu;
            On.Menu.PauseMenu.Singal += PauseMenuSingal;
            On.Menu.PauseMenu.Update += UpdatePauseMenu;
            On.Menu.Menu.Update += UpdatePaseMenuBase;
        }
        On.Menu.MainMenu.ctor += AddModsButtonToMainMenu;
        On.Menu.MainMenu.Singal += MainMenuSingal;
        IL.ProcessManager.SwitchMainProcess += CheckForModMenu;
    }

    #region Pause menu
    private static void AddReloadAsmsButtonToPauseMenu(On.Menu.PauseMenu.orig_ctor orig, PauseMenu self, ProcessManager manager, RainWorldGame game)
    {
        orig(self, manager, game);

        SimpleButton modsButton = new(self, self.pages[0], "HOT RELOAD", HOT_RELOAD, self.exitButton.pos - new Vector2(140, 0), new(110, 30));

        self.pages[0].subObjects.Add(modsButton);
    }

    private static void PauseMenuSingal(On.Menu.PauseMenu.orig_Singal orig, PauseMenu self, MenuObject sender, string message)
    {
        if (reloadingJob == null && message == HOT_RELOAD) {
            reloadingJob = Job.Start(() => {
                ProgramState.Instance.Prefs.Load();
                ProgramState.Instance.Mods.Reload(new MessagingProgressable());
            });
            self.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
            return;
        }

        orig(self, sender, message);
    }

    private static void UpdatePauseMenu(On.Menu.PauseMenu.orig_Update orig, PauseMenu self)
    {
        if (reloadingJob != null) {
            self.wantToContinue = false;
            self.counter = 0;
        } else {
            foreach (var sob in self.pages[0].subObjects) {
                if (sob is SimpleButton sib && sib.signalText == HOT_RELOAD) {
                    sib.GetButtonBehavior.greyedOut = false;
                    break;
                }
            }
        }

        orig(self);

        if (reloadingJob?.Status == JobStatus.Finished) {
            reloadingJob = null;
        }
    }

    private static void UpdatePaseMenuBase(On.Menu.Menu.orig_Update orig, Menu.Menu self)
    {
        if (self is PauseMenu && reloadingJob != null) {
            foreach (var sob in self.pages[0].subObjects) {
                if (sob is SimpleButton sib) {
                    sib.GetButtonBehavior.greyedOut = true;
                }
            }
        }

        orig(self);
    }
    #endregion

    private static void AddModsButtonToMainMenu(On.Menu.MainMenu.orig_ctor orig, MainMenu self, ProcessManager manager, bool showRegionSpecificBkg)
    {
        orig(self, manager, showRegionSpecificBkg);

        MenuObject? add = null;

        foreach (var mob in self.pages[0].subObjects)
            if (mob is SimpleButton button) {
                if (add == null && button.signalText == "OPTIONS") {
                    // Add MODS button
                    add = new SimpleButton(self, self.pages[0], "MODS", HOT_RELOAD, button.pos, button.size);
                    add.nextSelectable[0] = add;
                    add.nextSelectable[2] = add;
                }

                // If add is not null, then we have a mods button, so move other buttons down
                if (add != null) {
                    button.pos.y -= 40;
                }
            }

        if (add != null)
            self.pages[0].subObjects.Add(add);
        else
            Program.Logger.LogError("MODS button not added to main menu!");
    }

    private static void MainMenuSingal(On.Menu.MainMenu.orig_Singal orig, MainMenu self, MenuObject sender, string message)
    {
        if (message == HOT_RELOAD) {
            self.manager.RequestMainProcessSwitch(ModsMenu.ModsMenuID);
            self.PlaySound(SoundID.MENU_Switch_Page_In);
            return;
        }

        orig(self, sender, message);
    }

    private static void CheckForModMenu(ILContext il)
    {
        ILCursor cursor = new(il);

        if (!cursor.TryGotoNext(i => i.MatchSwitch(out _))) {
            Program.Logger.LogError("No switch statement in ProcessManager.SwitchMainProcess()!");
            return;
        }

        // TrySwitchToModMenu(this, ID);
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Ldarg_1);
        cursor.EmitDelegate<Action<ProcessManager, ProcessManager.ProcessID>>(TrySwitchToCustomProcess);
    }

    private static void TrySwitchToCustomProcess(ProcessManager pm, ProcessManager.ProcessID pid)
    {
        if (pid == ModsMenu.ModsMenuID) {
            pm.currentMainLoop = new ModsMenu(pm);
        } else if (pid == RaindbMenu.RaindbMenuID) {
            pm.currentMainLoop = new RaindbMenu(pm);
        }
    }
}
