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
        var occurrenceShard = resolvedShards.FirstOrDefault(s => s.PayloadKind == TransientCachePayloadKind.Occurrence);
        var fragmentShard = resolvedShards.FirstOrDefault(s => s.PayloadKind == TransientCachePayloadKind.Fragment);

        if (occurrenceShard.Sha256 is null || fragmentShard.Sha256 is null)
        {
            return null;
        }

        var reader = new TransientCachePayloadSegmentReader();
        byte[] occurrenceBytes = reader.ReadShard(
            _storageLayout.GetSegmentPath(occurrenceShard.RelativePath),
            occurrenceShard);
        byte[] fragmentBytes = reader.ReadShard(
            _storageLayout.GetSegmentPath(fragmentShard.RelativePath),
            fragmentShard);

        var (proteinOccurrences, fullSequences) = TransientCachePayloadSerializer.DeserializeOccurrencePayload(occurrenceBytes);
        var fragmentTable = TransientCachePayloadSerializer.DeserializeFragmentPayload(fragmentBytes);

        if (fullSequences.Count != fragmentTable.Count)
        {
            throw new InvalidDataException($"Occurrence payload has {fullSequences.Count} local sequences but fragment payload has {fragmentTable.Count}.");
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
        byte[] fragmentBytes = TransientCachePayloadSerializer.SerializeFragmentPayload(localSequenceFragments);

        string occurrenceRelativePath = $"{cacheKey.DatabaseContentHash}_{cacheKey.CacheSettingsId}_occurrence";
        string fragmentRelativePath = $"{cacheKey.DatabaseContentHash}_{cacheKey.CacheSettingsId}_fragment";

        var writer = new TransientCachePayloadSegmentWriter();
        var occurrenceResult = writer.AppendShard(
            _storageLayout.GetSegmentPath(occurrenceRelativePath),
            TransientCachePayloadKind.Occurrence,
            occurrenceBytes);
        var fragmentResult = writer.AppendShard(
            _storageLayout.GetSegmentPath(fragmentRelativePath),
            TransientCachePayloadKind.Fragment,
            fragmentBytes);

        var occurrenceSegment = manifestStore.UpsertPayloadSegment(TransientCachePayloadKind.Occurrence, occurrenceRelativePath, occurrenceResult.StoredLengthBytes);
        var fragmentSegment = manifestStore.UpsertPayloadSegment(TransientCachePayloadKind.Fragment, fragmentRelativePath, fragmentResult.StoredLengthBytes);

        var occurrenceShard = manifestStore.InsertPayloadShard(
            occurrenceSegment.SegmentId,
            TransientCachePayloadKind.Occurrence,
            occurrenceResult.OffsetBytes,
            occurrenceResult.StoredLengthBytes,
            occurrenceResult.LogicalLengthBytes,
            occurrenceResult.Sha256,
            referenceCount: 1);
        var fragmentShard = manifestStore.InsertPayloadShard(
            fragmentSegment.SegmentId,
            TransientCachePayloadKind.Fragment,
            fragmentResult.OffsetBytes,
            fragmentResult.StoredLengthBytes,
            fragmentResult.LogicalLengthBytes,
            fragmentResult.Sha256,
            referenceCount: 1);

        manifestStore.UpsertSourceDatabase(cacheKey.DatabaseContentHash, _dbFilePath);
        manifestStore.UpsertCacheSettings(cacheKey.CacheSettingsId, canonicalSettingsPayload);

        var entry = new TransientCacheManifestEntry(cacheKey, TransientCachePublishState.Published)
        {
            ProteinCount = proteins.Count,
            PeptideCount = fullSequences.Count,
            EntryChecksum = TransientCacheHashing.ComputeSha256Hex(occurrenceBytes.Concat(fragmentBytes).ToArray())
        };
        manifestStore.UpsertCacheEntry(entry);

        manifestStore.ReplaceEntryShards(cacheKey, new[]
        {
            new TransientCacheEntryShardReference(occurrenceShard.ShardId, TransientCachePayloadKind.Occurrence, 0),
            new TransientCacheEntryShardReference(fragmentShard.ShardId, TransientCachePayloadKind.Fragment, 1),
        });

        Telemetry.RecordPayloadBytesWritten(occurrenceBytes.Length + fragmentBytes.Length);
    }

    private static string GetLocalSequenceKey(IBioPolymerWithSetMods peptide)
    {
        return peptide.FullSequence;
    }
}
