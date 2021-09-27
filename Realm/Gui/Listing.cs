using Menu;
using UnityEngine;

namespace Realm.Gui;

public sealed class Listing : RectangularMenuObject, Slider.ISliderOwner
{
    private readonly VerticalSlider slider;
    private readonly MenuContainer sliderContainer;
    private readonly float edgePadding;

    public Listing(MenuObject owner, Vector2 pos, Vector2 elementSize, int elementsPerScreen, float edgePadding)
        : this(owner, pos, new(elementSize.x, elementSize.y * elementsPerScreen + edgePadding * 2), edgePadding)
    {

    }

    public Listing(MenuObject owner, Vector2 pos, Vector2 size, float edgePadding) : base(owner.menu, owner, pos, size)
    {
        subObjects.Add(new RoundedRect(menu, this, default, size, true) { fillAlpha = 0.75f });

        sliderContainer = new(this);
        slider = new(menu, sliderContainer, "", new(-34, 10), new(30, size.y - 41), Slider.SliderID.LevelsListScroll, false);

        sliderContainer.subObjects.Add(slider);
        subObjects.Add(sliderContainer);

        if (edgePadding < 0) throw new ArgumentOutOfRangeException(nameof(edgePadding));
        this.edgePadding = edgePadding;
    }

    public override void Update()
    {
        float depth = edgePadding;
        foreach (var sob in subObjects) {
            if (sob is not IListable listable) {
                continue;
            }

            float topDepth = depth;

            depth += listable.Size.y;

            listable.Pos = new Vector2(pos.x, scrollPos + size.y + pos.y - depth);

            if (topDepth < scrollPos) {
                listable.IsBelow = false;
                listable.BlockInteraction = true;
                listable.Visibility = Mathf.Clamp01(1 - (scrollPos - topDepth) / listable.Size.y);
            } else if (depth > scrollPos + size.y - edgePadding) {
                listable.IsBelow = true;
                listable.BlockInteraction = true;
                listable.Visibility = Mathf.Clamp01(1 - (depth - (scrollPos + size.y - edgePadding)) / listable.Size.y);
            } else {
                listable.IsBelow = false;
                listable.BlockInteraction = false;
                listable.Visibility = 1;
            }
        }

        bool shouldShowSlider = depth > size.y;

        if (shouldShowSlider && MouseOver) {
            float scrollDelta = Input.mouseScrollDelta.y * -25;
            if (vel < 0 == scrollDelta < 0) {
                vel += scrollDelta;
            } else if (scrollDelta != 0) {
                vel = scrollDelta;
            }
        }

        sliderValue += vel / (depth + edgePadding - size.y);
        sliderValue = Mathf.Clamp01(sliderValue);
        vel *= 0.8f;

        slider.GetButtonBehavior.greyedOut = !shouldShowSlider;
        sliderContainer.Container.isVisible = shouldShowSlider;
        scrollPos = shouldShowSlider ? sliderValue * (depth + edgePadding - size.y) : 0;

        base.Update();
    }

    private float scrollPos;
    private float sliderValue;
    private float vel;

    void Slider.ISliderOwner.SliderSetValue(Slider slider, float setValue) => sliderValue = 1 - setValue;
    float Slider.ISliderOwner.ValueOfSlider(Slider slider) => 1 - sliderValue;
}
