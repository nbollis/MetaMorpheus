using System;

namespace TaskLayer
{
    public enum ParallelSearchDashboardUpdateKind
    {
        Initialize,
        TaskStatus,
        DatabaseStarted,
        DatabaseProgress,
        DatabaseFinished,
        TaskCompleted,
    }

    public class ParallelSearchDashboardEventArgs : EventArgs
    {
        public ParallelSearchDashboardEventArgs(
            string taskId,
            ParallelSearchDashboardUpdateKind updateKind,
            string taskPhase,
            int finished,
            int total,
            int todo,
            int cached,
            string databaseName = null,
            string statusText = null,
            int? progressPercent = null,
            int? engineProgressMinimum = null,
            int? engineProgressMaximum = null)
        {
            TaskId = taskId;
            UpdateKind = updateKind;
            TaskPhase = taskPhase;
            Finished = finished;
            Total = total;
            Todo = todo;
            Cached = cached;
            DatabaseName = databaseName;
            StatusText = statusText;
            ProgressPercent = progressPercent;
            EngineProgressMinimum = engineProgressMinimum;
            EngineProgressMaximum = engineProgressMaximum;
        }

        public string TaskId { get; }

        public ParallelSearchDashboardUpdateKind UpdateKind { get; }

        public string TaskPhase { get; }

        public int Finished { get; }

        public int Total { get; }

        public int Todo { get; }

        public int Cached { get; }

        public string DatabaseName { get; }

        public string StatusText { get; }

        public int? ProgressPercent { get; }

        public int? EngineProgressMinimum { get; }

        public int? EngineProgressMaximum { get; }
    }
}
