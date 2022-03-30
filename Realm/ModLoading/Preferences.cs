namespace Realm.ModLoading;

sealed class Preferences
{
    private static string PreferencesPath => Path.Combine(RealmPaths.UserFolder.FullName, "prefs.json");

    private readonly List<string> previousEnabledMods = new();

    public void Load()
    {
        if (!File.Exists(PreferencesPath)) {
            Save();
            return;
        }

        EnabledMods.Clear();

        try {
            string pref = File.ReadAllText(PreferencesPath);

            var data = (Dictionary<string, object>)Json.Deserialize(pref);

            foreach (var name in (List<object>)data["enabled"]) {
                EnabledMods.Add((string)name);
            }
        }
        catch (Exception e) {
            Program.Logger.LogError("Error while loading: " + e);
            EnabledMods.Clear();
        }

        previousEnabledMods.Clear();
        previousEnabledMods.AddRange(EnabledMods);
    }

    public void Save()
    {
        Dictionary<string, object> objects = new();

        objects["enabled"] = EnabledMods.ToList();

        try {
            File.WriteAllText(PreferencesPath, Json.Serialize(objects));
        }
        catch (Exception e) {
            Program.Logger.LogError("Error while saving: " + e);
        }

        previousEnabledMods.Clear();
        previousEnabledMods.AddRange(EnabledMods);
    }

    public void Enable(IEnumerable<string> mods) => EnabledMods.UnionWith(mods);
    public void Disable(IEnumerable<string> mods) => EnabledMods.ExceptWith(mods);

    public void Revert()
    {
        EnabledMods.Clear();
        EnabledMods.UnionWith(previousEnabledMods);
    }

    public bool AnyChanges => !EnabledMods.SetEquals(previousEnabledMods);

    public HashSet<string> EnabledMods { get; } = new();
}
