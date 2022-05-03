using Menu;
using RWCustom;
using UnityEngine;

namespace Realm.Gui.Elements;

sealed class Listing : RectangularMenuObject, Slider.ISliderOwner
{
    private readonly VerticalSlider slider;
    private readonly FixedMenuContainer sliderContainer;
    private readonly Vector2 edgePadding;

    public bool ForceBlockInteraction;
    public float SnapLerp;

    public Listing(MenuObject owner, Vector2 pos, Vector2 elementSize, IntVector2 elementsPerScreen, Vector2 edgePadding)
        : this(
              owner,
              pos,
              size: new(elementSize.x * elementsPerScreen.x + edgePadding.x * 2, elementSize.y * elementsPerScreen.y + edgePadding.y * 2),
              edgePadding)
    { }

    public Listing(MenuObject owner, Vector2 pos, Vector2 size, Vector2 edgePadding) : base(owner.menu, owner, pos, size)
    {
        FContainer parent = Container;
        parent.AddChild(Container = new());

        subObjects.Add(new RoundedRect(menu, this, default, size, true) { fillAlpha = 0.75f });

        sliderContainer = new(this);
        slider = new(menu, sliderContainer, "", new(-34, 10), new(30, size.y - 41), Slider.SliderID.LevelsListScroll, false);

        sliderContainer.subObjects.Add(slider);
        subObjects.Add(sliderContainer);

        if (edgePadding.x < 0 || edgePadding.y < 0) {
            throw new ArgumentOutOfRangeException(nameof(edgePadding));
        }

        this.edgePadding = edgePadding;
    }

    public void ClearListElements() => ClearListElements(l => true);
    public void ClearListElements(Predicate<IListable> predicate)
    {
        for (int i = subObjects.Count - 1; i >= 0; i--) {
            if (subObjects[i] is IListable l && predicate(l)) {
                subObjects[i].RemoveSprites();
                RemoveSubObject(subObjects[i]);
            }
        }
    }

    public override void Update()
    {
        float snapToPos = 0;
        float snapDist = float.PositiveInfinity;

        float depth = edgePadding.y;
        foreach (var elem in subObjects.OfType<IListable>()) {
            float topDepth = depth;
            depth += elem.Size.y;

            elem.Pos = new Vector2(edgePadding.x, scrollPos + size.y - depth);

            if (topDepth < scrollPos) {
                if (snapDist > scrollPos - topDepth) {
                    snapDist = scrollPos - topDepth;
                    snapToPos = topDepth - edgePadding.y;
                }

                elem.IsBelow = false;
                elem.BlockInteraction = true;
                elem.Visibility = Mathf.Clamp01(1 - (scrollPos - topDepth) / elem.Size.y);
            }
            else if (depth > scrollPos + size.y) {
                elem.IsBelow = true;
                elem.BlockInteraction = true;
                elem.Visibility = Mathf.Clamp01(1 - (depth - (scrollPos + size.y)) / elem.Size.y);
            }
            else {
                if (snapDist > topDepth - scrollPos) {
                    snapDist = topDepth - scrollPos;
                    snapToPos = topDepth - edgePadding.y;
                }

                elem.IsBelow = false;
                elem.BlockInteraction = ForceBlockInteraction;
                elem.Visibility = 1;
            }
        }

        bool shouldShowSlider = depth > size.y;

        if (shouldShowSlider && MouseOver) {
            float scrollDelta = Input.mouseScrollDelta.y * -25;
            if (vel < 0 == scrollDelta < 0) {
                vel += scrollDelta;
            }
            else if (scrollDelta != 0) {
                vel = scrollDelta;
            }
        }

        float sliderSize = depth + edgePadding.y - size.y;

        bool beingUsed = slider.mouseDragged || slider.Selected && menu.input.y != 0;
        if (!beingUsed && vel < 0.01f) {
            sliderValue = Mathf.Lerp(sliderValue, snapToPos / sliderSize, SnapLerp);
        }

        sliderValue += vel / sliderSize;
        sliderValue = Mathf.Clamp01(sliderValue);
        vel *= 0.8f;

        slider.GetButtonBehavior.greyedOut = !shouldShowSlider;
        sliderContainer.Container.isVisible = shouldShowSlider;
        scrollPos = shouldShowSlider ? sliderValue * sliderSize : 0;

        base.Update();
    }

    public float sliderValue;
    public float scrollPos;
    public float vel;

    void Slider.ISliderOwner.SliderSetValue(Slider slider, float setValue) => sliderValue = 1 - setValue;
    float Slider.ISliderOwner.ValueOfSlider(Slider slider) => 1 - sliderValue;
}
