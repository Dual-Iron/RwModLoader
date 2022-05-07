namespace Realm.Threading;

enum TaskStatus { Queued, Running, Finished }

sealed class TaskSource
{
    public TaskStatus Status;
    public Exception? Error;
    public Action Run;

    public TaskSource(Action run) => Run = run;

    public override string ToString() => $"{Status} @ {Run.Method}";
}

struct Task
{
    private readonly TaskSource source;

    public TaskStatus Status => source.Status;
    public Exception? Error => source.Error;

    public Task(TaskSource source)
    {
        this.source = source;
    }

    public override string ToString() => source.ToString();
}
