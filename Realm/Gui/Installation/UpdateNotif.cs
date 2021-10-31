using Menu;
using System.Diagnostics;
using UnityEngine;

namespace Realm.Gui.Installation;

static class UpdateNotif
{
    const string UpdateSignal = "UPDATE";

    public static void ApplyHooks()
    {
        On.Menu.MainMenu.Singal += SignalUpdate;
        On.Menu.MainMenu.ctor += AddUpdateButton;
    }

    private static void SignalUpdate(On.Menu.MainMenu.orig_Singal orig, MainMenu self, MenuObject sender, string message)
    {
        if (message is UpdateSignal) {
            Process.Start(new ProcessStartInfo {
                FileName = "https://github.com/Dual-Iron/RwModLoader/releases/latest",
                UseShellExecute = true,
                Verb = "open"
            }).Dispose();
        } else {
            orig(self, sender, message);
        }
    }

    private static void AddUpdateButton(On.Menu.MainMenu.orig_ctor orig, MainMenu self, ProcessManager manager, bool showRegionSpecificBkg)
    {
        orig(self, manager, showRegionSpecificBkg);

        Vector2 size = new(110f, 30f);

        SimpleButton button = new(self, self.pages[0], "UPDATE REALM", UpdateSignal, new(683f - size.x / 2f, 40f + size.y / 2f), size);
        self.pages[0].subObjects.Add(button);
    }
}
