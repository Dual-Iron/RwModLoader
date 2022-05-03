using Menu;
using Realm.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Realm.Gui.Elements;

sealed class LoadSpinny : PositionedMenuObject
{
    public readonly MenuSprite ico;

    public LoadSpinny(MenuObject owner, Vector2 pos) : base(owner.menu, owner, pos)
    {
        subObjects.Add(ico = new(this, default, Asset.SpriteFromRes("HARDHAT")));
    }

    public override void Update()
    {
        base.Update();

        ico.sprite.rotation += 360f / 40f;
    }
}
