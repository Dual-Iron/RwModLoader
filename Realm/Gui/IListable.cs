using UnityEngine;

namespace Realm.Gui
{
    public interface IListable
    {
        bool IsBelow { set; }
        bool BlockInteraction { set; }
        float Visibility { set; }
        Vector2 Pos { set; }
        Vector2 Size { get; }
    }
}
