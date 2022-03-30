namespace Realm.Gui.Menus;

interface IMenuPage
{
    bool BlockMenuInteraction { get; }

    void EnterFocus();
}
