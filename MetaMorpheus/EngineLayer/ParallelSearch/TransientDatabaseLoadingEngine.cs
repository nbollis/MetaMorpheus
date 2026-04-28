using EngineLayer.DatabaseLoading;
using EngineLayer.ParallelSearch.PersistentCache;
using EngineLayer.ParallelSearch.PersistentCache.Manifest;
using EngineLayer.ParallelSearch.PersistentCache.Payloads;
using Omics;
using Omics.BioPolymer;
using Omics.Digestion;
using Omics.Fragmentation;
using Omics.Modifications;
using Proteomics.ProteolyticDigestion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        try
        {
            if (lookupResult.Context is not null)
            {
                PublishCacheEntry(lookupResult.Context, baseResults.BioPolymers);
            }
        }
        catch (Exception ex)
        {
            Warn(TransientCacheMessages.FormatPublishMessage(TransientCachePublishState.Failed, _dbFilePath, ex.Message));
        }
        Telemetry.StopPublish();

        Telemetry.Freeze();
        return baseResults;
    }

    private void PublishCacheEntry(TransientCacheContext cacheContext, List<IBioPolymer> proteins)
    {
        cacheContext.ManifestStore.UpsertSourceDatabase(cacheContext.CacheKey.DatabaseContentHash, _dbFilePath);
        cacheContext.ManifestStore.UpsertCacheSettings(cacheContext.CacheKey.CacheSettingsId, cacheContext.CanonicalSettingsPayload);

        var payload = BuildDbLocalOccurrencePayload(proteins);
        var segmentManager = new TransientCacheSegmentManager(cacheContext.ManifestStore, _storageLayout);
        var occurrencePublish = PublishOccurrenceShard(cacheContext.ManifestStore, segmentManager, payload.OccurrenceBytes);
        var sharedFragments = PublishSharedFragmentShards(cacheContext.CacheKey, cacheContext.ManifestStore, segmentManager, payload.FullSequences, payload.LocalSequenceFragments);

        var entry = new TransientCacheManifestEntry(cacheContext.CacheKey, TransientCachePublishState.Published)
        {
            ProteinCount = proteins.Count,
            PeptideCount = payload.FullSequences.Count,
            EntryChecksum = TransientCacheHashing.ComputeSha256Hex(
                new[] { payload.OccurrenceBytes }
                    .Concat(sharedFragments.FragmentChecksumBytes)
                    .SelectMany(bytes => bytes)
                    .ToArray())
        };
        cacheContext.ManifestStore.UpsertCacheEntry(entry);
        cacheContext.ManifestStore.ReplaceEntrySequences(cacheContext.CacheKey, sharedFragments.EntrySequenceReferences);

        cacheContext.ManifestStore.ReplaceEntryShards(cacheContext.CacheKey, new[]
        {
            new TransientCacheEntryShardReference(occurrencePublish.Shard.ShardId, TransientCachePayloadKind.Occurrence, 0),
        });

        Telemetry.RecordOccurrencePayloadBytesWritten(occurrencePublish.BytesWritten);
        Telemetry.RecordFragmentPayloadBytesWritten(sharedFragments.BytesWritten);
        Telemetry.RecordFragmentShardReuse(sharedFragments.ReusedShardCount);
        Telemetry.RecordPublishedSharedSequences(sharedFragments.EntrySequenceReferences.Count);
    }

    private static string GetLocalSequenceKey(IBioPolymerWithSetMods peptide)
    {
        return peptide.FullSequence;
    }

    private static string ComputeSharedSequenceHash(string fullSequence)
    {
        return TransientCacheHashing.ComputeSha256Hex(Encoding.UTF8.GetBytes(fullSequence));
    }

    private DbLocalOccurrencePayload BuildDbLocalOccurrencePayload(List<IBioPolymer> proteins)
    {
        var digestionParams = CommonParameters.DigestionParams;
        var fixedMods = ResolveSelectedMods(CommonParameters.ListOfModsFixed);
        var variableMods = ResolveSelectedMods(CommonParameters.ListOfModsVariable);

        var localSequenceOrdinalByFullSequence = new Dictionary<string, int>();
        var fullSequences = new List<string>();
        var proteinOccurrences = new Dictionary<int, List<(int localSequenceOrdinal, int oneBasedStartResidue, int oneBasedEndResidue, int missedCleavages, string peptideDescription)>>();
        var localSequenceFragments = new List<List<Product>>();

        for (int proteinIndex = 0; proteinIndex < proteins.Count; proteinIndex++)
        {
            var protein = proteins[proteinIndex];
            var digested = protein.Digest(digestionParams, fixedMods, variableMods).ToList();
            var occurrences = new List<(int, int, int, int, string)>();

            foreach (var peptide in digested)
            {
                string key = GetLocalSequenceKey(peptide);
                if (!localSequenceOrdinalByFullSequence.TryGetValue(key, out int localSequenceOrdinal))
                {
                    localSequenceOrdinal = fullSequences.Count;
                    localSequenceOrdinalByFullSequence[key] = localSequenceOrdinal;
                    fullSequences.Add(peptide.FullSequence);

                    var fragments = new List<Product>();
                    peptide.Fragment(CommonParameters.DissociationType, CommonParameters.DigestionParams.FragmentationTerminus, fragments);
                    localSequenceFragments.Add(fragments);
                }

                int start = 0;
                int end = 0;
                int missed = 0;
                string description = string.Empty;
                if (peptide is PeptideWithSetModifications setModPeptide)
                {
                    start = setModPeptide.OneBasedStartResidueInProtein;
                    end = setModPeptide.OneBasedEndResidueInProtein;
                    missed = setModPeptide.MissedCleavages;
                    description = setModPeptide.PeptideDescription ?? string.Empty;
                }

                occurrences.Add((localSequenceOrdinal, start, end, missed, description));
            }

            proteinOccurrences[proteinIndex] = occurrences;
        }

        byte[] occurrenceBytes = TransientCachePayloadSerializer.SerializeOccurrencePayload(proteins, proteinOccurrences, fullSequences);
        return new DbLocalOccurrencePayload(occurrenceBytes, fullSequences, proteinOccurrences, localSequenceFragments);
    }

    private List<Modification> ResolveSelectedMods(IEnumerable<(string, string)> selectedMods)
    {
        return GlobalVariables.AllModsKnown.Where(m =>
            selectedMods.Any(selected => selected.Item1 == m.ModificationType && selected.Item2 == m.IdWithMotif)).ToList();
    }

    private OccurrenceShardPublishResult PublishOccurrenceShard(
        TransientCacheManifestStore manifestStore,
        TransientCacheSegmentManager segmentManager,
        byte[] occurrenceBytes)
    {
        var appendResult = segmentManager.AppendPayloadShard(TransientCachePayloadKind.Occurrence, occurrenceBytes);
        var occurrenceShard = manifestStore.InsertPayloadShard(
            appendResult.Segment.SegmentId,
            TransientCachePayloadKind.Occurrence,
            appendResult.WriteResult.OffsetBytes,
            appendResult.WriteResult.StoredLengthBytes,
            appendResult.WriteResult.LogicalLengthBytes,
            appendResult.WriteResult.Sha256,
            referenceCount: 1);

        return new OccurrenceShardPublishResult(occurrenceShard, appendResult.WriteResult.LogicalLengthBytes);
    }

    private SharedFragmentPublishResult PublishSharedFragmentShards(
        TransientCacheKey cacheKey,
        TransientCacheManifestStore manifestStore,
        TransientCacheSegmentManager segmentManager,
        IReadOnlyList<string> fullSequences,
        IReadOnlyList<List<Product>> localSequenceFragments)
    {
        var entrySequenceReferences = new List<TransientCacheEntrySequenceReference>(fullSequences.Count);
        var fragmentChecksumBytes = new List<byte[]>(fullSequences.Count);
        long fragmentBytesWritten = 0;
        int reusedShardCount = 0;

        for (int localOrdinal = 0; localOrdinal < fullSequences.Count; localOrdinal++)
        {
            string fullSequence = fullSequences[localOrdinal];
            string sequenceHash = ComputeSharedSequenceHash(fullSequence);
            var sharedSequence = manifestStore.UpsertSharedSequence(cacheKey.CacheSettingsId, sequenceHash, fullSequence);

            byte[] fragmentBytes = TransientCachePayloadSerializer.SerializeSingleFragmentPayload(localSequenceFragments[localOrdinal]);
            fragmentChecksumBytes.Add(fragmentBytes);

            var fragmentPublish = ResolveOrPublishFragmentShard(manifestStore, segmentManager, sharedSequence, fragmentBytes);
            entrySequenceReferences.Add(new TransientCacheEntrySequenceReference(sharedSequence.SequenceId, localOrdinal));
            fragmentBytesWritten += fragmentPublish.BytesWritten;
            if (fragmentPublish.WasReused)
            {
                reusedShardCount++;
            }
        }

        return new SharedFragmentPublishResult(entrySequenceReferences, fragmentChecksumBytes, fragmentBytesWritten, reusedShardCount);
    }

    private FragmentShardPublishResult ResolveOrPublishFragmentShard(
        TransientCacheManifestStore manifestStore,
        TransientCacheSegmentManager segmentManager,
        TransientCacheSharedSequenceRecord sharedSequence,
        byte[] fragmentBytes)
    {
        string sha256 = TransientCacheHashing.ComputeSha256Hex(fragmentBytes);
        long logicalLengthBytes = fragmentBytes.LongLength;

        if (!sharedSequence.IsQuarantined && sharedSequence.FragmentShardId is long existingFragmentShardId)
        {
            var existingFragmentShard = manifestStore.GetPayloadShard(existingFragmentShardId);
            if (existingFragmentShard is not null &&
                existingFragmentShard.Value.LogicalLengthBytes == logicalLengthBytes &&
                string.Equals(existingFragmentShard.Value.Sha256, sha256, StringComparison.Ordinal))
            {
                manifestStore.AdjustPayloadShardReferenceCount(existingFragmentShardId, 1);
                return new FragmentShardPublishResult(existingFragmentShardId, true, 0);
            }
        }

        var reusableShard = manifestStore.TryGetPayloadShardByFingerprint(TransientCachePayloadKind.Fragment, sha256, logicalLengthBytes);
        if (reusableShard is not null)
        {
            manifestStore.AdjustPayloadShardReferenceCount(reusableShard.Value.ShardId, 1);
            if (sharedSequence.FragmentShardId != reusableShard.Value.ShardId)
            {
                manifestStore.UpdateSharedSequenceFragmentShard(sharedSequence.SequenceId, reusableShard.Value.ShardId);
            }
            return new FragmentShardPublishResult(reusableShard.Value.ShardId, true, 0);
        }

        var appendResult = segmentManager.AppendPayloadShard(TransientCachePayloadKind.Fragment, fragmentBytes);
        var newShard = manifestStore.InsertPayloadShard(
            appendResult.Segment.SegmentId,
            TransientCachePayloadKind.Fragment,
            appendResult.WriteResult.OffsetBytes,
            appendResult.WriteResult.StoredLengthBytes,
            appendResult.WriteResult.LogicalLengthBytes,
            appendResult.WriteResult.Sha256,
            referenceCount: 1);

        manifestStore.UpdateSharedSequenceFragmentShard(sharedSequence.SequenceId, newShard.ShardId);
        return new FragmentShardPublishResult(newShard.ShardId, false, appendResult.WriteResult.LogicalLengthBytes);
    }

    private readonly record struct DbLocalOccurrencePayload(
        byte[] OccurrenceBytes,
        List<string> FullSequences,
        Dictionary<int, List<(int localSequenceOrdinal, int oneBasedStartResidue, int oneBasedEndResidue, int missedCleavages, string peptideDescription)>> ProteinOccurrences,
        List<List<Product>> LocalSequenceFragments);

    private readonly record struct OccurrenceShardPublishResult(
        TransientCachePayloadShardRecord Shard,
        long BytesWritten);

    private readonly record struct SharedFragmentPublishResult(
        IReadOnlyList<TransientCacheEntrySequenceReference> EntrySequenceReferences,
        IReadOnlyList<byte[]> FragmentChecksumBytes,
        long BytesWritten,
        int ReusedShardCount);

    private readonly record struct FragmentShardPublishResult(
        long ShardId,
        bool WasReused,
        long BytesWritten);
}
