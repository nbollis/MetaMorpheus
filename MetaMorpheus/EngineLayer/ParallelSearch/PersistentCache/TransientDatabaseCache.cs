using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System;
using EngineLayer.ParallelSearch.PersistentCache.Manifest;
using Omics;
using UsefulProteomicsDatabases;

namespace EngineLayer.ParallelSearch.PersistentCache;

public sealed class TransientDatabaseCache : IDisposable
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
    private readonly ConcurrentDictionary<string, TransientCacheHandle> _handlesByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _publishLock = new();
    private bool _disposed;

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

    public void Prewarm(IEnumerable<string> dbFilePaths)
    {
        ThrowIfDisposed();

        foreach (var dbFilePath in dbFilePaths)
        {
            if (string.IsNullOrWhiteSpace(dbFilePath))
            {
                continue;
            }

            _handlesByPath.GetOrAdd(dbFilePath, CreateHandle);
        }
    }

    internal TransientCacheHandle Resolve(string dbFilePath)
    {
        ThrowIfDisposed();
        return _handlesByPath.GetOrAdd(dbFilePath, CreateHandle);
    }

    internal TransientCacheHydrationResult TryHydrate(
        TransientCacheHandle cacheHandle,
        IReadOnlyList<IBioPolymer> rawProteins)
    {
        ThrowIfDisposed();
        return _hydrator.TryHydrate(cacheHandle, rawProteins);
    }

    internal TransientCachePublishResult TryPublish(TransientCacheHandle cacheHandle, IReadOnlyList<IBioPolymer> rawProteins)
    {
        lock (_publishLock)
        {
            ThrowIfDisposed();

            var publishResult = _publisher.TryPublish(cacheHandle, rawProteins);
            if (publishResult.IsSuccess)
            {
                _handlesByPath[cacheHandle.DatabasePath] = CreateHandle(cacheHandle.DatabasePath);
            }

            return publishResult;
        }
    }

    public TransientCacheGrowthSummary GetGrowthSummary()
    {
        ThrowIfDisposed();
        return _manifestStore.GetCacheGrowthSummary();
    }

    public void Dispose()
    {
        lock (_publishLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _handlesByPath.Clear();
        }

        GC.SuppressFinalize(this);
    }

    private TransientCacheHandle CreateHandle(string dbFilePath)
    {
        if (string.IsNullOrWhiteSpace(dbFilePath) || !File.Exists(dbFilePath))
        {
            return TransientCacheHandle.Disabled(dbFilePath);
        }

        var settingsDescriptor = TransientCacheSettingsDescriptor.Create(
            _commonParameters,
            _decoyType,
            _generateTargets,
            _localizableMods,
            _targetContaminantAmbiguity);

        string databaseContentHash = TransientCacheHashing.ComputeDatabaseContentHash(dbFilePath);
        var cacheKey = new TransientCacheKey(databaseContentHash, settingsDescriptor.CacheSettingsId);

        var publishedEntry = _manifestStore.TryGetPublishedCacheEntry(cacheKey);
        if (publishedEntry is null)
        {
            return TransientCacheHandle.Miss(
                dbFilePath,
                cacheKey,
                settingsDescriptor.CanonicalSettingsPayload,
                _manifestStore,
                _storageLayout);
        }

        var resolvedShards = _manifestStore.GetResolvedEntryShardReferences(cacheKey);
        var resolvedSequences = _manifestStore.GetResolvedEntrySequenceReferences(cacheKey);
        return TransientCacheHandle.Hit(
            dbFilePath,
            cacheKey,
            settingsDescriptor.CanonicalSettingsPayload,
            _manifestStore,
            _storageLayout,
            publishedEntry,
            resolvedShards,
            resolvedSequences);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
