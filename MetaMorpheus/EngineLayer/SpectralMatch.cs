using MassSpectrometry;
using Proteomics.Fragmentation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chemistry;

namespace EngineLayer
{
    public abstract class SpectralMatch
    {
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


        public List<MatchedFragmentIon> MatchedFragmentIons { get; protected set; }

        public string NativeId { get; protected set; } // this is a property of the scan. used for mzID writing
        //One-based positions in peptide that are covered by fragments on both sides of amino acids
        public List<int> FragmentCoveragePositionInPeptide { get; protected set; }

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
    }
}
