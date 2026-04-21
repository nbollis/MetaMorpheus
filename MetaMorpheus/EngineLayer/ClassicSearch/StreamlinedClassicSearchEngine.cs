using Chemistry;
using EngineLayer.Util;
using MassSpectrometry;
using MzLibUtil;
using Omics;
using Omics.Fragmentation;
using Omics.Modifications;
using Proteomics;
using Readers.SpectralLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EngineLayer.ClassicSearch
{
    public class StreamlinedClassicSearchEngine : ClassicSearchEngine
    {
        private readonly ReaderWriterLockSlim[] Locks;
        private bool _singleThreadMode;
        private readonly ConcurrentDictionary<int, byte> UpdatedIndexes = new();
        public StreamlinedClassicSearchEngine(SpectralMatch[] globalPsms, Ms2ScanWithSpecificMass[] arrayOfSortedMS2Scans,
            List<Modification> variableModifications, List<Modification> fixedModifications,
            List<IBioPolymer> proteinList, MassDiffAcceptor searchMode, CommonParameters commonParameters, List<(string FileName, CommonParameters Parameters)> fileSpecificParameters, List<string> nestedIds)
            : base(globalPsms, arrayOfSortedMS2Scans, variableModifications, fixedModifications, null, null, null, proteinList, searchMode, commonParameters, fileSpecificParameters, null, nestedIds, false)
        {
            UpdatedIndexes = new ConcurrentDictionary<int, byte>();
            _singleThreadMode = CommonParameters.MaxThreadsToUsePerFile <= 1;

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
            Status("Getting ms2 scans...");

            double proteinsSearched = 0;
            int oldPercentProgress = 0;

            Status("Performing classic search...");

            if (Proteins.Any())
            {

                Action<int, int> processProteinRange = (start, end) =>
                {
                    List<Product> peptideTheorProducts = new();
                    HashSet<MatchedFragmentIon> matchedFragmentIons = new();
                    Tolerance productTolerance = CommonParameters.ProductMassTolerance;
                    for (int i = start; i < end; i++)
                    {
                        // Stop loop if canceled
                        if (GlobalVariables.StopLoops) { return; }

                        // digest each protein into peptides and search for each peptide in all spectra within precursor mass tolerance
                        foreach (var specificBioPolymer in Proteins[i].Digest(CommonParameters.DigestionParams, FixedModifications, VariableModifications))
                        {

                            peptideTheorProducts.Clear();
                            specificBioPolymer.Fragment(CommonParameters.DissociationType, CommonParameters.DigestionParams.FragmentationTerminus, peptideTheorProducts);

                            // score each scan that has an acceptable precursor mass
                            foreach (ScanWithIndexAndNotchInfo scan in GetAcceptableScans(specificBioPolymer.MonoisotopicMass, SearchMode))
                            {
                                matchedFragmentIons.Clear();
                                Ms2ScanWithSpecificMass theScan = ArrayOfSortedMS2Scans[scan.ScanIndex];
                                int precursorCharge = theScan.PrecursorCharge;

                                // Match Fragment Ions
                                foreach (var product in peptideTheorProducts)
                                {
                                    // unknown fragment mass; this only happens rarely for sequences with unknown amino acids
                                    if (double.IsNaN(product.NeutralMass))
                                    {
                                        continue;
                                    }

                                    // get the closest peak in the spectrum to the theoretical peak
                                    var closestExperimentalMass = theScan.GetClosestExperimentalIsotopicEnvelope(product.NeutralMass);

                                    // is the mass error acceptable?
                                    if (closestExperimentalMass != null
                                        && productTolerance.Within(closestExperimentalMass.MonoisotopicMass, product.NeutralMass)
                                        && Math.Abs(closestExperimentalMass.Charge) <= Math.Abs(precursorCharge))//TODO apply this filter before picking the envelope
                                    {
                                        matchedFragmentIons.Add(new MatchedFragmentIon(product, closestExperimentalMass.MonoisotopicMass.ToMz(closestExperimentalMass.Charge),
                                            closestExperimentalMass.Peaks.First().intensity, closestExperimentalMass.Charge));
                                    }
                                }

                                if (matchedFragmentIons.Count < CommonParameters.ScoreCutoff)
                                    continue;

                                // Score the peptide-spectrum match
                                double tic = theScan.TotalIonCurrent;
                                double score = 0;
                                foreach (var ion in matchedFragmentIons)
                                {
                                    if (ion.NeutralTheoreticalProduct.ProductType != ProductType.D)
                                    {
                                        score += 1 + ion.Intensity / tic;
                                    }
                                }

                                var matchedIons = matchedFragmentIons.ToList(); // materialize before passing to another thread
                                AddPeptideCandidateToPsm(scan, score, specificBioPolymer, matchedIons);
                            }
                        }

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

                if (_singleThreadMode)
                {
                    processProteinRange(0, Proteins.Count);
                }
                else
                {
                    var proteinPartioner = Partitioner.Create(0, Proteins.Count);
                    Parallel.ForEach(
                        proteinPartioner,
                        new ParallelOptions { MaxDegreeOfParallelism = CommonParameters.MaxThreadsToUsePerFile },
                        (range, loopState) =>
                        {
                            processProteinRange(range.Item1, range.Item2);
                        });
                }
            }

            foreach (SpectralMatch psm in SpectralMatches.Where(p => p != null && UpdatedIndexes.ContainsKey(p.ScanIndex)))
            {
                psm.ResolveAllAmbiguities();
            }

            HashSet<int> updatedIndexes = UpdatedIndexes.Keys.ToHashSet();
            return new StreamLinedClassicSearchEngineResults(this, updatedIndexes);
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

    public class StreamLinedClassicSearchEngineResults(
        StreamlinedClassicSearchEngine engine,
        HashSet<int> updatedSpectralMatchIndexes)
        : MetaMorpheusEngineResults(engine)
    {
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
