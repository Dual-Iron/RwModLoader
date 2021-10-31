namespace Realm.Logging;

interface IMessageable
{
    void Message(MessageType messageType, string message);
}
