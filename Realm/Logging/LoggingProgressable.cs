using System.Collections.ObjectModel;

namespace Realm.Logging;

sealed class LoggingProgressable : Progressable
{
    public MessageInfo this[int index] {
        get {
            lock (messages) {
                return messages[index];
            }
        }
    }

    public int Count => messages.Count;

    private readonly List<MessageInfo> messages = new();

    public override void Message(MessageType messageType, string message)
    {
        lock (messages) {
            messages.Add(new(messageType, message));
        }

        base.Message(messageType, message);
    }
}
