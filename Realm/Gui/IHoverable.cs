using Menu;

namespace Realm.Gui
{
    public interface IHoverable
    {
        string GetHoverInfo(MenuObject selected);
    }
}