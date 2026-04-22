using EngineLayer;
using Omics;
using System.Collections.Generic;

namespace TaskLayer.ParallelSearch;

/// <summary>
/// Represents a cached version of a ProteinGroup for use in parallel search.
/// </summary>
internal class CachedProteinGroup(ProteinGroup proteinGroup)
{
    private ProteinGroup ProteinGroup { get; } = new ProteinGroup(
        [..proteinGroup.Proteins],
        [..proteinGroup.AllPeptides],
        [..proteinGroup.UniquePeptides])
    {
        AllPsmsBelowOnePercentFDR = [..proteinGroup.AllPsmsBelowOnePercentFDR],
        ProteinGroupScore = proteinGroup.ProteinGroupScore,
        BestPeptideScore = proteinGroup.BestPeptideScore,
        BestPeptideQValue = proteinGroup.BestPeptideQValue,
        BestPeptidePEP = proteinGroup.BestPeptidePEP,
        QValue = proteinGroup.QValue,
        CumulativeTarget = proteinGroup.CumulativeTarget,
        CumulativeDecoy = proteinGroup.CumulativeDecoy,
        DisplayModsOnPeptides = proteinGroup.DisplayModsOnPeptides,
    };

    public ProteinGroup GetProteinGroup() => ProteinGroup;

    public ProteinGroup CreateRuntimeCopy()
    {
        return new ProteinGroup(
            [..ProteinGroup.Proteins],
            [..ProteinGroup.AllPeptides],
            [..ProteinGroup.UniquePeptides])
        {
            AllPsmsBelowOnePercentFDR = [..ProteinGroup.AllPsmsBelowOnePercentFDR],
            ProteinGroupScore = ProteinGroup.ProteinGroupScore,
            BestPeptideScore = ProteinGroup.BestPeptideScore,
            BestPeptideQValue = ProteinGroup.BestPeptideQValue,
            BestPeptidePEP = ProteinGroup.BestPeptidePEP,
            QValue = ProteinGroup.QValue,
            CumulativeTarget = ProteinGroup.CumulativeTarget,
            CumulativeDecoy = ProteinGroup.CumulativeDecoy,
            DisplayModsOnPeptides = ProteinGroup.DisplayModsOnPeptides,
        };
    }
}
