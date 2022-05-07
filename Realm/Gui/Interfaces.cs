using Menu;
using UnityEngine;

namespace Realm.Gui;

interface IListable
{
    bool BlockInteraction { set; }
    float Visibility { set; }
    Vector2 Pos { set; }
    Vector2 Size { get; }
}

interface ITextBoxMenu
{
    MenuObject? FocusedOn { get; set; }
}

interface IHoverable
{
    string? GetHoverInfo(MenuObject selected);
}
