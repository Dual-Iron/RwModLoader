namespace Realm.Logging;

sealed class CallbackProgressable : Progressable
{
    private readonly Action<float> onProgressUpdate;
    private readonly Action<MessageType, string> onMessage;

    public CallbackProgressable(Action<float> onProgressUpdate, Action<MessageType, string> onMessage)
    {
        this.onProgressUpdate = onProgressUpdate;
        this.onMessage = onMessage;
    }

    public override float Progress {
        get => base.Progress;
        set {
            try {
                onProgressUpdate(base.Progress = value);
            }
            catch (Exception e) {
                Program.Logger.LogError($"Progress tracker threw an exception while processing a progress update.\n\n{e}");
            }
        }
    }

    public override void Message(MessageType messageType, string message)
    {
        base.Message(messageType, message);

        try {
            onMessage(messageType, message);
        }
        catch (Exception e) {
            Program.Logger.LogError($"Progress tracker threw an exception while processing a message.\n\n{e}");
        }
    }
}
