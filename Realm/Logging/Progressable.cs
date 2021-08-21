using BepInEx.Logging;

namespace Realm.Logging
{
    public sealed class Progressable : IProgressable
    {
        public float Progress { get; set; }
        public ProgressStateType ProgressState { get; set; }

        public void Message(MessageType messageType, string message)
        {
            if (messageType == MessageType.Fatal) {
                ProgressState = ProgressStateType.Failed;
            }

            Program.Logger.Log(messageType switch {
                MessageType.Info => LogLevel.Info,
                MessageType.Warning => LogLevel.Warning,
                MessageType.Fatal => LogLevel.Fatal,
                _ => LogLevel.Message
            }, message);
        }
    }
}
