#nullable enable
using System.Collections.Generic;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Container for the outputs of two-stage hierarchical p-value combination.
/// Holds per-family combined results and the final across-family combined result,
/// along with a lookup dictionary keyed by cache key (e.g. "Combined_CountEnrichment").
/// </summary>
public sealed class HierarchicalCombinedScoringResult
{
    public HierarchicalCombinedScoringResult(
        Dictionary<string, List<StatisticalTestResult>> resultsByCacheKey,
        List<StatisticalTestResult> familyCombinedResults,
        List<StatisticalTestResult> overallCombinedResults)
    {
        ResultsByCacheKey = resultsByCacheKey;
        FamilyCombinedResults = familyCombinedResults;
        OverallCombinedResults = overallCombinedResults;
    }

    public Dictionary<string, List<StatisticalTestResult>> ResultsByCacheKey { get; }

    public List<StatisticalTestResult> FamilyCombinedResults { get; }

    public List<StatisticalTestResult> OverallCombinedResults { get; }
}
