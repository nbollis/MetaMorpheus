using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EngineLayer.ClassicSearch;
using EngineLayer.FdrAnalysis;
using EngineLayer.Util;
using Omics;
using Omics.Fragmentation;
using Omics.Modifications;

namespace EngineLayer.ParallelSearch
{

    /// <summary>
    /// Designed to be a more memory efficient version of ClassicSearchEngine for use in parallel searches.
    /// The base PSM list is shared across threads, and only the PSMs that are updated by the transient search are cloned and modified, while the rest of the PSMs remain shared and read-only.
    /// Features that are removed from classic search engine for runtime efficiency and memory efficiency:
    /// - Some detailed logging
    /// - Mass Differance acceptor is always exact.
    /// </summary>
    public class TransientClassicSearchEngine : ClassicSearchEngine
    {
        private readonly ReaderWriterLockSlim[] Locks;
        private bool _singleThreadMode;
        private readonly ConcurrentDictionary<int, byte> UpdatedIndexes = new();
        private readonly bool _copyOnWriteEnabled;
        // When supplied (e.g. from a .msl spectral library), the search iterates these peptides
        // directly instead of digesting Proteins, and matches each peptide's STORED (float) fragments
        // instead of re-fragmenting — skipping both digestion and fragmentation.
        private readonly List<(IBioPolymerWithSetMods Peptide, List<Product> Fragments)> _precomputedPeptides;
        public TransientClassicSearchEngine(SpectralMatch[] globalPsms, Ms2ScanWithSpecificMass[] arrayOfSortedMS2Scans,
            List<Modification> variableModifications, List<Modification> fixedModifications,
            List<IBioPolymer> proteinList, MassDiffAcceptor searchMode, CommonParameters commonParameters, List<(string FileName, CommonParameters Parameters)> fileSpecificParameters, List<string> nestedIds,
            bool copyOnWriteEnabled = false, List<(IBioPolymerWithSetMods Peptide, List<Product> Fragments)> precomputedPeptides = null)
            : base(globalPsms, arrayOfSortedMS2Scans, variableModifications, fixedModifications, null, null, null, proteinList, searchMode, commonParameters, fileSpecificParameters, null, nestedIds, false)
        {
            UpdatedIndexes = new ConcurrentDictionary<int, byte>();
            _singleThreadMode = CommonParameters.MaxThreadsToUsePerFile <= 1;
            _copyOnWriteEnabled = copyOnWriteEnabled;
            _precomputedPeptides = precomputedPeptides;

            if (!_singleThreadMode)
            {
                // Create one lock for each PSM to ensure thread safety
                Locks = new ReaderWriterLockSlim[SpectralMatches.Length];
                for (int i = 0; i < Locks.Length; i++)
                {
                    Locks[i] = new ReaderWriterLockSlim();
                }
            }
        }

        protected override MetaMorpheusEngineResults RunSpecific()
        {
            double proteinsSearched = 0;
            int oldPercentProgress = 0;
            int peptideCounter = 0;

            Status("Performing classic search...");

            bool usePrecomputedPeptides = _precomputedPeptides != null && _precomputedPeptides.Count > 0;
            if (Proteins.Any() || usePrecomputedPeptides)
            {
                // Match one peptide against every candidate scan and update PSMs directly, using the shared
                // MatchFragmentIons / CalculatePeptideScore so MatchedFragmentIon stays the type at the engine
                // boundary. The two memory/runtime wins over ClassicSearchEngine are kept: the base PSM list is
                // shared (only updated PSMs are cloned — see AddPeptideCandidateToPsm), and a .msl library
                // supplies precomputed fragments so the Fragment() call is skipped entirely.
                void ProcessOnePeptide(IBioPolymerWithSetMods peptide, List<Product> peptideTheorProducts,
                    List<Product> precomputedFragments = null)
                {
                    Interlocked.Increment(ref peptideCounter);

                    List<Product> products;
                    if (precomputedFragments != null)
                    {
                        products = precomputedFragments;
                    }
                    else
                    {
                        peptideTheorProducts.Clear();
                        peptide.Fragment(CommonParameters.DissociationType, CommonParameters.DigestionParams.FragmentationTerminus, peptideTheorProducts, CommonParameters.FragmentationParameters);
                        products = peptideTheorProducts;
                    }

                    foreach (ScanWithIndexAndNotchInfo scan in GetAcceptableScans(peptide.MonoisotopicMass, SearchMode))
                    {
                        Ms2ScanWithSpecificMass theScan = ArrayOfSortedMS2Scans[scan.ScanIndex];
                        List<MatchedFragmentIon> matchedIons = MatchFragmentIons(theScan, products, CommonParameters);
                        double thisScore = CalculatePeptideScore(theScan.TheScan, matchedIons);
                        AddPeptideCandidateToPsm(scan, thisScore, peptide, matchedIons);
                    }
                }

                // Protein (FASTA) peptide source: digest each protein and search each peptide.
                Action<int, int> processProteinRange = (start, end) =>
                {
                    List<Product> peptideTheorProducts = new();

                    for (int i = start; i < end; i++)
                    {
                        if (GlobalVariables.StopLoops) return;

                        // digest each protein into peptides and search each peptide in all spectra within precursor mass tolerance
                        foreach (var specificBioPolymer in Proteins[i].Digest(CommonParameters.DigestionParams, FixedModifications, VariableModifications))
                            ProcessOnePeptide(specificBioPolymer, peptideTheorProducts);

                        // report search progress (proteins searched so far out of total proteins in database)
                        proteinsSearched++;
                        var percentProgress = (int)((proteinsSearched / Proteins.Count) * 100);
                        if (percentProgress > oldPercentProgress)
                        {
                            oldPercentProgress = percentProgress;
                            ReportProgress(new ProgressEventArgs(percentProgress, "Performing classic search... ", NestedIds));
                        }
                    }
                };

                // Library (.msl) peptide source: iterate precomputed peptides directly, no digestion.
                Action<int, int> processPeptideRange = (start, end) =>
                {
                    List<Product> peptideTheorProducts = new();

                    for (int i = start; i < end; i++)
                    {
                        if (GlobalVariables.StopLoops) return;
                        var (peptide, fragments) = _precomputedPeptides[i];
                        ProcessOnePeptide(peptide, peptideTheorProducts, fragments);
                    }
                };

                int itemCount = usePrecomputedPeptides ? _precomputedPeptides.Count : Proteins.Count;
                Action<int, int> processRange = usePrecomputedPeptides ? processPeptideRange : processProteinRange;

                if (_singleThreadMode)
                {
                    processRange(0, itemCount);
                }
                else
                {
                    var partitioner = Partitioner.Create(0, itemCount);
                    Parallel.ForEach(
                        partitioner,
                        new ParallelOptions { MaxDegreeOfParallelism = CommonParameters.MaxThreadsToUsePerFile },
                        (range, loopState) =>
                        {
                            processRange(range.Item1, range.Item2);
                        });
                }
            }

            foreach (SpectralMatch psm in SpectralMatches.Where(p => p != null && UpdatedIndexes.ContainsKey(p.ScanIndex)))
            {
                psm.ResolveAllAmbiguities();
            }

            HashSet<int> updatedIndexes = UpdatedIndexes.Keys.ToHashSet();
            return new TransientSearchEngineResults(this, updatedIndexes, peptideCounter);
        }

        private void AddPeptideCandidateToPsm(ScanWithIndexAndNotchInfo scan, double thisScore, IBioPolymerWithSetMods peptide, List<MatchedFragmentIon> matchedIons)
        {
            // matched ion count below the cutoff is not a candidate (mirrors ClassicSearchEngine's ScoreCutoff gate)
            if (matchedIons.Count < CommonParameters.ScoreCutoff)
                return;

            int scanIndex = scan.ScanIndex;

            if (_singleThreadMode)
            {
                var existingPsm = SpectralMatches[scanIndex];

                if (existingPsm != null)
                {
                    double scoreDiff = thisScore - existingPsm.RunnerUpScore;
                    if (scoreDiff <= -SpectralMatch.ToleranceForScoreDifferentiation)
                        return;
                }

                existingPsm = EnsureWritablePsm(scanIndex, existingPsm);

                UpdatedIndexes.TryAdd(scanIndex, 0);

                if (existingPsm == null)
                {
                    SpectralMatches[scanIndex] = GlobalVariables.AnalyteType == AnalyteType.Oligo
                        ? new OligoSpectralMatch(peptide, scan.Notch, thisScore, scanIndex,
                            ArrayOfSortedMS2Scans[scanIndex], CommonParameters, matchedIons)
                        : new PeptideSpectralMatch(peptide, scan.Notch, thisScore, scanIndex,
                            ArrayOfSortedMS2Scans[scanIndex], CommonParameters, matchedIons);
                }
                else
                {
                    existingPsm.AddOrReplace(peptide, thisScore, scan.Notch,
                        CommonParameters.ReportAllAmbiguity, matchedIons);
                }

                return;
            }

            var lockObj = Locks[scanIndex];
            // this is thread-safe because even if the score improves from another thread writing to this PSM,
            // the lock combined with AddOrReplace method will ensure thread safety

            lockObj.EnterReadLock();
            try
            {
                var existingPsm = SpectralMatches[scanIndex];
                if (existingPsm != null)
                {
                    double scoreDiff = thisScore - existingPsm.RunnerUpScore;
                    if (scoreDiff <= -SpectralMatch.ToleranceForScoreDifferentiation)
                        return; // Early exit with just read lock
                }
            }
            finally
            {
                lockObj.ExitReadLock();
            }

            // Need to modify, get write lock
            lockObj.EnterWriteLock();
            try
            {
                var existingPsm = SpectralMatches[scanIndex];

                // Double-check after acquiring write lock
                if (existingPsm != null)
                {
                    double scoreDiff = thisScore - existingPsm.RunnerUpScore;
                    if (scoreDiff <= -SpectralMatch.ToleranceForScoreDifferentiation)
                        return;
                }

                existingPsm = EnsureWritablePsm(scanIndex, existingPsm);
                UpdatedIndexes.TryAdd(scanIndex, 0);

                // if the PSM is null, create a new one; otherwise, add or replace the peptide
                if (existingPsm == null)
                {
                    SpectralMatches[scanIndex] = GlobalVariables.AnalyteType == AnalyteType.Oligo
                        ? new OligoSpectralMatch(peptide, scan.Notch, thisScore, scanIndex,
                            ArrayOfSortedMS2Scans[scanIndex], CommonParameters, matchedIons)
                        : new PeptideSpectralMatch(peptide, scan.Notch, thisScore, scanIndex,
                            ArrayOfSortedMS2Scans[scanIndex], CommonParameters, matchedIons);
                }
                else
                {
                    existingPsm.AddOrReplace(peptide, thisScore, scan.Notch,
                        CommonParameters.ReportAllAmbiguity, matchedIons);
                }
            }
            finally
            {
                lockObj.ExitWriteLock();
            }

        }

        private SpectralMatch EnsureWritablePsm(int scanIndex, SpectralMatch existingPsm)
        {
            if (!_copyOnWriteEnabled || existingPsm == null || UpdatedIndexes.ContainsKey(scanIndex))
            {
                return existingPsm;
            }

            SpectralMatches[scanIndex] = ClonePsmForWrite(existingPsm);
            return SpectralMatches[scanIndex];
        }

        private static SpectralMatch ClonePsmForWrite(SpectralMatch source)
        {
            if (source is PeptideSpectralMatch peptidePsm)
            {
                var bestMatches = peptidePsm.BestMatchingBioPolymersWithSetMods.ToList();
                var cloned = peptidePsm.Clone(bestMatches);
                cloned.PsmFdrInfo = source.PsmFdrInfo?.Clone() ?? new FdrInfo();
                cloned.PeptideFdrInfo = source.PeptideFdrInfo?.Clone();
                return cloned;
            }

            throw new NotSupportedException($"Copy-on-write PSM cloning is not supported for {source.GetType().Name}.");
        }

        private IEnumerable<ScanWithIndexAndNotchInfo> GetAcceptableScans(double peptideMonoisotopicMass, MassDiffAcceptor searchMode)
        {
            foreach (AllowedIntervalWithNotch allowedIntervalWithNotch in searchMode.GetAllowedPrecursorMassIntervalsFromTheoreticalMass(peptideMonoisotopicMass))
            {
                int scanIndex = GetFirstScanWithMassOverOrEqual(allowedIntervalWithNotch.Minimum);
                if (scanIndex < ArrayOfSortedMS2Scans.Length)
                {
                    var scanMass = MyScanPrecursorMasses[scanIndex];
                    while (scanMass <= allowedIntervalWithNotch.Maximum)
                    {
                        yield return new ScanWithIndexAndNotchInfo(allowedIntervalWithNotch.Notch, scanIndex);
                        scanIndex++;
                        if (scanIndex == ArrayOfSortedMS2Scans.Length)
                        {
                            break;
                        }

                        scanMass = MyScanPrecursorMasses[scanIndex];
                    }
                }
            }
        }

        private int GetFirstScanWithMassOverOrEqual(double minimum)
        {
            int index = Array.BinarySearch(MyScanPrecursorMasses, minimum);
            if (index < 0)
            {
                index = ~index;
            }

            // index of the first element that is larger than value
            return index;
        }
    }

    public class TransientSearchEngineResults(
        TransientClassicSearchEngine engine,
        HashSet<int> updatedSpectralMatchIndexes,
        int peptidesSearched)
        : MetaMorpheusEngineResults(engine)
    {
        public int PeptidesSearched { get; private set; } = peptidesSearched;
        public HashSet<int> UpdatedSpectralMatchIndexes { get; private set; } = updatedSpectralMatchIndexes;

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(base.ToString());
            sb.AppendLine("Number of PSMs updated: " + UpdatedSpectralMatchIndexes.Count);
            return sb.ToString();
        }
    }
}
