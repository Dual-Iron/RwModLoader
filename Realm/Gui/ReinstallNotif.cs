using Menu;

namespace Realm.Gui;

public static class ReinstallNotif
{
    public static void ApplyHooks()
    {
        On.Menu.MainMenu.ctor += MainMenu_ctor;
    }

    private static void MainMenu_ctor(On.Menu.MainMenu.orig_ctor orig, MainMenu self, ProcessManager manager, bool showRegionSpecificBkg)
    {
        orig(self, manager, showRegionSpecificBkg);

        MenuLabel label = new(self, self.pages[0], "Please reinstall Realm!", new(683, 420), default, true);
        label.label.color = new(1, 0, 0);

        self.pages[0].subObjects.Add(label);
    }
}
