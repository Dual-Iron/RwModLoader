using Realm.Gui.Local;

namespace Realm.Gui;

static class GuiHandler
{
    public static void Hook()
    {
        ModsMenuMusic.Hook();

        if (State.Instance.DeveloperMode) {
            HotReloadingGui.Hook();
        }

        ModsMenuGui.Hook();
    }
}
