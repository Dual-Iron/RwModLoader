using System.Threading;

namespace Realm;

sealed class Job
{
    // Reads and writes of int and reference types is atomic, so these don't need locks.
    // Additionally, these fields do not need the volatile keyword:
    //  1. The volatile keyword means two or more threads may modify a field at once.
    //  2. The worker thread and main thread may modify these fields, but never at the same time.
    private bool finished;
    private Exception? exception;

    public bool Finished => finished;
    public Exception? Exception => exception;

    public static Job Start(Action callback, bool runSyncOnFail = true)
    {
        Job job = new();

        try {
            ThreadPool.QueueUserWorkItem(_ => Run(job, callback));
        } catch (NotSupportedException e) {
            Program.Logger.LogError($"Couldn't queue work item for job {callback.Method.Name}. {e}");

            // Worker thread isn't running right now, so it's safe to set `finished` and `exception` here.
            if (runSyncOnFail) {
                Program.Logger.LogDebug("Running job synchronously instead.");
                Run(job, callback);
            }
        }

        return job;
    }

    private static void Run(Job job, Action callback)
    {
        try {
            callback();
            job.finished = true;
        }
        catch (Exception e) {
            job.finished = true;
            job.exception = e;
        }
    }
}
