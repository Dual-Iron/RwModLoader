namespace Realm.Logging;

public sealed class EmptyProgressable : IProgressable
{
    public float Progress { get; set; }

    public ProgressStateType ProgressState { get; set; }

    public void Message(MessageType messageType, string message)
    {
        if (messageType == MessageType.Fatal)
            ProgressState = ProgressStateType.Failed;
    }
}
