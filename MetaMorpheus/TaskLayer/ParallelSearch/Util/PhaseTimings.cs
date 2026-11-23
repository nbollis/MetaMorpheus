#nullable enable
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace TaskLayer.ParallelSearch.Util;

/// <summary>
/// Opt-in wall-clock timings of each phase of each transient database, for profiling throughput. Off unless the environment
/// variable <see cref="EnvironmentVariable"/> names a file; then every phase is recorded and written there as a TSV when the
/// transient loop ends, followed by the loop's process CPU time and garbage collection totals. Never changes search output.
/// </summary>
public static class PhaseTimings
{
    public const string EnvironmentVariable = "MM_PS_PHASE_TIMINGS";

    private static readonly ConcurrentQueue<(string Database, string Phase, double Milliseconds, int ThreadId)> Records = new();
    private static string? _path = Environment.GetEnvironmentVariable(EnvironmentVariable);
    private static Stopwatch? _loopClock;
    private static TimeSpan _loopCpuStart;
    private static int[] _gcCountsStart = new int[3];
    private static TimeSpan _gcPauseStart;

    public static bool Enabled => _path != null;

    /// <summary> For tests: turns the timings on (writing to <paramref name="path"/>) or off (null). </summary>
    public static void Configure(string? path)
    {
        _path = path;
        Records.Clear();
    }

    /// <summary> Times the phase until the returned scope is disposed; costs nothing when timings are off. </summary>
    public static Scope Time(string database, string phase) => Enabled ? new Scope(database, phase) : default;

    public static void Record(string database, string phase, double milliseconds)
    {
        if (Enabled)
            Records.Enqueue((database, phase, milliseconds, Environment.CurrentManagedThreadId));
    }

    public static void StartLoop()
    {
        if (!Enabled)
            return;
        _loopClock = Stopwatch.StartNew();
        _loopCpuStart = Process.GetCurrentProcess().TotalProcessorTime;
        _gcCountsStart = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
        _gcPauseStart = GC.GetTotalPauseDuration();
    }

    /// <summary> Writes every recorded phase and the loop totals to the configured file. </summary>
    public static void EndLoopAndWrite(int filesInParallel, int threadsPerFile)
    {
        if (!Enabled || _loopClock is null)
            return;

        double wallSeconds = _loopClock.Elapsed.TotalSeconds;
        double cpuSeconds = (Process.GetCurrentProcess().TotalProcessorTime - _loopCpuStart).TotalSeconds;
        double gcPauseSeconds = (GC.GetTotalPauseDuration() - _gcPauseStart).TotalSeconds;
        var info = GC.GetGCMemoryInfo();

        var text = new StringBuilder();
        text.AppendLine("Database\tPhase\tMilliseconds\tThread");
        foreach (var (database, phase, ms, thread) in Records)
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{database}\t{phase}\t{ms:F2}\t{thread}"));
        text.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"#loop\twall_s={wallSeconds:F2}\tcpu_s={cpuSeconds:F2}\tbusy_threads={cpuSeconds / Math.Max(1e-9, wallSeconds):F2}" +
            $"\tfiles_in_parallel={filesInParallel}\tthreads_per_file={threadsPerFile}" +
            $"\tgc0={GC.CollectionCount(0) - _gcCountsStart[0]}\tgc1={GC.CollectionCount(1) - _gcCountsStart[1]}" +
            $"\tgc2={GC.CollectionCount(2) - _gcCountsStart[2]}\tgc_pause_s={gcPauseSeconds:F2}" +
            $"\tserver_gc={System.Runtime.GCSettings.IsServerGC}\theap_gb={info.HeapSizeBytes / 1e9:F2}" +
            $"\tdatabases={Records.Select(r => r.Database).Distinct().Count()}"));
        File.WriteAllText(_path!, text.ToString());
    }

    /// <summary> Records consecutive phases: each <see cref="Lap.Mark"/> records the time since the previous mark. </summary>
    public static Lap StartLap(string database) => new(Enabled ? database : null);

    public sealed class Lap
    {
        private readonly string? _database;
        private long _last = Stopwatch.GetTimestamp();

        internal Lap(string? database) => _database = database;

        public void Mark(string phase)
        {
            if (_database is null)
                return;
            long now = Stopwatch.GetTimestamp();
            Record(_database, phase, Stopwatch.GetElapsedTime(_last, now).TotalMilliseconds);
            _last = now;
        }
    }

    public readonly struct Scope : IDisposable
    {
        private readonly string? _database;
        private readonly string? _phase;
        private readonly long _start;

        public Scope(string database, string phase)
        {
            _database = database;
            _phase = phase;
            _start = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            if (_database is null)
                return;
            Record(_database, _phase!, Stopwatch.GetElapsedTime(_start).TotalMilliseconds);
        }
    }
}
