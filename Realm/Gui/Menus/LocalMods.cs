using BepInEx;
using Menu;
using Realm.Gui.Elements;
using Realm.Gui.Warnings;
using Realm.Logging;
using Realm.ModLoading;
using System.Diagnostics;
using UnityEngine;
using static Menu.Menu;

namespace Realm.Gui.Menus;

sealed class LocalMods : ModMenuPage, IHoverable
{
    static readonly string[] ReloadingIssues = { "Sharpener", "SeamlessLevels" };
    
    public override bool BlockMenuInteraction => menu.manager.upcomingProcess != null || reloadJob != null || refreshJob != null || forceExitGame;
    public override string Tooltip =>
@"This is your mod list.
The mods are sorted alphabetically.
Here, you can enable, disable, install, and uninstall mods.

To install mods, you can either:
- Put DLL files, ZIP files, and folders in the plugins folder, or
- Use the mod browser on the next page.";

    readonly MenuLabel quitWarning;
    readonly MenuLabel changesMade;
    readonly SimpleButton openPluginsButton;
    readonly SimpleButton cancelButton;
    readonly SimpleButton saveButton;
    readonly SimpleButton enableAll;
    readonly SimpleButton disableAll;
    readonly SimpleButton refresh;
    readonly Listing modListing;
    readonly FixedMenuContainer modListingGroup;
    SimpleButton? openPatchesButton;

    readonly LoggingProgressable progress = new();
    readonly FixedMenuContainer progressContainer;
    readonly ProgressableDisplay progressDisplay;

    Job? reloadJob;
    Job? refreshJob;
    bool forceExitGame;
    bool quitOnSave;

    public LocalMods(MenuObject owner, Vector2 pos) : base(owner, pos)
    {
        // Buttons
        subObjects.Add(cancelButton = new(menu, this, "CANCEL", "", new(200, 50), new(110, 30)));
        subObjects.Add(saveButton = new(menu, this, "SAVE & EXIT", "", new(360, 50), new(110, 30)));
        subObjects.Add(refresh = new(menu, this, "REFRESH", "", new(200, 200), new(110, 30)));
        subObjects.Add(disableAll = new(menu, this, "DISABLE ALL", "", new(200, 250), new(110, 30)));
        subObjects.Add(enableAll = new(menu, this, "ENABLE ALL", "", new(200, 300), new(110, 30)));
        subObjects.Add(openPluginsButton = new(menu, this, "PLUGINS", "", new(200, 350), new(110, 30)));

        // Reminder: 1366 is screen width on max res, and 1366 - 200 is the screen width on min res with some padding
        subObjects.Add(modListingGroup = new(this));
        subObjects.Add(
            modListing = new(this, pos: new(1366 - 200 - LocalModPane.Width, 50), elementSize: new(LocalModPane.Width, LocalModPane.Height), elementsPerScreen: new(1, 12), edgePadding: new(0, 5))
            );

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

        quitWarning = new(menu, this, "(this will close the game)", new(saveButton.pos.x, saveButton.pos.y - 28), saveButton.size, false);
        quitWarning.label.color = MenuColor(MenuColors.MediumGrey).rgb;
        quitWarning.label.isVisible = false;
        subObjects.Add(quitWarning);
    }

    public void DisplayError(Exception error)
    {
        const string title = "Something went wrong.";
        const string body = 
@"This is likely because one or more mods caused an error when they shouldn't have. For more details, check the game's log.

If you have an idea of which mod caused the error, disable the mod and send your log to that mod's developers.

To find which mod caused the error, disable half your mods until the error stops happening. Then, re-enable those mods one-by-one. When the error happens again, the last mod you enabled is likely the culprit.";

        var bodySplit = body.SplitLongLines(Gui.GetFont("font"), modListing.pos.x - 50 - 335);

        RoundedRect rect;
        MenuLabel errLabel;

        subObjects.Add(rect = new RoundedRect(menu, this, pos: new(335, 140), size: new(modListing.pos.x - 25 - 335, 275), true));
        rect.fillAlpha = 0.5f;

        subObjects.Add(errLabel = new MenuLabel(menu, this, title, new(335, rect.pos.y + rect.size.y - 10), new(rect.size.x, 0), true));
        errLabel.label.color = new(1, 0, 0);
        errLabel.label.anchorY = 1;

        subObjects.Add(errLabel = new MenuLabel(menu, this, bodySplit.JoinStr("\n"), new(335 + 8, rect.pos.y + rect.size.y - 50), default, false));
        errLabel.label.alignment = FLabelAlignment.Left;
        errLabel.label.anchorY = 1;
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

        string? message = null;

        // Check if there are patchers other than BepInEx Chainloader, MonoModder, and Realm
        if (BepInEx.Preloader.Patching.AssemblyPatcher.PatcherPlugins.Count > 3) {
            message = "can't reload because there are extra patcher plugins in \"Rain World/BepInEx/patchers\"";
        }
        // Check if there are patch mods that aren't currently loaded, or patch mods that should be unloaded
        else if (!Program.GetPatchMods().SequenceEqual(State.PatchMods)) {
            modListingGroup.subObjects.Add(openPatchesButton = new(menu, this, "PATCHES", "", new(200, 400), new(110, 30)));

            message = "updated patches prevent you from reloading";
        }
        // Check if there are any loaded patch mods
        else if (State.PatchMods.Count > 0) {
            modListingGroup.subObjects.Add(openPatchesButton = new(menu, this, "PATCHES", "", new(200, 400), new(110, 30)));

            string offenders = State.PatchMods.JoinStrEnglish();
            string s = State.PatchMods.Count > 1 ? "es" : "";
            string S = State.PatchMods.Count > 1 ? "" : "s";

            message = $"the patch{s} {offenders} prevent{S} you from reloading";
        }
        // Check if there are any mods that are blacklisted from reloading (https://github.com/Dual-Iron/RwModLoader/issues/7)
        else if (State.Mods.LoadedAssemblyPool != null) {
            var cantReload = State.Mods.LoadedAssemblyPool.LoadedAssemblies.Where(l => ReloadingIssues.Contains(l.AsmName)).ToList();
            if (cantReload.Count > 0) {
                string offenders = cantReload.Select(l => l.AsmName).JoinStrEnglish();
                string s = cantReload.Count > 1 ? "s" : "";
                string S = cantReload.Count > 1 ? "" : "s";

                message = $"the mod{s} {offenders} prevent{S} you from reloading";
            }
        }

        if (message != null) {
            quitOnSave = true;

            Vector2 noticePos = new(modListing.pos.x, quitWarning.pos.y);
            Vector2 noticeSize = new(modListing.size.x, quitWarning.size.y);
            MenuLabel notice = new(menu, this, message, noticePos, noticeSize, false);
            notice.label.color = MenuColor(MenuColors.MediumGrey).rgb;
            modListingGroup.subObjects.Add(notice);
        }

        // Reset mod listing with new panels
        modListing.ClearListElements();

        // Add the panels sorted alphabetically
        List<RwmodFileHeader> headers = State.CurrentRefreshCache.Headers.ToList();

        headers.Sort(RwmodFileHeader.AlphabeticSort);

        foreach (var header in headers) {
            modListing.subObjects.Add(new LocalModPane(header, modListing));
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
        if (refreshJob?.Finished == true) {
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
        quitWarning.label.alpha = saveButton.buttonBehav.col;

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
            SaveChanges();
            reloadJob = Job.Start(SaveExitJob);
            progressDisplay.ShowProgressPercent = true;
            menu.PlaySound(SoundID.MENU_Switch_Page_Out);
            return;
        }

        if (sender == enableAll) {
            foreach (LocalModPane panel in modListing.subObjects.OfType<LocalModPane>()) {
                panel.IsEnabled = true;
            }
        }
        else if (sender == disableAll) {
            foreach (LocalModPane panel in modListing.subObjects.OfType<LocalModPane>()) {
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

    private void SaveChanges()
    {
        State.Prefs.Save();

        foreach (var panel in modListing.subObjects.OfType<LocalModPane>()) {
            if (panel.WillDelete) {
                File.Delete(panel.FileHeader.FilePath);
            }
        }
    }

    private void SaveExitJob()
    {
        FailedLoad.UndoHooks();

        if (quitOnSave) {
            State.Mods.UnloadAndQuit(progress);
        }
        else {
            State.Mods.Reload(progress);
        }

        if (progress.ProgressState == ProgressStateType.Failed) {
            forceExitGame = true;
        }
        else {
            menu.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.MainMenu);
        }
    }

    public override void SetFocus(bool focus)
    {
        base.SetFocus(focus);

        if (focus && menu is ModMenu m && m.NeedsRefresh) {
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
        if (selected == openPatchesButton) return "Open patches folder";
        return null;
    }
}
