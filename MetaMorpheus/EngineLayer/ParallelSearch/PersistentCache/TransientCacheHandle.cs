using EngineLayer.ParallelSearch.PersistentCache.Manifest;
using System.Collections.Generic;

namespace EngineLayer.ParallelSearch.PersistentCache;

internal sealed class TransientCacheHandle
{
    public TransientCacheLookupOutcome Outcome { get; }
    public string? Detail { get; }
    public string DatabasePath { get; }
    public TransientCacheKey CacheKey { get; }
    public string CanonicalSettingsPayload { get; }
    public TransientCacheManifestStore ManifestStore { get; }
    public TransientCacheStorageLayout StorageLayout { get; }
    public TransientCacheManifestEntry? PublishedEntry { get; }
    public IReadOnlyList<TransientCacheResolvedShardReference> ResolvedShards { get; }
    public IReadOnlyList<TransientCacheResolvedSequenceReference> ResolvedSequences { get; }

    public bool HasReusableEntry => Outcome == TransientCacheLookupOutcome.Hit && PublishedEntry is not null;

    private TransientCacheHandle(
        TransientCacheLookupOutcome outcome,
        string? detail,
        string databasePath,
        TransientCacheKey cacheKey,
        string canonicalSettingsPayload,
        TransientCacheManifestStore manifestStore,
        TransientCacheStorageLayout storageLayout,
        TransientCacheManifestEntry? publishedEntry,
        IReadOnlyList<TransientCacheResolvedShardReference>? resolvedShards,
        IReadOnlyList<TransientCacheResolvedSequenceReference>? resolvedSequences)
    {
        Outcome = outcome;
        Detail = detail;
        DatabasePath = databasePath;
        CacheKey = cacheKey;
        CanonicalSettingsPayload = canonicalSettingsPayload;
        ManifestStore = manifestStore;
        StorageLayout = storageLayout;
        PublishedEntry = publishedEntry;
        ResolvedShards = resolvedShards ?? [];
        ResolvedSequences = resolvedSequences ?? [];
    }

    public static TransientCacheHandle Disabled(string databasePath, string? detail = null)
        => new(TransientCacheLookupOutcome.Disabled, detail, databasePath, default!, string.Empty, default!, default!, null, null, null);

    public static TransientCacheHandle Miss(
        string databasePath,
        TransientCacheKey cacheKey,
        string canonicalSettingsPayload,
        TransientCacheManifestStore manifestStore,
        TransientCacheStorageLayout storageLayout)
        => new(TransientCacheLookupOutcome.Miss, null, databasePath, cacheKey, canonicalSettingsPayload, manifestStore, storageLayout, null, null, null);

    public static TransientCacheHandle Hit(
        string databasePath,
        TransientCacheKey cacheKey,
        string canonicalSettingsPayload,
        TransientCacheManifestStore manifestStore,
        TransientCacheStorageLayout storageLayout,
        TransientCacheManifestEntry publishedEntry,
        IReadOnlyList<TransientCacheResolvedShardReference> resolvedShards,
        IReadOnlyList<TransientCacheResolvedSequenceReference> resolvedSequences)
        => new(TransientCacheLookupOutcome.Hit, null, databasePath, cacheKey, canonicalSettingsPayload, manifestStore, storageLayout, publishedEntry, resolvedShards, resolvedSequences);

    public static TransientCacheHandle Corrupt(
        string databasePath,
        TransientCacheKey cacheKey,
        string canonicalSettingsPayload,
        TransientCacheManifestStore manifestStore,
        TransientCacheStorageLayout storageLayout,
        string detail)
        => new(TransientCacheLookupOutcome.Corrupt, detail, databasePath, cacheKey, canonicalSettingsPayload, manifestStore, storageLayout, null, null, null);
}
