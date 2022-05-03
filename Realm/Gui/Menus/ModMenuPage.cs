using Menu;
using UnityEngine;

namespace Realm.Gui.Menus;

abstract class ModMenuPage : RectangularMenuObject
{
    public ModMenuPage(MenuObject owner, Vector2 pos) : base(owner.menu, owner, pos, new(1366, 768))
    {
    }

    // Determines if the menu is currently viewed. If false, nothing in the menu should be selectable.
    public bool InFocus { get; private set; }

    public abstract string Tooltip { get; }
    public abstract bool BlockMenuInteraction { get; }
    public virtual void SetFocus(bool focus)
    {
        InFocus = focus;
    }
}
