using BepInEx.Logging;

namespace Realm.Logging;

class Progressable
{
    public virtual float Progress { get; set; }
    public bool Errors { get; set; }

    public virtual void Message(MessageType messageType, string message)
    {
        if (messageType == MessageType.Fatal) {
            Errors = true;
        }

        Program.Logger.Log(messageType switch {
            MessageType.Debug => LogLevel.Debug,
            MessageType.Info => LogLevel.Info,
            MessageType.Warning => LogLevel.Warning,
            MessageType.Fatal => LogLevel.Fatal,
            _ => LogLevel.Message
        }, message);
    }
}
