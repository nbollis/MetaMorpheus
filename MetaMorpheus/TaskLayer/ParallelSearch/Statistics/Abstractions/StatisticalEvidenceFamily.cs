#nullable enable

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Categorizes statistical tests by the type of evidence they evaluate.
/// Used for family-level aggregation and reporting so that correlated tests
/// within the same evidence family can be grouped before cross-family ranking.
/// </summary>
public enum StatisticalEvidenceFamily
{
    CountEnrichment,
    AmbiguityOrTargetDecoy,
    ScoreDistribution,
    Fragmentation,
    RetentionTime,
    ProteinGroup,
    DeNovo
}
