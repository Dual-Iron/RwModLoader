using System.Linq;

namespace Realm.ModLoading;

public sealed class Preferences
{
    private static string PreferencesPath => Path.Combine(RealmPaths.UserFolder.FullName, "prefs.json");

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
        } catch (Exception e) {
            Program.Logger.LogError("Error while loading: " + e);
            EnabledMods.Clear();
        }
    }

    public void Save()
    {
        Dictionary<string, object> objects = new();

        objects["enabled"] = EnabledMods.ToList();

        try {
            File.WriteAllText(PreferencesPath, Json.Serialize(objects));
        } catch (Exception e) {
            Program.Logger.LogError("Error while saving: " + e);
        }
    }

    public void EnableThenSave(IEnumerable<string> modsToEnable)
    {
        if (!modsToEnable.Any()) {
            return;
        }

        foreach (var item in modsToEnable) {
            EnabledMods.Add(item);
        }

        Save();
    }

    public HashSet<string> EnabledMods { get; } = new();
}
