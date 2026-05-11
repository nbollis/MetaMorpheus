using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Omics;
using Omics.BioPolymer;
using Omics.Digestion;
using Omics.Modifications;

namespace Test.Mocks;

[ExcludeFromCodeCoverage]
public class TestBioPolymer : IBioPolymer
{
    public TestBioPolymer(
        string baseSequence = "GUACUGCCUCUAGUGAAGCA",
        string accession = "",
        IDictionary<int, List<Modification>>? oneBasedPossibleLocalizedModifications = null)
    {
        BaseSequence = baseSequence;
        Accession = accession;
        OneBasedPossibleLocalizedModifications = oneBasedPossibleLocalizedModifications ?? new Dictionary<int, List<Modification>>();
        Length = baseSequence.Length;
        DatabaseFilePath = string.Empty;
        IsDecoy = false;
        IsContaminant = false;
        Organism = string.Empty;
        GeneNames = new List<Tuple<string, string>>();
        Name = string.Empty;
        FullName = string.Empty;
        SampleNameForVariants = string.Empty;
        OriginalNonVariantModifications = new Dictionary<int, List<Modification>>();
        ConsensusVariant = this;
        AppliedSequenceVariations = new List<SequenceVariation>();
        SequenceVariations = new List<SequenceVariation>();
        TruncationProducts = new List<TruncationProduct>();
    }

    public string BaseSequence { get; }
    public int Length { get; }
    public string DatabaseFilePath { get; }
    public bool IsDecoy { get; }
    public bool IsContaminant { get; }
    public bool IsEntrapment { get; } = false;
    public string Organism { get; }
    public string Accession { get; set; }
    public List<Tuple<string, string>> GeneNames { get; }
    public IDictionary<int, List<Modification>> OneBasedPossibleLocalizedModifications { get; }
    public string Name { get; }
    public string FullName { get; }
    public string SampleNameForVariants { get; }
    public IDictionary<int, List<Modification>> OriginalNonVariantModifications { get; set; }
    public IBioPolymer ConsensusVariant { get; }
    public List<SequenceVariation> AppliedSequenceVariations { get; }
    public List<SequenceVariation> SequenceVariations { get; }
    public List<TruncationProduct> TruncationProducts { get; }

    public IEnumerable<IBioPolymerWithSetMods> Digest(IDigestionParams digestionParams, List<Modification> allKnownFixedModifications,
        List<Modification> variableModifications, List<SilacLabel>? silacLabels = null,
        (SilacLabel startLabel, SilacLabel endLabel)? turnoverLabels = null, bool topDownTruncationSearch = false)
        => throw new NotImplementedException();

    public IBioPolymer CloneWithNewSequenceAndMods(string newBaseSequence, IDictionary<int, List<Modification>>? newMods)
        => new TestBioPolymer(newBaseSequence, Accession, newMods);

    public TBioPolymerType CreateVariant<TBioPolymerType>(string variantBaseSequence, TBioPolymerType original,
        IEnumerable<SequenceVariation> appliedSequenceVariants, IEnumerable<TruncationProduct> applicableProteolysisProducts,
        IDictionary<int, List<Modification>> oneBasedModifications, string sampleNameForVariants)
        where TBioPolymerType : IHasSequenceVariants => original;

    public bool Equals(IBioPolymer? other) => other != null && Accession == other.Accession && BaseSequence == other.BaseSequence;
    public override bool Equals(object? obj) => obj is IBioPolymer other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Accession, BaseSequence);
}
