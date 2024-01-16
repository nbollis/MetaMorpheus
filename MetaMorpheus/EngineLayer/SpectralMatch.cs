using MassSpectrometry;
using System.Collections.Generic;
using System.Linq;
using Chemistry;
using Omics.Fragmentation;
using EngineLayer.FdrAnalysis;
using Omics;
using Omics.Digestion;
using Transcriptomics;

namespace EngineLayer
{
    public abstract class SpectralMatch
    {
        public const double ToleranceForScoreDifferentiation = 1e-9;

        protected SpectralMatch(int scanIndex, Ms2ScanWithSpecificMass scan,
            CommonParameters commonParameters, double xcorr = 0)
        {
            ScanIndex = scanIndex;
            FullFilePath = scan.FullFilePath;
            ScanNumber = scan.OneBasedScanNumber;
            PrecursorScanNumber = scan.OneBasedPrecursorScanNumber;
            ScanRetentionTime = scan.RetentionTime;
            ScanExperimentalPeaks = scan.NumPeaks;
            TotalIonCurrent = scan.TotalIonCurrent;
            ScanPrecursorCharge = scan.PrecursorCharge;
            ScanPrecursorMonoisotopicPeakMz = scan.PrecursorMonoisotopicPeakMz;
            ScanPrecursorMass = scan.PrecursorMass;
            Xcorr = xcorr;
            NativeId = scan.NativeId;
            RunnerUpScore = commonParameters.ScoreCutoff;
            MsDataScan = scan.TheScan;
            SpectralAngle = -1;

            FragmentCoveragePositionInPeptide = new List<int>();
        }

        public MsDataScan MsDataScan { get; set; }
        public string BaseSequence { get; protected set; }
        public string FullSequence { get; protected set; }
        public string EssentialSequence { get; protected set; }
        public int ScanNumber { get; protected set; }
        public int? PrecursorScanNumber { get; protected set; }
        public double ScanRetentionTime { get; protected set; }
        public int ScanExperimentalPeaks { get; protected set; }
        public double TotalIonCurrent { get; protected set; }
        public int ScanPrecursorCharge { get; protected set; }
        public double ScanPrecursorMonoisotopicPeakMz { get; protected set; }
        public double ScanPrecursorMass { get; protected set; }
        public string FullFilePath { get; protected set; }
        public int ScanIndex { get; protected set; }

        public ChemicalFormula ModsChemicalFormula { get; protected set; } // these fields will be null if they are ambiguous
        public int? Notch { get; protected set; }
        public double Score { get; protected set; }
        public double DeltaScore => (Score - RunnerUpScore);
        public double Xcorr { get; protected set; }
        public double SpectralAngle { get; set; }
        public double RunnerUpScore { get; protected set; }
        public bool IsDecoy { get; protected set; }
        public bool IsContaminant { get; protected set; }
        public int PsmCount { get; internal set; }
        public string Organism { get; protected set; }
        public int? OneBasedStartResidue { get; protected set; }
        public int? OneBasedEndResidue { get; protected set; }
        public Dictionary<string, int> ModsIdentified { get; protected set; } // these should never be null under normal circumstances
        public List<double> LocalizedScores { get; internal set; }
        public List<MatchedFragmentIon> MatchedFragmentIons { get; protected set; }
        public string NativeId { get; protected set; } // this is a property of the scan. used for mzID writing
        //One-based positions in peptide that are covered by fragments on both sides of amino acids
        public List<int> FragmentCoveragePositionInPeptide { get; protected set; }
        public IDigestionParams DigestionParams { get; protected set; }

        protected SpectralMatch() { }

        public void GetCoverage(List<int> leftFragmentNumbers, List<int> rightFragmentNumbers)
        {
            //Create a hashset to store the covered amino acid positions
            HashSet<int> fragmentCoveredAminoAcids = new();

            //Check N term frags first
            if (leftFragmentNumbers.Any())
            {
                leftFragmentNumbers.Sort();

                //if the final NFragment is present, last AA is covered
                if (leftFragmentNumbers.Contains(this.BaseSequence.Length - 1))
                {
                    fragmentCoveredAminoAcids.Add(this.BaseSequence.Length);
                }

                // if the first NFragment is present, first AA is covered
                if (leftFragmentNumbers.Contains(1))
                {
                    fragmentCoveredAminoAcids.Add(1);
                }

                //Check all amino acids except for the last one in the list
                for (int i = 0; i < leftFragmentNumbers.Count - 1; i++)
                {
                    //sequential AA, second one is covered
                    if (leftFragmentNumbers[i + 1] - leftFragmentNumbers[i] == 1)
                    {
                        fragmentCoveredAminoAcids.Add(leftFragmentNumbers[i + 1]);
                    }

                    //check to see if the position is covered from both directions, inclusive
                    if (rightFragmentNumbers.Contains(leftFragmentNumbers[i + 1]))
                    {
                        fragmentCoveredAminoAcids.Add(leftFragmentNumbers[i + 1]);
                    }

                    //check to see if the position is covered from both directions, exclusive
                    if (rightFragmentNumbers.Contains(leftFragmentNumbers[i + 1] + 2))
                    {
                        fragmentCoveredAminoAcids.Add(leftFragmentNumbers[i + 1] + 1);
                    }
                }

            }

            //Check C term frags
            if (rightFragmentNumbers.Any())
            {
                rightFragmentNumbers.Sort();

                //if the second AA is present, the first AA is covered
                if (rightFragmentNumbers.Contains(2))
                {
                    fragmentCoveredAminoAcids.Add(1);
                }

                //if the last AA is present, the final AA is covered
                if (rightFragmentNumbers.Contains(this.BaseSequence.Length))
                {
                    fragmentCoveredAminoAcids.Add(this.BaseSequence.Length);
                }

                //check all amino acids except for the last one in the list
                for (int i = 0; i < rightFragmentNumbers.Count - 1; i++)
                {
                    //sequential AA, the first one is covered
                    if (rightFragmentNumbers[i + 1] - rightFragmentNumbers[i] == 1)
                    {
                        fragmentCoveredAminoAcids.Add(rightFragmentNumbers[i]);
                    }
                }
            }

            //store in PSM
            var fragmentCoveredAminoAcidsList = fragmentCoveredAminoAcids.ToList();
            fragmentCoveredAminoAcidsList.Sort();
            FragmentCoveragePositionInPeptide = fragmentCoveredAminoAcidsList;
        }


        #region Search

        public abstract void ResolveAllAmbiguities();

        public abstract void AddOrReplace(IBioPolymerWithSetMods owsm, double newScore, int notch, bool reportAllAmbiguity,
            List<MatchedFragmentIon> matchedFragmentIons, double newXcorr);

        #endregion


        #region FDR

        public FdrInfo FdrInfo { get; protected set; }
        public void SetFdrValues(double cumulativeTarget, double cumulativeDecoy, double qValue, double cumulativeTargetNotch, double cumulativeDecoyNotch, double qValueNotch, double pep, double pepQValue)
        {
            FdrInfo = new FdrInfo
            {
                CumulativeTarget = cumulativeTarget,
                CumulativeDecoy = cumulativeDecoy,
                QValue = qValue,
                CumulativeTargetNotch = cumulativeTargetNotch,
                CumulativeDecoyNotch = cumulativeDecoyNotch,
                QValueNotch = qValueNotch,
                PEP = pep,
                PEP_QValue = pepQValue
            };
        }

        #endregion

        #region IO

        public static Dictionary<string, string> DataDictionary(SpectralMatch psm, IReadOnlyDictionary<string, int> ModsToWritePruned)
        {
            Dictionary<string, string> s = new Dictionary<string, string>();
            PsmTsvWriter.AddBasicMatchData(s, psm);
            PsmTsvWriter.AddPeptideSequenceData(s, psm, ModsToWritePruned);
            PsmTsvWriter.AddMatchedIonsData(s, psm?.MatchedFragmentIons);
            PsmTsvWriter.AddMatchScoreData(s, psm);
            return s;
        }

        public static string GetTabSeparatedHeader()
        {
            return string.Join("\t", DataDictionary(null, null).Keys);
        }

        public override string ToString()
        {
            return ToString(new Dictionary<string, int>());
        }

        public string ToString(IReadOnlyDictionary<string, int> ModstoWritePruned)
        {
            return string.Join("\t", DataDictionary(this, ModstoWritePruned).Values);
        }

        #endregion
    }
}
