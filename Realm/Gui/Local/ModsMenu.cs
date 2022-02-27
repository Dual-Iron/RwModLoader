using BepInEx;
using Menu;
using Realm.Assets;
using Realm.Gui.Installation;
using Realm.Jobs;
using Realm.Logging;
using System.Diagnostics;
using UnityEngine;

namespace Realm.Gui.Local;

sealed class ModsMenu : Menu.Menu
{
    public const ProcessManager.ProcessID ModsMenuID = (ProcessManager.ProcessID)(-666);

    // Buttons that are always available
    private readonly MenuLabel quitWarning;
    private readonly SimpleButton openPluginsButton;
    private readonly SimpleButton cancelButton;
    private readonly SimpleButton saveButton;
    private readonly SimpleButton enableAll;
    private readonly SimpleButton disableAll;
    private readonly SimpleButton refresh;

    // Mod list
    private readonly Listing modListing;
    private readonly MenuContainer modListingGroup;
    private SimpleButton? openPatchesButton;

    // Logging progress
    private readonly LoggingProgressable progress = new();
    private readonly MenuContainer? progressContainer;
    private readonly ProgressableDisplay progressDisplay;

    private readonly FSprite headerSprite;
    private readonly FSprite headerShadowSprite;

    private Page Page => pages[0];

    private Job? reloadJob;
    private Job? refreshJob;
    private bool shutDownMusic;
    private bool forceExitGame;
    private bool quitOnSave;

    private bool PreventButtonClicks => manager.upcomingProcess != null || reloadJob != null || refreshJob != null;

    public ModsMenu(ProcessManager manager) : base(manager, ModsMenuID)
    {
        pages.Add(new(this, null, "main", 0));

        // Big pretty background picture
        Page.subObjects.Add(new InteractiveMenuScene(this, Page, ModsMenuGui.TimedScene) { cameraRange = 0.2f });

        // A scaled up translucent black pixel to make the background less distracting
        Page.subObjects.Add(new MenuSprite(Page, new(-1, -1), new("pixel") {
            color = new(0, 0, 0, 0.75f),
            scaleX = 2000,
            scaleY = 1000,
            anchorX = 0,
            anchorY = 0
        }));

        // Buttons
        //Page.subObjects.Add(nextButton = new BigArrowButton(this, Page, "", new(1116f, 668f), 1));
        Page.subObjects.Add(cancelButton = new(this, Page, "CANCEL", "", new(200, 50), new(110, 30)));
        Page.subObjects.Add(saveButton = new(this, Page, "SAVE & EXIT", "", new(360, 50), new(110, 30)));
        Page.subObjects.Add(refresh = new(this, Page, "REFRESH", "", new(200, 200), new(110, 30)));
        Page.subObjects.Add(disableAll = new(this, Page, "DISABLE ALL", "", new(200, 250), new(110, 30)));
        Page.subObjects.Add(enableAll = new(this, Page, "ENABLE ALL", "", new(200, 300), new(110, 30)));
        Page.subObjects.Add(openPluginsButton = new(this, Page, "PLUGINS", "", new(200, 350), new(110, 30)));

        modListing = new(Page, pos: new(1366 - ModPanel.Width - 200, 50), elementSize: new(ModPanel.Width, ModPanel.Height), elementsPerScreen: 15, edgePadding: 5);
        Page.subObjects.Add(modListing);
        Page.subObjects.Add(modListingGroup = new(Page));

        Page.subObjects.Add(progressContainer = new(Page));
        progressContainer.subObjects.Add(
            progressDisplay = new(progress, progressContainer, modListing.pos, modListing.size)
        );

        // This method must come after `progressDisplay` is set.
        // This comment exists because I forgot this once.
        StartRefreshJob();

        quitWarning = new(this, Page, "(this will close the game)", new(saveButton.pos.x, saveButton.pos.y - 30), saveButton.size, false);
        quitWarning.label.color = MenuColor(MenuColors.MediumGrey).rgb;
        quitWarning.label.isVisible = false;
        Page.subObjects.Add(quitWarning);

        ModsMenuMusic.Start(manager.musicPlayer);

        // Offset by tiny amount so it looks good
        float headerX = manager.rainWorld.options.ScreenSize.x / 2 - 0.01f; // 682.99
        float headerY = 680.01f;

        container.AddChild(headerShadowSprite = Asset.GetSpriteFromRes("ASSETS.MODS.SHADOW"));
        container.AddChild(headerSprite = Asset.GetSpriteFromRes("ASSETS.MODS"));

        headerSprite.x = headerShadowSprite.x = headerX;
        headerSprite.y = headerShadowSprite.y = headerY;

        headerSprite.shader = manager.rainWorld.Shaders["MenuText"];
    }

    private void StartRefreshJob()
    {
        progressDisplay.ShowProgressPercent = false;
        progress.Message(MessageType.Info, "Loading mods");
        refreshJob = Job.Start(() => State.CurrentRefreshCache.Refresh(progress));
    }

    private void UpdateModListing()
    {
        modListingGroup.ClearSubObjects();

        if (!Program.GetPatchMods().SequenceEqual(State.PatchMods)) {
            quitOnSave = true;

            MenuLabel notListedNotice = new(this, Page, "restart the game to refresh patch mods", new(modListing.pos.x, modListing.pos.y - modListing.size.y / 2 - 15), modListing.size, false);
            notListedNotice.label.color = MenuColor(MenuColors.MediumGrey).rgb;
            modListingGroup.subObjects.Add(notListedNotice);
        }
        else if (State.PatchMods.Count > 0) {
            quitOnSave = true;

            modListingGroup.subObjects.Add(openPatchesButton = new(this, Page, "PATCHES", "", new(200, 400), new(110, 30)));

            string s = State.PatchMods.Count > 1 ? "s" : "";

            MenuLabel notListedNotice = new(this, Page, $"and {State.PatchMods.Count} patch mod{s}", new(modListing.pos.x, modListing.pos.y - modListing.size.y / 2 - 15), modListing.size, false);
            notListedNotice.label.color = MenuColor(MenuColors.MediumGrey).rgb;
            modListingGroup.subObjects.Add(notListedNotice);
        }

        // Reset mod listing with new panels
        modListing.ClearListElements();

        foreach (var header in State.CurrentRefreshCache.Headers) {
            modListing.subObjects.Add(new ModPanel(header, Page, default));
        }
    }

    private IEnumerable<ModPanel> GetPanels()
    {
        foreach (var sob in modListing.subObjects)
            if (sob is ModPanel p)
                yield return p;
    }

    public override void ShutDownProcess()
    {
        headerSprite.RemoveFromContainer();
        headerShadowSprite.RemoveFromContainer();

        if (shutDownMusic) {
            ModsMenuMusic.ShutDown(manager.musicPlayer);
        }

        base.ShutDownProcess();
    }

    public override void Update()
    {
        var e = reloadJob?.Exception ?? refreshJob?.Exception;
        if (e != null) {
            reloadJob = null;
            refreshJob = null;

            forceExitGame = true;

            progress.Message(MessageType.Fatal, e.ToString());
        }

        if (refreshJob?.Status == JobStatus.Finished) {
            refreshJob = null;
            UpdateModListing();
        }

        foreach (var mob in Page.subObjects) {
            if (mob is ButtonTemplate button) {
                button.GetButtonBehavior.greyedOut = PreventButtonClicks || forceExitGame;
            }
        }

        if (forceExitGame) {
            cancelButton.menuLabel.text = "EXIT GAME";
            cancelButton.GetButtonBehavior.greyedOut = false;
        }

        base.Update();
    }

    public override void GrafUpdate(float timeStacker)
    {
        if (progressContainer != null) {
            progressContainer.Container.alpha = reloadJob != null || refreshJob != null ? 1 : 0;
        }

        quitWarning.label.isVisible = quitOnSave;

        base.GrafUpdate(timeStacker);
    }

    public override void Singal(MenuObject sender, string message)
    {
        if (sender == cancelButton) {
            if (forceExitGame) {
                Application.Quit();
                return;
            }

            manager.RequestMainProcessSwitch(ProcessManager.ProcessID.MainMenu);
            PlaySound(SoundID.MENU_Switch_Page_Out);
            shutDownMusic = true;
            return;
        }

        if (sender == saveButton && reloadJob == null) {
            progressDisplay.ShowProgressPercent = true;
            reloadJob = Job.Start(SaveExit);
            shutDownMusic = true;
            PlaySound(SoundID.MENU_Switch_Page_Out);

            if (quitOnSave) {
                reloadJob = null;
                forceExitGame = true;
            }
            return;
        }

        if (sender == enableAll) {
            foreach (ModPanel panel in GetPanels()) {
                panel.SetEnabled(true);
            }
        }
        else if (sender == disableAll) {
            foreach (ModPanel panel in GetPanels()) {
                panel.SetEnabled(false);
            }
        }
        else if (sender == refresh) {
            StartRefreshJob();
        }
        else if (sender == openPluginsButton) {
            Process.Start("explorer", $"\"{Path.Combine(Paths.BepInExRootPath, "plugins")}\"")
                .Dispose();
        }
        else if (sender == openPatchesButton) {
            Process.Start("explorer", $"\"{Path.Combine(Paths.BepInExRootPath, "monomod")}\"")
                .Dispose();
        }

        PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
    }

    private void SaveExit()
    {
        State.Prefs.EnabledMods.Clear();

        foreach (var panel in GetPanels()) {
            if (panel.WillDelete) {
                File.Delete(panel.FileHeader.FilePath);
            }
            else if (panel.IsEnabled) {
                State.Prefs.EnabledMods.Add(panel.FileHeader.Header.Name);
            }
        }

        State.Prefs.Save();

        if (quitOnSave) {
            Application.Quit();
            return;
        }

        FailedLoadNotif.UndoHooks();

        State.Mods.Reload(progress);

        if (progress.ProgressState == ProgressStateType.Failed) {
            forceExitGame = true;
        }
        else {
            manager.RequestMainProcessSwitch(ProcessManager.ProcessID.MainMenu);
        }
    }

    public override string UpdateInfoText()
    {
        if (selectedObject is IHoverable hoverable) {
            return hoverable.GetHoverInfo(selectedObject);
        }

        if (selectedObject?.owner is IHoverable ownerHoverable) {
            return ownerHoverable.GetHoverInfo(selectedObject);
        }

        if (selectedObject == cancelButton && forceExitGame) return "Exit game";
        if (selectedObject == cancelButton) return "Return to main menu";
        if (selectedObject == saveButton && quitOnSave) return "Save changes and exit the game";
        if (selectedObject == saveButton) return "Save changes and return to main menu";
        if (selectedObject == enableAll) return "Enable all mods";
        if (selectedObject == disableAll) return "Disable all mods";
        if (selectedObject == refresh) return "Refresh mod list";
        if (selectedObject == openPluginsButton) return "Open plugins folder";
        if (selectedObject == openPatchesButton) return "Open patch mods folder";

        return base.UpdateInfoText();
    }
}
