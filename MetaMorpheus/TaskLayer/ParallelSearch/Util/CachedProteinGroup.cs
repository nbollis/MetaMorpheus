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
        ProteinGroupScore = proteinGroup.ProteinGroupScore,
        BestPeptideScore = proteinGroup.BestPeptideScore,
        QValue = proteinGroup.QValue,
        CumulativeTarget = proteinGroup.CumulativeTarget,
        CumulativeDecoy = proteinGroup.CumulativeDecoy,
        DisplayModsOnPeptides = proteinGroup.DisplayModsOnPeptides,
    };

    public ProteinGroup CreateRuntimeCopy()
    {
        return new ProteinGroup(
            [..ProteinGroup.Proteins],
            [..ProteinGroup.AllPeptides],
            [..ProteinGroup.UniquePeptides])
        {
            ProteinGroupScore = ProteinGroup.ProteinGroupScore,
            BestPeptideScore = ProteinGroup.BestPeptideScore,
            QValue = ProteinGroup.QValue,
            CumulativeTarget = ProteinGroup.CumulativeTarget,
            CumulativeDecoy = ProteinGroup.CumulativeDecoy,
            DisplayModsOnPeptides = ProteinGroup.DisplayModsOnPeptides,
        };
    }
}
