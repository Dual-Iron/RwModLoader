using BepInEx;
using Menu;
using Realm.Gui.Installation;
using Realm.Jobs;
using Realm.Logging;
using Realm.ModLoading;
using System.Diagnostics;
using UnityEngine;
using static Menu.Menu;

namespace Realm.Gui.Menus;

sealed class LocalMods : PositionedMenuObject, IHoverable, IMenuPage
{
    public bool BlockMenuInteraction => menu.manager.upcomingProcess != null || reloadJob != null || refreshJob != null || forceExitGame;

    readonly MenuLabel quitWarning;
    readonly MenuLabel changesMade;
    readonly SimpleButton openPluginsButton;
    readonly SimpleButton cancelButton;
    readonly SimpleButton saveButton;
    readonly SimpleButton enableAll;
    readonly SimpleButton disableAll;
    readonly SimpleButton refresh;
    readonly Listing modListing;
    readonly MenuContainer modListingGroup;
    SimpleButton? openPatchesButton;

    readonly LoggingProgressable progress = new();
    readonly MenuContainer progressContainer;
    readonly ProgressableDisplay progressDisplay;

    Job? reloadJob;
    Job? refreshJob;
    bool forceExitGame;
    bool quitOnSave;

    public LocalMods(MenuObject owner, Vector2 pos) : base(owner.menu, owner, pos)
    {
        // Buttons
        subObjects.Add(cancelButton = new(menu, this, "CANCEL", "", new(200, 50), new(110, 30)));
        subObjects.Add(saveButton = new(menu, this, "SAVE & EXIT", "", new(360, 50), new(110, 30)));
        subObjects.Add(refresh = new(menu, this, "REFRESH", "", new(200, 200), new(110, 30)));
        subObjects.Add(disableAll = new(menu, this, "DISABLE ALL", "", new(200, 250), new(110, 30)));
        subObjects.Add(enableAll = new(menu, this, "ENABLE ALL", "", new(200, 300), new(110, 30)));
        subObjects.Add(openPluginsButton = new(menu, this, "PLUGINS", "", new(200, 350), new(110, 30)));

        subObjects.Add(modListing = new(this, pos: new(1366 - 200 - ModPane.Width, 50), elementSize: new(ModPane.Width, ModPane.Height), elementsPerScreen: new(1, 15), edgePadding: new(0, 5)));
        subObjects.Add(modListingGroup = new(this));

        subObjects.Add(progressContainer = new(this));
        progressContainer.subObjects.Add(
            progressDisplay = new(progress, progressContainer, modListing.pos, modListing.size)
        );

        // This method must come after `progressDisplay` is set.
        // This comment exists because I forgot this once.
        StartRefreshJob();

        changesMade = new(menu, this, "*", new(cancelButton.pos.x + 81, cancelButton.pos.y + 13), default, true);
        changesMade.label.color = MenuColor(MenuColors.MediumGrey).rgb;
        changesMade.label.isVisible = false;
        subObjects.Add(changesMade);

        quitWarning = new(menu, this, "(this will close the game)", new(saveButton.pos.x, saveButton.pos.y - 30), saveButton.size, false);
        quitWarning.label.color = MenuColor(MenuColors.MediumGrey).rgb;
        quitWarning.label.isVisible = false;
        subObjects.Add(quitWarning);
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

        // Check if there are patch mods that aren't currently loaded, or patch mods that should be unloaded
        if (!Program.GetPatchMods().SequenceEqual(State.PatchMods)) {
            quitOnSave = true;

            MenuLabel notListedNotice = new(menu, this, "restart the game to refresh patch mods", new(modListing.pos.x, modListing.pos.y - modListing.size.y / 2 - 15), modListing.size, false);
            notListedNotice.label.color = MenuColor(MenuColors.MediumGrey).rgb;
            modListingGroup.subObjects.Add(notListedNotice);
        }
        // Check if there are any loaded patch mods
        else if (State.PatchMods.Count > 0) {
            quitOnSave = true;

            modListingGroup.subObjects.Add(openPatchesButton = new(menu, this, "PATCHES", "", new(200, 400), new(110, 30)));

            string s = State.PatchMods.Count > 1 ? "s" : "";

            MenuLabel notListedNotice = new(menu, this, $"and {State.PatchMods.Count} patch mod{s}", new(modListing.pos.x, modListing.pos.y - modListing.size.y / 2 - 15), modListing.size, false);
            notListedNotice.label.color = MenuColor(MenuColors.MediumGrey).rgb;
            modListingGroup.subObjects.Add(notListedNotice);
        }

        // Reset mod listing with new panels
        modListing.ClearListElements();

        foreach (var header in State.CurrentRefreshCache.Headers) {
            modListing.subObjects.Add(new ModPane(header, modListing));
        }
    }

    public override void Update()
    {
        // Check for errors on reload/refresh
        var e = reloadJob?.Exception ?? refreshJob?.Exception;
        if (e != null) {
            reloadJob = null;
            refreshJob = null;

            forceExitGame = true;

            progress.Message(MessageType.Fatal, e.ToString());
        }

        // If refreshing just finished, update the mod list
        if (refreshJob?.Status == JobStatus.Finished) {
            refreshJob = null;
            UpdateModListing();
        }

        // Make sure buttons are greyed out if need be
        foreach (var mob in subObjects.OfType<ButtonTemplate>()) {
            mob.buttonBehav.greyedOut = BlockMenuInteraction;
        }
        modListing.ForceBlockInteraction = BlockMenuInteraction;

        // Etc
        if (forceExitGame) {
            cancelButton.menuLabel.text = "EXIT GAME";
            cancelButton.buttonBehav.greyedOut = false;
        }

        base.Update();
    }

    public override void GrafUpdate(float timeStacker)
    {
        progressContainer.Container.alpha = reloadJob != null || refreshJob != null || forceExitGame ? 1 : 0;

        changesMade.label.isVisible = !forceExitGame && State.Prefs.AnyChanges;
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

            State.Prefs.Revert();
            menu.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.MainMenu);
            menu.PlaySound(SoundID.MENU_Switch_Page_Out);
            return;
        }

        if (sender == saveButton && reloadJob == null) {
            if (quitOnSave) {
                Application.Quit();
            }
            else {
                reloadJob = Job.Start(SaveExit);
                progressDisplay.ShowProgressPercent = true;
                menu.PlaySound(SoundID.MENU_Switch_Page_Out);
            }
            return;
        }

        if (sender == enableAll) {
            foreach (ModPane panel in modListing.subObjects.OfType<ModPane>()) {
                panel.IsEnabled = true;
            }
        }
        else if (sender == disableAll) {
            foreach (ModPane panel in modListing.subObjects.OfType<ModPane>()) {
                panel.IsEnabled = false;
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

        menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
    }

    private void SaveExit()
    {
        State.Prefs.Save();

        foreach (var panel in modListing.subObjects.OfType<ModPane>()) {
            if (panel.WillDelete) {
                File.Delete(panel.FileHeader.FilePath);
            }
        }

        FailedLoadNotif.UndoHooks();

        State.Mods.Reload(progress);

        if (progress.ProgressState == ProgressStateType.Failed) {
            forceExitGame = true;
        }
        else {
            menu.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.MainMenu);
        }
    }

    public void EnterFocus()
    {
        if (menu is ModMenu m && m.NeedsRefresh) {
            m.NeedsRefresh = false;

            StartRefreshJob();
        }
    }

    string? IHoverable.GetHoverInfo(MenuObject selected)
    {
        if (selected == cancelButton && forceExitGame) return "Exit game";
        if (selected == cancelButton) return "Return to main menu";
        if (selected == saveButton && quitOnSave) return "Save changes and exit the game";
        if (selected == saveButton) return "Save changes and return to main menu";
        if (selected == enableAll) return "Enable all mods";
        if (selected == disableAll) return "Disable all mods";
        if (selected == refresh) return "Refresh mod list";
        if (selected == openPluginsButton) return "Open plugins folder";
        if (selected == openPatchesButton) return "Open patch mods folder";
        return null;
    }
}
