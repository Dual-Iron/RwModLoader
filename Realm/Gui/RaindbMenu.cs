using Menu;
using Realm.Remote;
using System.Linq;

namespace Realm.Gui;

public sealed class RaindbMenu : Menu.Menu
{
    public const ProcessManager.ProcessID RaindbMenuID = (ProcessManager.ProcessID)(-667);

    public RaindbMenu(ProcessManager manager) : base(manager, RaindbMenuID)
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

        Page.subObjects.Add(backButton = new(this, Page, "", new(200f, 668f), -1));

        modListing = new(Page, pos: new(620, 50), elementSize: new(RaindbPanel.Width, RaindbPanel.Height), elementsPerScreen: 5, edgePadding: 5);

        var raindb = RaindbMod.Fetch();

        foreach (var mod in raindb) {
            modListing.subObjects.Add(new RaindbPanel(mod, Page, default));
        }

        Page.subObjects.Add(modListing);
    }

    private readonly BigArrowButton backButton;
    private readonly Listing modListing;

    private Page Page => pages[0];

    public override void Update()
    {
        backButton.GetButtonBehavior.greyedOut = modListing.subObjects.Any(sob => sob is RaindbPanel rdbp && rdbp.PreventButtonClicks);

        base.Update();
    }

    public override void Singal(MenuObject sender, string message)
    {
        if (sender == backButton) {
            manager.RequestMainProcessSwitch(ModsMenu.ModsMenuID);
            PlaySound(SoundID.MENU_Switch_Arena_Gametype);
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
        if (selectedObject == backButton) return "Return to mods menu";

        return base.UpdateInfoText();
    }
}
