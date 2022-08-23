using Realm.ModLoading;
using Realm.Threading;
using System.Threading;
using UnityEngine;

namespace Realm.Gui.Menus;

sealed class BrowserPageState
{
    public readonly List<RdbEntry> Entries = new();
    public readonly string? Search;

    private readonly CancelationToken cancel;

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

    public BrowserPageState(string? search, CancelationToken cancelation)
    {
        Search = search;

        cancel = cancelation;
    }

    public void LoadPage()
    {
        if (State is not BrowserState.LoadingPages and not BrowserState.Errored) {
            State = BrowserState.LoadingPages;
            NetworkThread.Instance.Enqueue(DoLoad);
        }
    }

    private void DoLoad()
    {
        BackendProcess proc = BackendProcess.Execute($"-rdblist {Page} \"{Search}\"", timeout: 3000, cancel);

        if (proc.ExitCode != 0) {
            Program.Logger.LogError($"Error while adding mods: {proc}");

            State = BrowserState.Errored;
            Error = "Offline";
            return;
        }

        // Load AUDB entries from the worker thread. Don't need to use the value yet
        AudbEntry.PopulateAudb(cancel);

        if (cancel.Canceled) {
            return;
        }

        // Get rdb entries and add them to the list
        List<RdbEntry> entries = GetEntriesFrom(proc.Output.TrimEnd()).ToList();

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
