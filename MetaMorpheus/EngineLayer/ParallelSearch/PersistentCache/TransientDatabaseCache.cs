using System.Collections.Generic;
using System.IO;
using EngineLayer.ParallelSearch.PersistentCache.Manifest;
using Omics;
using UsefulProteomicsDatabases;

namespace EngineLayer.ParallelSearch.PersistentCache;

public sealed class TransientDatabaseCache
{
    private readonly CommonParameters _commonParameters;
    private readonly DecoyType _decoyType;
    private readonly bool _generateTargets;
    private readonly List<string> _localizableMods;
    private readonly TargetContaminantAmbiguity _targetContaminantAmbiguity;
    private readonly TransientCacheStorageLayout _storageLayout;
    private readonly TransientCacheManifestStore _manifestStore;
    private readonly TransientCacheHydrator _hydrator;
    private readonly TransientCachePublisher _publisher;

    public TransientCacheTelemetry Telemetry { get; } = new();

    public TransientDatabaseCache(
        CommonParameters commonParameters,
        DecoyType decoyType,
        bool generateTargets,
        List<string>? localizableMods,
        TargetContaminantAmbiguity targetContaminantAmbiguity)
        : this(commonParameters, decoyType, generateTargets, localizableMods, targetContaminantAmbiguity, TransientCacheStorageLayout.CreateDefault())
    {
    }

    internal TransientDatabaseCache(
        CommonParameters commonParameters,
        DecoyType decoyType,
        bool generateTargets,
        List<string>? localizableMods,
        TargetContaminantAmbiguity targetContaminantAmbiguity,
        TransientCacheStorageLayout storageLayout)
    {
        _commonParameters = commonParameters;
        _decoyType = decoyType;
        _generateTargets = generateTargets;
        _localizableMods = localizableMods ?? [];
        _targetContaminantAmbiguity = targetContaminantAmbiguity;
        _storageLayout = storageLayout;
        _storageLayout.EnsureDirectoriesExist();
        _manifestStore = new TransientCacheManifestStore(_storageLayout.ManifestPath);
        _manifestStore.Initialize();
        _hydrator = new TransientCacheHydrator(commonParameters, storageLayout, Telemetry);
        _publisher = new TransientCachePublisher(commonParameters, storageLayout, Telemetry);
    }

    internal TransientCacheProbeResult Resolve(string dbFilePath)
    {
        if (string.IsNullOrWhiteSpace(dbFilePath) || !File.Exists(dbFilePath))
        {
            return TransientCacheProbeResult.Disabled();
        }

        var settingsDescriptor = TransientCacheSettingsDescriptor.Create(
            _commonParameters,
            _decoyType,
            _generateTargets,
            _localizableMods,
            _targetContaminantAmbiguity);

        string databaseContentHash = TransientCacheHashing.ComputeDatabaseContentHash(dbFilePath);
        var cacheKey = new TransientCacheKey(databaseContentHash, settingsDescriptor.CacheSettingsId);

        var handle = new TransientCacheHandle(
            dbFilePath,
            cacheKey,
            settingsDescriptor.CanonicalSettingsPayload,
            _manifestStore,
            _storageLayout);

        var publishedEntry = _manifestStore.TryGetPublishedCacheEntry(cacheKey);
        if (publishedEntry is null)
        {
            return TransientCacheProbeResult.Miss(handle);
        }

        var resolvedShards = _manifestStore.GetResolvedEntryShardReferences(cacheKey);
        var resolvedSequences = _manifestStore.GetResolvedEntrySequenceReferences(cacheKey);
        return TransientCacheProbeResult.Hit(handle, publishedEntry, resolvedShards, resolvedSequences);
    }

    internal TransientCacheHydrationResult TryHydrate(
        TransientCacheProbeResult lookupResult,
        IReadOnlyList<IBioPolymer> rawProteins)
    {
        return _hydrator.TryHydrate(lookupResult, rawProteins);
    }

    internal TransientCachePublishResult TryPublish(TransientCacheHandle cacheHandle, IReadOnlyList<IBioPolymer> rawProteins)
    {
        return _publisher.TryPublish(cacheHandle, rawProteins);
    }
}
