using Menu;
using Realm.Assets;
using UnityEngine;
using static Menu.Menu;
using Realm.Jobs;
using System.Diagnostics;
using Realm.ModLoading;
using System.Threading;
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

        owner.Container.AddChild(Container = new());

        FixedMenuContainer inner = new(this, new Vector2((TotalWidth - Width) / 2 + 1, (TotalHeight - Height) / 2));
        subObjects.Add(inner);

        inner.subObjects.Add(icon = new MenuSprite(inner, default, Asset.SpriteFromRes("NO_ICON")));
        icon.sprite.isVisible = false;

        inner.subObjects.Add(iconSpinny = new(inner, new Vector2(52, 52)));

        iconLoader = new($"{entry.Owner}~{entry.Name}", entry.Icon);
        iconLoader.StartLoading();

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

        inner.subObjects.Add(status = new(inner, new(128 + 32 * 2 + pad * 3, 16), default));

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
    enum DownloadStatus { None, InProgress, Success, Err }

    public readonly RdbEntry entry;

    readonly AsyncIcon iconLoader;
    readonly LoadSpinny iconSpinny;
    readonly MenuSprite icon;
    readonly SymbolButton downloadBtn;
    readonly SymbolButton homepageBtn;
    readonly MultiLabel status;

    Availability availability;
    DownloadStatus downloadStatus;
    string? downloadMessage;
    bool previewingHomepage;

    public bool PreventButtonClicks => downloadStatus == DownloadStatus.InProgress;

    public bool IsBelow { get; set; }
    public bool BlockInteraction { get; set; }
    public float Visibility { get; set; }
    public Vector2 Pos { get => pos; set => pos = value; }
    public Vector2 Size => size;

    public override void Update()
    {
        downloadBtn.buttonBehav.greyedOut = BlockInteraction || availability == Availability.Installed || downloadStatus == DownloadStatus.InProgress;
        homepageBtn.buttonBehav.greyedOut = BlockInteraction || entry.Homepage.Trim().Length == 0;

        // If the download just finished, display its message
        if (downloadMessage != null) {
            string culledMessage = downloadMessage.Replace('\n', ' ').CullLong("font", Width - status.pos.x - 8);
            Color color = downloadStatus == DownloadStatus.Success ? new(.5f, 1, .5f) : new(1, .5f, .5f);
            status.SetLabel(color, culledMessage);

            previewingHomepage = false;
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

    static readonly float prefixWidth = "Click again to visit [".MeasureWidth(Gui.GetFont("font"));

    public override void Singal(MenuObject sender, string message)
    {
        if (sender == downloadBtn) {
            previewingHomepage = false;
            downloadStatus = DownloadStatus.InProgress;

            status.SetLabel(MenuRGB(MenuColors.MediumGrey), "Downloading");
            menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
            Job.Start(Download);
        }
        else if (sender == homepageBtn) {
            if (!previewingHomepage) {
                previewingHomepage = true;

                string homepage = entry.Homepage;
                if (homepage.StartsWith("https://"))
                    homepage = homepage.Substring("https://".Length);

                status.SetLabel(MenuRGB(MenuColors.MediumGrey), "Click again to visit [");
                status.AddLabel(new(0.5f, 0.9f, 1f), homepage.CullLong("font", Width - status.pos.x - prefixWidth - 12));
                status.AddLabel(MenuRGB(MenuColors.MediumGrey), "]");
            }
            else {
                Process.Start(entry.Homepage)?.Dispose();
            }

            menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
        }
    }

    private void Download()
    {
        // TODO remove unnecessary Interlocked and verify that this lockless multithreading is safe

        BackendProcess proc = BackendProcess.Execute($"-rdb \"{entry.Owner}/{entry.Name}\"");

        if (proc.ExitCode == 0) {
            Program.Logger.LogInfo($"Downloaded {entry.Owner}/{entry.Name}");

            availability = Availability.Installed;
            downloadStatus = DownloadStatus.Success;
            downloadMessage = "Download successful";

            if (menu is ModMenu m) {
                m.NeedsRefresh = true;
            }
        }
        else if (proc.ExitCode == null) {
            downloadStatus = DownloadStatus.Err;
            downloadMessage = "Download timed out";
        }
        else {
            Program.Logger.LogError($"Failed to download {entry.Owner}/{entry.Name} with err {proc.ExitCode}.\n{proc.Error}");

            downloadStatus = DownloadStatus.Err;
            downloadMessage = proc.Error;
        }
    }

    string? IHoverable.GetHoverInfo(MenuObject selected)
    {
        if (selected == downloadBtn) return availability switch {
            Availability.Installed => "Mod is already installed",
            Availability.CanUpdate => "Update mod",
            _ => "Download mod",
        };
        if (selected == homepageBtn && string.IsNullOrEmpty(entry.Homepage)) return "Mod has no homepage";
        if (selected == homepageBtn && previewingHomepage) return "Visit homepage";
        if (selected == homepageBtn) return "Preview homepage";
        return null;
    }
}
