using BepInEx.Logging;

namespace Realm.Logging
{
    public class Progressable : IProgressable
    {
        public virtual float Progress { get; set; }
        public ProgressStateType ProgressState { get; set; }

        public virtual void Message(MessageType messageType, string message)
        {
            if (messageType == MessageType.Fatal) {
                ProgressState = ProgressStateType.Failed;
            }

            Program.Logger.Log(messageType switch {
                MessageType.Diagnostic => LogLevel.Debug,
                MessageType.Info => LogLevel.Info,
                MessageType.Warning => LogLevel.Warning,
                MessageType.Fatal => LogLevel.Fatal,
                _ => LogLevel.Message
            }, message);
        }
    }
}
