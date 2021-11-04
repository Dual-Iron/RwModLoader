using Realm.ModLoading;

namespace Realm;

static class State
{
    public static readonly RefreshCache CurrentRefreshCache = new();
    public static readonly ModLoader Mods = new();
    public static readonly Preferences Prefs = new();
    public static bool DeveloperMode;
    public static bool NoHotReloading;
}
