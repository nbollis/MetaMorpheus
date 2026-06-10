using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Chemistry;
using EngineLayer.ClassicSearch;
using EngineLayer.FdrAnalysis;
using EngineLayer.ParallelSearch.Scoring;
using EngineLayer.Util;
using MzLibUtil;
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

        // The flattened experimental spectra depend only on the (shared) scan array, so cache by
        // its reference: every transient engine over the same file set reuses one build.
        private static readonly ConditionalWeakTable<Ms2ScanWithSpecificMass[], SpectralScoringData> _scoringDataCache = new();

        private static SpectralScoringData GetOrBuildScoringData(Ms2ScanWithSpecificMass[] sortedScans)
            => _scoringDataCache.GetValue(sortedScans, SpectralScoringData.Build);

        protected override MetaMorpheusEngineResults RunSpecific()
        {
            double proteinsSearched = 0;
            int oldPercentProgress = 0;
            int peptideCounter = 0;

            Status("Performing classic search...");

            bool usePrecomputedPeptides = _precomputedPeptides != null && _precomputedPeptides.Count > 0;
            if (Proteins.Any() || usePrecomputedPeptides)
            {

                // Experimental spectra are identical and read-only across every transient database
                // search, so flatten them once (struct-of-arrays) and reuse across all peptides/threads.
                var scoringData = GetOrBuildScoringData(ArrayOfSortedMS2Scans);
                using var scorerProvider = new CpuScorerProvider(
                    scoringData, CommonParameters.ProductMassTolerance);
                Status("ParallelSearch scoring backend: " + scorerProvider.BackendDescription);

                // Shared per-peptide work: fragment (in double), queue candidate scans, flush. The
                // scorer does ONLY the fragment matching (the GPU-able hot step); the accumulator
                // builds MatchedFragmentIons, applies the score cutoff, scores, and updates PSMs.
                // A peptide's work items are queued contiguously so per-scan candidate ordering is
                // preserved (bit-identical results).
                // precomputedFragments != null (from a .msl library) skips the Fragment() call and matches
                // the library's stored fragments directly — the fragmentation-time savings.
                void ProcessOnePeptide(IBioPolymerWithSetMods peptide, TransientScoringBatch batch,
                    ISpectralScorer scorer, List<Product> peptideTheorProducts, List<Product> precomputedFragments = null)
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

                    int slot = -1;
                    foreach (ScanWithIndexAndNotchInfo scan in GetAcceptableScans(peptide.MonoisotopicMass, SearchMode))
                    {
                        if (slot < 0)
                            slot = batch.BeginPeptide(peptide, products);
                        batch.AddWorkItem(slot, scan.ScanIndex, scan.Notch);
                    }

                    if (batch.ShouldFlush)
                        batch.Flush(scorer);
                }

                // GPU = one shared thread-safe scorer (resident spectra); CPU = a cheap per-partition
                // scorer. The provider owns the scorer's lifetime.
                Action<int, int> processProteinRange = (start, end) =>
                {
                    var scorer = scorerProvider.GetScorer();
                    var batch = new TransientScoringBatch(this, scoringData);
                    List<Product> peptideTheorProducts = new();

                    for (int i = start; i < end; i++)
                    {
                        if (GlobalVariables.StopLoops) { batch.Flush(scorer); return; }

                        // digest each protein into peptides and search each peptide in all spectra within precursor mass tolerance
                        foreach (var specificBioPolymer in Proteins[i].Digest(CommonParameters.DigestionParams, FixedModifications, VariableModifications))
                            ProcessOnePeptide(specificBioPolymer, batch, scorer, peptideTheorProducts);

                        // report search progress (proteins searched so far out of total proteins in database)
                        proteinsSearched++;
                        var percentProgress = (int)((proteinsSearched / Proteins.Count) * 100);
                        if (percentProgress > oldPercentProgress)
                        {
                            oldPercentProgress = percentProgress;
                            ReportProgress(new ProgressEventArgs(percentProgress, "Performing classic search... ", NestedIds));
                        }
                    }

                    batch.Flush(scorer);
                };

                // Library (.msl) peptide source: iterate precomputed peptides directly, no digestion.
                Action<int, int> processPeptideRange = (start, end) =>
                {
                    var scorer = scorerProvider.GetScorer();
                    var batch = new TransientScoringBatch(this, scoringData);
                    List<Product> peptideTheorProducts = new();

                    for (int i = start; i < end; i++)
                    {
                        if (GlobalVariables.StopLoops) { batch.Flush(scorer); return; }
                        var (peptide, fragments) = _precomputedPeptides[i];
                        ProcessOnePeptide(peptide, batch, scorer, peptideTheorProducts, fragments);
                    }

                    batch.Flush(scorer);
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

        /// <summary>
        /// Per-thread accumulator that batches (peptide, candidate-scan) work for the
        /// <see cref="ISpectralScorer"/> and, on flush, turns each item's matched fragments into a
        /// PSM update — reproducing the original per-scan logic (HashSet dedup, ScoreCutoff gate,
        /// (1 + intensity/TIC) scoring) exactly, just deferred to flush time. Work items are queued
        /// in digestion order with each peptide's items contiguous, so PSM update order (and thus
        /// tie resolution) matches the unbatched engine.
        /// </summary>
        private sealed class TransientScoringBatch : IScoringSink
        {
            private const int WorkItemFlushThreshold = 16384;
            private const int PeptideFlushThreshold = 4096;

            private readonly TransientClassicSearchEngine _engine;
            private readonly SpectralScoringData _data;
            private readonly ScoringBatch _batch = new();
            // Reused across work items / peptides to keep the batched path allocation-free in the
            // hot loop (the unbatched engine reused one cleared HashSet and never copied products).
            private readonly HashSet<MatchedFragmentIon> _matchedIonScratch = new();
            private IBioPolymerWithSetMods[] _slotPeptides = new IBioPolymerWithSetMods[1024];
            private int[] _slotProductOffset = new int[1025];
            private Product[] _productPool = new Product[4096];
            private int[] _workNotch = new int[1024];
            private int _fragmentWrite;
            private int _productWrite;

            public TransientScoringBatch(TransientClassicSearchEngine engine, SpectralScoringData data)
            {
                _engine = engine;
                _data = data;
            }

            public bool ShouldFlush =>
                _batch.WorkItemCount >= WorkItemFlushThreshold || _batch.PeptideCount >= PeptideFlushThreshold;

            /// <summary>Register a peptide and its theoretical products; returns its batch slot.</summary>
            public int BeginPeptide(IBioPolymerWithSetMods peptide, List<Product> products)
            {
                int slot = _batch.PeptideCount;
                int count = products.Count;

                EnsureSlotCapacity(slot + 1);
                _slotPeptides[slot] = peptide;
                _slotProductOffset[slot] = _productWrite;

                // Snapshot the products (the caller reuses/clears its list) into a pooled array,
                // and mirror their neutral masses into the flat batch buffer for the scorer.
                EnsureProductPoolCapacity(_productWrite + count);
                EnsureFragmentCapacity(_fragmentWrite + count);
                for (int k = 0; k < count; k++)
                {
                    Product p = products[k];
                    _productPool[_productWrite + k] = p;
                    _batch.FragmentNeutralMasses[_fragmentWrite + k] = p.NeutralMass;
                }
                _productWrite += count;
                _fragmentWrite += count;

                EnsureOffsetCapacity(slot + 2);
                _batch.PeptideFragmentOffsets[slot + 1] = _fragmentWrite;
                _slotProductOffset[slot + 1] = _productWrite;
                _batch.PeptideCount = slot + 1;
                return slot;
            }

            public void AddWorkItem(int slot, int scanIndex, int notch)
            {
                int w = _batch.WorkItemCount;
                EnsureWorkCapacity(w + 1);
                _batch.WorkPeptideSlot[w] = slot;
                _batch.WorkScanIndex[w] = scanIndex;
                _workNotch[w] = notch;
                _batch.WorkItemCount = w + 1;
            }

            public void Flush(ISpectralScorer scorer)
            {
                if (_batch.WorkItemCount > 0)
                    scorer.ScoreBatch(_batch, this);
                Reset();
            }

            private void Reset()
            {
                _batch.Clear();
                _fragmentWrite = 0;
                _productWrite = 0;
            }

            void IScoringSink.AcceptWorkItem(int workIndex, FragmentMatch[] matches, int matchCount)
            {
                int slot = _batch.WorkPeptideSlot[workIndex];
                int scanIndex = _batch.WorkScanIndex[workIndex];
                int notch = _workNotch[workIndex];
                int productBase = _slotProductOffset[slot];

                HashSet<MatchedFragmentIon> matchedFragmentIons = _matchedIonScratch;
                matchedFragmentIons.Clear();
                for (int k = 0; k < matchCount; k++)
                {
                    FragmentMatch m = matches[k];
                    matchedFragmentIons.Add(new MatchedFragmentIon(
                        _productPool[productBase + m.LocalProductIndex], m.ExperimentalMz, m.ExperimentalIntensity, m.ExperimentalCharge));
                }

                if (matchedFragmentIons.Count < _engine.CommonParameters.ScoreCutoff)
                    return;

                double tic = _data.ScanTotalIonCurrents[scanIndex];
                double score = 0;
                foreach (var ion in matchedFragmentIons)
                {
                    if (ion.NeutralTheoreticalProduct.ProductType != ProductType.D)
                        score += 1 + ion.Intensity / tic;
                }

                var matchedIons = matchedFragmentIons.ToList();
                _engine.AddPeptideCandidateToPsm(
                    new ScanWithIndexAndNotchInfo(notch, scanIndex), score, _slotPeptides[slot], matchedIons);
            }

            private void EnsureSlotCapacity(int neededSlots)
            {
                if (_slotPeptides.Length < neededSlots)
                {
                    int n = Math.Max(neededSlots, _slotPeptides.Length * 2);
                    Array.Resize(ref _slotPeptides, n);
                    Array.Resize(ref _slotProductOffset, n + 1);
                }
            }

            private void EnsureProductPoolCapacity(int needed)
            {
                if (_productPool.Length < needed)
                    Array.Resize(ref _productPool, Math.Max(needed, Math.Max(4096, _productPool.Length * 2)));
            }

            private void EnsureFragmentCapacity(int needed)
            {
                if (_batch.FragmentNeutralMasses.Length < needed)
                    Array.Resize(ref _batch.FragmentNeutralMasses, Math.Max(needed, Math.Max(1024, _batch.FragmentNeutralMasses.Length * 2)));
            }

            private void EnsureOffsetCapacity(int needed)
            {
                if (_batch.PeptideFragmentOffsets.Length < needed)
                    Array.Resize(ref _batch.PeptideFragmentOffsets, Math.Max(needed, _batch.PeptideFragmentOffsets.Length * 2));
            }

            private void EnsureWorkCapacity(int needed)
            {
                if (_batch.WorkPeptideSlot.Length < needed)
                {
                    int n = Math.Max(needed, Math.Max(1024, _batch.WorkPeptideSlot.Length * 2));
                    Array.Resize(ref _batch.WorkPeptideSlot, n);
                    Array.Resize(ref _batch.WorkScanIndex, n);
                    Array.Resize(ref _workNotch, n);
                }
            }
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
