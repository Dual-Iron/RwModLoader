using Menu;

namespace Realm.Gui.Warnings;

static class FailedLoad
{
    static bool applied;

    public static void Hook()
    {
        applied = true;
        On.Menu.MainMenu.ctor += AddWarningLabel;
    }

    public static void UndoHooks()
    {
        if (applied) {
            applied = false;
            On.Menu.MainMenu.ctor -= AddWarningLabel;
        }
    }

    private static void AddWarningLabel(On.Menu.MainMenu.orig_ctor orig, MainMenu self, ProcessManager manager, bool showRegionSpecificBkg)
    {
        orig(self, manager, showRegionSpecificBkg);

        MenuLabel label = new(self, self.pages[0], "Mods failed to load!\nTry reloading through the mods menu.", new(683, 430), default, false);
        label.label.color = new(1f, 0f, 0f);

        self.pages[0].subObjects.Add(label);
    }
}
