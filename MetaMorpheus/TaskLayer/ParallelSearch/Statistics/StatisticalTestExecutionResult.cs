#nullable enable
using System.Collections.Generic;

namespace TaskLayer.ParallelSearch.Statistics;

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
