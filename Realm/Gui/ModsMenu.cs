﻿using Menu;
using Realm.Jobs;
using Realm.Logging;
using System.Collections.Generic;
using System.IO;

namespace Realm.Gui
{
    public sealed class ModsMenu : Menu.Menu
    {
        public const ProcessManager.ProcessID ModsMenuID = (ProcessManager.ProcessID)(-666);

        public ModsMenu(ProcessManager manager, ProgramState state) : base(manager, ModsMenuID)
        {
            State = state;

            State.Mods.Refresh(performingProgress);

            pages.Add(new(this, null, "main", 0));

            // Big pretty background picture
            Page.subObjects.Add(new InteractiveMenuScene(this, Page, GuiHandler.TimedScene) { cameraRange = 0.2f });

            // A scaled up translucent black pixel to make the background less distracting
            Page.subObjects.Add(new MenuSprite(Page, new(-1, -1), new("pixel") {
                color = new(0, 0, 0, 0.75f),
                scaleX = 2000,
                scaleY = 1000,
                anchorX = 0,
                anchorY = 0
            }));

            // Buttons
            Page.subObjects.Add(nextButton = new BigArrowButton(this, Page, "", new(1116f, 668f), 1));
            Page.subObjects.Add(cancelButton = new SimpleButton(this, Page, "CANCEL", "", new(200, 50), new(110, 30)));
            Page.subObjects.Add(saveButton = new SimpleButton(this, Page, "SAVE & EXIT", "", new(360, 50), new(110, 30)));
            Page.subObjects.Add(refresh = new SimpleButton(this, Page, "REFRESH", "", new(200, 200), new(110, 30)));
            Page.subObjects.Add(disableAll = new SimpleButton(this, Page, "DISABLE ALL", "", new(200, 250), new(110, 30)));
            Page.subObjects.Add(enableAll = new SimpleButton(this, Page, "ENABLE ALL", "", new(200, 300), new(110, 30)));

            modListing = new(Page, pos: new(650, 50), elementSize: new(ModPanel.Width, ModPanel.Height), elementsPerScreen: 16, edgePadding: 5);

            foreach (var file in state.Mods.AllRwmods) {
                modListing.subObjects.Add(new ModPanel(file, Page, default));
            }

            Page.subObjects.Add(modListing);

            Page.subObjects.Add(
                progDisplayContainer = new(Page)
            );
            progDisplayContainer.subObjects.Add(
                new ProgressableDisplay(performingProgress, progDisplayContainer, modListing.pos, modListing.size)
            );

            ModsMenuMusic.Start(manager.musicPlayer);
        }

        public ProgramState State { get; }

        private readonly SimpleButton cancelButton;
        private readonly SimpleButton saveButton;
        private readonly SimpleButton enableAll;
        private readonly SimpleButton disableAll;
        private readonly SimpleButton refresh;
        private readonly Listing modListing;

        private readonly MenuContainer progDisplayContainer;
        private readonly LoggedProgressable performingProgress = new();
        private readonly BigArrowButton nextButton;

        private Page Page => pages[0];

        private Job? performingJob;     // Current job.
        private bool shutDownMusic;     // Set false to not restart music on shutdown. Useful for refreshing.

        private bool PreventButtonClicks => manager.upcomingProcess != null || performingJob != null;

        private IEnumerable<ModPanel> Panels {
            get {
                foreach (var sob in modListing.subObjects)
                    if (sob is ModPanel p)
                        yield return p;
            }
        }

        public override void ShutDownProcess()
        {
            if (shutDownMusic) {
                ModsMenuMusic.ShutDown(manager.musicPlayer);
            }

            base.ShutDownProcess();
        }

        public override void Update()
        {
            foreach (var mob in Page.subObjects) {
                if (mob is ButtonTemplate button) {
                    button.GetButtonBehavior.greyedOut = PreventButtonClicks;
                }
            }

            base.Update();
        }

        public override void GrafUpdate(float timeStacker)
        {
            progDisplayContainer.Container.alpha = performingJob != null ? 1 : 0;

            base.GrafUpdate(timeStacker);
        }

        public override void Singal(MenuObject sender, string message)
        {
            if (sender == cancelButton) {
                manager.RequestMainProcessSwitch(ProcessManager.ProcessID.MainMenu);
                PlaySound(SoundID.MENU_Switch_Page_Out);
                shutDownMusic = true;
                return;
            }

            if (sender == saveButton && performingJob == null) {
                performingJob = Job.Start(SaveExit);
                PlaySound(SoundID.MENU_Switch_Page_Out);
                shutDownMusic = true;
                return;
            }

            if (sender == nextButton) {
                manager.RequestMainProcessSwitch(RaindbMenu.RaindbMenuID);
                PlaySound(SoundID.MENU_Switch_Arena_Gametype);
                return;
            }

            if (sender == enableAll) {
                foreach (ModPanel panel in Panels) {
                    panel.SetEnabled(true);
                }
            } else if (sender == disableAll) {
                foreach (ModPanel panel in Panels) {
                    panel.SetEnabled(false);
                }
            } else if (sender == refresh) {
                manager.RequestMainProcessSwitch(ID);
            }

            PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
        }

        private void SaveExit()
        {
            State.Prefs.EnabledMods.Clear();

            List<string> delete = new();

            foreach (var panel in Panels) {
                if (panel.WillDelete) {
                    delete.Add(panel.RwmodFile.FilePath);
                } else if (panel.IsEnabled) {
                    State.Prefs.EnabledMods.Add(panel.RwmodFile.Name);
                }
            }

            State.Prefs.Save();

            State.Mods.Reload(performingProgress);

            foreach (var file in delete) {
                File.Delete(file);
            }

            manager.RequestMainProcessSwitch(ProcessManager.ProcessID.MainMenu);
        }

        public override string UpdateInfoText()
        {
            if (selectedObject is IHoverable hoverable) {
                return hoverable.GetHoverInfo(selectedObject);
            }
            if (selectedObject?.owner is IHoverable ownerHoverable) {
                return ownerHoverable.GetHoverInfo(selectedObject);
            }
            if (selectedObject == cancelButton) return "Return to main menu";
            if (selectedObject == saveButton) return "Return to main menu, save changes, and reload mods";
            if (selectedObject == enableAll) return "Enable all mods";
            if (selectedObject == disableAll) return "Disable all mods";
            if (selectedObject == refresh) return "Refresh mods";

            return base.UpdateInfoText();
        }
    }
}