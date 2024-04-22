using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    public override IEnumerable<PrecursorFragmentMassSet> GeneratePrecursorFragmentMasses(Protein protein)
    {
        return null;
    }
}