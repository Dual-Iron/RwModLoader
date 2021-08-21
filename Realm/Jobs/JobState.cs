using System;

namespace Realm.Jobs
{
    public sealed class JobState
    {
        public JobState()
        {
            Progress = JobProgress.NotStarted;
        }

        private static readonly object o = new();

        public JobProgress Progress { get; private set; } 
        public Exception? Exception { get; private set; }
        public bool Ok => Exception == null;

        public void Start()
        {
            lock (o) {
                if (Progress != JobProgress.NotStarted) {
                    throw new InvalidOperationException("Job was already started.");
                }
                Progress = JobProgress.InProgress;
            }
        }

        public void Finish(Exception? e)
        {
            lock (o) {
                if (Progress == JobProgress.Finished) {
                    throw new InvalidOperationException("Job was already finished.");
                }
                Progress = JobProgress.Finished;
                Exception = e;
            }
        }
    }
}
