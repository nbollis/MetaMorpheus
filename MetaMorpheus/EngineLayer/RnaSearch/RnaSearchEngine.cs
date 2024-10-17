using Omics.Modifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Transcriptomics.Digestion;
using Transcriptomics;
using MassSpectrometry;
using Omics.Fragmentation;
using MzLibUtil;
using Omics.Digestion;

namespace EngineLayer
{
    public class RnaSearchEngine : MetaMorpheusEngine
    {
        private readonly MassDiffAcceptor MassDiffAcceptor;
        private readonly List<RNA> Oligos;
        private readonly Ms2ScanWithSpecificMass[] ArrayOfSortedMS2Scans;
        private readonly SpectralMatch[] OligoSpectralMatches;
        private readonly List<Modification> FixedModifications;
        private readonly List<Modification> VariableModifications;

        private readonly double[] MyScanPrecursorMasses;

        public RnaSearchEngine(SpectralMatch[] globalOligoSpectralMatches, List<RNA> rnaSequences,
            Ms2ScanWithSpecificMass[] arrayOfSortedMs2Scans, CommonParameters commonParameters,
            MassDiffAcceptor massDiffAcceptor, 
            List<Modification> variableModifications, List<Modification> fixedModifications,
            List<(string FileName, CommonParameters Parameters)> fileSpecificParameters,
            List<string> nestedIds) : base(commonParameters, fileSpecificParameters, nestedIds)
        {
            Oligos = rnaSequences;
            ArrayOfSortedMS2Scans = arrayOfSortedMs2Scans;
            MyScanPrecursorMasses = ArrayOfSortedMS2Scans.Select(b => b.PrecursorMass).ToArray();
            MassDiffAcceptor = massDiffAcceptor;
            OligoSpectralMatches = globalOligoSpectralMatches;
            FixedModifications = fixedModifications;
            VariableModifications = variableModifications;
        }

        protected override MetaMorpheusEngineResults RunSpecific()
        {
            Status("Getting ms2 scans...");
            double oligosSearched = 0;
            int oldPercentProgress = 0;

            // one lock for each MS2 scan; a scan can only be accessed by one thread at a time
            var myLocks = new object[OligoSpectralMatches.Length];
            for (int i = 0; i < myLocks.Length; i++)
            {
                myLocks[i] = new object();
            }

            Status("Performing search...");

            if (Oligos.Any())
            {
                int maxThreadsPerFile = CommonParameters.MaxThreadsToUsePerFile;
                int[] threads = Enumerable.Range(0, maxThreadsPerFile).ToArray();
                Parallel.ForEach(threads, (i) =>
                {
                    // determine fragment types to look for
                    var fragmentsToSearchFor = new Dictionary<DissociationType, List<Product>>
                    {
                        { CommonParameters.DissociationType, new List<Product>() }
                    };

                    // search
                    for (; i < Oligos.Count; i += maxThreadsPerFile)
                    {
                        if (GlobalVariables.StopLoops) { break; }

                        var precursors = Oligos[i]
                            .Digest(CommonParameters.DigestionParams, FixedModifications, VariableModifications).ToList();
                        foreach (var precursor1 in Oligos[i].Digest(CommonParameters.DigestionParams, FixedModifications, VariableModifications))
                        {
                            var precursor = (OligoWithSetMods)precursor1;

                            // clear fragments from last precursor
                            foreach (var fragmentList in fragmentsToSearchFor.Values)
                                fragmentList.Clear();

                            var scans = GetAcceptableScans(precursor.MonoisotopicMass, MassDiffAcceptor).ToList();
                            // score each scan with an acceptable precursor mass
                            foreach (ScanWithIndexAndNotchInfo scan in GetAcceptableScans(precursor.MonoisotopicMass, MassDiffAcceptor))
                            {
                                var dissociationType = CommonParameters.DissociationType == DissociationType.Autodetect ?
                                    scan.TheScan.TheScan.DissociationType.Value : CommonParameters.DissociationType;

                                if (!fragmentsToSearchFor.TryGetValue(dissociationType, out var theoreticalProducts))
                                {
                                    // TODO: log error. Scan header dissociation type was unknown
                                    continue;
                                }

                                // check if we've already generated theoretical fragments for this oligo and dissociation type
                                if (theoreticalProducts.Count == 0)
                                    precursor.Fragment(dissociationType, FragmentationTerminus.Both, theoreticalProducts);

                                List<MatchedFragmentIon> matchedIons = MatchFragmentIons(scan.TheScan, theoreticalProducts,
                                    CommonParameters, true);

                                double score = CalculatePeptideScore(scan.TheScan.TheScan, matchedIons, true);

                                AddPeptideCandidateToPsm(scan, myLocks, score, precursor, matchedIons);
                            }
                        }

                        oligosSearched++;
                        var percentProgress = (int)((oligosSearched / Oligos.Count) * 100);

                        if (percentProgress > oldPercentProgress)
                        {
                            oldPercentProgress = percentProgress;
                            ReportProgress(new ProgressEventArgs(percentProgress, "Performing classic search... ", NestedIds));
                        }
                    }
                });
                ReportProgress(new ProgressEventArgs(100, "Finished Search!", NestedIds));
            }

            var temp = OligoSpectralMatches.Where(p => p != null).ToList();
            foreach (OligoSpectralMatch osm in OligoSpectralMatches.Where(p => p != null))
            {
                osm.ResolveAllAmbiguities();
            }

            return new MetaMorpheusEngineResults(this);
        }

        internal IEnumerable<ScanWithIndexAndNotchInfo> GetAcceptableScans(double peptideMonoisotopicMass, MassDiffAcceptor searchMode)
        {
            foreach (AllowedIntervalWithNotch allowedIntervalWithNotch in searchMode.GetAllowedPrecursorMassIntervalsFromTheoreticalMass(peptideMonoisotopicMass).ToList())
            {
                DoubleRange allowedInterval = allowedIntervalWithNotch.AllowedInterval;
                int scanIndex = GetFirstScanWithMassOverOrEqual(allowedInterval.Minimum);
                if (scanIndex < ArrayOfSortedMS2Scans.Length)
                {
                    var scanMass = MyScanPrecursorMasses[scanIndex];
                    while (scanMass <= allowedInterval.Maximum)
                    {
                        var scan = ArrayOfSortedMS2Scans[scanIndex];
                        yield return new ScanWithIndexAndNotchInfo(scan, allowedIntervalWithNotch.Notch, scanIndex);
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

        private void AddPeptideCandidateToPsm(ScanWithIndexAndNotchInfo scan, object[] myLocks, double thisScore, OligoWithSetMods oligo, List<MatchedFragmentIon> matchedIons)
        {
            bool meetsScoreCutoff = thisScore >= CommonParameters.ScoreCutoff;

            // this is thread-safe because even if the score improves from another thread writing to this PSM,
            // the lock combined with AddOrReplace method will ensure thread safety
            if (meetsScoreCutoff)
            {
                // valid hit (met the cutoff score); lock the scan to prevent other threads from accessing it
                lock (myLocks[scan.ScanIndex])
                {
                    bool scoreImprovement = OligoSpectralMatches[scan.ScanIndex] == null || (thisScore - OligoSpectralMatches[scan.ScanIndex].RunnerUpScore) > -SpectralMatch.ToleranceForScoreDifferentiation;

                    if (scoreImprovement)
                    {
                        if (OligoSpectralMatches[scan.ScanIndex] == null)
                        {
                            OligoSpectralMatches[scan.ScanIndex] = new OligoSpectralMatch(oligo, scan.Notch, thisScore, scan.ScanIndex, scan.TheScan, CommonParameters, matchedIons);
                        }
                        else
                        {
                            OligoSpectralMatches[scan.ScanIndex].AddOrReplace(oligo, thisScore, scan.Notch, CommonParameters.ReportAllAmbiguity, matchedIons, 0);
                        }
                    }
                }
            }
        }
    }
}
