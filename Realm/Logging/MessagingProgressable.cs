namespace Realm.Logging;

sealed class MessagingProgressable : Progressable
{
    public override float Progress {
        get => base.Progress;
        set => Message(MessageType.Debug, $"Progress: {base.Progress = value:p}");
    }
}
