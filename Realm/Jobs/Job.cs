using System.Threading;

namespace Realm.Jobs;

sealed class Job
{
    public static Job Start(Action callback)
    {
        Job ret = new(callback);
        ret.Start();
        return ret;
    }

    private readonly object o = new();
    private Action? action;

    public JobStatus Status { get; private set; }
    public Exception? Exception { get; private set; }
    public bool Ok => Exception == null;

    public Job(Action callback)
    {
        action = callback;
    }

    public void Start()
    {
        if (Status != JobStatus.Unstarted) {
            throw new InvalidOperationException("Already started");
        }
        Status = JobStatus.InProgress;
        ThreadPool.QueueUserWorkItem(Work);
    }

    public void Wait()
    {
        while (Status != JobStatus.Finished) {
            Thread.Sleep(0);
        }
    }

    private void Work(object _)
    {
        try {
            action!();
            Finish(null);
        } catch (Exception e) {
            Finish(e);
        }
        action = null;
    }

    private void Finish(Exception? e)
    {
        lock (o) {
            Status = JobStatus.Finished;
            Exception = e;
        }
    }
}
