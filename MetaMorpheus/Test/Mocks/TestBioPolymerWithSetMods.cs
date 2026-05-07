using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using MassSpectrometry;
using Omics;
using Omics.Digestion;
using Omics.Fragmentation;
using Omics.Modifications;

namespace Test.Mocks;

[ExcludeFromCodeCoverage]
public class TestBioPolymerWithSetMods : IBioPolymerWithSetMods
{
    public TestBioPolymerWithSetMods(string baseSequence, string fullSequence, IBioPolymer parent)
    {
        BaseSequence = baseSequence;
        FullSequence = fullSequence;
        Parent = parent;
        OneBasedStartResidue = 1;
        OneBasedEndResidue = baseSequence.Length;
        AllModsOneIsNterminus = new Dictionary<int, Modification>();
    }

    public string BaseSequence { get; }
    public string FullSequence { get; }
    public double MostAbundantMonoisotopicMass { get; } = 500.0;
    public double MonoisotopicMass { get; } = 500.0;
    public string SequenceWithChemicalFormulas => BaseSequence;
    public int OneBasedStartResidue { get; }
    public int OneBasedEndResidue { get; }
    public int MissedCleavages => 0;
    public string Description => string.Empty;
    public CleavageSpecificity CleavageSpecificityForFdrCategory { get; set; } = CleavageSpecificity.Full;
    public char PreviousResidue => '-';
    public char NextResidue => '-';
    public IDigestionParams DigestionParams => null!;
    public Dictionary<int, Modification> AllModsOneIsNterminus { get; }
    public int NumMods => 0;
    public int NumFixedMods => 0;
    public int NumVariableMods => 0;
    public int Length => BaseSequence.Length;
    public IBioPolymer Parent { get; }
    public Chemistry.ChemicalFormula ThisChemicalFormula => new();
    public char this[int zeroBasedIndex] => BaseSequence[zeroBasedIndex];

    public void Fragment(DissociationType d, FragmentationTerminus t, List<Product> p, IFragmentationParams? f = null) { }
    public void FragmentInternally(DissociationType d, int m, List<Product> p, IFragmentationParams? f = null) { }
    public IBioPolymerWithSetMods Localize(int i, double m) => this;
    public bool Equals(IBioPolymerWithSetMods? other) => other != null && FullSequence == other.FullSequence && Equals(Parent, other.Parent);
    public override bool Equals(object? obj) => obj is IBioPolymerWithSetMods other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(FullSequence, Parent?.Accession);
}
