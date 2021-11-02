using Menu;
using Realm.Logging;
using UnityEngine;

namespace Realm.Gui;

sealed class ProgressableDisplay : RectangularMenuObject
{
    private readonly MenuLabel? progress;
    private readonly MenuLabel message;
    private readonly LoggingProgressable prog;

    public ProgressableDisplay(LoggingProgressable prog, MenuObject owner, Vector2 pos, Vector2 size, bool messageOnly = false) : base(owner.menu, owner, pos, size)
    {
        subObjects.Add(new RoundedRect(menu, this, default, size, true) { fillAlpha = 0.8f });
        subObjects.Add(message = new MenuLabel(menu, this, $"Starting", size / 2 - 10 * Vector2.up, default, false));

        if (!messageOnly) {
            subObjects.Add(progress = new MenuLabel(menu, this, $"{0:p}", size / 2 + 10 * Vector2.up, default, true));
        }

        this.prog = prog;
    }

    int lastCount = 0;

    public override void Update()
    {
        if (lastCount != prog.Messages.Count) {
            lastCount = prog.Messages.Count;

            MessageInfo latestMessage = prog.Messages[prog.Messages.Count - 1];
            message.text = string.Join("\n", latestMessage.Message.SplitLongLines(size.x, message.label._font).ToArray());
            message.label.color = latestMessage.Type switch {
                MessageType.Info => Color.white,
                MessageType.Warning => Color.yellow,
                MessageType.Fatal => Color.red,
                _ => Color.grey
            };
        }

        if (progress != null) {
            if (prog.ProgressState != ProgressStateType.Failed)
                progress.text = $"{prog.Progress:p}";
            else
                progress.label.isVisible = false;
        }

        base.Update();
    }
}
