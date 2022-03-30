using Menu;
using Realm.Jobs;
using Realm.Logging;
using UnityEngine;

namespace Realm.Gui;

static class HotReloadingHooks
{
    private static Job? reloadingJob;
    private const string HOT_RELOAD = "HRL";

    public static void Hook()
    {
        On.Menu.PauseMenu.ctor += AddReloadAsmsButtonToPauseMenu;
        On.Menu.PauseMenu.Singal += PauseMenuSingal;
        On.Menu.PauseMenu.Update += UpdatePauseMenu;
        On.Menu.Menu.Update += UpdatePauseMenuBase;
    }

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
                State.Prefs.Load();
                State.Mods.Reload(new MessagingProgressable());
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
        }
        else {
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

    private static void UpdatePauseMenuBase(On.Menu.Menu.orig_Update orig, Menu.Menu self)
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
}
