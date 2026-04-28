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
        var fragmentTable = fragmentShard is not null && fragmentShard.Sha256 is not null
            ? LoadLegacyFragmentTable(fragmentShard, reader)
            : LoadSharedFragmentTable(fullSequences, resolvedSequences, manifestStore, reader);

        if (fullSequences.Count != fragmentTable.Count)
        {
            throw new InvalidDataException($"Occurrence payload has {fullSequences.Count} local sequences but fragment lookup resolved {fragmentTable.Count} fragment payloads.");
        }

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
                            fragmentFactory: () => fragmentTable[occ.localSequenceOrdinal]);
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
        var digestionParams = CommonParameters.DigestionParams;
        var fixedMods = GlobalVariables.AllModsKnown.Where(m =>
            CommonParameters.ListOfModsFixed.Any(f => f.Item1 == m.ModificationType && f.Item2 == m.IdWithMotif)).ToList();
        var variableMods = GlobalVariables.AllModsKnown.Where(m =>
            CommonParameters.ListOfModsVariable.Any(v => v.Item1 == m.ModificationType && v.Item2 == m.IdWithMotif)).ToList();

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

        string occurrenceRelativePath = $"{cacheKey.DatabaseContentHash}_{cacheKey.CacheSettingsId}_occurrence";

        var writer = new TransientCachePayloadSegmentWriter();
        var occurrenceResult = writer.AppendShard(
            _storageLayout.GetSegmentPath(occurrenceRelativePath),
            TransientCachePayloadKind.Occurrence,
            occurrenceBytes);

        var occurrenceSegment = manifestStore.UpsertPayloadSegment(TransientCachePayloadKind.Occurrence, occurrenceRelativePath, occurrenceResult.StoredLengthBytes);

        var occurrenceShard = manifestStore.InsertPayloadShard(
            occurrenceSegment.SegmentId,
            TransientCachePayloadKind.Occurrence,
            occurrenceResult.OffsetBytes,
            occurrenceResult.StoredLengthBytes,
            occurrenceResult.LogicalLengthBytes,
            occurrenceResult.Sha256,
            referenceCount: 1);

        manifestStore.UpsertSourceDatabase(cacheKey.DatabaseContentHash, _dbFilePath);
        manifestStore.UpsertCacheSettings(cacheKey.CacheSettingsId, canonicalSettingsPayload);

        var segmentManager = new TransientCacheSegmentManager(manifestStore, _storageLayout);
        var entrySequenceReferences = new List<TransientCacheEntrySequenceReference>(fullSequences.Count);
        long fragmentBytesWritten = 0;
        var entryChecksumBytes = new List<byte[]>(fullSequences.Count + 1) { occurrenceBytes };

        for (int localOrdinal = 0; localOrdinal < fullSequences.Count; localOrdinal++)
        {
            string fullSequence = fullSequences[localOrdinal];
            string sequenceHash = ComputeSharedSequenceHash(fullSequence);
            var sharedSequence = manifestStore.UpsertSharedSequence(cacheKey.CacheSettingsId, sequenceHash, fullSequence);

            byte[] fragmentBytes = TransientCachePayloadSerializer.SerializeSingleFragmentPayload(localSequenceFragments[localOrdinal]);
            entryChecksumBytes.Add(fragmentBytes);

            long fragmentShardId = ResolveOrPublishFragmentShard(manifestStore, segmentManager, sharedSequence, fragmentBytes);
            entrySequenceReferences.Add(new TransientCacheEntrySequenceReference(sharedSequence.SequenceId, localOrdinal));

            if (manifestStore.GetPayloadShard(fragmentShardId)?.ReferenceCount == 1)
            {
                fragmentBytesWritten += fragmentBytes.Length;
            }
        }

        var entry = new TransientCacheManifestEntry(cacheKey, TransientCachePublishState.Published)
        {
            ProteinCount = proteins.Count,
            PeptideCount = fullSequences.Count,
            EntryChecksum = TransientCacheHashing.ComputeSha256Hex(entryChecksumBytes.SelectMany(bytes => bytes).ToArray())
        };
        manifestStore.UpsertCacheEntry(entry);
        manifestStore.ReplaceEntrySequences(cacheKey, entrySequenceReferences);

        manifestStore.ReplaceEntryShards(cacheKey, new[]
        {
            new TransientCacheEntryShardReference(occurrenceShard.ShardId, TransientCachePayloadKind.Occurrence, 0),
        });

        Telemetry.RecordPayloadBytesWritten(occurrenceBytes.Length + fragmentBytesWritten);
    }

    private static string GetLocalSequenceKey(IBioPolymerWithSetMods peptide)
    {
        return peptide.FullSequence;
    }

    private static string ComputeSharedSequenceHash(string fullSequence)
    {
        return TransientCacheHashing.ComputeSha256Hex(Encoding.UTF8.GetBytes(fullSequence));
    }

    private Dictionary<int, List<Product>> LoadLegacyFragmentTable(
        TransientCacheResolvedShardReference fragmentShard,
        TransientCachePayloadSegmentReader reader)
    {
        byte[] fragmentBytes = reader.ReadShard(
            _storageLayout.GetSegmentPath(fragmentShard.RelativePath),
            fragmentShard);

        var legacyFragmentTable = TransientCachePayloadSerializer.DeserializeFragmentPayload(fragmentBytes);
        return legacyFragmentTable
            .Select((fragments, localOrdinal) => new { localOrdinal, fragments })
            .ToDictionary(entry => entry.localOrdinal, entry => entry.fragments);
    }

    private Dictionary<int, List<Product>> LoadSharedFragmentTable(
        List<string> fullSequences,
        IReadOnlyList<TransientCacheResolvedSequenceReference> resolvedSequences,
        TransientCacheManifestStore manifestStore,
        TransientCachePayloadSegmentReader reader)
    {
        if (resolvedSequences.Count != fullSequences.Count)
        {
            throw new InvalidDataException($"Occurrence payload has {fullSequences.Count} local sequences but manifest has {resolvedSequences.Count} sequence mappings.");
        }

        var fragmentsByShardId = new Dictionary<long, List<Product>>();
        var fragmentsByLocalOrdinal = new Dictionary<int, List<Product>>(resolvedSequences.Count);

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

            long fragmentShardId = resolvedSequence.FragmentShardId.Value;
            if (!fragmentsByShardId.TryGetValue(fragmentShardId, out List<Product>? fragments))
            {
                var resolvedShard = manifestStore.GetResolvedPayloadShard(fragmentShardId)
                    ?? throw new InvalidDataException($"Fragment shard '{fragmentShardId}' was not found in the manifest.");
                byte[] fragmentBytes = reader.ReadShard(_storageLayout.GetSegmentPath(resolvedShard.RelativePath), resolvedShard);
                fragments = TransientCachePayloadSerializer.DeserializeSingleFragmentPayload(fragmentBytes);
                fragmentsByShardId[fragmentShardId] = fragments;
            }

            fragmentsByLocalOrdinal[resolvedSequence.LocalOrdinal] = fragments;
        }

        return fragmentsByLocalOrdinal;
    }

    private long ResolveOrPublishFragmentShard(
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
                return existingFragmentShardId;
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
            return reusableShard.Value.ShardId;
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
        return newShard.ShardId;
    }
}
