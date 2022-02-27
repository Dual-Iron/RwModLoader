namespace Realm.Logging;

struct MessageInfo
{
    public readonly MessageType Type;
    public readonly string Message;

    public MessageInfo(MessageType type, string message)
    {
        Type = type;
        Message = message;
    }
}

enum MessageType
{
    Debug, Info, Warning, Fatal
}
