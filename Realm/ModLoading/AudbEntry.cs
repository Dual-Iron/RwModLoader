using UnityEngine;

namespace Realm.ModLoading;

sealed class AudbEntry
{
    public record struct AudbID(long Key, long Mod);

    public string Name;
    public string Type;
    public string Description;
    public string Filename;
    public long Version;
    public DateTime LastUpdated;
    public string Url;
    public AudbID ID;

    public readonly List<AudbID> Dependencies = new();

    public AudbEntry(string name, string type, string description, string filename, long version, DateTime lastUpdated, string url, AudbID id)
    {
        Name = name;
        Type = type;
        Description = description;
        Filename = filename;
        Version = version;
        LastUpdated = lastUpdated;
        Url = url;
        ID = id;
    }

    public static AudbEntry? FromJson(Dictionary<string, object> json)
    {
        if (json.TryGetValue("url", out var v) && v is string url &&
            json.TryGetValue("version", out v) && v is long version &&
            json.TryGetValue("lastmodified", out v) && v is var updated &&
            json.TryGetValue("metadata", out v) && v is Dictionary<string, object> metadata &&
            metadata.TryGetValue("name", out v) && v is string name &&
            metadata.TryGetValue("description", out v) && v is string description &&
            metadata.TryGetValue("type", out v) && v is string type &&
            metadata.TryGetValue("filename", out v) && v is string filename &&
            json.TryGetValue("id", out v) && v is Dictionary<string, object> id &&
            id.TryGetValue("key", out v) && v is long key &&
            id.TryGetValue("mod", out v) && v is long mod
            ) {
            DateTime lastUpdated = RdbEntry.GetUtcFromTimestamp(updated is double d ? d : 1577854800);

            var entry = new AudbEntry(name, type.Trim().ToUpper(), description.Trim(), filename, version, lastUpdated, url, new AudbID(key, mod));

            if (metadata.TryGetValue("deps", out v) && v is List<object> deps) {
                foreach (var dep in deps.OfType<Dictionary<string, object>>()) {
                    if (dep.TryGetValue("key", out v) && v is long depKey &&
                        dep.TryGetValue("mod", out v) && v is long depMod &&
                        !(depKey == 0 && depMod == 0 || depKey == 0 && depMod == 1) // Exclude AU and EE
                        ) {
                        entry.Dependencies.Add(new AudbID(depKey, depMod));
                    }
                }
            }

            return entry;
        }
        return null;
    }

    #region AUDB cache
    private static readonly List<AudbEntry> entries = new();
    private static bool err;

    public static List<AudbEntry> GetAudbEntriesBlocking()
    {
        if (entries.Count == 0 && !err) {
            Populate();

            // Remove AutoUpdate and EnumExtender
            entries.RemoveAll(e => e.ID == new AudbID(0, 0) || e.ID == new AudbID(0, 1));
        }
        return entries;
    }

    private static void Populate()
    {
        BackendProcess proc = BackendProcess.Execute("-audb");

        if (proc.ExitCode != 0) {
            Program.Logger.LogError($"Error while getting AUDB entries: {proc}");
            err = true;
            return;
        }

        AddEntriesFrom(proc.Output);
    }

    private static void AddEntriesFrom(string text)
    {
        if (Json.Deserialize(text) is not List<object> objs) {
            return;
        }

        foreach (var json in objs.OfType<Dictionary<string, object>>()) {
            if (FromJson(json) is AudbEntry entry) {
                entries.Add(entry);
            }
            else {
                Program.Logger.LogError($"Failed to parse AUDB entries. JSON: {text}");
                err = true;
                break;
            }
        }
    }
    #endregion
}
