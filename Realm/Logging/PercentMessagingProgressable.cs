namespace Realm.Logging
{
    public class PercentMessagingProgressable : Progressable
    {
        public override float Progress {
            get => base.Progress;
            set => Message(MessageType.Info, $"Progress: {base.Progress = value:p}");
        }
    }
}
