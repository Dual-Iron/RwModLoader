using Menu;
using UnityEngine;

namespace Realm.Gui.Elements;

sealed class FixedMenuContainer : PositionedMenuObject
{
    public FixedMenuContainer(MenuObject owner) : this(owner, default) { }
    public FixedMenuContainer(MenuObject owner, Vector2 position) : base(owner.menu, owner, position)
    {
        FContainer parent = Container;
        parent.AddChild(Container = new());
    }

    public override void RemoveSprites()
    {
        base.RemoveSprites();
        myContainer.RemoveFromContainer();
    }
}
