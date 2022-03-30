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
}
