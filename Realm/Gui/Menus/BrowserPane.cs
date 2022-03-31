using Menu;
using Realm.Assets;
using UnityEngine;
using static Menu.Menu;
using Realm.Jobs;
using System.Diagnostics;
using Realm.ModLoading;
using System.Threading;
using System.Collections;

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

        MenuContainer inner = new(this) { pos = new((TotalWidth - Width) / 2 + 1, (TotalHeight - Height) / 2) };
        subObjects.Add(inner);

        inner.subObjects.Add(icon = new MenuSprite(inner, default, Asset.SpriteFromRes("NO_ICON")));

        SetIcon();

        // Add name + owner
        LabelLeft(inner, entry.Name.CullLong("DisplayFont", Width - 128 - verWidth - pad * 2), pos: new(128 + pad, 110), big: true, dull: false);
        LabelLeft(inner, $"by {entry.Owner}".CullLong("font", Width - 128 - updWidth - pad * 2), pos: new(128 + pad, 86));

        // Add description
        var descStr = GuiExt.SplitLinesAndCull(entry.Description, width: Width - 128 - padDesc * 2, rows: 2);
        var desc = new MenuLabel(menu, inner, descStr.JoinStr("\n"), new(128 + padDesc, 74), new(), false);
        desc.label.anchorY = 1f;
        desc.label.alignment = FLabelAlignment.Left;
        desc.label.color = MenuColor(MenuColors.MediumGrey).rgb;
        inner.subObjects.Add(desc);

        // Add version + last updated text
        string updatedText = $"updated {GuiExt.GetRelativeTime(entry.LastUpdated)}";

        LabelRight(inner, $"v{entry.Version}".CullLong("font", verWidth - pad), new(Width - pad, 106), dull: false);
        LabelRight(inner, updatedText.CullLong("font", updWidth - pad), new(Width - pad, 86));

        inner.subObjects.Add(downloadBtn = new SymbolButton(menu, inner, Asset.SpriteFromRes("DOWNLOAD").element.name, "", new(128 + pad, pad)));
        downloadBtn.size = new(32, 32);
        downloadBtn.roundedRect.size = downloadBtn.size;

        inner.subObjects.Add(homepageBtn = new SymbolButton(menu, inner, Asset.SpriteFromRes("LINK").element.name, "", new(128 + 32 + pad * 2, pad)));
        homepageBtn.size = new(32, 32);
        homepageBtn.roundedRect.size = homepageBtn.size;

        inner.subObjects.Add(status = new(inner, new(128 + 32 * 2 + pad * 3, 16), default));

        RwmodFileHeader match = State.CurrentRefreshCache.Headers.FirstOrDefault(h => h.Header.Owner == entry.Owner && h.Header.Name == entry.Name);

        if (match.Header != null && (match.Header.Flags & RwmodHeader.FileFlags.IsRdbEntry) != 0) {
            if (match.Header.Version >= entry.Version) {
                availability = Availability.Installed;
            }
            else if (!downloadBtn.buttonBehav.greyedOut) {
                availability = Availability.CanUpdate;
            }
        }
    }

    private static void LabelLeft(MenuObject owner, string txt, Vector2 pos, bool big = false, bool dull = true)
    {
        MenuLabel ret = new(owner.menu, owner, txt, pos, default, big);
        ret.label.alignment = FLabelAlignment.Left;
        owner.subObjects.Add(ret);

        if (dull) {
            ret.label.color = MenuRGB(MenuColors.MediumGrey);
        }
    }

    private static void LabelRight(MenuObject owner, string txt, Vector2 pos, bool big = false, bool dull = true)
    {
        MenuLabel ret = new(owner.menu, owner, txt, pos, default, big);
        ret.label.alignment = FLabelAlignment.Right;
        owner.subObjects.Add(ret);

        if (dull) {
            ret.label.color = MenuRGB(MenuColors.MediumGrey);
        }
    }

    int loadIcon; // 0 for loading, 1 for loaded and ready to finish, 2 for finished

    private void SetIcon()
    {
        if (Futile.atlasManager._allElementsByName.TryGetValue(entry.Icon, out var elem)) {
            Interlocked.Exchange(ref loadIcon, 1);
            return;
        }

        string path = Path.Combine(RealmPaths.IconFolder.FullName, $"{entry.Owner}~{entry.Name}");

        if (File.Exists(path) && (DateTime.UtcNow - File.GetLastWriteTimeUtc(path)).TotalDays < 10) {
            LoadIcon();
            return;
        }

        BackendProcess proc = BackendProcess.Execute($"-dl \"{entry.Icon}\" \"{path}\"");

        if (proc.ExitCode != 0) {
            Program.Logger.LogError($"Error downloading icon for {entry.Owner}/{entry.Name}. {proc}");
        }
        else if (File.Exists(path)) {
            LoadIcon();
        }

        void LoadIcon()
        {
            using FileStream stream = File.OpenRead(path);
            Texture2D tex = Asset.LoadTexture(stream);

            if (tex.width != 128 || tex.height != 128) {
                Program.Logger.LogError($"Icon texture for {entry.Owner}/{entry.Name} was not 128x128");
                UnityEngine.Object.Destroy(tex);
            }
            else {
                HeavyTexturesCache.LoadAndCacheAtlasFromTexture(entry.Icon, tex);
                Interlocked.Exchange(ref loadIcon, 1);
            }
        }
    }

    enum Availability { CanInstall, Installed, CanUpdate }

    readonly RdbEntry entry;
    readonly MenuSprite icon;
    readonly SymbolButton downloadBtn;
    readonly SymbolButton homepageBtn;
    readonly MultiLabel status;
    readonly Availability availability;

    const int DownloadInProgress = 1;
    const int DownloadSuccess = 2;
    const int DownloadErr = 3;

    int downloadStatus; // must be int for use with Interlocked.Exchange (see Download() method)
    string? downloadMessage;
    bool previewingHomepage;

    public bool PreventButtonClicks => downloadStatus == DownloadInProgress;

    public bool IsBelow { get; set; }
    public bool BlockInteraction { get; set; }
    public float Visibility { get; set; }
    public Vector2 Pos { get => pos; set => pos = value; }
    public Vector2 Size => size;

    public override void Update()
    {
        downloadBtn.buttonBehav.greyedOut = BlockInteraction || availability == Availability.Installed || downloadStatus is DownloadInProgress or DownloadSuccess;
        homepageBtn.buttonBehav.greyedOut = BlockInteraction || entry.Homepage.Trim().Length == 0;

        // If the download just finished, display its message
        if (downloadMessage != null) {
            string culledMessage = downloadMessage.Replace('\n', ' ').CullLong("font", Width - status.pos.x - 8);
            Color color = downloadStatus == DownloadSuccess ? new(.5f, 1, .5f) : new(1, .5f, .5f);
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

        if (loadIcon == 1) {
            loadIcon = 2;
            icon.sprite.element = Futile.atlasManager._allElementsByName[entry.Icon];
        }
    }

    static readonly float prefixWidth = "Click again to visit [".MeasureWidth("font");

    public override void Singal(MenuObject sender, string message)
    {
        if (sender == downloadBtn) {
            previewingHomepage = false;
            downloadStatus = DownloadInProgress;

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
                else if (homepage.StartsWith("http://"))
                    homepage = homepage.Substring("http://".Length);

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
        BackendProcess proc = BackendProcess.Execute($"-rdb \"{entry.Owner}/{entry.Name}\"");

        if (proc.ExitCode == 0) {
            Program.Logger.LogInfo($"Downloaded {entry.Owner}/{entry.Name}");

            Interlocked.Exchange(ref downloadStatus, DownloadSuccess);
            Interlocked.Exchange(ref downloadMessage, "Download successful");

            if (menu is ModMenu m) {
                m.NeedsRefresh = true;
            }
        }
        else if (proc.ExitCode == null) {
            Interlocked.Exchange(ref downloadStatus, DownloadErr);
            Interlocked.Exchange(ref downloadMessage, "Download timed out");
        }
        else {
            Program.Logger.LogError($"Failed to download {entry.Owner}/{entry.Name} with err {proc.ExitCode}.\n{proc.Error}");

            Interlocked.Exchange(ref downloadStatus, DownloadErr);
            Interlocked.Exchange(ref downloadMessage, proc.Error);
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
