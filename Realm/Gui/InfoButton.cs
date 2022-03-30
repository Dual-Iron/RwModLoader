using Menu;
using RWCustom;
using UnityEngine;
using static Menu.Menu;

namespace Realm.Gui;

struct InfoBox
{
    public string Text;
    public bool BigText;
}

sealed class InfoButton : SymbolButton
{
    private readonly Func<InfoBox> getInfo;

    private InfoWindow? window;

    public bool ForceClose;

    public InfoButton(MenuObject owner, Vector2 pos, Func<InfoBox> getInfo) : base(owner.menu, owner, "Menu_InfoI", "", pos)
    {
        this.getInfo = getInfo;
    }

    public override void Clicked()
    {
        base.Clicked();

        if (window != null) {
            window.wantToGoAway = true;
            menu.PlaySound(SoundID.MENU_Remove_Level);
        }
        else {
            subObjects.Add(window = new InfoWindow(this, default, getInfo()));
            menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
        }
    }

    public override void Update()
    {
        if (window != null && ForceClose) {
            window.wantToGoAway = true;
        }

        base.Update();
    }

    public override bool CurrentlySelectableMouse => base.CurrentlySelectableMouse && !ForceClose;
    public override bool CurrentlySelectableNonMouse => CurrentlySelectableMouse;

    // Copied and modified from vanilla's `global::Menu.InfoWindow`
    sealed class InfoWindow : RectangularMenuObject
    {
        private readonly new InfoButton owner;

        public RoundedRect roundedRect;
        public float lastFade;
        public float fade;
        public float labelFade;
        public float lastLabelFade;
        public bool wantToGoAway;
        public FSprite fadeSprite;
        public int fadeCounter;
        public MenuLabel label;
        public Vector2 goalSize;
        public int counter;

        public InfoWindow(InfoButton owner, Vector2 pos, InfoBox info) : base(owner.menu, owner, pos, new Vector2(24f, 24f))
        {
            this.owner = owner;

            Container.AddChild(fadeSprite = new("Futile_White", true) {
                shader = menu.manager.rainWorld.Shaders["FlatLight"],
                color = Color.black,
                alpha = 0f,
            });

            subObjects.Add(roundedRect = new RoundedRect(menu, this, default, size, true));

            string[] array = info.Text.Split('\n');

            goalSize = new Vector2(array.Max(a => a.Length) * 10.5f, array.Length * 30) + new Vector2(20, 20);

            label = new MenuLabel(menu, this, info.Text, new Vector2(20f, 20f), goalSize, info.BigText);
            label.label.alignment = FLabelAlignment.Left;
            subObjects.Add(label);
        }

        public override void Update()
        {
            lastFade = fade;
            lastLabelFade = labelFade;
            counter++;

            fade = Custom.LerpAndTick(fade, (!wantToGoAway) ? 1f : 0f, 0.05f, 0.025f);
            if (labelFade > Mathf.InverseLerp(0.8f, 1f, fade)) {
                labelFade = Mathf.InverseLerp(0.8f, 1f, fade);
            }
            else {
                labelFade = Custom.LerpAndTick(labelFade, Mathf.InverseLerp(0.8f, 1f, fade) * Mathf.InverseLerp(30f, 5f, fadeCounter), 0.03f, 0.025f);
            }

            float fadeSmoothed = Custom.SCurve(fade, 0.65f);
            size = Vector2.Lerp(new Vector2(24f, 24f), goalSize, fadeSmoothed);
            size = Vector2.Lerp(size, new Vector2(Mathf.Sqrt(size.x * size.y), Mathf.Sqrt(size.x * size.y)), Mathf.Pow(1f - fadeSmoothed, 1.5f));
            pos = new Vector2(24f, 24f) - size;

            roundedRect.size = size;
            roundedRect.addSize = owner.roundedRect.addSize;

            if (wantToGoAway && fade == 0f && lastFade == 0f) {
                owner.RemoveSubObject(this);
                RemoveSprites();
                owner.window = null;
            }

            if (owner.Selected) {
                fadeCounter = 0;
            }
            else {
                fadeCounter++;
                if (fadeCounter > 30) {
                    wantToGoAway = true;
                }
            }

            label.pos = new Vector2(-goalSize.x / 2f + 20f, 0f);

            base.Update();
        }

        public override void GrafUpdate(float timeStacker)
        {
            base.GrafUpdate(timeStacker);

            float fadeSmoothed = Custom.SCurve(Mathf.Lerp(lastFade, fade, timeStacker), 0.65f);

            fadeSprite.x = DrawX(timeStacker) + DrawSize(timeStacker).x / 2f;
            fadeSprite.y = DrawY(timeStacker) + DrawSize(timeStacker).y / 2f;
            fadeSprite.scaleX = (DrawSize(timeStacker).x * 1.5f + 600f) / 16f;
            fadeSprite.scaleY = (DrawSize(timeStacker).y * 1.5f + 600f) / 16f;
            fadeSprite.alpha = Mathf.Pow(fadeSmoothed, 2f) * 0.85f;

            label.label.alpha = Mathf.Min(Custom.SCurve(Mathf.Lerp(lastLabelFade, labelFade, timeStacker), 0.5f), Mathf.InverseLerp(0.8f, 1f, fadeSmoothed));
            label.label.color = MenuRGB(MenuColors.MediumGrey);

            Color color = Color.Lerp(MenuRGB(MenuColors.DarkGrey), MenuRGB(MenuColors.MediumGrey), 0.5f + 0.5f * Mathf.Sin((counter + timeStacker) / 60f * Mathf.PI * 2f));

            // Rect backdrop. Should be black when the info box is layed out.
            for (int i = 0; i < 9; i++) {
                roundedRect.sprites[i].color = Color.black;
                roundedRect.sprites[i].alpha = Mathf.Pow(fadeSmoothed, 2f);
            }

            // Rect outline.
            for (int j = 9; j < 17; j++) {
                roundedRect.sprites[j].color = color;
                roundedRect.sprites[j].alpha = fadeSmoothed;
            }
        }

        public override void RemoveSprites()
        {
            base.RemoveSprites();

            fadeSprite.RemoveFromContainer();
        }
    }
}
