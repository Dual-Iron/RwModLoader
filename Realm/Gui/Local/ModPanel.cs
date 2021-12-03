using Menu;
using Realm.ModLoading;
using UnityEngine;

namespace Realm.Gui.Local;

sealed class ModPanel : RectangularMenuObject, CheckBox.IOwnCheckBox, IListable, IHoverable
{
    public const float Height = 36;
    public const float Width = 540;

    public ModPanel(RwmodFileHeader fileHeader, MenuObject owner, Vector2 pos) : base(owner.menu, owner, pos, new(Width, Height))
    {
        var header = fileHeader.Header;

        FileHeader = fileHeader;

        FContainer parent = Container;
        parent.AddChild(Container = new());

        IsEnabled = header.Enabled();

        float posX = 0;

        subObjects.Add(enabledCheckBox = new CheckBox(menu, this, this, new(posX += 10, size.y / 2 - 12), 0, "", ""));

        string display = $"{header.Name} v{header.Version.Major}.{header.Version.Minor}";
        float displayWidth = display.MeasureWidth("DisplayFont");
        MenuLabel displayLabel = new(menu, this, display, new(posX += 34, 2), new(displayWidth, size.y), true);
        subObjects.Add(displayLabel);

        if (!string.IsNullOrEmpty(header.Owner)) {
            string author = $"by {header.Owner}";
            float authorWidth = author.MeasureWidth("font");
            MenuLabel authorLabel = new(menu, this, author, new(posX += displayWidth + 4, 2), new(authorWidth, size.y), false);
            subObjects.Add(authorLabel);
        }

        subObjects.Add(deleteButton = new(menu, this, "Menu_Symbol_Clear_All", "", new(size.x - 34, size.y / 2 - 12)));

        subObjects.Add(bar = new MenuSprite(this, new(60, 0), new("pixel") {
            scaleX = size.x - 120,
            scaleY = 1,
            alpha = 0.5f
        }));
    }

    public readonly RwmodFileHeader FileHeader;
    private readonly CheckBox enabledCheckBox;
    private readonly SymbolButton deleteButton;
    private readonly MenuSprite bar;

    public bool IsBelow { get; set; }
    public bool BlockInteraction { get; set; }
    public float Visibility { get; set; }

    public Vector2 Pos { set => pos = value; }
    public Vector2 Size => size;

    public bool IsEnabled { get; private set; }
    public bool WillDelete { get; private set; }

    public void SetEnabled(bool value)
    {
        IsEnabled = value;
        if (IsEnabled) {
            WillDelete = false;
        }
    }

    public void SetEnabledWithDependencies(bool value)
    {
        if (IsEnabled == value) {
            return;
        }

        SetEnabled(value);

        //if (value) {
        //    foreach (var sob in owner.subObjects) {
        //        if (sob is ModPanel p && RwmodFile.ModDependencies.Dependencies.Contains(p.RwmodFile.Name)) {
        //            p.SetEnabledWithDependencies(true);
        //        }
        //    }
        //} else {
        //    foreach (var sob in owner.subObjects) {
        //        if (sob is ModPanel p && p.RwmodFile.ModDependencies.Dependencies.Contains(RwmodFile.Name)) {
        //            p.SetEnabledWithDependencies(false);
        //        }
        //    }
        //}
    }

    public override void Update()
    {
        enabledCheckBox.GetButtonBehavior.greyedOut = BlockInteraction;
        deleteButton.GetButtonBehavior.greyedOut = BlockInteraction;

        base.Update();
    }

    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);

        Container.alpha = Visibility;

        if (IsBelow) {
            bar.sprite.isVisible = false;
        }

        if (WillDelete) {
            Color color = deleteButton.symbolSprite.color;
            color.r *= 4;
            color.g *= 0.5f;
            color.b *= 0.5f;
            deleteButton.symbolSprite.color = color;
        }
    }

    public override void Singal(MenuObject sender, string message)
    {
        if (sender == deleteButton) {
            IsEnabled = false;

            WillDelete = !WillDelete;

            menu.PlaySound(WillDelete ? SoundID.MENU_Checkbox_Check : SoundID.MENU_Checkbox_Uncheck);
        }
    }

    string IHoverable.GetHoverInfo(MenuObject selected)
    {
        if (selected == enabledCheckBox) {
            return $"Click to {(IsEnabled ? "disable" : "enable")}";
        }
        if (selected == deleteButton) {
            return $"Click to {(WillDelete ? "reinstall" : "uninstall")}";
        }
        return "";
    }

    bool CheckBox.IOwnCheckBox.GetChecked(CheckBox box) => IsEnabled;
    void CheckBox.IOwnCheckBox.SetChecked(CheckBox box, bool c) => SetEnabledWithDependencies(c);
}
