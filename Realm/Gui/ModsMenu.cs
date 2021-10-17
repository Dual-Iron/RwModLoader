using Menu;
using Realm.Assets;
using Realm.Jobs;
using Realm.Logging;
using UnityEngine;

namespace Realm.Gui;

public sealed class ModsMenu : Menu.Menu
{
    public const ProcessManager.ProcessID ModsMenuID = (ProcessManager.ProcessID)(-666);

    public ModsMenu(ProcessManager manager) : base(manager, ModsMenuID)
    {
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
        //Page.subObjects.Add(nextButton = new BigArrowButton(this, Page, "", new(1116f, 668f), 1));
        Page.subObjects.Add(cancelButton = new SimpleButton(this, Page, "CANCEL", "", new(200, 50), new(110, 30)));
        Page.subObjects.Add(saveButton = new SimpleButton(this, Page, "SAVE & EXIT", "", new(360, 50), new(110, 30)));
        Page.subObjects.Add(refresh = new SimpleButton(this, Page, "REFRESH", "", new(200, 200), new(110, 30)));
        Page.subObjects.Add(disableAll = new SimpleButton(this, Page, "DISABLE ALL", "", new(200, 250), new(110, 30)));
        Page.subObjects.Add(enableAll = new SimpleButton(this, Page, "ENABLE ALL", "", new(200, 300), new(110, 30)));

        modListing = new(Page, pos: new(650, 50), elementSize: new(ModPanel.Width, ModPanel.Height), elementsPerScreen: 15, edgePadding: 5);

        State.Instance.CurrentRefreshCache.Refresh(new MessagingProgressable());

        foreach (var header in State.Instance.CurrentRefreshCache.Headers) {
            modListing.subObjects.Add(new ModPanel(header, Page, default));
        }

        Page.subObjects.Add(modListing);

        Page.subObjects.Add(
            progDisplayContainer = new(Page)
        );
        progDisplayContainer.subObjects.Add(
            new ProgressableDisplay(performingProgress, progDisplayContainer, modListing.pos, modListing.size)
        );

        ModsMenuMusic.Start(manager.musicPlayer);

        const float headerX = 682.99f;
        const float headerY = 680.01f;

        container.AddChild(headerShadowSprite = Asset.GetSpriteFromRes("ASSETS.MODS.SHADOW"));
        container.AddChild(headerSprite = Asset.GetSpriteFromRes("ASSETS.MODS"));

        headerSprite.x = headerShadowSprite.x = headerX;
        headerSprite.y = headerShadowSprite.y = headerY;

        headerSprite.shader = manager.rainWorld.Shaders["MenuText"];
    }

    private readonly SimpleButton cancelButton;
    private readonly SimpleButton saveButton;
    private readonly SimpleButton enableAll;
    private readonly SimpleButton disableAll;
    private readonly SimpleButton refresh;
    private readonly Listing modListing;

    private readonly MenuContainer progDisplayContainer;
    private readonly LoggingProgressable performingProgress = new();
    private readonly BigArrowButton? nextButton;

    private readonly FSprite headerSprite;
    private readonly FSprite headerShadowSprite;

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
        headerSprite.RemoveFromContainer();
        headerShadowSprite.RemoveFromContainer();

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
        State.Instance.Prefs.EnabledMods.Clear();

        List<string> delete = new();

        foreach (var panel in Panels) {
            if (panel.WillDelete) {
                delete.Add(panel.Rwmod.FilePath);
            } else if (panel.IsEnabled) {
                State.Instance.Prefs.EnabledMods.Add(panel.Rwmod.Name);
            }
        }

        State.Instance.Prefs.Save();

        State.Instance.Mods.Reload(performingProgress);

        foreach (var file in delete) {
            File.Delete(file);
        }

        if (performingProgress.ProgressState != ProgressStateType.Failed) {
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

        if (selectedObject == cancelButton) return "Return to main menu";
        if (selectedObject == saveButton) return "Save changes, reload mods, and return to main menu";
        if (selectedObject == enableAll) return "Enable all mods";
        if (selectedObject == disableAll) return "Disable all mods";
        if (selectedObject == refresh) return "Refresh mods";

        return base.UpdateInfoText();
    }
}
