using Menu;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Realm.Jobs;
using Realm.Logging;
using System;
using UnityEngine;

namespace Realm.Gui
{
    public sealed class GuiHandler
    {
        private const string MODS_BUTTON = "M";

        public static void Hook(ProgramState state)
        {
            ModsMenuMusic.Hook();

            GuiHandler handler = new(state);

            if (state.DeveloperMode) {
                On.Menu.PauseMenu.ctor += handler.AddReloadAsmsButtonToPauseMenu;
                On.Menu.PauseMenu.Singal += handler.PauseMenuSingal;
                On.Menu.PauseMenu.Update += handler.UpdatePauseMenu;
            }
            On.Menu.MainMenu.ctor += handler.AddModsButtonToMainMenu;
            On.Menu.MainMenu.Singal += handler.MainMenuSingal;
            IL.ProcessManager.SwitchMainProcess += handler.CheckForModMenu;
        }

        private readonly ProgramState state;

        private Job? reloadingJob;

        private GuiHandler(ProgramState state) => this.state = state;

        private void AddReloadAsmsButtonToPauseMenu(On.Menu.PauseMenu.orig_ctor orig, PauseMenu self, ProcessManager manager, RainWorldGame game)
        {
            orig(self, manager, game);

            SimpleButton modsButton = new(self, self.pages[0], "HOT RELOAD", MODS_BUTTON, self.exitButton.pos - new Vector2(140, 0), new(110, 30));

            self.pages[0].subObjects.Add(modsButton);
        }

        private void PauseMenuSingal(On.Menu.PauseMenu.orig_Singal orig, PauseMenu self, MenuObject sender, string message)
        {
            if (message == MODS_BUTTON) {
                reloadingJob ??= Job.Start(() => state.Mods.ReloadJustAssemblies(new ProgressMessagingProgressable()));
                self.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
                return;
            }

            orig(self, sender, message);
        }

        private void UpdatePauseMenu(On.Menu.PauseMenu.orig_Update orig, PauseMenu self)
        {
            orig(self);

            if (reloadingJob != null) {
                foreach (var sob in self.pages[0].subObjects) {
                    if (sob is ButtonTemplate button) {
                        button.GetButtonBehavior.greyedOut = true;
                    }
                }

                if (reloadingJob.Status == JobStatus.Finished) {
                    reloadingJob = null;
                }
            } else {
                foreach (var sob in self.pages[0].subObjects) {
                    if (sob is SimpleButton button && button.signalText == MODS_BUTTON) {
                        button.GetButtonBehavior.greyedOut = false;
                        break;
                    }
                }
            }
        }

        private void AddModsButtonToMainMenu(On.Menu.MainMenu.orig_ctor orig, MainMenu self, ProcessManager manager, bool showRegionSpecificBkg)
        {
            orig(self, manager, showRegionSpecificBkg);

            MenuObject? add = null;

            foreach (var mob in self.pages[0].subObjects)
                if (mob is SimpleButton button) {
                    if (add == null && button.signalText == "OPTIONS") {
                        // Add MODS button
                        add = new SimpleButton(self, self.pages[0], "MODS", MODS_BUTTON, button.pos, button.size);
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

        private void MainMenuSingal(On.Menu.MainMenu.orig_Singal orig, MainMenu self, MenuObject sender, string message)
        {
            if (message == MODS_BUTTON) {
                self.manager.RequestMainProcessSwitch(ModsMenu.ModsMenuID);
                self.PlaySound(SoundID.MENU_Switch_Page_In);
                return;
            }

            orig(self, sender, message);
        }

        private void CheckForModMenu(ILContext il)
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

        private void TrySwitchToCustomProcess(ProcessManager pm, ProcessManager.ProcessID pid)
        {
            if (pid == ModsMenu.ModsMenuID) {
                pm.currentMainLoop = new ModsMenu(pm, state);
            }
        }
    }
}
