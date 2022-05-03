using Realm.Jobs;
using Realm.ModLoading;
using System.Threading;
using UnityEngine;

namespace Realm.Gui.Menus;

sealed class BrowserPageState
{
    public readonly List<RdbEntry> Entries = new();
    public readonly string? Search;

    private bool justFinished;
    public bool JustFinished {
        get {
            if (justFinished) {
                justFinished = false;
                return true;
            }
            return false;
        }
    }

    public BrowserState State;
    public string? Error;
    public int Page;

    public BrowserPageState(string? search)
    {
        Search = search;
    }

    public void LoadPage()
    {
        if (State is not BrowserState.LoadingPages and not BrowserState.Errored) {
            State = BrowserState.LoadingPages;
            Job.Start(DoLoad);
        }
    }

    private void DoLoad()
    {
        string? searchQuery = Search == null ? "" : $"&search={Uri.EscapeDataString(Search)}";

        using WWW www = new($"https://rdb.dual-iron.xyz/mods?page={Page}{searchQuery}");

        int time = 3000; // milliseconds approx

        while (time > 0 && !www.isDone) {
            time -= 1;
            Thread.Sleep(1);
        }

        string error = time > 0 ? www.error : "Timed out";

        if (!string.IsNullOrEmpty(error)) {
            Program.Logger.LogError($"Error while adding mods: {error}");

            State = BrowserState.Errored;
            Error = "Offline";
            return;
        }

        // Load AUDB entries from the worker thread. Don't need to use the value yet
        _ = AudbEntry.GetAudbEntriesBlocking();

        // Get rdb entries and add them to the list
        List<RdbEntry> entries = GetEntriesFrom(www.text).ToList();

        foreach (var entry in entries) {
            Entries.Add(entry);
        }

        if (entries.Count < 20) {
            State = BrowserState.EndReached;
        }
        else {
            State = BrowserState.Idling;
            Page++;
        }

        justFinished = true;
    }

    private IEnumerable<RdbEntry> GetEntriesFrom(string json)
    {
        object? root = Json.Deserialize(json);

        if (root is not List<object> objs) {
            yield break;
        }

        foreach (var dict in objs.OfType<Dictionary<string, object>>()) {
            if (RdbEntry.FromJson(dict) is RdbEntry entry) {
                yield return entry;
            }
            else {
                State = BrowserState.Errored;
                Error = "You have an outdated client.\nUpdate Realm!";
                Program.Logger.LogError($"Failed to parse rdb entries. JSON: {json}");
                yield break;
            }
        }
    }
}
