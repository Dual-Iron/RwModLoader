namespace Realm.Logging;

public interface IMessageable
{
    void Message(MessageType messageType, string message);
}
