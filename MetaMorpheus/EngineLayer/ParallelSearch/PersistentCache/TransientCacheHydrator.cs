using Omics;
using Omics.Fragmentation;
using Omics.Modifications;
using Proteomics;
using Proteomics.ProteolyticDigestion;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace EngineLayer.ParallelSearch.PersistentCache;

internal sealed class TransientCacheHydrator
{
    private readonly CommonParameters _commonParameters;
    private readonly TransientCacheStorageLayout _storageLayout;
    private readonly TransientCacheTelemetry _telemetry;

    public TransientCacheHydrator(
        CommonParameters commonParameters,
        TransientCacheStorageLayout storageLayout,
        TransientCacheTelemetry telemetry)
    {
        _commonParameters = commonParameters;
        _storageLayout = storageLayout;
        _telemetry = telemetry;
    }

    public TransientCacheHydrationResult TryHydrate(
        TransientCacheLookupResult lookupResult,
        IReadOnlyList<IBioPolymer> rawProteins)
    {
        if (!lookupResult.HasReusableEntry || lookupResult.Context is null)
        {
            return TransientCacheHydrationResult.NotApplicable();
        }

        try
        {
            var resolvedShards = lookupResult.ResolvedShards;
            var resolvedSequences = lookupResult.ResolvedSequences;
            var manifestStore = lookupResult.Context.ManifestStore;
            var occurrenceShard = resolvedShards.FirstOrDefault(s => s.PayloadKind == Payloads.TransientCachePayloadKind.Occurrence);
            var fragmentShard = resolvedShards.FirstOrDefault(s => s.PayloadKind == Payloads.TransientCachePayloadKind.Fragment);

            if (occurrenceShard.Sha256 is null)
            {
                return TransientCacheHydrationResult.Failure(TransientCacheLookupOutcome.Corrupt, "Occurrence shard checksum was missing.");
            }

            var reader = new Payloads.TransientCachePayloadSegmentReader();
            byte[] occurrenceBytes = reader.ReadShard(
                _storageLayout.GetSegmentPath(occurrenceShard.RelativePath),
                occurrenceShard);

            var (proteinOccurrences, fullSequences) = TransientCachePayloadSerializer.DeserializeOccurrencePayload(occurrenceBytes);
            Func<int, IReadOnlyList<Product>> fragmentResolver = fragmentShard is not null && fragmentShard.Sha256 is not null
                ? CreateLegacyFragmentResolver(fragmentShard, fullSequences.Count)
                : CreateSharedFragmentResolver(fullSequences, resolvedSequences, manifestStore);

            var modLookup = GlobalVariables.AllModsKnownDictionary ?? new Dictionary<string, Modification>();
            var wrappedProteins = new List<IBioPolymer>(rawProteins.Count);
            for (int proteinIndex = 0; proteinIndex < rawProteins.Count; proteinIndex++)
            {
                int currentProteinIndex = proteinIndex;
                if (!proteinOccurrences.TryGetValue(currentProteinIndex, out var occurrences))
                {
                    occurrences = new List<(int, int, int, int, string)>();
                }

                int peptideCount = occurrences.Count;
                var transientBioPolymer = new TransientBioPolymer(
                    rawProteins[currentProteinIndex],
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
                                p: rawProteins[currentProteinIndex] as Protein,
                                oneBasedStartResidueInProtein: occ.oneBasedStartResidue,
                                oneBasedEndResidueInProtein: occ.oneBasedEndResidue);

                            var peptide = new PeptideWithSetModifications(
                                rawProteins[currentProteinIndex] as Protein,
                                _commonParameters.DigestionParams,
                                occ.oneBasedStartResidue,
                                occ.oneBasedEndResidue,
                                _commonParameters.DigestionParams.SearchModeType,
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

            return TransientCacheHydrationResult.Success(wrappedProteins);
        }
        catch (Exception ex)
        {
            return TransientCacheHydrationResult.Failure(TransientCacheLookupOutcome.Corrupt, ex.Message);
        }
    }

    private Func<int, IReadOnlyList<Product>> CreateLegacyFragmentResolver(
        Manifest.TransientCacheResolvedShardReference fragmentShard,
        int expectedSequenceCount)
    {
        var fragmentTable = new Lazy<IReadOnlyList<List<Product>>>(() =>
        {
            var reader = new Payloads.TransientCachePayloadSegmentReader();
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
        IReadOnlyList<Manifest.TransientCacheResolvedSequenceReference> resolvedSequences,
        Manifest.TransientCacheManifestStore manifestStore)
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

            if (resolvedSequence.IsQuarantined)
            {
                throw new InvalidDataException($"Shared sequence '{resolvedSequence.FullSequence}' is quarantined and must be rebuilt before reuse.");
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

    private IReadOnlyList<Product> LoadSharedFragmentPayload(long fragmentShardId, Manifest.TransientCacheManifestStore manifestStore)
    {
        try
        {
            var resolvedShard = manifestStore.GetResolvedPayloadShard(fragmentShardId)
                ?? throw new InvalidDataException($"Fragment shard '{fragmentShardId}' was not found in the manifest.");

            var reader = new Payloads.TransientCachePayloadSegmentReader();
            byte[] fragmentBytes = reader.ReadShard(_storageLayout.GetSegmentPath(resolvedShard.RelativePath), resolvedShard);
            return TransientCachePayloadSerializer.DeserializeSingleFragmentPayload(fragmentBytes);
        }
        catch (Exception ex)
        {
            int quarantinedSequenceCount = manifestStore.QuarantineSharedSequencesByFragmentShard(fragmentShardId, ex.Message);
            if (quarantinedSequenceCount > 0)
            {
                _telemetry.RecordQuarantinedSharedSequences(quarantinedSequenceCount);
            }

            throw;
        }
    }
}
