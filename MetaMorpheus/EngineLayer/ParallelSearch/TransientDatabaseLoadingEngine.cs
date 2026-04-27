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
            try
            {
                var hydrated = TryHydrateFromCache(cacheKey, manifestStore, publishedEntry);
                if (hydrated is not null)
                {
                    Status(TransientCacheMessages.FormatLookupMessage(TransientCacheLookupOutcome.Hit, _dbFilePath));
                    return new DatabaseLoadingEngineResults(this, DbForTask, hydrated, 0, 0, 0);
                }
            }
            catch (Exception ex)
            {
                Warn(TransientCacheMessages.FormatLookupMessage(TransientCacheLookupOutcome.Corrupt, _dbFilePath, ex.Message));
            }
        }
        else
        {
            Status(TransientCacheMessages.FormatLookupMessage(TransientCacheLookupOutcome.Miss, _dbFilePath));
        }

        var baseResults = base.RunSpecific() as DatabaseLoadingEngineResults;
        if (baseResults is null)
        {
            return baseResults;
        }

        try
        {
            PublishCacheEntry(cacheKey, manifestStore, baseResults.BioPolymers, settingsDescriptor.CanonicalSettingsPayload);
        }
        catch (Exception ex)
        {
            Warn(TransientCacheMessages.FormatPublishMessage(TransientCachePublishState.Failed, _dbFilePath, ex.Message));
        }

        return baseResults;
    }

    private List<IBioPolymer>? TryHydrateFromCache(
        TransientCacheKey cacheKey,
        TransientCacheManifestStore manifestStore,
        TransientCacheManifestEntry entry)
    {
        var resolvedShards = manifestStore.GetResolvedEntryShardReferences(cacheKey);
        var digestShard = resolvedShards.FirstOrDefault(s => s.PayloadKind == TransientCachePayloadKind.ProteinDigest);
        var fragmentShard = resolvedShards.FirstOrDefault(s => s.PayloadKind == TransientCachePayloadKind.Fragment);

        if (digestShard.Sha256 is null || fragmentShard.Sha256 is null)
        {
            return null;
        }

        var reader = new TransientCachePayloadSegmentReader();
        byte[] digestBytes = reader.ReadShard(
            _storageLayout.GetSegmentPath(digestShard.RelativePath),
            digestShard);
        byte[] fragmentBytes = reader.ReadShard(
            _storageLayout.GetSegmentPath(fragmentShard.RelativePath),
            fragmentShard);

        var (proteinOccurrences, peptidoforms) = TransientCachePayloadSerializer.DeserializeDigestPayload(digestBytes);
        var fragmentTable = TransientCachePayloadSerializer.DeserializeFragmentPayload(fragmentBytes);

        if (peptidoforms.Count != fragmentTable.Count)
        {
            throw new InvalidDataException($"Digest payload has {peptidoforms.Count} peptidoforms but fragment payload has {fragmentTable.Count}.");
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
            if (!proteinOccurrences.TryGetValue(proteinIndex, out var occurrences))
            {
                occurrences = new List<(int, int, int, int, int, string)>();
            }

            int peptideCount = occurrences.Count;
            var transientBioPolymer = new TransientBioPolymer(
                proteins[proteinIndex],
                peptideCount,
                digestionProductFactory: parent =>
                {
                    var peptides = new List<IBioPolymerWithSetMods>(occurrences.Count);
                    foreach (var occ in occurrences)
                    {
                        var pf = peptidoforms[occ.peptidoformIndex];
                        var mods = new Dictionary<int, Modification>();
                        foreach (var kvp in pf.mods)
                        {
                            if (modLookup.TryGetValue(kvp.Value, out var mod))
                            {
                                mods[kvp.Key] = mod;
                            }
                        }

                        var peptide = new PeptideWithSetModifications(
                            proteins[proteinIndex] as Protein,
                            CommonParameters.DigestionParams,
                            occ.oneBasedStartResidue,
                            occ.oneBasedEndResidue,
                            (CleavageSpecificity)occ.cleavageSpecificity,
                            occ.peptideDescription,
                            occ.missedCleavages,
                            mods,
                            numFixedMods: 0);

                        var wrapped = new TransientBioPolymerWithSetMods(
                            peptide,
                            parent,
                            fragmentFactory: () => fragmentTable[occ.peptidoformIndex]);
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

        var peptidoformIndexMap = new Dictionary<string, int>();
        var peptidoforms = new List<(string fullSequence, string baseSequence, string digestionAgent, double monoisotopicMass, Dictionary<int, string> mods)>();
        var proteinOccurrences = new Dictionary<int, List<(int peptidoformIndex, int oneBasedStartResidue, int oneBasedEndResidue, int missedCleavages, int cleavageSpecificity, string peptideDescription)>>();
        var peptidoformFragments = new List<List<Product>>();

        for (int proteinIndex = 0; proteinIndex < proteins.Count; proteinIndex++)
        {
            var protein = proteins[proteinIndex];
            var digested = protein.Digest(digestionParams, fixedMods, variableMods).ToList();
            var occurrences = new List<(int, int, int, int, int, string)>();

            foreach (var peptide in digested)
            {
                string key = GetPeptidoformKey(peptide);
                if (!peptidoformIndexMap.TryGetValue(key, out int pfIndex))
                {
                    pfIndex = peptidoforms.Count;
                    peptidoformIndexMap[key] = pfIndex;

                    var mods = new Dictionary<int, string>();
                    if (peptide is PeptideWithSetModifications pwsm)
                    {
                        foreach (var mod in pwsm.AllModsOneIsNterminus)
                        {
                            mods[mod.Key] = mod.Value.IdWithMotif;
                        }
                    }

                    peptidoforms.Add((
                        peptide.FullSequence,
                        peptide.BaseSequence,
                        peptide.DigestionParams?.DigestionAgent?.ToString() ?? string.Empty,
                        peptide.MonoisotopicMass,
                        mods));

                    var fragments = new List<Product>();
                    peptide.Fragment(CommonParameters.DissociationType, CommonParameters.DigestionParams.FragmentationTerminus, fragments);
                    peptidoformFragments.Add(fragments);
                }

                int start = 0;
                int end = 0;
                int missed = 0;
                int specificity = 0;
                string description = string.Empty;
                if (peptide is PeptideWithSetModifications setModPeptide)
                {
                    start = setModPeptide.OneBasedStartResidueInProtein;
                    end = setModPeptide.OneBasedEndResidueInProtein;
                    missed = setModPeptide.MissedCleavages;
                    specificity = (int)setModPeptide.CleavageSpecificityForFdrCategory;
                    description = setModPeptide.PeptideDescription ?? string.Empty;
                }

                occurrences.Add((pfIndex, start, end, missed, specificity, description));
            }

            proteinOccurrences[proteinIndex] = occurrences;
        }

        byte[] digestBytes = TransientCachePayloadSerializer.SerializeDigestPayload(proteins, proteinOccurrences, peptidoforms);
        byte[] fragmentBytes = TransientCachePayloadSerializer.SerializeFragmentPayload(peptidoformFragments);

        string digestRelativePath = $"{cacheKey.DatabaseContentHash}_{cacheKey.CacheSettingsId}_digest";
        string fragmentRelativePath = $"{cacheKey.DatabaseContentHash}_{cacheKey.CacheSettingsId}_fragment";

        var writer = new TransientCachePayloadSegmentWriter();
        var digestResult = writer.AppendShard(
            _storageLayout.GetSegmentPath(digestRelativePath),
            TransientCachePayloadKind.ProteinDigest,
            digestBytes);
        var fragmentResult = writer.AppendShard(
            _storageLayout.GetSegmentPath(fragmentRelativePath),
            TransientCachePayloadKind.Fragment,
            fragmentBytes);

        var digestSegment = manifestStore.UpsertPayloadSegment(TransientCachePayloadKind.ProteinDigest, digestRelativePath, digestResult.StoredLengthBytes);
        var fragmentSegment = manifestStore.UpsertPayloadSegment(TransientCachePayloadKind.Fragment, fragmentRelativePath, fragmentResult.StoredLengthBytes);

        var digestShard = manifestStore.InsertPayloadShard(
            digestSegment.SegmentId,
            TransientCachePayloadKind.ProteinDigest,
            digestResult.OffsetBytes,
            digestResult.StoredLengthBytes,
            digestResult.LogicalLengthBytes,
            digestResult.Sha256,
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
            PeptideCount = peptidoforms.Count,
            EntryChecksum = TransientCacheHashing.ComputeSha256Hex(digestBytes.Concat(fragmentBytes).ToArray())
        };
        manifestStore.UpsertCacheEntry(entry);

        manifestStore.ReplaceEntryShards(cacheKey, new[]
        {
            new TransientCacheEntryShardReference(digestShard.ShardId, TransientCachePayloadKind.ProteinDigest, 0),
            new TransientCacheEntryShardReference(fragmentShard.ShardId, TransientCachePayloadKind.Fragment, 1),
        });
    }

    private static string GetPeptidoformKey(IBioPolymerWithSetMods peptide)
    {
        return $"{peptide.FullSequence}|{peptide.DigestionParams?.DigestionAgent?.ToString()}|{peptide.MonoisotopicMass:R}";
    }
}
