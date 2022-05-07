using static Menu.Menu;
using Menu;
using Realm.ModLoading;
using Rwml;
using UnityEngine;
using Realm.Assets;
using System.Diagnostics;

namespace Realm.Gui.Menus;

sealed class LocalModPane : RectangularMenuObject, CheckBox.IOwnCheckBox, IListable, IHoverable
{
    public const float Height = 42;
    public const float Width = 540;

    public LocalModPane(RwmodFileHeader fileHeader, MenuObject owner) : base(owner.menu, owner, default, new(Width, Height))
    {
        const float rightColWidth = 100;
        const float leftColPos = 52;
        const float leftColWidth = Width - rightColWidth - leftColPos - 4; // 4 pixels of padding

        owner.Container.AddChild(Container = new());

        FileHeader = fileHeader;

        string subtext = Subtext(fileHeader.Header);

        subObjects.Add(enabledBox = new CheckBox(menu, this, this, new(10, (Height - 32) / 2), 0, "", ""));
        enabledBox.size = enabledBox.roundedRect.size = new(32, 32);

        if (subtext.Length > 0) {
            subObjects.Add(new MenuLabel(menu, this, fileHeader.Header.Name.CullLong("DisplayFont", leftColWidth), new(leftColPos, Height / 2 - 2), new(0, Height / 2), true)
                .WithAlignment(FLabelAlignment.Left));
            subObjects.Add(new MenuLabel(menu, this, subtext.CullLong("font", leftColWidth), new(leftColPos, 0), new(0, Height / 2), false)
                .WithAlignment(FLabelAlignment.Left)
                .WithColor(MenuColors.MediumGrey));
        }
        else {
            subObjects.Add(new MenuLabel(menu, this, fileHeader.Header.Name.CullLong("DisplayFont", leftColWidth), new(leftColPos, 0), new(0, Height), true)
                .WithAlignment(FLabelAlignment.Left));
        }

        float posY = (Height - 32) / 2;
        float posX = size.x;

        subObjects.Add(deleteButton = new(menu, this, "Menu_Symbol_Clear_All", "", new(posX -= 42, posY)));
        deleteButton.size = deleteButton.roundedRect.size = new(32, 32);

        if (fileHeader.Header.Homepage.Length > 0) {
            subObjects.Add(homepageButton = new(menu, this, Asset.SpriteFromRes("LINK").element.name, "", new(posX -= 42, posY)));
            homepageButton.size = homepageButton.roundedRect.size = new(32, 32);
        }
    }

    public readonly RwmodFileHeader FileHeader;
    public readonly CheckBox enabledBox;

    private readonly SymbolButton deleteButton;
    private readonly SymbolButton? homepageButton;

    public bool BlockInteraction { get; set; }
    public float Visibility { get; set; }

    public Vector2 Pos { get => pos; set => pos = value; }
    public Vector2 Size => size;

    public bool WillDelete { get; private set; }

    public override void Update()
    {
        enabledBox.buttonBehav.greyedOut = BlockInteraction;
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
        else if (sender == homepageButton) {
            Process.Start(FileHeader.Header.Homepage)?.Dispose();

            menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
        }
    }

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

    string? IHoverable.GetHoverInfo(MenuObject selected)
    {
        if (selected == enabledBox) return $"Click to {(enabledBox.Checked ? "disable" : "enable")}";
        if (selected == deleteButton) return $"Click to {(WillDelete ? "reinstall" : "uninstall")}";
        if (selected == homepageButton) {
            string homepage = FileHeader.Header.Homepage;
            if (homepage.StartsWith("https://"))
                homepage = homepage.Substring("https://".Length);
            homepage += "]";

            return $"Click to go to [{homepage.CullLong("font", 500, "...]")}";
        }

        return null;
    }

    bool CheckBox.IOwnCheckBox.GetChecked(CheckBox box) => IsEnabled;
    void CheckBox.IOwnCheckBox.SetChecked(CheckBox box, bool c) => IsEnabled = c;

    private static string Subtext(RwmodHeader header)
    {
        StringBuilder sb = new();
        if (header.Version is SemVer ver) {
            if ((header.Flags & RwmodHeader.FileFlags.AudbEntry) != 0) {
                sb.Append($"v{ver.Minor} ");
            }
            else {
                sb.Append($"v{ver} ");
            }
        }
        if (header.Owner.Length > 0) {
            sb.Append($"by {header.Owner} ");
        }
        if ((header.Flags & RwmodHeader.FileFlags.AudbEntry) != 0) {
            sb.Append("from AUDB ");

            if (header.Version is SemVer ver2) {
                bool newer = AudbEntry.AudbEntries.Any(e => e.Name == header.Name && e.Version > ver2.Minor);
                if (newer) {
                    sb.Append("- update available in browser!");
                }
            }
        }
        else if ((header.Flags & RwmodHeader.FileFlags.RdbEntry) != 0) {
            sb.Append("from RDB ");
        }
        return sb.ToString().TrimEnd();
    }
}
