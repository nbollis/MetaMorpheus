#nullable enable
using Chemistry;
using MassSpectrometry;
using Omics;
using Omics.BioPolymer;
using Omics.Digestion;
using Omics.Fragmentation;
using Omics.Modifications;
using Proteomics;
using Proteomics.ProteolyticDigestion;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EngineLayer.ClassicSearch;

// Adaptor class to cache properties of IBioPolymer
public class CachedBioPolymer : IBioPolymer
{
    private readonly IBioPolymer _bioPolymer;
    public List<IBioPolymerWithSetMods>? DigestionProducts { get; private set; } = null;

    public CachedBioPolymer(IBioPolymer bioPolymer)
    {
        _bioPolymer = bioPolymer;
    }

    public IEnumerable<IBioPolymerWithSetMods> Digest(IDigestionParams digestionParams, List<Modification> allKnownFixedModifications, List<Modification> variableModifications,
        List<SilacLabel>? silacLabels = null, (SilacLabel startLabel, SilacLabel endLabel)? turnoverLabels = null,
        bool topDownTruncationSearch = false)
    {

        if (DigestionProducts == null)
        {
            // Pre-cache the digestion products
            DigestionProducts = _bioPolymer.Digest(digestionParams, allKnownFixedModifications, variableModifications, silacLabels, turnoverLabels, topDownTruncationSearch)
                .Select(p => new CachedBioPolymerWithSetMods(p))
                .Cast<IBioPolymerWithSetMods>()
                .ToList();
        }

        return DigestionProducts;
    }

    #region Wrapped Properties
    public string BaseSequence => _bioPolymer.BaseSequence;
    public int Length => _bioPolymer.Length;
    public string DatabaseFilePath => _bioPolymer.DatabaseFilePath;
    public bool IsDecoy => _bioPolymer.IsDecoy;
    public bool IsContaminant => _bioPolymer.IsContaminant;
    public string Organism => _bioPolymer.Organism;
    public string Accession => _bioPolymer.Accession;
    public List<Tuple<string, string>> GeneNames => _bioPolymer.GeneNames;
    public IDictionary<int, List<Modification>> OneBasedPossibleLocalizedModifications => _bioPolymer.OneBasedPossibleLocalizedModifications;
    public string Name => _bioPolymer.Name;
    public string FullName => _bioPolymer.FullName;
    public string SampleNameForVariants => _bioPolymer.SampleNameForVariants;
    public IDictionary<int, List<Modification>> OriginalNonVariantModifications
    {
        get => _bioPolymer.OriginalNonVariantModifications;
        set => _bioPolymer.OriginalNonVariantModifications = value;
    }
    public IBioPolymer ConsensusVariant => _bioPolymer.ConsensusVariant;
    public List<SequenceVariation> AppliedSequenceVariations => _bioPolymer.AppliedSequenceVariations;
    public List<SequenceVariation> SequenceVariations => _bioPolymer.SequenceVariations;
    public List<TruncationProduct> TruncationProducts => _bioPolymer.TruncationProducts;

    #endregion

    #region Wrapped Methods

    public IBioPolymer CloneWithNewSequenceAndMods(string newBaseSequence, IDictionary<int, List<Modification>>? newMods) => new CachedBioPolymer(_bioPolymer.CloneWithNewSequenceAndMods(newBaseSequence, newMods));

    public TBioPolymerType CreateVariant<TBioPolymerType>(string variantBaseSequence, TBioPolymerType original,
        IEnumerable<SequenceVariation> appliedSequenceVariants, IEnumerable<TruncationProduct> applicableProteolysisProducts, IDictionary<int, List<Modification>> oneBasedModifications,
        string sampleNameForVariants) where TBioPolymerType : IHasSequenceVariants 
        => _bioPolymer.CreateVariant(variantBaseSequence, original, appliedSequenceVariants, applicableProteolysisProducts, oneBasedModifications, sampleNameForVariants);

    public override bool Equals(object? obj)
    {
        // Unwrap if comparing to another CachedBioPolymer
        if (obj is CachedBioPolymer cachedOther)
            return _bioPolymer.Equals(cachedOther._bioPolymer);

        // Direct comparison if comparing to raw IBioPolymer
        if (obj is IBioPolymer protein)
            return _bioPolymer.Equals(protein);

        return false;
    }

    public override int GetHashCode()
    {
        return BaseSequence.GetHashCode();
    }

    #endregion
}

// adaptor
public class CachedBioPolymerWithSetMods : IBioPolymerWithSetMods, IEquatable<PeptideWithSetModifications>, IEquatable<IBioPolymerWithSetMods>
{
    private readonly IBioPolymerWithSetMods _withSetMods;
    public List<Product>? TheoreticalFragments { get; private set; } = null;
    public CachedBioPolymerWithSetMods(IBioPolymerWithSetMods withSetMods)
    {
        _withSetMods = withSetMods;
    }

    public void Fragment(DissociationType dissociationType, FragmentationTerminus fragmentationTerminus, List<Product> products, FragmentationParams? fragmentationParams = null)
    {
        if (TheoreticalFragments == null)
        {
            _withSetMods.Fragment(dissociationType, fragmentationTerminus, products, fragmentationParams);
            TheoreticalFragments = products.ToList();
        }
        else
        {
            products.AddRange(TheoreticalFragments);
        }
    }

    #region Wrapped Properties

    public string BaseSequence => _withSetMods.BaseSequence;
    public string FullSequence => _withSetMods.FullSequence;

    public double MostAbundantMonoisotopicMass => _withSetMods.MostAbundantMonoisotopicMass;
    public string SequenceWithChemicalFormulas => _withSetMods.SequenceWithChemicalFormulas;
    public int OneBasedStartResidue => _withSetMods.OneBasedStartResidue;
    public int OneBasedEndResidue => _withSetMods.OneBasedEndResidue;
    public int MissedCleavages => _withSetMods.MissedCleavages;
    public string Description => _withSetMods.Description;
    public CleavageSpecificity CleavageSpecificityForFdrCategory
    {
        get => _withSetMods.CleavageSpecificityForFdrCategory;
        set => _withSetMods.CleavageSpecificityForFdrCategory = value;
    }
    public char PreviousResidue => _withSetMods.PreviousResidue;
    public char NextResidue => _withSetMods.NextResidue;
    public IDigestionParams DigestionParams => _withSetMods.DigestionParams;
    public Dictionary<int, Modification> AllModsOneIsNterminus => _withSetMods.AllModsOneIsNterminus;
    public int NumMods => _withSetMods.NumMods;
    public int NumFixedMods => _withSetMods.NumFixedMods;
    public int NumVariableMods => _withSetMods.NumVariableMods;
    public int Length => _withSetMods.Length;
    public IBioPolymer Parent => _withSetMods.Parent;
    public Protein Protein => _withSetMods.Parent as Protein;
    public double MonoisotopicMass => _withSetMods.MonoisotopicMass;
    public ChemicalFormula ThisChemicalFormula => _withSetMods.ThisChemicalFormula;

    #endregion

    #region Wrapped Methods

    public IBioPolymerWithSetMods Localize(int indexOfMass, double massToLocalize) => _withSetMods.Localize(indexOfMass, massToLocalize);

    public void FragmentInternally(DissociationType dissociationType, int minLengthOfFragments, List<Product> products,
        FragmentationParams? fragmentationParams = null) => _withSetMods.FragmentInternally(dissociationType, minLengthOfFragments, products, fragmentationParams);

    // Direct copy of PeptideWithSetModifications equality, important for parsimony in MetaMorpheus
    public bool Equals(IBioPolymerWithSetMods? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (other.GetType() != GetType()) return false;

        // for those constructed from sequence and mods only
        if (Parent is null && other.Parent is null)
            return FullSequence.Equals(other.FullSequence);

        return FullSequence == other.FullSequence
               && Equals(DigestionParams?.DigestionAgent, other.DigestionParams?.DigestionAgent)
               // These last two are important for parsimony in MetaMorpheus
               && OneBasedStartResidue == other!.OneBasedStartResidue
               && Equals(Parent?.Accession, other.Parent?.Accession);
    }

    public bool Equals(PeptideWithSetModifications? other) => Equals(other as IBioPolymerWithSetMods);

    // Funnel all equality checks through the IBioPolymerWithSetMods equality
    public override bool Equals(object? obj)
    {
        if (obj is CachedBioPolymerWithSetMods cachedOther)
            return Equals(cachedOther._withSetMods);

        if (obj is IBioPolymerWithSetMods pwsm)
            return Equals(pwsm);

        return false;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_withSetMods.FullSequence);
        hash.Add(_withSetMods.OneBasedStartResidue);
        if (_withSetMods.Parent?.Accession != null)
        {
            hash.Add(_withSetMods.Parent.Accession);
        }
        if (_withSetMods.DigestionParams?.DigestionAgent != null)
        {
            hash.Add(_withSetMods.DigestionParams.DigestionAgent);
        }
        return hash.ToHashCode();
    }

    #endregion
}
