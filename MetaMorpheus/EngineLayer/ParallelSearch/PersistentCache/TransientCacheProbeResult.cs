using System.Collections.Generic;
using EngineLayer.ParallelSearch.PersistentCache.Manifest;

namespace EngineLayer.ParallelSearch.PersistentCache;

internal sealed class TransientCacheProbeResult
{
    public TransientCacheLookupOutcome Outcome { get; }
    public string? Detail { get; }
    public TransientCacheHandle? Handle { get; }
    public TransientCacheManifestEntry? PublishedEntry { get; }
    public IReadOnlyList<TransientCacheResolvedShardReference> ResolvedShards { get; }
    public IReadOnlyList<TransientCacheResolvedSequenceReference> ResolvedSequences { get; }

    public bool HasReusableEntry => Outcome == TransientCacheLookupOutcome.Hit && Handle is not null && PublishedEntry is not null;

    private TransientCacheProbeResult(
        TransientCacheLookupOutcome outcome,
        string? detail,
        TransientCacheHandle? handle,
        TransientCacheManifestEntry? publishedEntry,
        IReadOnlyList<TransientCacheResolvedShardReference>? resolvedShards,
        IReadOnlyList<TransientCacheResolvedSequenceReference>? resolvedSequences)
    {
        Outcome = outcome;
        Detail = detail;
        Handle = handle;
        PublishedEntry = publishedEntry;
        ResolvedShards = resolvedShards ?? [];
        ResolvedSequences = resolvedSequences ?? [];
    }

    public static TransientCacheProbeResult Disabled(string? detail = null)
        => new(TransientCacheLookupOutcome.Disabled, detail, null, null, null, null);

    public static TransientCacheProbeResult Miss(TransientCacheHandle handle)
        => new(TransientCacheLookupOutcome.Miss, null, handle, null, null, null);

    public static TransientCacheProbeResult Hit(
        TransientCacheHandle handle,
        TransientCacheManifestEntry publishedEntry,
        IReadOnlyList<TransientCacheResolvedShardReference> resolvedShards,
        IReadOnlyList<TransientCacheResolvedSequenceReference> resolvedSequences)
        => new(TransientCacheLookupOutcome.Hit, null, handle, publishedEntry, resolvedShards, resolvedSequences);

    public static TransientCacheProbeResult Corrupt(string detail, TransientCacheHandle? handle = null)
        => new(TransientCacheLookupOutcome.Corrupt, detail, handle, null, null, null);
}
