using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using EngineLayer;
using Proteomics;
using Proteomics.ProteolyticDigestion;
using Test.RyanJulain;
using UsefulProteomicsDatabases;

namespace Test.RyanJulian;

public class CysteineFragmentExplorer : RadicalFragmentationExplorer
{
    protected override string AnalysisType => "Cysteine";

    public CysteineFragmentExplorer(string databasePath, int numberOfMods, string species, int ambiguityLevel, int fragmentationEvents) 
        : base(databasePath, numberOfMods, species, fragmentationEvents, ambiguityLevel)
    {

        
    }

    public DigestionParams digestionParameters;


    public override IEnumerable<PrecursorFragmentMassSet> GeneratePrecursorFragmentMasses(Protein protein)
    {

        foreach (var proteoform in protein.Digest(PrecursorDigestionParams, fixedMods, variableMods)
                     .DistinctBy(p => p.FullSequence).Where(p => p.MonoisotopicMass < 60000))
        {
            var mods = proteoform.AllModsOneIsNterminus
                .ToDictionary(p => p.Key, p => new List<Modification>() { p.Value });

            // TODO: Fragment the proteoform +- 5 amino acids from each cysteine and return those as fragment peaks

            //yield return new PrecursorFragmentMassSet(proteoform.MonoisotopicMass, proteoform.Protein.Accession, fragments, proteoform.FullSequence);
        }

        return null;
    }
}