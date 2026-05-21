#nullable enable
using System.Collections.Generic;

namespace TaskLayer.ParallelSearch.Statistics;

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
