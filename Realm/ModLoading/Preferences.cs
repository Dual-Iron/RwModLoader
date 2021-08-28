using System.Collections.Generic;
using System.IO;

namespace Realm.ModLoading
{
    public sealed class Preferences
    {
        private static string PreferencesPath => Path.Combine(Extensions.UserFolder, "prefs.json");

        public void Load()
        {
            if (!File.Exists(PreferencesPath)) {
                Save();
            }

            string pref = File.ReadAllText(PreferencesPath);

            var data = (Dictionary<string, object>)Json.Deserialize(pref);

            foreach (var name in (List<object>)data["enabled"]) {
                EnabledMods.Add((string)name);
            }
        }

        public void Save()
        {
            Dictionary<string, object> objects = new();

            objects["enabled"] = EnabledMods;

            File.WriteAllText(PreferencesPath, Json.Serialize(objects));
        }

        public List<string> EnabledMods { get; private set; } = new();
    }
}
