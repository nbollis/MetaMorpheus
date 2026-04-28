using EngineLayer.DatabaseLoading;
using EngineLayer.ParallelSearch.PersistentCache;
using EngineLayer.ParallelSearch.PersistentCache.Manifest;
using EngineLayer.ParallelSearch.PersistentCache.Payloads;
using Omics;
using Omics.BioPolymer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UsefulProteomicsDatabases;

namespace EngineLayer.ParallelSearch;

public class TransientDatabaseLoadingEngine : DatabaseLoadingEngine
{
    private readonly string _dbFilePath;
    private readonly bool _useCache;
    private readonly TransientCacheStorageLayout _storageLayout;
    private readonly TransientDatabaseCache _cache;

    public TransientCacheTelemetry Telemetry => _cache.Telemetry;

    public TransientDatabaseLoadingEngine(
        CommonParameters commonParameters,
        List<(string FileName, CommonParameters Parameters)> fileSpecificParameters,
        List<string> nestedIds,
        List<DbForTask> dbFilenameList,
        string taskId,
        DecoyType decoyType,
        bool generateTargets = true,
        List<string> localizableMods = null,
        TargetContaminantAmbiguity tcAmbiguity = TargetContaminantAmbiguity.RemoveContaminant,
        bool writeTargetDecoyFasta = false,
        string outputFolder = null,
        bool useCache = false)
        : this(commonParameters, fileSpecificParameters, nestedIds, dbFilenameList, taskId, decoyType, generateTargets, localizableMods, tcAmbiguity, writeTargetDecoyFasta, outputFolder, useCache, null)
    {
    }

    public TransientDatabaseLoadingEngine(
        CommonParameters commonParameters,
        List<(string FileName, CommonParameters Parameters)> fileSpecificParameters,
        List<string> nestedIds,
        List<DbForTask> dbFilenameList,
        string taskId,
        DecoyType decoyType,
        bool generateTargets,
        List<string> localizableMods,
        TargetContaminantAmbiguity tcAmbiguity,
        bool writeTargetDecoyFasta,
        string outputFolder,
        bool useCache,
        TransientCacheStorageLayout? storageLayout)
        : base(commonParameters, fileSpecificParameters, nestedIds, dbFilenameList, taskId, decoyType, generateTargets, localizableMods, tcAmbiguity, writeTargetDecoyFasta, outputFolder)
    {
        _useCache = useCache;
        _dbFilePath = dbFilenameList.FirstOrDefault()?.FilePath ?? string.Empty;
        _storageLayout = storageLayout ?? TransientCacheStorageLayout.CreateDefault();
        _cache = new TransientDatabaseCache(
            commonParameters,
            decoyType,
            generateTargets,
            localizableMods,
            tcAmbiguity,
            _dbFilePath,
            _storageLayout);
    }

    protected override MetaMorpheusEngineResults RunSpecific()
    {
        TransientCacheLookupResult lookupResult = _cache.TryLookup(_useCache);
        if (lookupResult.Outcome == TransientCacheLookupOutcome.Disabled)
        {
            Telemetry.RecordFallback();
            return base.RunSpecific();
        }

        DatabaseLoadingEngineResults? baseResults = null;
        if (lookupResult.HasReusableEntry)
        {
            Telemetry.StartHydrate();
            baseResults = base.RunSpecific() as DatabaseLoadingEngineResults;
            if (baseResults is null)
            {
                Telemetry.StopHydrate();
                return baseResults;
            }

            var hydrationResult = _cache.TryHydrate(lookupResult, baseResults.BioPolymers);
            if (hydrationResult.IsSuccess)
            {
                Telemetry.RecordHit();
                Telemetry.StopHydrate();
                Status(TransientCacheMessages.FormatLookupMessage(TransientCacheLookupOutcome.Hit, _dbFilePath));
                return new DatabaseLoadingEngineResults(this, DbForTask, hydrationResult.HydratedBioPolymers!.ToList(), 0, 0, 0);
            }

            if (TransientCacheMessages.ShouldWarn(hydrationResult.Outcome))
            {
                Warn(TransientCacheMessages.FormatLookupMessage(hydrationResult.Outcome, _dbFilePath, hydrationResult.Detail));
            }

            Telemetry.RecordCorrupt();
            Telemetry.StopHydrate();
        }
        else if (lookupResult.Outcome == TransientCacheLookupOutcome.Miss)
        {
            Status(TransientCacheMessages.FormatLookupMessage(TransientCacheLookupOutcome.Miss, _dbFilePath));
        }
        else
        {
            if (TransientCacheMessages.ShouldWarn(lookupResult.Outcome))
            {
                Warn(TransientCacheMessages.FormatLookupMessage(lookupResult.Outcome, _dbFilePath, lookupResult.Detail));
            }
        }

        if (baseResults is null)
        {
            Telemetry.RecordFallback();
            Telemetry.StartFallback();
            baseResults = base.RunSpecific() as DatabaseLoadingEngineResults;
            Telemetry.StopFallback();
        }
        else
        {
            Telemetry.RecordFallback();
        }

        if (baseResults is null)
        {
            return baseResults;
        }

        Telemetry.RecordMiss();
        Telemetry.StartPublish();
        if (lookupResult.Context is not null)
        {
            var publishResult = _cache.TryPublish(lookupResult.Context, baseResults.BioPolymers);
            if (!publishResult.IsSuccess)
            {
                Warn(TransientCacheMessages.FormatPublishMessage(publishResult.PublishState, _dbFilePath, publishResult.Detail));
            }
        }
        Telemetry.StopPublish();

        Telemetry.Freeze();
        return baseResults;
    }
}
