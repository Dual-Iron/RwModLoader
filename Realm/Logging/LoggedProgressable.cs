using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Realm.Logging
{
    public class LoggedProgressable : Progressable
    {
        public ReadOnlyCollection<MessageInfo> Messages { get; }

        private readonly List<MessageInfo> messages = new();

        public LoggedProgressable()
        {
            Messages = new(messages);
        }

        public override void Message(MessageType messageType, string message)
        {
            messages.Add(new(messageType, message));

            base.Message(messageType, message);
        }
    }
}
