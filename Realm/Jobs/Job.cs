using System;
using System.Collections.Generic;
using System.Threading;

namespace Realm.Jobs
{
    public sealed class Job
    {
        public static Job Start(Action callback)
        {
            Job ret = new(callback);
            ret.Start();
            return ret;
        }

        public static void WaitAll(params Job[] jobs) => WaitAll((IEnumerable<Job>)jobs);
        public static void WaitAll(IEnumerable<Job> jobs)
        {
            foreach (var job in jobs) {
                job.Wait();
            }
        }

        public JobState State { get; } = new();

        private Action? action;

        public Job(Action callback)
        {
            action = callback;
        }

        public void Start()
        {
            State.Start();
            ThreadPool.QueueUserWorkItem(Work);
        }

        public void Wait()
        {
            while (State.Progress != JobProgress.Finished) {
                Thread.Sleep(0);
            }
        }

        private void Work(object _)
        {
            try {
                action!();
                State.Finish(null);
            } catch (Exception e) {
                State.Finish(e);
            }
            action = null;
        }
    }
}
