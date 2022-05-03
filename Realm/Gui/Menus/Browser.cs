using Menu;
using Realm.Assets;
using Realm.Gui.Elements;
using Realm.ModLoading;
using UnityEngine;

namespace Realm.Gui.Menus;

using static BrowserState;

enum BrowserState { Idling, LoadingPages, Errored, EndReached }

sealed class Browser : ModMenuPage
{
    public override bool BlockMenuInteraction =>
        rdbListing.subObjects.OfType<BrowserPane>().Any(r => r.PreventButtonClicks) ||
        rdbListing.subObjects.OfType<AudbPane>().Any(r => r.PreventButtonClicks);

    public override string Tooltip => 
@"This is the mod browser.
Here, you can install mods that were uploaded by other people.

There are two types of mods available on the browser:
- RDB mods (the ones with icons), and
- AUDB mods (the ones without icons).";

    readonly MenuSprite loadSpinner;
    readonly Listing rdbListing;
    readonly MenuLabel warningLabel;
    readonly TextBox search;

    BrowserPageState pageState = new(null);

    public Browser(MenuObject owner, Vector2 pos) : base(owner, pos)
    {
        subObjects.Add(rdbListing = new(
            this,
            pos: new(1366 / 2 - BrowserPane.TotalWidth / 2, 40),
            elementSize: new(BrowserPane.TotalWidth, BrowserPane.TotalHeight),
            elementsPerScreen: new(1, 4),
            edgePadding: new(0, 5)
            ));

        rdbListing.SnapLerp = 0.15f;

        subObjects.Add(search = new(this, new(rdbListing.pos.x + 4, rdbListing.pos.y + rdbListing.size.y + 8), new TextBox.Settings {
            Big = true,
            Rect = true,
            Placeholder = "Search by name or author",
            Rows = 1,
            Width = rdbListing.size.x / 2 - 8,
            OnInsert = UpdateSearch
        }));

        subObjects.Add(loadSpinner = new(this, rdbListing.pos + rdbListing.size + new Vector2(-24, 8+12), Asset.SpriteFromRes("HARDHAT")));

        subObjects.Add(warningLabel = new(menu, this, "", rdbListing.pos, rdbListing.size, true));

        pageState.LoadPage();
    }

    public override void Update()
    {
        base.Update();

        search.GetButtonBehavior.greyedOut = pageState.State == Errored || BlockMenuInteraction;

        loadSpinner.sprite.rotation += 360f / 40f;
        loadSpinner.sprite.isVisible = pageState.State == LoadingPages;

        if (pageState.State == Errored) {
            if (warningLabel.text == "") {
                rdbListing.ClearListElements();
            }

            warningLabel.text = pageState.Error ?? "Something went very wrong";
        }

        // SAFETY: Only access pageState.Entries when state is not LoadingPages
        if (pageState.State is not LoadingPages and not Errored && pageState.JustFinished) {

            // Add new entries
            foreach (var entry in pageState.Entries) {
                rdbListing.subObjects.Add(new BrowserPane(rdbListing, entry));

                if (!InFocus) {
                    RecursiveRemoveSelectables(rdbListing.subObjects.Last());
                }
            }
            pageState.Entries.Clear();

            // Keep AUDB entries behind all others by clearing/readding them whenever this refreshes
            rdbListing.ClearListElements(m => m is AudbPane);
            foreach (var entry in AudbEntry.GetAudbEntriesBlocking()) {

                // If there's a search query, skip entries that don't match it
                if (pageState.Search != null &&
                    entry.Name.IndexOf(pageState.Search, StringComparison.OrdinalIgnoreCase) == -1 &&
                    entry.Type.IndexOf(pageState.Search, StringComparison.OrdinalIgnoreCase) == -1
                    ) {
                    continue;
                }

                rdbListing.subObjects.Add(new AudbPane(rdbListing, entry));

                if (!InFocus) {
                    RecursiveRemoveSelectables(rdbListing.subObjects.Last());
                }
            }
        }

        float loadMorePos = rdbListing.subObjects.OfType<BrowserPane>().Count() * BrowserPane.TotalHeight - rdbListing.size.y + 10;

        if (pageState.State != EndReached && rdbListing.scrollPos >= loadMorePos) {
            pageState.LoadPage();
        }
    }

    bool searching;
    private void UpdateSearch(string text)
    {
        if (pageState.State is BrowserState.Errored) {
            return;
        }

        if (text.Length > 2) {
            if (pageState.Search != text) {
                RefreshPageState(text);
            }
        }
        else if (searching) {
            RefreshPageState(null);
        }
    }

    private void RefreshPageState(string? search)
    {
        searching = search != null;
        rdbListing.ClearListElements();
        pageState = new(search);
        pageState.LoadPage();
    }
}
