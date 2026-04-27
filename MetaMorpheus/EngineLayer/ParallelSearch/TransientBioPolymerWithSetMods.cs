using EngineLayer.ClassicSearch;
using MassSpectrometry;
using Omics;
using Omics.Digestion;
using Omics.Fragmentation;
using Omics.Modifications;
using Proteomics;
using Proteomics.ProteolyticDigestion;
using System;
using System.Collections.Generic;
using System.Linq;
using Chemistry;

namespace EngineLayer.ParallelSearch;
public class TransientBioPolymerWithSetMods : IBioPolymerWithSetMods, IEquatable<PeptideWithSetModifications>, IEquatable<IBioPolymerWithSetMods>
{
    private readonly IBioPolymerWithSetMods _withSetMods;
    private readonly Func<IReadOnlyList<Product>>? _fragmentFactory;
    private readonly IBioPolymer _parent;

    public IBioPolymerWithSetMods WrappedBioPolymerWithSetMods => _withSetMods;
    public List<Product>? TheoreticalFragments { get; private set; }

    public TransientBioPolymerWithSetMods(IBioPolymerWithSetMods withSetMods)
        : this(withSetMods, withSetMods.Parent)
    {
    }

    public TransientBioPolymerWithSetMods(
        IBioPolymerWithSetMods withSetMods,
        IBioPolymer parent,
        IEnumerable<Product>? theoreticalFragments = null,
        Func<IReadOnlyList<Product>>? fragmentFactory = null)
    {
        ArgumentNullException.ThrowIfNull(withSetMods);

        _withSetMods = withSetMods;
        _parent = parent ?? withSetMods.Parent;
        _fragmentFactory = fragmentFactory;

        if (theoreticalFragments != null)
        {
            TheoreticalFragments = [.. theoreticalFragments];
        }
    }

    public void Fragment(DissociationType dissociationType, FragmentationTerminus fragmentationTerminus, List<Product> products, IFragmentationParams? fragmentationParams = null)
    {
        if (TheoreticalFragments == null)
        {
            if (_fragmentFactory != null)
            {
                TheoreticalFragments = [.. _fragmentFactory()];
            }
            else
            {
                List<Product> computedProducts = [];
                _withSetMods.Fragment(dissociationType, fragmentationTerminus, computedProducts, fragmentationParams);
                TheoreticalFragments = computedProducts;
            }
        }

        products.AddRange(TheoreticalFragments);
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
    public IBioPolymer Parent => _parent;
    public Protein Protein => Parent switch
    {
        TransientBioPolymer transientParent => transientParent.WrappedBioPolymer as Protein,
        Protein proteinParent => proteinParent,
        _ => _withSetMods.Parent as Protein,
    };
    public double MonoisotopicMass => _withSetMods.MonoisotopicMass;
    public ChemicalFormula ThisChemicalFormula => _withSetMods.ThisChemicalFormula;

    #endregion

    #region Wrapped Methods

    public IBioPolymerWithSetMods Localize(int indexOfMass, double massToLocalize)
        => new TransientBioPolymerWithSetMods(_withSetMods.Localize(indexOfMass, massToLocalize), Parent);

    public void FragmentInternally(DissociationType dissociationType, int minLengthOfFragments, List<Product> products,
        IFragmentationParams? fragmentationParams = null) => _withSetMods.FragmentInternally(dissociationType, minLengthOfFragments, products, fragmentationParams);

    // Direct copy of PeptideWithSetModifications equality, important for parsimony in MetaMorpheus
    public bool Equals(IBioPolymerWithSetMods? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

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
        if (obj is TransientBioPolymerWithSetMods cachedOther)
            return Equals((IBioPolymerWithSetMods)cachedOther);

        if (obj is IBioPolymerWithSetMods pwsm)
            return Equals(pwsm);

        return false;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_withSetMods.FullSequence);
        hash.Add(_withSetMods.OneBasedStartResidue);
        if (Parent?.Accession != null)
        {
            hash.Add(Parent.Accession);
        }
        if (_withSetMods.DigestionParams?.DigestionAgent != null)
        {
            hash.Add(_withSetMods.DigestionParams.DigestionAgent);
        }
        return hash.ToHashCode();
    }

    #endregion
}
