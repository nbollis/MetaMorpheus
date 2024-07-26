using System;
using System.Collections.Generic;
using System.Linq;
using Proteomics;
using Proteomics.AminoAcidPolymer;

namespace Test.RyanJulian;

public class CysteineFragmentExplorer : RadicalFragmentationExplorer
{
    public override string AnalysisType => "Cysteine";
    public double CysteineToSelect = 1;

    public CysteineFragmentExplorer(string databasePath, int numberOfMods, string species, int ambiguityLevel, int fragmentationEvents, string? baseDirectory = null) 
        : base(databasePath, numberOfMods, species, fragmentationEvents, ambiguityLevel, baseDirectory)
    {

        
    }



    public override IEnumerable<PrecursorFragmentMassSet> GeneratePrecursorFragmentMasses(Protein protein)
    {
        var random = new Random();

        // add on the modifications
        foreach (var proteoform in protein.Digest(PrecursorDigestionParams, fixedMods, variableMods)
                     .DistinctBy(p => p.FullSequence).Where(p => p.MonoisotopicMass < 60000))
        {
            var mods = proteoform.AllModsOneIsNterminus
                .ToDictionary(p => p.Key, p => new List<Modification>() { p.Value });

            // get a random number between 3 and cysMax to split on 
            var cysCount = proteoform.BaseSequence.Count(p => p == 'C');
            if (cysCount == 0)
            {
                yield return new PrecursorFragmentMassSet(proteoform.MonoisotopicMass, proteoform.Protein.Accession,
                    new List<double> { proteoform.MonoisotopicMass }, proteoform.FullSequence);
            }
            else
            {
                // select a cysteine at random 
                int cysIndex;
                do
                {
                    cysIndex = proteoform.BaseSequence
                        .IndexOf('C', random.Next(0, proteoform.BaseSequence.Length));
                } while (cysIndex == -1);

                // select MaxFragmentationEvents indices within +- 5 residues of the cysteine
                var maxFrag = cysIndex == 0 ? 5 : MaximumFragmentationEvents;
                maxFrag = Math.Min(maxFrag, proteoform.BaseSequence.Length-1);
                int[] indicesToFragment = new int[maxFrag];
                for (int i = 0; i < maxFrag; i++)
                {
                    int index;
                    do
                    {
                        index = random.Next(cysIndex - 5, cysIndex + 6);
                    } while (index < 0 || index >= proteoform.BaseSequence.Length || indicesToFragment.Contains(index));
                    indicesToFragment[i] = index;
                }

                // split the protein sequence and mods based upon indices to fragment
                // foreach split there will be 2 masses, one the left side and one the right side
                // the mass for each split will be the sum of the sequence and then the sum of mods
                double[] fragmentMasses = new double[maxFrag * 2];
                for (int i = 0; i < maxFrag; i++)
                {
                    var leftSide = proteoform.BaseSequence.Substring(0, indicesToFragment[i]);
                    var leftMods = mods
                        .Where(p => p.Key < indicesToFragment[i])
                        .ToDictionary(p => p.Key, p => p.Value);
                    var leftSequence = new Peptide(leftSide);
                    var leftMass = leftSequence.MonoisotopicMass +
                                   leftMods.Values.Sum(p => p.Sum(m => m.MonoisotopicMass))!.Value;

                    var rightSide = proteoform.BaseSequence.Substring(indicesToFragment[i]);
                    var rightMods = mods
                        .Where(p => p.Key >= indicesToFragment[i])
                        .ToDictionary(p => p.Key - indicesToFragment[i], p => p.Value);
                    var rightSequence = new Peptide(rightSide);
                    var rightMass = rightSequence.MonoisotopicMass +
                                    rightMods.Values.Sum(p => p.Sum(m => m.MonoisotopicMass))!.Value;

                    fragmentMasses[i * 2] = leftMass;
                    fragmentMasses[i * 2 + 1] = rightMass;
                }

                yield return new PrecursorFragmentMassSet(proteoform.MonoisotopicMass, proteoform.Protein.Accession,
                    fragmentMasses.ToList(), proteoform.FullSequence);
            }
        }
    }
}