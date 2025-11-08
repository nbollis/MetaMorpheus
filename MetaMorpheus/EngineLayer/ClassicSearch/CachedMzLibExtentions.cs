#nullable enable
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
