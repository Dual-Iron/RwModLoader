using Menu;
using Realm.Assets;
using UnityEngine;
using static Menu.Menu;
using System.Diagnostics;
using Realm.ModLoading;
using Realm.Gui.Elements;

namespace Realm.Gui.Menus;

sealed class BrowserPane : RectangularMenuObject, IListable, IHoverable
{
    public const float TotalWidth = 564;
    public const float TotalHeight = 134;
    public const float Width = 540;
    public const float Height = 128;

    public BrowserPane(MenuObject owner, RdbEntry entry) : base(owner.menu, owner, new(-10000, -10000), new(TotalWidth, TotalHeight))
    {
        const float pad = 8;            // padding between edges of available space
        const float padDesc = 12;       // padding between edges of desc
        const float verWidth = 100;     // row 1, col 2 (version) width
        const float updWidth = 150;     // row 2, col 2 (last updated) width

        this.entry = entry;

        downloadJob = new($"-rdb \"{entry.Owner}/{entry.Name}\"");
        downloadJob.OnFinish += FinishDownload;
        downloadJob.OnProgressUpdate += (i, j) => downloadProgress = i / (float)j;

        owner.Container.AddChild(Container = new());

        FixedMenuContainer inner = new(this, new Vector2((TotalWidth - Width) / 2 + 1, (TotalHeight - Height) / 2));
        subObjects.Add(inner);

        inner.subObjects.Add(icon = new MenuSprite(inner, default, Asset.SpriteFromRes("NO_ICON")));
        icon.sprite.isVisible = false;

        inner.subObjects.Add(iconSpinny = new(inner, new Vector2(52, 52)));

        iconLoader = new($"{entry.Owner}~{entry.Name}", entry.Icon);
        iconLoader.Start();

        // Add name + owner
        Label(entry.Name.CullLong("DisplayFont", Width - 128 - verWidth - pad * 2), pos: new(128 + pad, 110), true).WithAlignment(FLabelAlignment.Left);
        Label($"by {entry.Owner}".CullLong("font", Width - 128 - updWidth - pad * 2), pos: new(128 + pad, 86)).WithAlignment(FLabelAlignment.Left).WithColor(MenuColors.MediumGrey);

        // Add description
        var descStr = Gui.SplitLinesAndCull(entry.Description, width: Width - 128 - padDesc * 2, rows: 2);
        var desc = new MenuLabel(menu, inner, descStr.JoinStr("\n"), new(128 + padDesc, 74), new(), false);
        desc.label.anchorY = 1f;
        desc.label.alignment = FLabelAlignment.Left;
        desc.label.color = MenuColor(MenuColors.MediumGrey).rgb;
        inner.subObjects.Add(desc);

        // Add version + last updated text
        string updatedText = $"updated {Gui.GetRelativeTime(entry.LastUpdated)}";

        Label($"v{entry.Version}".CullLong("font", verWidth - pad), new(Width - pad, 106)).WithAlignment(FLabelAlignment.Right);
        Label(updatedText.CullLong("font", updWidth - pad), new(Width - pad, 86)).WithAlignment(FLabelAlignment.Right).WithColor(MenuColors.MediumGrey);

        inner.subObjects.Add(downloadBtn = new SymbolButton(menu, inner, Asset.SpriteFromRes("DOWNLOAD").element.name, "", new(128 + pad, pad)));
        downloadBtn.size = new(32, 32);
        downloadBtn.roundedRect.size = downloadBtn.size;

        inner.subObjects.Add(homepageBtn = new SymbolButton(menu, inner, Asset.SpriteFromRes("LINK").element.name, "", new(128 + 32 + pad * 2, pad)));
        homepageBtn.size = new(32, 32);
        homepageBtn.roundedRect.size = homepageBtn.size;

        inner.subObjects.Add(downloadLabel = new(inner, new(128 + 32 * 2 + pad * 3, 16), default));
        inner.subObjects.Add(homepageLabel = new(inner, new(128 + 32 * 2 + pad * 3, 32), default));

        RwmodFileHeader match = State.CurrentRefreshCache.Headers.FirstOrDefault(h => h.Header.Owner == entry.Owner && h.Header.Name == entry.Name);

        if (match.Header != null && (match.Header.Flags & RwmodHeader.FileFlags.RdbEntry) != 0) {
            if (match.Header.Version >= entry.Version) {
                availability = Availability.Installed;
            }
            else if (!downloadBtn.buttonBehav.greyedOut) {
                availability = Availability.CanUpdate;
            }
        }

        MenuLabel Label(string txt, Vector2 pos, bool big = false)
        {
            MenuLabel ret = new(menu, inner, txt, pos, default, big);
            owner.subObjects.Add(ret);
            return ret;
        }
    }

    enum Availability { CanInstall, Installed, CanUpdate }

    public readonly RdbEntry entry;

    readonly AsyncIcon iconLoader;
    readonly LoadSpinny iconSpinny;
    readonly MenuSprite icon;
    readonly SymbolButton downloadBtn;
    readonly SymbolButton homepageBtn;
    readonly MultiLabel downloadLabel;
    readonly MultiLabel homepageLabel;
    readonly AsyncDownload downloadJob;

    Availability availability;
    string? downloadMessage;
    float downloadProgress;

    public bool PreventButtonClicks => downloadJob.Status == AsyncDownloadStatus.Downloading;

    public bool IsBelow { get; set; }
    public bool BlockInteraction { get; set; }
    public float Visibility { get; set; }
    public Vector2 Pos { get => pos; set => pos = value; }
    public Vector2 Size => size;

    public override void Update()
    {
        downloadBtn.buttonBehav.greyedOut = BlockInteraction || availability == Availability.Installed || downloadJob.Status == AsyncDownloadStatus.Downloading;
        homepageBtn.buttonBehav.greyedOut = BlockInteraction || entry.Homepage.Trim().Length == 0;

        // If the download just finished, display its message
        if (downloadProgress > 0 && downloadProgress < 1) {
            downloadLabel.SetLabel(MenuRGB(MenuColors.MediumGrey), $"Downloading... ");
            downloadLabel.AddLabel(MenuRGB(MenuColors.DarkGrey), $"{Mathf.FloorToInt(downloadProgress * 100)}%");
        }

        if (downloadMessage != null) {
            string culledMessage = downloadMessage.Replace('\n', ' ').CullLong("font", Width - downloadLabel.pos.x - 8);
            Color color = downloadJob.Status == AsyncDownloadStatus.Success ? new(.5f, 1, .5f) : new(1, .5f, .5f);
            downloadLabel.SetLabel(color, culledMessage);

            downloadMessage = null;
        }

        base.Update();
    }

    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);

        if (availability == Availability.CanUpdate) {
            var color = downloadBtn.symbolSprite.color;

            downloadBtn.symbolSprite.color = new(color.r / 2, color.g, color.b / 2);
        }

        Container.alpha = (float)Mathf.Pow(Mathf.InverseLerp(0.5f, 1, Visibility), 3);

        if (iconLoader.Status == AsyncIconStatus.Errored) {
            iconSpinny.ico.sprite.isVisible = false;
            icon.sprite.isVisible = true;
        }
        else if (iconLoader.Status == AsyncIconStatus.Loaded && !icon.sprite.isVisible) {
            iconSpinny.ico.sprite.isVisible = false;
            icon.sprite.isVisible = true;
            icon.sprite.element = Futile.atlasManager._allElementsByName[entry.Icon];
        }
    }

    public override void Singal(MenuObject sender, string message)
    {
        if (sender == downloadBtn && downloadJob.Status == AsyncDownloadStatus.Unstarted) {
            downloadLabel.SetLabel(MenuRGB(MenuColors.MediumGrey), "Downloading...");
            menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
            downloadJob.Start();
        }
        else if (sender == homepageBtn) {
            if (homepageLabel.IsEmpty) {
                float prefixWidth = "Click again to visit [".MeasureWidth(Gui.GetFont("font"));

                string homepage = entry.Homepage;
                if (homepage.StartsWith("https://"))
                    homepage = homepage.Substring("https://".Length);

                homepageLabel.SetLabel(MenuRGB(MenuColors.MediumGrey), "Click again to visit [");
                homepageLabel.AddLabel(new(0.5f, 0.9f, 1f), homepage.CullLong("font", Width - homepageLabel.pos.x - prefixWidth - 12));
                homepageLabel.AddLabel(MenuRGB(MenuColors.MediumGrey), "]");
            }
            else {
                Process.Start(entry.Homepage)?.Dispose();
            }

            menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
        }
    }

    private void FinishDownload()
    {
        downloadMessage = downloadJob.ToString();
        availability = Availability.Installed;

        if (menu is ModMenu modMenu) modMenu.NeedsRefresh = true;
    }

    string? IHoverable.GetHoverInfo(MenuObject selected)
    {
        if (selected == downloadBtn) return availability switch {
            Availability.Installed => "Mod is already installed",
            Availability.CanUpdate => "Update mod",
            _ => "Download mod",
        };
        if (selected == homepageBtn && string.IsNullOrEmpty(entry.Homepage)) return "Mod has no homepage";
        if (selected == homepageBtn && homepageLabel.IsEmpty) return "Preview homepage";
        if (selected == homepageBtn) return "Visit homepage";
        return null;
    }
}
