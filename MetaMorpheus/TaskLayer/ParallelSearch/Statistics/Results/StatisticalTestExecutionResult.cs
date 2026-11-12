#nullable enable
using System.Collections.Generic;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Packages the outputs of running configured statistical tests across all
/// transient database results. Carries both the generated result rows and
/// the list of tests that were removed due to insufficient data or runtime errors.
/// </summary>
public sealed class StatisticalTestExecutionResult
{
    public StatisticalTestExecutionResult(List<StatisticalTestResult> results, List<IStatisticalTest> testsToRemove)
    {
        Results = results;
        TestsToRemove = testsToRemove;
    }

    public List<StatisticalTestResult> Results { get; }

    public List<IStatisticalTest> TestsToRemove { get; }
}
