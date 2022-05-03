using static Menu.Menu;
using Menu;
using Realm.Assets;
using UnityEngine;
using Realm.ModLoading;
using Realm.Gui.Elements;
using Rwml;

namespace Realm.Gui.Menus;

sealed class AudbPane : RectangularMenuObject, IListable, IHoverable
{
    public const float TotalWidth = 564;
    public const float TotalHeight = 134;
    public const float Width = 540;
    public const float Height = 128;

    public AudbPane(MenuObject owner, AudbEntry entry) : base(owner.menu, owner, new(-10000, -10000), new(TotalWidth, TotalHeight))
    {
        const float pad = 8;
        const float padDesc = 12;
        const float rightColWidth = 128;

        this.entry = entry;

        owner.Container.AddChild(Container = new());

        FixedMenuContainer inner = new(this, new Vector2((TotalWidth - Width) / 2 + 1, (TotalHeight - Height) / 2));
        subObjects.Add(inner);

        // Add name + owner
        Label(entry.Name.CullLong("DisplayFont", Width - rightColWidth - pad * 2), pos: new(pad, 110), true).WithAlignment(FLabelAlignment.Left);

        // Add description
        var descStr = Gui.SplitLinesAndCull(entry.Description, width: Width - rightColWidth - padDesc * 2, rows: 3);
        var desc = new MenuLabel(menu, inner, descStr.JoinStr("\n"), new(padDesc, 93), new(), false);
        desc.label.anchorY = 1f;
        desc.label.alignment = FLabelAlignment.Left;
        desc.label.color = MenuColor(MenuColors.MediumGrey).rgb;
        inner.subObjects.Add(desc);

        // Add version + last updated text
        string updatedText = $"updated {Gui.GetRelativeTime(entry.LastUpdated)}";

        Label($"v{entry.Version}".CullLong("font", rightColWidth - pad), new(Width - pad, 106)).WithAlignment(FLabelAlignment.Right);
        Label(updatedText.CullLong("font", rightColWidth), new(Width - pad, 86)).WithAlignment(FLabelAlignment.Right).WithColor(MenuColors.MediumGrey);
        Label(entry.Type.CullLong("font", rightColWidth), new(Width - pad, 66)).WithAlignment(FLabelAlignment.Right).WithColor(MenuColors.MediumGrey);

        inner.subObjects.Add(downloadBtn = new SymbolButton(menu, inner, Asset.SpriteFromRes("DOWNLOAD").element.name, "", new(pad, pad)));
        downloadBtn.size = new(32, 32);
        downloadBtn.roundedRect.size = downloadBtn.size;

        inner.subObjects.Add(status = new(inner, new(32 + pad * 3, 16), default));

        RwmodFileHeader match = State.CurrentRefreshCache.Headers.FirstOrDefault(h => h.Header.Name == entry.Name);

        if (match.Header?.Version != null && (match.Header.Flags & RwmodHeader.FileFlags.AudbEntry) != 0) {
            if (match.Header.Version.Value.Minor >= entry.Version) {
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

    readonly AudbEntry entry;
    readonly SymbolButton downloadBtn;
    readonly MultiLabel status;
    
    Availability availability;
    DownloadStatus downloadStatus;
    string? downloadMessage;

    public bool PreventButtonClicks => downloadStatus == DownloadStatus.InProgress;

    public bool IsBelow { get; set; }
    public bool BlockInteraction { get; set; }
    public float Visibility { get; set; }
    public Vector2 Pos { get => pos; set => pos = value; }
    public Vector2 Size => size;

    public override void Update()
    {
        downloadBtn.buttonBehav.greyedOut = BlockInteraction || availability == Availability.Installed || downloadStatus == DownloadStatus.InProgress;

        // If the download just finished, display its message
        if (downloadMessage != null) {
            string culledMessage = downloadMessage.Replace('\n', ' ').CullLong("font", Width - status.pos.x - 8);
            Color color = downloadStatus == DownloadStatus.Success ? new(.5f, 1, .5f) : new(1, .5f, .5f);
            status.SetLabel(color, culledMessage);

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
    }

    public override void Singal(MenuObject sender, string message)
    {
        if (downloadStatus == DownloadStatus.None && sender == downloadBtn) {
            downloadStatus = DownloadStatus.InProgress;

            status.SetLabel(MenuRGB(MenuColors.MediumGrey), "Downloading");
            menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
            Job.Start(Download);
        }
    }

    private void Download()
    {
        using TempDir dir = new();
        string path = dir.Info.CreateSubdirectory(entry.Name).FullName;

        StringBuilder dls = new();

        dls.Append($"-dl \"{entry.Url}\" \"{Path.Combine(path, entry.Filename)}\" ");

        // Won't block here, was called on a worker thread already.
        var allMods = AudbEntry.GetAudbEntriesBlocking();

        foreach (var dep in entry.Dependencies) {
            var depEntry = allMods.FirstOrDefault(a => a.ID == dep);
            if (depEntry != null) {
                dls.Append($"-dl \"{depEntry.Url}\" \"{Path.Combine(path, depEntry.Filename)}\" ");
            }
            else {
                Program.Logger.LogError($"Couldn't find AUDB mod matching dependency {dep}!");

                downloadStatus = DownloadStatus.Err;
                downloadMessage = "Couldn't locate one of the mod's dependencies";
                return;
            }
        }

        dls.Append($"-wau \"{path}\" \"{entry.Version}\"");

        BackendProcess proc = BackendProcess.Execute(dls.ToString());

        if (proc.ExitCode == 0) {
            Program.Logger.LogInfo($"Downloaded {entry.Name} ({entry.ID})");

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
            Program.Logger.LogError($"Failed to download {entry.Name} ({entry.ID}) from AUDB with err {proc.ExitCode}.\n{proc.Error}");

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
        return null;
    }
}
