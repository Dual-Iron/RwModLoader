using Menu;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace Realm.Gui.Local;

static class ModsMenuGui
{
    private static bool RedUnlocked()
    {
        try {
            return SlugcatSelectMenu.CheckUnlockRed() || RealmUtils.RainWorld!.progression.miscProgressionData.redUnlocked;
        } catch (Exception e) {
            Program.Logger.LogError(e);
            return false;
        }
    }

    public static MenuScene.SceneID TimedScene => DateTime.Now.DayOfWeek switch {
        DayOfWeek.Sunday => RedUnlocked() ? MenuScene.SceneID.Outro_4_Tree : MenuScene.SceneID.Intro_1_Tree,
        DayOfWeek.Monday => MenuScene.SceneID.Intro_2_Branch,
        DayOfWeek.Tuesday => MenuScene.SceneID.Intro_3_In_Tree,
        DayOfWeek.Wednesday => MenuScene.SceneID.Intro_4_Walking,
        DayOfWeek.Thursday => RedUnlocked() ? MenuScene.SceneID.Void_Slugcat_Upright : MenuScene.SceneID.Intro_5_Hunting,
        DayOfWeek.Friday => RedUnlocked() ? MenuScene.SceneID.Void_Slugcat_Down : MenuScene.SceneID.Intro_6_7_Rain_Drop,
        _ => RedUnlocked() ? MenuScene.SceneID.Outro_2_Up_Swim : MenuScene.SceneID.SleepScreen
    };

    private const string MODS_MENU = "MODM";

    internal static void Hook()
    {
        On.Menu.MainMenu.ctor += AddModsButtonToMainMenu;
        On.Menu.MainMenu.Singal += MainMenuSingal;
        IL.ProcessManager.SwitchMainProcess += CheckForModMenu;
    }

    private static void AddModsButtonToMainMenu(On.Menu.MainMenu.orig_ctor orig, MainMenu self, ProcessManager manager, bool showRegionSpecificBkg)
    {
        orig(self, manager, showRegionSpecificBkg);

        MenuObject? add = null;

        foreach (var mob in self.pages[0].subObjects)
            if (mob is SimpleButton button) {
                if (add == null && button.signalText == "OPTIONS") {
                    // Add MODS button
                    add = new SimpleButton(self, self.pages[0], "MODS", MODS_MENU, button.pos, button.size);
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
        if (message == MODS_MENU) {
            self.manager.RequestMainProcessSwitch(ModsMenu.ModsMenuID);
            self.PlaySound(SoundID.MENU_Switch_Page_In);
            return;
        }

        orig(self, sender, message);
    }

    private static void CheckForModMenu(ILContext il)
    {
        ILCursor cursor = new(il);

        cursor.GotoNext(i => i.MatchSwitch(out _));

        // TrySwitchToModMenu(this, ID);
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Ldarg_1);
        cursor.EmitDelegate(SwitchToCustomProcess);
    }

    private static void SwitchToCustomProcess(ProcessManager pm, ProcessManager.ProcessID pid)
    {
        if (pid == ModsMenu.ModsMenuID) {
            pm.currentMainLoop = new ModsMenu(pm);
        }
    }
}
