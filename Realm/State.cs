using Realm.ModLoading;

namespace Realm;

public sealed class State
{
    public static State Instance { get; } = new();

    public RefreshCache CurrentRefreshCache { get; } = new();
    public ModLoader Mods { get; } = new();
    public Preferences Prefs { get; } = new();
    public bool DeveloperMode { get; set; }
}
