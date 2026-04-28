using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace EngineLayer.ParallelSearch.PersistentCache;

public sealed class TransientCacheTelemetry
{
    public int CacheHits { get; private set; }
    public int CacheMisses { get; private set; }
    public int SettingsMismatches { get; private set; }
    public int CorruptEntries { get; private set; }
    public int IdentityMismatches { get; private set; }
    public int Fallbacks { get; private set; }
    public int ReusedFragmentShardCount { get; private set; }
    public long PayloadBytesWritten { get; private set; }
    public long OccurrencePayloadBytesWritten { get; private set; }
    public long FragmentPayloadBytesWritten { get; private set; }
    public TimeSpan TotalHydrateTime { get; private set; }
    public TimeSpan TotalFallbackTime { get; private set; }
    public TimeSpan TotalPublishTime { get; private set; }

    private readonly Stopwatch _hydrateStopwatch = new();
    private readonly Stopwatch _fallbackStopwatch = new();
    private readonly Stopwatch _publishStopwatch = new();

    public void RecordHit()
    {
        CacheHits++;
    }

    public void RecordMiss()
    {
        CacheMisses++;
    }

    public void RecordSettingsMismatch()
    {
        SettingsMismatches++;
        Fallbacks++;
    }

    public void RecordCorrupt()
    {
        CorruptEntries++;
        Fallbacks++;
    }

    public void RecordIdentityMismatch()
    {
        IdentityMismatches++;
        Fallbacks++;
    }

    public void RecordFallback()
    {
        Fallbacks++;
    }

    public void RecordPayloadBytesWritten(long bytes)
    {
        PayloadBytesWritten += bytes;
    }

    public void RecordOccurrencePayloadBytesWritten(long bytes)
    {
        OccurrencePayloadBytesWritten += bytes;
        RecordPayloadBytesWritten(bytes);
    }

    public void RecordFragmentPayloadBytesWritten(long bytes)
    {
        FragmentPayloadBytesWritten += bytes;
        RecordPayloadBytesWritten(bytes);
    }

    public void RecordFragmentShardReuse(int count = 1)
    {
        ReusedFragmentShardCount += count;
    }

    public void StartHydrate() => _hydrateStopwatch.Start();
    public void StopHydrate() => _hydrateStopwatch.Stop();
    public void StartFallback() => _fallbackStopwatch.Start();
    public void StopFallback() => _fallbackStopwatch.Stop();
    public void StartPublish() => _publishStopwatch.Start();
    public void StopPublish() => _publishStopwatch.Stop();

    public void Freeze()
    {
        TotalHydrateTime = _hydrateStopwatch.Elapsed;
        TotalFallbackTime = _fallbackStopwatch.Elapsed;
        TotalPublishTime = _publishStopwatch.Elapsed;
    }

    public IReadOnlyDictionary<string, object> ToMetrics()
    {
        return new Dictionary<string, object>
        {
            ["CacheHits"] = CacheHits,
            ["CacheMisses"] = CacheMisses,
            ["SettingsMismatches"] = SettingsMismatches,
            ["CorruptEntries"] = CorruptEntries,
            ["IdentityMismatches"] = IdentityMismatches,
            ["Fallbacks"] = Fallbacks,
            ["ReusedFragmentShardCount"] = ReusedFragmentShardCount,
            ["PayloadBytesWritten"] = PayloadBytesWritten,
            ["OccurrencePayloadBytesWritten"] = OccurrencePayloadBytesWritten,
            ["FragmentPayloadBytesWritten"] = FragmentPayloadBytesWritten,
            ["TotalHydrateTimeMs"] = TotalHydrateTime.TotalMilliseconds,
            ["TotalFallbackTimeMs"] = TotalFallbackTime.TotalMilliseconds,
            ["TotalPublishTimeMs"] = TotalPublishTime.TotalMilliseconds,
        };
    }
}
