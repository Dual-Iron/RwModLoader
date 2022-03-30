using Menu;
using UnityEngine;

namespace Realm.Gui;

sealed class MultiLabel : RectangularMenuObject
{
    readonly List<FLabel> labels = new();
    readonly string font;
    
    public float LineHeight { get; set; }

    public MultiLabel(MenuObject owner, Vector2 pos, Vector2 size, string font = "font") : base(owner.menu, owner, pos, size)
    {
        this.font = font;

        owner.Container.AddChild(Container = new());
    }

    public void ClearLabels()
    {
        Container.RemoveAllChildren();
        labels.Clear();
    }

    public void SetLabel(Color color, string text)
    {
        if (labels.Count == 1) {
            labels[0].color = color;
            labels[0].text = text;
        } else {
            if (labels.Count > 1) {
                ClearLabels();
            }
            AddLabel(color, text);
        }
    }

    public void AddLabel(Color color, string text)
    {
        // Assume single-line for now because that's good enough for my purposes
        FLabel newLabel = new(font, text) {
            alignment = FLabelAlignment.Left,
            color = color,
        };
        labels.Add(newLabel);
        Container.AddChild(newLabel);
    }

    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);

        Container.x = DrawX(timeStacker);
        Container.y = DrawY(timeStacker);

        UpdateLabelPositions();
    }

    private void UpdateLabelPositions()
    {
        float width = 0;
        foreach (var label in labels) {
            label.x = width;
            width += label.text.MeasureWidth(label._font);
        }
    }
}
