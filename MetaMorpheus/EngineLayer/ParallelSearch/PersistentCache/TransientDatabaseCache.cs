using System.Collections.Generic;
using System.IO;
using EngineLayer.ParallelSearch.PersistentCache.Manifest;
using Omics;
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
    private readonly TransientCacheHydrator _hydrator;
    private readonly TransientCachePublisher _publisher;

    public TransientCacheTelemetry Telemetry { get; } = new();

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
        _hydrator = new TransientCacheHydrator(commonParameters, storageLayout, Telemetry);
        _publisher = new TransientCachePublisher(commonParameters, dbFilePath, storageLayout, Telemetry);
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

    public TransientCacheHydrationResult TryHydrate(
        TransientCacheLookupResult lookupResult,
        IReadOnlyList<IBioPolymer> rawProteins)
    {
        return _hydrator.TryHydrate(lookupResult, rawProteins);
    }

    public TransientCachePublishResult TryPublish(TransientCacheContext cacheContext, IReadOnlyList<IBioPolymer> rawProteins)
    {
        return _publisher.TryPublish(cacheContext, rawProteins);
    }
}
