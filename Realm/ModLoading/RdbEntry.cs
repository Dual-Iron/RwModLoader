using Rwml;

namespace Realm.ModLoading;

sealed class RdbEntry
{
    public string Name;
    public string Owner;
    public DateTime LastUpdated;
    public long Downloads;
    public string Description;
    public string Homepage;
    public SemVer Version;
    public string Icon;

    public RdbEntry(string name, string owner, DateTime lastUpdated, long downloads, string description, string homepage, SemVer version, string icon)
    {
        Name = name;
        Owner = owner;
        LastUpdated = lastUpdated;
        Downloads = downloads;
        Description = description;
        Homepage = homepage;
        Version = version;
        Icon = icon;
    }

    internal static DateTime GetUtcFromTimestamp(double timestamp)
    {
        DateTime epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return epoch.AddSeconds(timestamp);
    }

    public static RdbEntry? FromJson(Dictionary<string, object> json)
    {
        if (json.TryGetValue("name", out var v) && v is string name &&
            json.TryGetValue("owner", out v) && v is string owner &&
            json.TryGetValue("updated", out v) && v is long updated &&
            json.TryGetValue("downloads", out v) && v is long downloads &&
            json.TryGetValue("description", out v) && v is string description &&
            json.TryGetValue("homepage", out v) && v is string homepage &&
            json.TryGetValue("version", out v) && v is string version && SemVer.Parse(version) is SemVer semver &&
            json.TryGetValue("icon", out v) && v is string icon
            ) {
            return new RdbEntry(name, owner, GetUtcFromTimestamp(updated), downloads, description, homepage, semver, icon);
        }
        return null;
    }
}
