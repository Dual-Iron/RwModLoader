using Realm.ModLoading;

namespace Realm;

public sealed class ProgramState
{
    public static ProgramState Instance { get; } = new();

    public RwmodHeaderCache CurrentRwmodHeaderCache { get; } = new();
    public ModLoader Mods { get; } = new();
    public Preferences Prefs { get; } = new();
    public bool DeveloperMode { get; set; }
}
