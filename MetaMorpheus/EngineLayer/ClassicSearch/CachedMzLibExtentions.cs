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
        return DigestionProducts ??= _bioPolymer.Digest(digestionParams, allKnownFixedModifications, variableModifications, silacLabels, turnoverLabels, topDownTruncationSearch)
            .Select(p => new CachedBioPolymerWithSetMods(p))
            .Cast<IBioPolymerWithSetMods>()
            .ToList();
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

    #endregion
}

// adaptor
public class CachedBioPolymerWithSetMods : IBioPolymerWithSetMods
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

    public bool Equals(IBioPolymerWithSetMods? other) => _withSetMods.Equals(other);

    #endregion
}



public class CachedProtein : Protein, IBioPolymer
{
    public CachedProtein(Protein prot) : base(prot, prot.BaseSequence) { }

    #region Passthrough Constructors
    public CachedProtein(string sequence, string accession, string organism = null, List<Tuple<string, string>> geneNames = null, IDictionary<int, List<Modification>> oneBasedModifications = null, List<TruncationProduct> proteolysisProducts = null, string name = null, string fullName = null, bool isDecoy = false, bool isContaminant = false, List<DatabaseReference> databaseReferences = null, List<SequenceVariation> sequenceVariations = null, List<SequenceVariation> appliedSequenceVariations = null, string sampleNameForVariants = null, List<DisulfideBond> disulfideBonds = null, List<SpliceSite> spliceSites = null, string databaseFilePath = null, bool addTruncations = false, string dataset = "unknown", string created = "unknown", string modified = "unknown", string version = "unknown", string xmlns = "http://uniprot.org/uniprot", UniProtSequenceAttributes uniProtSequenceAttributes = null) : base(sequence, accession, organism, geneNames, oneBasedModifications, proteolysisProducts, name, fullName, isDecoy, isContaminant, databaseReferences, sequenceVariations, appliedSequenceVariations, sampleNameForVariants, disulfideBonds, spliceSites, databaseFilePath, addTruncations, dataset, created, modified, version, xmlns, uniProtSequenceAttributes)
    {
    }

    public CachedProtein(Protein originalProtein, string newBaseSequence) : base(originalProtein, newBaseSequence)
    {
    }

    public CachedProtein(string variantBaseSequence, Protein protein, IEnumerable<SequenceVariation> appliedSequenceVariations, IEnumerable<TruncationProduct> applicableProteolysisProducts, IDictionary<int, List<Modification>> oneBasedModifications, string sampleNameForVariants) : base(variantBaseSequence, protein, appliedSequenceVariations, applicableProteolysisProducts, oneBasedModifications, sampleNameForVariants)
    {
    }

    #endregion

    public List<CachedPeptide>? DigestionProducts { get; private set; } = null;

    public new IEnumerable<IBioPolymerWithSetMods> Digest(IDigestionParams digestionParams, List<Modification> allKnownFixedModifications,
        List<Modification> variableModifications, List<SilacLabel> silacLabels = null, (SilacLabel startLabel, SilacLabel endLabel)? turnoverLabels = null, bool topDownTruncationSearch = false)
    {
        return DigestionProducts ??= base.Digest(digestionParams, allKnownFixedModifications,
            variableModifications, silacLabels, turnoverLabels, topDownTruncationSearch)
            .Select(p => new CachedPeptide((p as PeptideWithSetModifications)!))
            .ToList();
    }
}

public class CachedPeptide : PeptideWithSetModifications
{
    public CachedPeptide(PeptideWithSetModifications pep) : base(pep.Protein, pep.DigestionParams, pep.OneBasedStartResidueInProtein, pep.OneBasedEndResidueInProtein, pep.CleavageSpecificityForFdrCategory, pep.PeptideDescription, pep.MissedCleavages, pep.AllModsOneIsNterminus, pep.NumFixedMods, pep.BaseSequence, pep.PairedTargetDecoySequence)
    {
    }

    #region Passthrough Constructors
    public CachedPeptide(Protein protein, IDigestionParams digestionParams, int oneBasedStartResidueInProtein, int oneBasedEndResidueInProtein, CleavageSpecificity cleavageSpecificity, string peptideDescription, int missedCleavages, Dictionary<int, Modification> allModsOneIsNterminus, int numFixedMods, string baseSequence = null, string pairedTargetDecoySequence = null) : base(protein, digestionParams, oneBasedStartResidueInProtein, oneBasedEndResidueInProtein, cleavageSpecificity, peptideDescription, missedCleavages, allModsOneIsNterminus, numFixedMods, baseSequence, pairedTargetDecoySequence)
    {
    }

    public CachedPeptide(string sequence, Dictionary<string, Modification> allKnownMods, int numFixedMods = 0, IDigestionParams digestionParams = null, Protein p = null, int oneBasedStartResidueInProtein = -2147483648, int oneBasedEndResidueInProtein = -2147483648, int missedCleavages = -2147483648, CleavageSpecificity cleavageSpecificity = CleavageSpecificity.Full, string peptideDescription = null, string pairedTargetDecoySequence = null) : base(sequence, allKnownMods, numFixedMods, digestionParams, p, oneBasedStartResidueInProtein, oneBasedEndResidueInProtein, missedCleavages, cleavageSpecificity, peptideDescription, pairedTargetDecoySequence)
    {
    }

    #endregion

    public List<Product>? TheoreticalFragments { get; private set; } = null;

    public new void Fragment(DissociationType dissociationType, FragmentationTerminus fragmentationTerminus, List<Product> products, FragmentationParams? fragmentationParams = null)
    {
        if (TheoreticalFragments == null)
        {
            base.Fragment(dissociationType, fragmentationTerminus, products, fragmentationParams);
            TheoreticalFragments = products.ToList();
        }
        else
        {
            products.AddRange(TheoreticalFragments);
        }
    }
}
