using Menu;
using Realm.ModLoading;
using UnityEngine;

namespace Realm.Gui.Menus;

sealed class ModPane : RectangularMenuObject, CheckBox.IOwnCheckBox, IListable, IHoverable
{
    public const float Height = 36;
    public const float Width = 540;

    public ModPane(RwmodFileHeader fileHeader, MenuObject owner) : base(owner.menu, owner, default, new(Width, Height))
    {
        var header = fileHeader.Header;

        FileHeader = fileHeader;

        FContainer parent = Container;
        parent.AddChild(Container = new());

        float posX = 0;

        subObjects.Add(enabledCheckBox = new CheckBox(menu, this, this, new(posX += 10, size.y / 2 - 12), 0, "", ""));

        string display = $"{header.Name}";
        if (header.Version.HasValue) {
            display += $" v{header.Version.Value.Major}.{header.Version.Value.Minor}";
        }
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
    }

    public readonly RwmodFileHeader FileHeader;
    private readonly CheckBox enabledCheckBox;
    private readonly SymbolButton deleteButton;

    public bool IsBelow { get; set; }
    public bool BlockInteraction { get; set; }
    public float Visibility { get; set; }

    public Vector2 Pos { get => pos; set => pos = value; }
    public Vector2 Size => size;

    public bool IsEnabled {
        get => FileHeader.Header.Enabled();
        set {
            if (value) {
                State.Prefs.EnabledMods.Add(FileHeader.Header.Name);
                WillDelete = false;
            }
            else {
                State.Prefs.EnabledMods.Remove(FileHeader.Header.Name);
            }
        }
    }
    public bool WillDelete { get; private set; }

    public override void Update()
    {
        enabledCheckBox.buttonBehav.greyedOut = BlockInteraction;
        deleteButton.buttonBehav.greyedOut = BlockInteraction;

        base.Update();
    }

    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);

        Container.alpha = Visibility;

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
            State.Prefs.EnabledMods.Remove(FileHeader.Header.Name);

            WillDelete = !WillDelete;

            menu.PlaySound(WillDelete ? SoundID.MENU_Checkbox_Check : SoundID.MENU_Checkbox_Uncheck);
        }
    }

    string? IHoverable.GetHoverInfo(MenuObject selected)
    {
        if (selected == enabledCheckBox) return $"Click to {(IsEnabled ? "disable" : "enable")}";
        if (selected == deleteButton) return $"Click to {(WillDelete ? "reinstall" : "uninstall")}";
        return null;
    }

    bool CheckBox.IOwnCheckBox.GetChecked(CheckBox box) => IsEnabled;
    void CheckBox.IOwnCheckBox.SetChecked(CheckBox box, bool c) => IsEnabled = c;
}
