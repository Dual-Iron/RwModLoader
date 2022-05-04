using Menu;
using Realm.Assets;
using Realm.Gui.Elements;
using RWCustom;
using UnityEngine;

namespace Realm.Gui.Menus;

sealed class ModMenu : Menu.Menu, ITextBoxMenu
{
    public const ProcessManager.ProcessID ModMenuID = (ProcessManager.ProcessID)(-666);

    const int minPage = 0;
    const int maxPage = 1;

    readonly FSprite headerSprite;
    readonly FSprite headerShadowSprite;
    readonly BigArrowButton right;
    readonly BigArrowButton left;
    readonly InfoButton info;
    readonly LocalMods local;
    readonly Browser browser;

    public bool NeedsRefresh;

    int page;
    float pageSmoothed;

    public ModMenu(ProcessManager manager, Exception error) : this(manager) => local.DisplayError(error);

    public ModMenu(ProcessManager manager) : base(manager, ModMenuID)
    {
        pages.Add(new(this, null, "main", 0));

        // Big pretty background picture
        pages[0].subObjects.Add(new InteractiveMenuScene(this, pages[0], ModMenuHooks.TimedScene) { cameraRange = 0.2f });

        // A scaled up translucent black pixel to make the background less distracting
        pages[0].subObjects.Add(new MenuSprite(pages[0], new(-1, -1), new("pixel") {
            color = new(0, 0, 0, 0.75f),
            scaleX = 2000,
            scaleY = 1000,
            anchorX = 0,
            anchorY = 0
        }));

        ModMenuMusic.Start(manager.musicPlayer);

        // Offset by tiny amount so it looks good
        float headerX = manager.rainWorld.options.ScreenSize.x / 2 - 0.01f; // 682.99
        float headerY = 680.01f;

        container.AddChild(headerShadowSprite = Asset.SpriteFromRes("MODS_SHADOW"));
        container.AddChild(headerSprite = Asset.SpriteFromRes("MODS"));

        headerSprite.x = headerShadowSprite.x = headerX;
        headerSprite.y = headerShadowSprite.y = headerY;
        headerSprite.shader = manager.rainWorld.Shaders["MenuText"];

        // Add pages and don't include selectables from anything except LocalMods
        pages[0].subObjects.Add(browser = new Browser(pages[0], new(2000, 0)));
        pages[0].selectables.Clear();
        pages[0].subObjects.Add(local = new LocalMods(pages[0], default));

        // Add arrows + trivia
        pages[0].subObjects.Add(left = new BigArrowButton(this, pages[0], "", new(200, 668), -1));
        pages[0].subObjects.Add(right = new BigArrowButton(this, pages[0], "", new(1366 - 250, 668), 1));
        pages[0].subObjects.Add(info = new InfoButton(pages[0], new(1142, 624), Info));

        InfoBox Info() => new() { Text = PageAt(page).Tooltip, BigText = true };
    }

    public bool MovingPages => Mathf.Abs(page - pageSmoothed) > 0.01f;

    public MenuObject? FocusedOn { get; set; }

    public override bool FreezeMenuFunctions => base.FreezeMenuFunctions || FocusedOn != null;

    public override void ShutDownProcess()
    {
        headerSprite.RemoveFromContainer();
        headerShadowSprite.RemoveFromContainer();
        ModMenuMusic.ShutDown(manager.musicPlayer);

        base.ShutDownProcess();
    }

    private ModMenuPage PageAt(int index)
    {
        return index switch {
            0 => local,
            1 => browser,
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    public override string UpdateInfoText()
    {
        if (selectedObject == left) return "Previous page";
        if (selectedObject == right) return "Next page";
        if (selectedObject == info) return "Page description";

        var obj = selectedObject;
        while (obj != null) {
            if (obj is IHoverable hoverable && hoverable.GetHoverInfo(selectedObject) is string s) {
                return s;
            }
            obj = obj.owner;
        }
        return base.UpdateInfoText();
    }

    public override void Singal(MenuObject sender, string message)
    {
        if (sender == left && page > minPage) {
            ChangePage(page, page - 1);
        }

        if (sender == right && page < maxPage) {
            ChangePage(page, page + 1);
        }
    }

    private void ChangePage(int oldPage, int newPage)
    {
        // Remove old page from selectables
        pages[0].RecursiveRemoveSelectables(PageAt(oldPage));

        // Add new page
        pages[0].selectables.AddRange(PageAt(newPage).RecursiveSubObjects().OfType<SelectableMenuObject>());

        PlaySound(SoundID.MENU_Switch_Arena_Gametype);

        PageAt(oldPage).SetFocus(false);
        PageAt(newPage).SetFocus(true);

        page = newPage;
    }

    public override void Update()
    {
        bool block = PageAt(page).BlockMenuInteraction;

        left.buttonBehav.greyedOut = page == minPage || block;
        right.buttonBehav.greyedOut = page == maxPage || block;
        info.ForceClose = MovingPages || block;

        pageSmoothed = Custom.LerpAndTick(pageSmoothed, page, 0.1f, 0.02f);

        for (int i = minPage; i <= maxPage; i++) {
            PageAt(i).pos.x = (i - pageSmoothed) * 1400;
        }

        base.Update();
    }
}
