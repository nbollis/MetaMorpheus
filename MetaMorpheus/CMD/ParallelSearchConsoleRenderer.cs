using EngineLayer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using TaskLayer;

namespace MetaMorpheusCommandLine
{
    internal sealed class ParallelSearchConsoleRenderer
    {
        private const int LabelWidth = 22;
        private const int ProgressBarWidth = 24;

        private readonly object _lock = new object();
        private readonly Stopwatch _renderStopwatch = Stopwatch.StartNew();
        private readonly TimeSpan _minimumRenderInterval = TimeSpan.FromMilliseconds(125);
        private readonly Dictionary<string, DatabaseRowState> _databaseRows = new Dictionary<string, DatabaseRowState>(StringComparer.Ordinal);
        private readonly List<string> _databaseDisplayOrder = new List<string>();

        private string _taskId;
        private string _taskPhase = "Initializing";
        private string _taskStatus = "Initializing parallel search...";
        private int _finished;
        private int _total;
        private int _todo;
        private int _cached;
        private int _originTop = -1;
        private int _renderedLineCount;
        private TimeSpan _lastRenderTime = TimeSpan.MinValue;
        private bool _isActive;

        public bool IsActive
        {
            get
            {
                lock (_lock)
                {
                    return _isActive;
                }
            }
        }

        public void Start(string taskId)
        {
            lock (_lock)
            {
                if (_isActive)
                {
                    return;
                }

                _taskId = taskId;
                _isActive = true;
                TrySetCursorVisible(false);
            }
        }

        public bool IsTrackingTask(string taskId)
        {
            lock (_lock)
            {
                return _isActive && string.Equals(_taskId, taskId, StringComparison.Ordinal);
            }
        }

        public void HandleUpdate(ParallelSearchDashboardEventArgs update)
        {
            lock (_lock)
            {
                if (!_isActive)
                {
                    Start(update.TaskId);
                }

                if (!string.Equals(_taskId, update.TaskId, StringComparison.Ordinal))
                {
                    return;
                }

                _taskPhase = update.TaskPhase;
                _finished = update.Finished;
                _total = update.Total;
                _todo = update.Todo;
                _cached = update.Cached;

                switch (update.UpdateKind)
                {
                    case ParallelSearchDashboardUpdateKind.Initialize:
                    case ParallelSearchDashboardUpdateKind.TaskStatus:
                    case ParallelSearchDashboardUpdateKind.TaskCompleted:
                        _taskStatus = update.StatusText ?? _taskStatus;
                        break;

                    case ParallelSearchDashboardUpdateKind.DatabaseStarted:
                    case ParallelSearchDashboardUpdateKind.DatabaseProgress:
                        UpdateDatabaseRow(update);
                        break;

                    case ParallelSearchDashboardUpdateKind.DatabaseFinished:
                        RemoveDatabaseRow(update.DatabaseName);
                        break;
                }

                RequestRender(force: update.UpdateKind != ParallelSearchDashboardUpdateKind.DatabaseProgress);
            }
        }

        public void HandleEngineProgress(ProgressEventArgs progressEventArgs)
        {
            if (progressEventArgs.NestedIDs == null || progressEventArgs.NestedIDs.Count < 2)
            {
                return;
            }

            lock (_lock)
            {
                if (!_isActive || !string.Equals(_taskId, progressEventArgs.NestedIDs[0], StringComparison.Ordinal))
                {
                    return;
                }

                string databaseName = progressEventArgs.NestedIDs[1];
                if (!_databaseRows.TryGetValue(databaseName, out var row) || !row.AcceptsEngineProgress)
                {
                    return;
                }

                int weightedProgress = row.EngineProgressMinimum + (int)Math.Round(
                    (row.EngineProgressMaximum - row.EngineProgressMinimum) * (progressEventArgs.NewProgress / 100.0));
                weightedProgress = Math.Clamp(weightedProgress, row.EngineProgressMinimum, row.EngineProgressMaximum);

                if (weightedProgress == row.ProgressPercent)
                {
                    return;
                }

                row.ProgressPercent = weightedProgress;
                RequestRender(force: false);
            }
        }

        public void WriteMessage(string message)
        {
            lock (_lock)
            {
                if (!_isActive)
                {
                    Console.WriteLine(message);
                    return;
                }

                ClearDashboardLocked();
                WriteMessageLines(message);
                _originTop = Console.CursorTop;
                _renderedLineCount = 0;
                RenderLocked();
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (!_isActive)
                {
                    return;
                }

                RenderLocked();

                if (_originTop >= 0)
                {
                    TrySetCursorPosition(0, _originTop + _renderedLineCount);
                }

                Console.WriteLine();
                TrySetCursorVisible(true);

                _databaseRows.Clear();
                _databaseDisplayOrder.Clear();
                _originTop = -1;
                _renderedLineCount = 0;
                _isActive = false;
                _taskId = null;
            }
        }

        private void UpdateDatabaseRow(ParallelSearchDashboardEventArgs update)
        {
            if (string.IsNullOrEmpty(update.DatabaseName))
            {
                return;
            }

            if (!_databaseRows.TryGetValue(update.DatabaseName, out var row))
            {
                row = new DatabaseRowState(update.DatabaseName);
                _databaseRows[update.DatabaseName] = row;
                _databaseDisplayOrder.Add(update.DatabaseName);
            }

            row.StatusText = update.StatusText ?? row.StatusText;
            if (update.ProgressPercent.HasValue)
            {
                row.ProgressPercent = Math.Clamp(update.ProgressPercent.Value, 0, 100);
            }

            row.AcceptsEngineProgress = update.EngineProgressMinimum.HasValue && update.EngineProgressMaximum.HasValue;
            row.EngineProgressMinimum = update.EngineProgressMinimum ?? row.ProgressPercent;
            row.EngineProgressMaximum = update.EngineProgressMaximum ?? row.ProgressPercent;
        }

        private void RemoveDatabaseRow(string databaseName)
        {
            if (string.IsNullOrEmpty(databaseName))
            {
                return;
            }

            _databaseRows.Remove(databaseName);
            _databaseDisplayOrder.Remove(databaseName);
        }

        private void RequestRender(bool force)
        {
            if (force || _renderStopwatch.Elapsed - _lastRenderTime >= _minimumRenderInterval)
            {
                RenderLocked();
            }
        }

        private void RenderLocked()
        {
            if (!_isActive)
            {
                return;
            }

            int width = GetConsoleWidth();
            if (width <= 0)
            {
                return;
            }

            if (_originTop < 0)
            {
                _originTop = Console.CursorTop;
            }

            var lines = BuildLines(width);
            for (int i = 0; i < lines.Count; i++)
            {
                WriteLineAt(_originTop + i, lines[i], width);
            }

            for (int i = lines.Count; i < _renderedLineCount; i++)
            {
                WriteLineAt(_originTop + i, string.Empty, width);
            }

            TrySetCursorPosition(0, _originTop + lines.Count);
            _renderedLineCount = lines.Count;
            _lastRenderTime = _renderStopwatch.Elapsed;
        }

        private List<string> BuildLines(int width)
        {
            var lines = new List<string>
            {
                FormatHeaderLine(width),
                FormatRowLine("Task", GetTaskProgressPercent(), _taskStatus ?? _taskPhase, width)
            };

            foreach (var databaseName in _databaseDisplayOrder)
            {
                if (_databaseRows.TryGetValue(databaseName, out var row))
                {
                    lines.Add(FormatRowLine(row.Name, row.ProgressPercent, row.StatusText, width));
                }
            }

            return lines;
        }

        private string FormatHeaderLine(int width)
        {
            string header = $"Parallel Search Task | Phase: {_taskPhase} | Finished: {_finished} | Total: {_total} | TODO: {_todo} | Cached: {_cached}";
            return Truncate(header, width);
        }

        private string FormatRowLine(string label, int progressPercent, string statusText, int width)
        {
            string safeLabel = Truncate(label ?? string.Empty, LabelWidth).PadRight(LabelWidth);
            string progressBar = BuildProgressBar(progressPercent);
            string prefix = $"{safeLabel} {progressBar} {progressPercent,3}% ";
            int remainingWidth = Math.Max(0, width - prefix.Length);
            string safeStatus = Truncate(statusText ?? string.Empty, remainingWidth);
            return prefix + safeStatus;
        }

        private static string BuildProgressBar(int progressPercent)
        {
            int clampedPercent = Math.Clamp(progressPercent, 0, 100);
            int filled = (int)Math.Round(ProgressBarWidth * (clampedPercent / 100.0));
            return "[" + new string('#', filled) + new string('-', ProgressBarWidth - filled) + "]";
        }

        private int GetTaskProgressPercent()
        {
            if (_total <= 0)
            {
                return 0;
            }

            return Math.Clamp((int)Math.Round(_finished * 100.0 / _total), 0, 100);
        }

        private void ClearDashboardLocked()
        {
            if (_originTop < 0 || _renderedLineCount <= 0)
            {
                return;
            }

            int width = GetConsoleWidth();
            for (int i = 0; i < _renderedLineCount; i++)
            {
                WriteLineAt(_originTop + i, string.Empty, width);
            }

            TrySetCursorPosition(0, _originTop);
        }

        private static string Truncate(string value, int width)
        {
            if (string.IsNullOrEmpty(value) || width <= 0)
            {
                return string.Empty;
            }

            return value.Length <= width ? value : value.Substring(0, width);
        }

        private static int GetConsoleWidth()
        {
            try
            {
                return Math.Max(40, Console.BufferWidth - 1);
            }
            catch
            {
                return 120;
            }
        }

        private static void WriteLineAt(int top, string line, int width)
        {
            TrySetCursorPosition(0, top);
            string paddedLine = Truncate(line, width).PadRight(width);
            Console.Write(paddedLine);
        }

        private static void TrySetCursorPosition(int left, int top)
        {
            try
            {
                Console.SetCursorPosition(left, top);
            }
            catch
            {
            }
        }

        private static void TrySetCursorVisible(bool visible)
        {
            try
            {
                Console.CursorVisible = visible;
            }
            catch
            {
            }
        }

        private static void WriteMessageLines(string message)
        {
            string normalized = (message ?? string.Empty).Replace("\r", string.Empty);
            foreach (var line in normalized.Split('\n'))
            {
                Console.WriteLine(line);
            }
        }

        private sealed class DatabaseRowState
        {
            public DatabaseRowState(string name)
            {
                Name = name;
                StatusText = string.Empty;
            }

            public string Name { get; }

            public string StatusText { get; set; }

            public int ProgressPercent { get; set; }

            public bool AcceptsEngineProgress { get; set; }

            public int EngineProgressMinimum { get; set; }

            public int EngineProgressMaximum { get; set; }
        }
    }
}
