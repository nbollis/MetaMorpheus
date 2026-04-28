using System.Collections.Generic;
using System.IO;
using EngineLayer.ParallelSearch.PersistentCache.Manifest;
using UsefulProteomicsDatabases;

namespace EngineLayer.ParallelSearch.PersistentCache;

internal sealed class TransientDatabaseCache
{
    private readonly CommonParameters _commonParameters;
    private readonly DecoyType _decoyType;
    private readonly bool _generateTargets;
    private readonly List<string> _localizableMods;
    private readonly TargetContaminantAmbiguity _targetContaminantAmbiguity;
    private readonly string _dbFilePath;
    private readonly TransientCacheStorageLayout _storageLayout;

    public TransientDatabaseCache(
        CommonParameters commonParameters,
        DecoyType decoyType,
        bool generateTargets,
        List<string>? localizableMods,
        TargetContaminantAmbiguity targetContaminantAmbiguity,
        string dbFilePath,
        TransientCacheStorageLayout storageLayout)
    {
        _commonParameters = commonParameters;
        _decoyType = decoyType;
        _generateTargets = generateTargets;
        _localizableMods = localizableMods ?? [];
        _targetContaminantAmbiguity = targetContaminantAmbiguity;
        _dbFilePath = dbFilePath;
        _storageLayout = storageLayout;
    }

    public TransientCacheLookupResult TryLookup(bool useCache)
    {
        if (!useCache || string.IsNullOrWhiteSpace(_dbFilePath) || !File.Exists(_dbFilePath))
        {
            return TransientCacheLookupResult.Disabled();
        }

        var settingsDescriptor = TransientCacheSettingsDescriptor.Create(
            _commonParameters,
            _decoyType,
            _generateTargets,
            _localizableMods,
            _targetContaminantAmbiguity);

        string databaseContentHash = TransientCacheHashing.ComputeDatabaseContentHash(_dbFilePath);
        var cacheKey = new TransientCacheKey(databaseContentHash, settingsDescriptor.CacheSettingsId);

        _storageLayout.EnsureDirectoriesExist();
        var manifestStore = new TransientCacheManifestStore(_storageLayout.ManifestPath);
        manifestStore.Initialize();

        var context = new TransientCacheContext(
            _dbFilePath,
            cacheKey,
            settingsDescriptor.CanonicalSettingsPayload,
            manifestStore,
            _storageLayout);

        var publishedEntry = manifestStore.TryGetPublishedCacheEntry(cacheKey);
        if (publishedEntry is null)
        {
            return TransientCacheLookupResult.Miss(context);
        }

        var resolvedShards = manifestStore.GetResolvedEntryShardReferences(cacheKey);
        var resolvedSequences = manifestStore.GetResolvedEntrySequenceReferences(cacheKey);
        return TransientCacheLookupResult.Hit(context, publishedEntry, resolvedShards, resolvedSequences);
    }
}
