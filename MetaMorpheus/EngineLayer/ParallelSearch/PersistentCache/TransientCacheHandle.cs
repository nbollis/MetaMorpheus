using EngineLayer.ParallelSearch.PersistentCache.Manifest;

namespace EngineLayer.ParallelSearch.PersistentCache;

internal sealed class TransientCacheHandle
{
    public string DatabasePath { get; }
    public TransientCacheKey CacheKey { get; }
    public string CanonicalSettingsPayload { get; }
    public TransientCacheManifestStore ManifestStore { get; }
    public TransientCacheStorageLayout StorageLayout { get; }

    public TransientCacheHandle(
        string databasePath,
        TransientCacheKey cacheKey,
        string canonicalSettingsPayload,
        TransientCacheManifestStore manifestStore,
        TransientCacheStorageLayout storageLayout)
    {
        DatabasePath = databasePath;
        CacheKey = cacheKey;
        CanonicalSettingsPayload = canonicalSettingsPayload;
        ManifestStore = manifestStore;
        StorageLayout = storageLayout;
    }
}
