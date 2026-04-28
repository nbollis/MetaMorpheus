using Chemistry;
using EngineLayer.DatabaseLoading;
using EngineLayer.ParallelSearch.PersistentCache;
using EngineLayer.ParallelSearch.PersistentCache.Manifest;
using EngineLayer.ParallelSearch.PersistentCache.Payloads;
using Omics;
using Omics.BioPolymer;
using Omics.Digestion;
using Omics.Fragmentation;
using Omics.Modifications;
using Proteomics;
using Proteomics.ProteolyticDigestion;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UsefulProteomicsDatabases;

namespace EngineLayer.ParallelSearch;

public class TransientDatabaseLoadingEngine : DatabaseLoadingEngine
{
    private readonly string _dbFilePath;
    private readonly bool _useCache;
    private readonly TransientCacheStorageLayout _storageLayout;

    public TransientCacheTelemetry Telemetry { get; } = new();

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
    }

    protected override MetaMorpheusEngineResults RunSpecific()
    {
        if (!_useCache || string.IsNullOrWhiteSpace(_dbFilePath) || !File.Exists(_dbFilePath))
        {
            Telemetry.RecordFallback();
            return base.RunSpecific();
        }

        var settingsDescriptor = TransientCacheSettingsDescriptor.Create(
            CommonParameters,
            DecoyType,
            GenerateTargets,
            LocalizableMods,
            TcAmbiguity);

        string databaseContentHash = TransientCacheHashing.ComputeDatabaseContentHash(_dbFilePath);
        var cacheKey = new TransientCacheKey(databaseContentHash, settingsDescriptor.CacheSettingsId);

        _storageLayout.EnsureDirectoriesExist();
        var manifestStore = new TransientCacheManifestStore(_storageLayout.ManifestPath);
        manifestStore.Initialize();

        var publishedEntry = manifestStore.TryGetPublishedCacheEntry(cacheKey);
        if (publishedEntry is not null)
        {
            Telemetry.StartHydrate();
            try
            {
                var hydrated = TryHydrateFromCache(cacheKey, manifestStore, publishedEntry);
                if (hydrated is not null)
                {
                    Telemetry.RecordHit();
                    Telemetry.StopHydrate();
                    Status(TransientCacheMessages.FormatLookupMessage(TransientCacheLookupOutcome.Hit, _dbFilePath));
                    return new DatabaseLoadingEngineResults(this, DbForTask, hydrated, 0, 0, 0);
                }
            }
            catch (Exception ex)
            {
                Telemetry.RecordCorrupt();
                Telemetry.StopHydrate();
                Warn(TransientCacheMessages.FormatLookupMessage(TransientCacheLookupOutcome.Corrupt, _dbFilePath, ex.Message));
            }
        }
        else
        {
            Status(TransientCacheMessages.FormatLookupMessage(TransientCacheLookupOutcome.Miss, _dbFilePath));
        }

        Telemetry.StartFallback();
        var baseResults = base.RunSpecific() as DatabaseLoadingEngineResults;
        Telemetry.StopFallback();

        if (baseResults is null)
        {
            return baseResults;
        }

        Telemetry.RecordMiss();
        Telemetry.StartPublish();
        try
        {
            PublishCacheEntry(cacheKey, manifestStore, baseResults.BioPolymers, settingsDescriptor.CanonicalSettingsPayload);
        }
        catch (Exception ex)
        {
            Warn(TransientCacheMessages.FormatPublishMessage(TransientCachePublishState.Failed, _dbFilePath, ex.Message));
        }
        Telemetry.StopPublish();

        Telemetry.Freeze();
        return baseResults;
    }

    private List<IBioPolymer>? TryHydrateFromCache(
        TransientCacheKey cacheKey,
        TransientCacheManifestStore manifestStore,
        TransientCacheManifestEntry entry)
    {
        var resolvedShards = manifestStore.GetResolvedEntryShardReferences(cacheKey);
        var resolvedSequences = manifestStore.GetResolvedEntrySequenceReferences(cacheKey);
        var occurrenceShard = resolvedShards.FirstOrDefault(s => s.PayloadKind == TransientCachePayloadKind.Occurrence);
        var fragmentShard = resolvedShards.FirstOrDefault(s => s.PayloadKind == TransientCachePayloadKind.Fragment);

        if (occurrenceShard.Sha256 is null)
        {
            return null;
        }

        var reader = new TransientCachePayloadSegmentReader();
        byte[] occurrenceBytes = reader.ReadShard(
            _storageLayout.GetSegmentPath(occurrenceShard.RelativePath),
            occurrenceShard);

        var (proteinOccurrences, fullSequences) = TransientCachePayloadSerializer.DeserializeOccurrencePayload(occurrenceBytes);
        Func<int, IReadOnlyList<Product>> fragmentResolver = fragmentShard is not null && fragmentShard.Sha256 is not null
            ? CreateLegacyFragmentResolver(fragmentShard, fullSequences.Count)
            : CreateSharedFragmentResolver(fullSequences, resolvedSequences, manifestStore);

        var modLookup = GlobalVariables.AllModsKnownDictionary ?? new Dictionary<string, Modification>();

        var proteins = (base.RunSpecific() as DatabaseLoadingEngineResults)?.BioPolymers;
        if (proteins is null || proteins.Count == 0)
        {
            return null;
        }

        var wrappedProteins = new List<IBioPolymer>(proteins.Count);
        for (int proteinIndex = 0; proteinIndex < proteins.Count; proteinIndex++)
        {
            int currentProteinIndex = proteinIndex;
            if (!proteinOccurrences.TryGetValue(currentProteinIndex, out var occurrences))
            {
                occurrences = new List<(int, int, int, int, string)>();
            }

            int peptideCount = occurrences.Count;
            var transientBioPolymer = new TransientBioPolymer(
                proteins[currentProteinIndex],
                peptideCount,
                digestionProductFactory: parent =>
                {
                    var peptides = new List<IBioPolymerWithSetMods>(occurrences.Count);
                    foreach (var occ in occurrences)
                    {
                        string fullSequence = fullSequences[occ.localSequenceOrdinal];
                        var parsedPeptide = new PeptideWithSetModifications(
                            fullSequence,
                            modLookup,
                            p: proteins[currentProteinIndex] as Protein,
                            oneBasedStartResidueInProtein: occ.oneBasedStartResidue,
                            oneBasedEndResidueInProtein: occ.oneBasedEndResidue);

                        var peptide = new PeptideWithSetModifications(
                            proteins[currentProteinIndex] as Protein,
                            CommonParameters.DigestionParams,
                            occ.oneBasedStartResidue,
                            occ.oneBasedEndResidue,
                            CleavageSpecificity.Full,
                            occ.peptideDescription,
                            occ.missedCleavages,
                            parsedPeptide.AllModsOneIsNterminus,
                            parsedPeptide.NumFixedMods);

                        var wrapped = new TransientBioPolymerWithSetMods(
                            peptide,
                            parent,
                            fragmentFactory: () => fragmentResolver(occ.localSequenceOrdinal));
                        peptides.Add(wrapped);
                    }
                    return peptides;
                });

            wrappedProteins.Add(transientBioPolymer);
        }

        return wrappedProteins;
    }

    private void PublishCacheEntry(
        TransientCacheKey cacheKey,
        TransientCacheManifestStore manifestStore,
        List<IBioPolymer> proteins,
        string canonicalSettingsPayload)
    {
        manifestStore.UpsertSourceDatabase(cacheKey.DatabaseContentHash, _dbFilePath);
        manifestStore.UpsertCacheSettings(cacheKey.CacheSettingsId, canonicalSettingsPayload);

        var payload = BuildDbLocalOccurrencePayload(proteins);
        var segmentManager = new TransientCacheSegmentManager(manifestStore, _storageLayout);
        var occurrencePublish = PublishOccurrenceShard(manifestStore, segmentManager, payload.OccurrenceBytes);
        var sharedFragments = PublishSharedFragmentShards(cacheKey, manifestStore, segmentManager, payload.FullSequences, payload.LocalSequenceFragments);

        var entry = new TransientCacheManifestEntry(cacheKey, TransientCachePublishState.Published)
        {
            ProteinCount = proteins.Count,
            PeptideCount = payload.FullSequences.Count,
            EntryChecksum = TransientCacheHashing.ComputeSha256Hex(
                new[] { payload.OccurrenceBytes }
                    .Concat(sharedFragments.FragmentChecksumBytes)
                    .SelectMany(bytes => bytes)
                    .ToArray())
        };
        manifestStore.UpsertCacheEntry(entry);
        manifestStore.ReplaceEntrySequences(cacheKey, sharedFragments.EntrySequenceReferences);

        manifestStore.ReplaceEntryShards(cacheKey, new[]
        {
            new TransientCacheEntryShardReference(occurrencePublish.Shard.ShardId, TransientCachePayloadKind.Occurrence, 0),
        });

        Telemetry.RecordOccurrencePayloadBytesWritten(occurrencePublish.BytesWritten);
        Telemetry.RecordFragmentPayloadBytesWritten(sharedFragments.BytesWritten);
        Telemetry.RecordFragmentShardReuse(sharedFragments.ReusedShardCount);
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

    private Func<int, IReadOnlyList<Product>> CreateLegacyFragmentResolver(
        TransientCacheResolvedShardReference fragmentShard,
        int expectedSequenceCount)
    {
        var fragmentTable = new Lazy<IReadOnlyList<List<Product>>>(() =>
        {
            var reader = new TransientCachePayloadSegmentReader();
            byte[] fragmentBytes = reader.ReadShard(
                _storageLayout.GetSegmentPath(fragmentShard.RelativePath),
                fragmentShard);

            var legacyFragmentTable = TransientCachePayloadSerializer.DeserializeFragmentPayload(fragmentBytes);
            if (legacyFragmentTable.Count != expectedSequenceCount)
            {
                throw new InvalidDataException($"Occurrence payload has {expectedSequenceCount} local sequences but legacy fragment payload has {legacyFragmentTable.Count} fragment payloads.");
            }

            return legacyFragmentTable;
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        return localOrdinal =>
        {
            var resolvedFragments = fragmentTable.Value;
            if (localOrdinal < 0 || localOrdinal >= resolvedFragments.Count)
            {
                throw new InvalidDataException($"Legacy fragment payload does not contain local ordinal {localOrdinal}.");
            }

            return resolvedFragments[localOrdinal];
        };
    }

    private Func<int, IReadOnlyList<Product>> CreateSharedFragmentResolver(
        List<string> fullSequences,
        IReadOnlyList<TransientCacheResolvedSequenceReference> resolvedSequences,
        TransientCacheManifestStore manifestStore)
    {
        if (resolvedSequences.Count != fullSequences.Count)
        {
            throw new InvalidDataException($"Occurrence payload has {fullSequences.Count} local sequences but manifest has {resolvedSequences.Count} sequence mappings.");
        }

        var fragmentShardIdByLocalOrdinal = new Dictionary<int, long>(resolvedSequences.Count);

        foreach (var resolvedSequence in resolvedSequences)
        {
            if (resolvedSequence.LocalOrdinal < 0 || resolvedSequence.LocalOrdinal >= fullSequences.Count)
            {
                throw new InvalidDataException($"Manifest sequence ordinal {resolvedSequence.LocalOrdinal} is outside the occurrence payload range.");
            }

            if (!string.Equals(fullSequences[resolvedSequence.LocalOrdinal], resolvedSequence.FullSequence, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Occurrence payload full sequence '{fullSequences[resolvedSequence.LocalOrdinal]}' does not match manifest sequence '{resolvedSequence.FullSequence}' at local ordinal {resolvedSequence.LocalOrdinal}.");
            }

            if (resolvedSequence.FragmentShardId is null)
            {
                throw new InvalidDataException($"Shared sequence '{resolvedSequence.FullSequence}' does not have an associated fragment shard.");
            }

            fragmentShardIdByLocalOrdinal[resolvedSequence.LocalOrdinal] = resolvedSequence.FragmentShardId.Value;
        }

        var fragmentsByShardId = new ConcurrentDictionary<long, Lazy<IReadOnlyList<Product>>>();
        return localOrdinal =>
        {
            if (!fragmentShardIdByLocalOrdinal.TryGetValue(localOrdinal, out long fragmentShardId))
            {
                throw new InvalidDataException($"Shared fragment mapping does not contain local ordinal {localOrdinal}.");
            }

            var lazyFragments = fragmentsByShardId.GetOrAdd(fragmentShardId, shardId =>
                new Lazy<IReadOnlyList<Product>>(() => LoadSharedFragmentPayload(shardId, manifestStore), LazyThreadSafetyMode.ExecutionAndPublication));

            return lazyFragments.Value;
        };
    }

    private IReadOnlyList<Product> LoadSharedFragmentPayload(long fragmentShardId, TransientCacheManifestStore manifestStore)
    {
        var resolvedShard = manifestStore.GetResolvedPayloadShard(fragmentShardId)
            ?? throw new InvalidDataException($"Fragment shard '{fragmentShardId}' was not found in the manifest.");

        var reader = new TransientCachePayloadSegmentReader();
        byte[] fragmentBytes = reader.ReadShard(_storageLayout.GetSegmentPath(resolvedShard.RelativePath), resolvedShard);
        return TransientCachePayloadSerializer.DeserializeSingleFragmentPayload(fragmentBytes);
    }

    private FragmentShardPublishResult ResolveOrPublishFragmentShard(
        TransientCacheManifestStore manifestStore,
        TransientCacheSegmentManager segmentManager,
        TransientCacheSharedSequenceRecord sharedSequence,
        byte[] fragmentBytes)
    {
        string sha256 = TransientCacheHashing.ComputeSha256Hex(fragmentBytes);
        long logicalLengthBytes = fragmentBytes.LongLength;

        if (sharedSequence.FragmentShardId is long existingFragmentShardId)
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
