namespace Realm.Logging;

interface IProgressable : IMessageable
{
    float Progress { get; set; }
    ProgressStateType ProgressState { get; }
}
