using System.Collections.Generic;
using EngineLayer.ParallelSearch.PersistentCache.Manifest;

namespace EngineLayer.ParallelSearch.PersistentCache;

internal sealed class TransientCacheLookupResult
{
    public TransientCacheLookupOutcome Outcome { get; }
    public string? Detail { get; }
    public TransientCacheContext? Context { get; }
    public TransientCacheManifestEntry? PublishedEntry { get; }
    public IReadOnlyList<TransientCacheResolvedShardReference> ResolvedShards { get; }
    public IReadOnlyList<TransientCacheResolvedSequenceReference> ResolvedSequences { get; }

    public bool HasReusableEntry => Outcome == TransientCacheLookupOutcome.Hit && Context is not null && PublishedEntry is not null;

    private TransientCacheLookupResult(
        TransientCacheLookupOutcome outcome,
        string? detail,
        TransientCacheContext? context,
        TransientCacheManifestEntry? publishedEntry,
        IReadOnlyList<TransientCacheResolvedShardReference>? resolvedShards,
        IReadOnlyList<TransientCacheResolvedSequenceReference>? resolvedSequences)
    {
        Outcome = outcome;
        Detail = detail;
        Context = context;
        PublishedEntry = publishedEntry;
        ResolvedShards = resolvedShards ?? [];
        ResolvedSequences = resolvedSequences ?? [];
    }

    public static TransientCacheLookupResult Disabled(string? detail = null)
        => new(TransientCacheLookupOutcome.Disabled, detail, null, null, null, null);

    public static TransientCacheLookupResult Miss(TransientCacheContext context)
        => new(TransientCacheLookupOutcome.Miss, null, context, null, null, null);

    public static TransientCacheLookupResult Hit(
        TransientCacheContext context,
        TransientCacheManifestEntry publishedEntry,
        IReadOnlyList<TransientCacheResolvedShardReference> resolvedShards,
        IReadOnlyList<TransientCacheResolvedSequenceReference> resolvedSequences)
        => new(TransientCacheLookupOutcome.Hit, null, context, publishedEntry, resolvedShards, resolvedSequences);

    public static TransientCacheLookupResult Corrupt(string detail, TransientCacheContext? context = null)
        => new(TransientCacheLookupOutcome.Corrupt, detail, context, null, null, null);
}
