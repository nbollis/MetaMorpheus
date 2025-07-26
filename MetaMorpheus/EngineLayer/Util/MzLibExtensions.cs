using MassSpectrometry;
using System.Collections.Generic;
using Omics;
using Omics.Digestion;
using Omics.Fragmentation;
using Omics.Modifications;
using Proteomics;
using Transcriptomics.Digestion;
using Proteomics.ProteolyticDigestion;
using Transcriptomics;

namespace EngineLayer;
public static partial class MzLibExtensions
{
    public static bool IsPeptide(this IBioPolymerWithSetMods withSetMods)
    {
        if (withSetMods is OligoWithSetMods)
            return false;
        return true;
    }

    public static Dictionary<DissociationType, List<ProductType>> ProductsFromDissociationType(this IBioPolymerWithSetMods withSetMods)
    {
        if (withSetMods.IsPeptide())
            return Omics.Fragmentation.Peptide.DissociationTypeCollection.ProductsFromDissociationType;
        else
            return Omics.Fragmentation.Oligo.DissociationTypeCollection.ProductsFromDissociationType;
    }

    public static double GetMassShiftFromProductType(this IBioPolymerWithSetMods withSetMods, ProductType type)
    {
        if (withSetMods.IsPeptide())
            return Omics.Fragmentation.Peptide.DissociationTypeCollection.GetMassShiftFromProductType(type);
        else
            return Omics.Fragmentation.Oligo.DissociationTypeCollection.GetRnaMassShiftFromProductType(type);
    }

    public static IBioPolymerWithSetMods CreateNew(this IBioPolymerWithSetMods withSetMods, IBioPolymer? parent = null, IDigestionParams? digestionParams = null, int? oneBasedStartResidue = null, int? oneBasedEndResidue = null, CleavageSpecificity? cleavageSpecificity = null, string? description = null, int? missedCleavages = null, Dictionary<int, Modification>? updatedMods = null, int? numFixedMods = null)
    {
        // Use existing values from withSetMods if the caller does not provide them  
        oneBasedStartResidue ??= withSetMods.OneBasedStartResidue;
        oneBasedEndResidue ??= withSetMods.OneBasedEndResidue;
        cleavageSpecificity ??= withSetMods.CleavageSpecificityForFdrCategory;
        description ??= withSetMods.Description ?? string.Empty;
        missedCleavages ??= withSetMods.MissedCleavages;
        updatedMods ??= withSetMods.AllModsOneIsNterminus;
        numFixedMods ??= withSetMods.NumFixedMods;

        if (withSetMods.IsPeptide())
        {
            var protein = (parent ?? withSetMods.Parent) as Protein;
            var digParams = digestionParams ?? withSetMods.DigestionParams as DigestionParams;
            return new PeptideWithSetModifications(protein, digParams, oneBasedStartResidue.Value,
                oneBasedEndResidue.Value,
                cleavageSpecificity.Value, description, missedCleavages.Value, updatedMods, numFixedMods.Value);
        }
        else
        {
            var rna = (parent ?? withSetMods.Parent) as NucleicAcid;
            var rnaDigestionParams = (digestionParams ?? withSetMods.DigestionParams) as RnaDigestionParams;
            return new OligoWithSetMods(rna!, rnaDigestionParams!, oneBasedStartResidue.Value,
                oneBasedEndResidue.Value, missedCleavages.Value,
                cleavageSpecificity.Value, updatedMods, numFixedMods.Value);
        }
    }
}
