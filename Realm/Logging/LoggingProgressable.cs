using System.Collections.ObjectModel;

namespace Realm.Logging;

sealed class LoggingProgressable : Progressable
{
    public ReadOnlyCollection<MessageInfo> Messages {
        get {
            lock (messages) {
                return messagesWrapper;
            }
        }
    }

    private readonly ReadOnlyCollection<MessageInfo> messagesWrapper;
    private readonly List<MessageInfo> messages = new();

    public LoggingProgressable()
    {
        messagesWrapper = new(messages);
    }

    public override void Message(MessageType messageType, string message)
    {
        lock (messages) {
            messages.Add(new(messageType, message));
        }

        base.Message(messageType, message);
    }
}
