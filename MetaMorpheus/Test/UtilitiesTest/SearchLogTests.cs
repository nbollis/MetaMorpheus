using EngineLayer;
using EngineLayer.SpectrumMatch;
using NUnit.Framework;
using Omics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using Proteomics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Test.UtilitiesTest;

[TestFixture]
public class SearchLogTests
{

    private static BioPolymerNotchFragmentIonComparer comparer;
    private static Protein targetProtein;
    private static Protein decoyProtein;
    private static PeptideWithSetModifications targetPwsm;
    private static PeptideWithSetModifications decoyPwsm;


    [SetUp]
    public static void Setup()
    {
        comparer = new BioPolymerNotchFragmentIonComparer();
        targetProtein = new Protein("PEPTIDEK", "accession");
        decoyProtein = new Protein("PEPTIDEK", "decoy", isDecoy: true);
        targetPwsm = new PeptideWithSetModifications("PEPTIDEK", null, p: targetProtein);
        decoyPwsm = new PeptideWithSetModifications("PEPTIDEK", null, p: decoyProtein);
    }
}