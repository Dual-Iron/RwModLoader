namespace Realm.Logging;

public interface IProgressable : IMessageable
{
    float Progress { get; set; }
    ProgressStateType ProgressState { get; }
}
