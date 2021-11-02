using Realm.ModLoading;

namespace Realm;

sealed class State
{
    public static State Instance { get; } = new();

    public readonly RefreshCache CurrentRefreshCache = new();
    public readonly ModLoader Mods = new();
    public readonly Preferences Prefs = new();
    public bool DeveloperMode;
    public bool NoHotReloading;
}
