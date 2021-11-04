using Realm.Gui.Local;

namespace Realm.Gui;

static class GuiHandler
{
    public static void Hook()
    {
        ModsMenuMusic.Hook();

        if (State.DeveloperMode) {
            HotReloadingGui.Hook();
        }

        ModsMenuGui.Hook();
    }
}
