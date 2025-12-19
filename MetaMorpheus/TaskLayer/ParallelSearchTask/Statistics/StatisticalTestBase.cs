using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearchTask.Analysis;

namespace TaskLayer.ParallelSearchTask.Statistics;

/// <summary>
/// Base class providing common functionality for statistical tests
/// </summary>
public abstract class StatisticalTestBase : IStatisticalTest
{
    public abstract string TestName { get; }
    public abstract string MetricName { get; }
    public abstract string Description { get; }

    protected int MinimumSampleSize { get; set; } = 5;

    public abstract Dictionary<string, double> ComputePValues(List<AggregatedAnalysisResult> allResults);

    public virtual bool CanRun(List<AggregatedAnalysisResult> allResults)
    {
        // Default: need at least 2 databases and some non-zero observations
        return allResults != null && allResults.Count >= 2;
    }

    /// <summary>
    /// Extract the relevant metric value from a result
    /// Override in derived classes to specify which metric to test
    /// </summary>
    protected abstract int GetObservedCount(AggregatedAnalysisResult result);

    /// <summary>
    /// Helper to filter results with sufficient sample size
    /// </summary>
    protected List<AggregatedAnalysisResult> FilterValidResults(List<AggregatedAnalysisResult> allResults)
    {
        return allResults
            .Where(r => GetObservedCount(r) >= MinimumSampleSize)
            .ToList();
    }
}