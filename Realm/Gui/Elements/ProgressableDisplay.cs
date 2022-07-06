using Menu;
using Realm.Logging;
using UnityEngine;

namespace Realm.Gui.Elements;

sealed class ProgressableDisplay : RectangularMenuObject
{
    private readonly MenuLabel progress;
    private readonly MenuLabel message;
    private readonly CachedProgressable messages;

    public ProgressableDisplay(CachedProgressable prog, MenuObject owner, Vector2 pos, Vector2 size) : base(owner.menu, owner, pos, size)
    {
        subObjects.Add(new RoundedRect(menu, this, default, size, true) { fillAlpha = 0.8f });
        subObjects.Add(message = new MenuLabel(menu, this, $"Starting", size / 2 - 10 * Vector2.up, default, false));
        subObjects.Add(progress = new MenuLabel(menu, this, $"{0:p}", size / 2 + 10 * Vector2.up, default, true));

        messages = prog;
    }

    public bool ShowProgressPercent {
        get => progress.label.isVisible;
        set => progress.label.isVisible = value;
    }

    int lastCount;
    bool freeze;

    public override void Update()
    {
        if (lastCount != messages.Count && !freeze) {
            int messageIndex = messages.Count - 1;

            // If there's a fatal error since the last message, display that error and nothing else.
            for (int i = lastCount; i < messages.Count; i++) {
                if (messages[i].Type == MessageType.Fatal) {
                    freeze = true;
                    messageIndex = i;
                    break;
                }
            }

            lastCount = messages.Count;

            // Set the actual message text
            MessageInfo latestMessage = messages[messageIndex];
            message.text = latestMessage.Message.SplitLongLines(message.label._font, size.x - 10).JoinStr("\n");
            message.label.color = latestMessage.Type switch {
                MessageType.Info => Color.white,
                MessageType.Warning => Color.yellow,
                MessageType.Fatal => Color.red,
                _ => Color.grey
            };
        }

        if (progress != null) {
            if (messages.ProgressState == ProgressStateType.Failed)
                progress.label.isVisible = false;
            else
                progress.text = messages.Progress.ToString("p");
        }

        base.Update();
    }
}
