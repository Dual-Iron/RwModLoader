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
    private JobStatus status;
    private Exception? exception;

    public JobStatus Status {
        get {
            lock (o) {
                return status;
            }
        }
    }

    public Exception? Exception {
        get {
            lock (o) {
                return exception;
            }
        }
    }

    public Job(Action callback)
    {
        action = callback;
    }

    public void Start()
    {
        if (Status != JobStatus.Unstarted) {
            throw new InvalidOperationException("Already started");
        }
        status = JobStatus.InProgress; // No need to lock yet, this is the same thread as the caller.
        ThreadPool.QueueUserWorkItem(Work);
    }

    private void Work(object _)
    {
        try {
            action!();
            Finish(null);
        }
        catch (Exception e) {
            Finish(e);
        }
        action = null;
    }

    private void Finish(Exception? e)
    {
        lock (o) {
            status = JobStatus.Finished;
            exception = e;
        }
    }
}
