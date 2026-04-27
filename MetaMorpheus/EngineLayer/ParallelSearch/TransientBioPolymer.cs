using Omics;
using Omics.BioPolymer;
using Omics.Digestion;
using Omics.Modifications;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EngineLayer.ParallelSearch;
public class TransientBioPolymer : IBioPolymer
{
    private readonly IBioPolymer _bioPolymer;
    private readonly Func<TransientBioPolymer, IEnumerable<IBioPolymerWithSetMods>>? _digestionProductFactory;
    private readonly int? _precomputedPeptideCount;

    public IBioPolymer WrappedBioPolymer => _bioPolymer;
    public int? PeptideCount => DigestionProducts?.Count ?? _precomputedPeptideCount;
    public List<IBioPolymerWithSetMods>? DigestionProducts { get; private set; }

    public TransientBioPolymer(IBioPolymer bioPolymer)
        : this(bioPolymer, null, null, null)
    {
    }

    public TransientBioPolymer(
        IBioPolymer bioPolymer,
        int? peptideCount,
        IEnumerable<IBioPolymerWithSetMods>? digestionProducts = null,
        Func<TransientBioPolymer, IEnumerable<IBioPolymerWithSetMods>>? digestionProductFactory = null)
    {
        ArgumentNullException.ThrowIfNull(bioPolymer);

        _bioPolymer = bioPolymer;
        _precomputedPeptideCount = peptideCount;
        _digestionProductFactory = digestionProductFactory;

        if (digestionProducts != null)
        {
            DigestionProducts = NormalizeDigestionProducts(digestionProducts);
        }
    }

    public IEnumerable<IBioPolymerWithSetMods> Digest(IDigestionParams digestionParams, List<Modification> allKnownFixedModifications, List<Modification> variableModifications,
        List<SilacLabel>? silacLabels = null, (SilacLabel startLabel, SilacLabel endLabel)? turnoverLabels = null,
        bool topDownTruncationSearch = false)
    {
        if (DigestionProducts != null)
        {
            return DigestionProducts;
        }

        IEnumerable<IBioPolymerWithSetMods> digestionProducts = _digestionProductFactory != null
            ? _digestionProductFactory(this)
            : _bioPolymer.Digest(digestionParams, allKnownFixedModifications,
                variableModifications, silacLabels, turnoverLabels, topDownTruncationSearch);

        DigestionProducts = NormalizeDigestionProducts(digestionProducts);
        return DigestionProducts;
    }

    private List<IBioPolymerWithSetMods> NormalizeDigestionProducts(IEnumerable<IBioPolymerWithSetMods> digestionProducts)
    {
        List<IBioPolymerWithSetMods> normalizedProducts = [];
        foreach (IBioPolymerWithSetMods digestionProduct in digestionProducts)
        {
            if (digestionProduct is TransientBioPolymerWithSetMods transientDigestionProduct)
            {
                if (!ReferenceEquals(transientDigestionProduct.Parent, this))
                {
                    throw new InvalidOperationException("Transient digestion products must reference the current transient parent instance.");
                }

                normalizedProducts.Add(transientDigestionProduct);
            }
            else
            {
                normalizedProducts.Add(new TransientBioPolymerWithSetMods(digestionProduct, this));
            }
        }

        return normalizedProducts;
    }

    #region Wrapped Properties
    public string BaseSequence => _bioPolymer.BaseSequence;
    public int Length => _bioPolymer.Length;
    public string DatabaseFilePath => _bioPolymer.DatabaseFilePath;
    public bool IsDecoy => _bioPolymer.IsDecoy;
    public bool IsContaminant => _bioPolymer.IsContaminant;
    public bool IsEntrapment => _bioPolymer.IsEntrapment;
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

    public IBioPolymer CloneWithNewSequenceAndMods(string newBaseSequence, IDictionary<int, List<Modification>>? newMods) => new TransientBioPolymer(_bioPolymer.CloneWithNewSequenceAndMods(newBaseSequence, newMods));

    public TBioPolymerType CreateVariant<TBioPolymerType>(string variantBaseSequence, TBioPolymerType original,
        IEnumerable<SequenceVariation> appliedSequenceVariants, IEnumerable<TruncationProduct> applicableProteolysisProducts, IDictionary<int, List<Modification>> oneBasedModifications,
        string sampleNameForVariants) where TBioPolymerType : IHasSequenceVariants
        => _bioPolymer.CreateVariant(variantBaseSequence, original, appliedSequenceVariants, applicableProteolysisProducts, oneBasedModifications, sampleNameForVariants);

    public override bool Equals(object? obj)
    {
        // Unwrap if comparing to another TransientBioPolymer
        if (obj is TransientBioPolymer cachedOther)
            return _bioPolymer.Equals(cachedOther._bioPolymer);

        // Direct comparison if comparing to raw IBioPolymer
        if (obj is IBioPolymer protein)
            return _bioPolymer.Equals(protein);

        return false;
    }

    public override int GetHashCode() => _bioPolymer.GetHashCode();

    #endregion
}
