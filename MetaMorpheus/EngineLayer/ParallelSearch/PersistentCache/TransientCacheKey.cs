using System;

namespace EngineLayer.ParallelSearch.PersistentCache;

internal readonly record struct TransientCacheKey
{
    public string DatabaseContentHash { get; }
    public string CacheSettingsId { get; }

    public TransientCacheKey(string databaseContentHash, string cacheSettingsId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseContentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheSettingsId);

        DatabaseContentHash = databaseContentHash;
        CacheSettingsId = cacheSettingsId;
    }

    public override string ToString()
    {
        return $"{DatabaseContentHash}:{CacheSettingsId}";
    }
}
