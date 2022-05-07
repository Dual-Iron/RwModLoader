using System.Threading;

namespace Realm.Threading;

sealed class NetworkThread : BackgroundThread
{
    private NetworkThread() : base("network", sleepTime: 10_000) { }

    private static readonly NetworkThread current = new();

    public static NetworkThread Instance {
        get {
            current.Awaken();
            return current;
        }
    }
}

abstract class BackgroundThread
{
    private readonly object _lock = new();
    private readonly AutoResetEvent mre = new(false);
    private readonly Queue<TaskSource> actions = new();
    private readonly List<TaskSource> actionsNext = new();
    private readonly int sleepTime;

    protected BackgroundThread(string name, int sleepTime)
    {
        this.sleepTime = sleepTime;

        new Thread(RunLoop) {
            Name = name,
            IsBackground = true
        }.Start();
    }

    public Task Enqueue(Action action)
    {
        TaskSource src = new(action);

        lock (_lock) {
            actionsNext.Add(src);
        }

        return new(src);
    }

    protected void Awaken() => mre.Set();

    private void RunLoop()
    {
        while (true) {
            mre.WaitOne();
            RunOnce();
        }
    }

    private void RunOnce()
    {
        int idleTime = 0;

        while (idleTime < sleepTime) {
            if (actions.Count == 0) {
                AddTasks();
            }

            if (actions.Count == 0) {
                idleTime++;
                Thread.Sleep(1);
            }
            else {
                idleTime = 0;
                RunTask(actions.Dequeue());
            }
        }
    }

    private void AddTasks()
    {
        lock (_lock) {
            if (actionsNext.Count > 0) {
                foreach (var action in actionsNext) {
                    actions.Enqueue(action);
                }

                actionsNext.Clear();
            }
        }
    }

    private static void RunTask(TaskSource src)
    {
        src.Status = TaskStatus.Running;
        try {
            src.Run();
        }
        catch (Exception e) {
            src.Error = e;
        }
        src.Status = TaskStatus.Finished;
    }
}
