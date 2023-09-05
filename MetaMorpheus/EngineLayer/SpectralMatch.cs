using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineLayer
{
    public abstract class SpectralMatch
    {
        public string BaseSequence { get; protected set; }
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
