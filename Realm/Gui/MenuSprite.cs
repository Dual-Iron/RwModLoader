using Menu;
using UnityEngine;

namespace Realm.Gui;

public class MenuSprite : RectangularMenuObject
{
    public readonly FSprite sprite;

    public MenuSprite(MenuObject owner, Vector2 pos, FSprite sprite) : base(owner.menu, owner, pos, new Vector2(sprite.width, sprite.height))
    {
        this.sprite = sprite;
        Container.AddChild(sprite);
    }

    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);
        Vector2 drawSize = DrawSize(timeStacker);
        sprite.SetPosition(DrawPos(timeStacker) + new Vector2(drawSize.x * sprite.anchorX, drawSize.y * sprite.anchorY));
        sprite.scaleX = Mathf.Max(1, size.x / sprite.textureRect.width);
        sprite.scaleY = Mathf.Max(1, size.y / sprite.textureRect.height);
    }

    public override void RemoveSprites()
    {
        sprite.RemoveFromContainer();
        base.RemoveSprites();
    }
}
