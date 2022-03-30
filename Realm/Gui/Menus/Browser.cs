using Menu;
using Realm.Jobs;
using Realm.ModLoading;
using Rwml;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace Realm.Gui.Menus;

sealed class Browser : PositionedMenuObject, IMenuPage
{
    private static DateTime GetUtcFromTimestamp(long timestamp)
    {
        DateTime epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return epoch.AddSeconds(timestamp);
    }

    public bool BlockMenuInteraction => rdbListing.subObjects.OfType<BrowserPane>().Any(r => r.PreventButtonClicks);

    readonly Listing rdbListing;
    readonly MenuContainer warningContainer;
    readonly MenuLabel warningLabel;

    bool loadingPages;
    bool stopLoadingPages;
    int pageCount;

    public Browser(MenuObject owner, Vector2 pos) : base(owner.menu, owner, pos)
    {
        subObjects.Add(rdbListing = new(
            this,
            pos: new(1366 / 2 - BrowserPane.TotalWidth / 2, 40),
            elementSize: new(BrowserPane.TotalWidth, BrowserPane.TotalHeight),
            elementsPerScreen: new(1, 4),
            edgePadding: new(0, 5)
            ));

        rdbListing.SnapLerp = 0.15f;

        subObjects.Add(warningContainer = new(this));
        warningContainer.Container.alpha = 0;

        subObjects.Add(warningLabel = new(menu, this, "", rdbListing.pos, rdbListing.size, true));

        LoadPages();
    }

    public override void Update()
    {
        base.Update();

        if (rdbListing.sliderValue > 0.9f) {
            LoadPages();
        }
    }

    private void LoadPages()
    {
        if (!loadingPages && !stopLoadingPages) {
            loadingPages = true;
            Job.Start(AddMods);
        }
    }

    string Query => $"?page={pageCount}";

    private void AddMods()
    {
        using Disposable resetLoading = new(() => loadingPages = false);
        using WWW www = new($"https://rdb.dual-iron.xyz/mods{Query}");

        int time = 1000; // milliseconds approx

        while (time > 0 && !www.isDone) {
            time -= 1;
            Thread.Sleep(1);
        }

        string error = time > 0 ? www.error : "Timed out";

        if (!string.IsNullOrEmpty(error)) {
            Program.Logger.LogError($"Error while adding mods: {error}");

            stopLoadingPages = true;
            warningContainer.Container.alpha = 1;
            warningLabel.text = "Offline";
            return;
        }

        var entries = GetEntriesFrom(www.text).ToList();
        if (entries.Count == 0) {
            stopLoadingPages = true;
            return;
        }

        foreach (var entry in entries) {
            rdbListing.subObjects.Add(new BrowserPane(rdbListing, entry));
        }

        pageCount++;
    }

    private IEnumerable<RdbEntry> GetEntriesFrom(string json)
    {
        object? root = Json.Deserialize(json);

        if (root is not List<object> objs) {
            yield break;
        }

        foreach (var dict in objs.OfType<Dictionary<string, object>>()) {
            if (dict.TryGetValue("name", out var v) && v is string name &&
                dict.TryGetValue("owner", out v) && v is string owner &&
                dict.TryGetValue("updated", out v) && v is long updated &&
                dict.TryGetValue("downloads", out v) && v is long downloads &&
                dict.TryGetValue("description", out v) && v is string description &&
                dict.TryGetValue("homepage", out v) && v is string homepage &&
                dict.TryGetValue("version", out v) && v is string version && SemVer.Parse(version) is SemVer semver &&
                dict.TryGetValue("icon", out v) && v is string icon
                ) {
                yield return new RdbEntry(name, owner, GetUtcFromTimestamp(updated), downloads, description, homepage, semver, icon);
            }
            else {
                rdbListing.subObjects.RemoveAll(o => o is IListable);

                Program.Logger.LogError($"Outdated client: {json}");

                stopLoadingPages = true;
                warningContainer.Container.alpha = 1;
                warningLabel.text = "You have an outdated client.\nUpdate Realm!";
                warningLabel.label.color = new(1f, 0.5f, 0.5f);
                yield break;
            }
        }
    }

    void IMenuPage.EnterFocus() { }
}
