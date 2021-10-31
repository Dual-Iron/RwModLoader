using Menu;

namespace Realm.Gui;

sealed class MenuContainer : PositionedMenuObject
{
    public MenuContainer(MenuObject owner) : base(owner.menu, owner, default)
    {
        FContainer parent = Container;
        parent.AddChild(Container = new());
    }
}
