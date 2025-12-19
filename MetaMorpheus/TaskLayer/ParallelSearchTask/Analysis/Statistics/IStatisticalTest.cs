using System.Collections.Generic;

namespace TaskLayer.ParallelSearchTask.Analysis.Statistics;

/// <summary>
/// Interface for statistical significance tests on transient database results
/// </summary>
public interface IStatisticalTest
{
    /// <summary>
    /// Name of the statistical test (e.g., "Gaussian", "Permutation")
    /// </summary>
    string TestName { get; }

    /// <summary>
    /// Metric being tested (e.g., "PSM", "Peptide", "Protein")
    /// </summary>
    string MetricName { get; }

    /// <summary>
    /// Description of what the test measures
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Run the statistical test on all databases
    /// </summary>
    /// <param name="allResults">All database results</param>
    /// <returns>Dictionary of database name to p-value</returns>
    Dictionary<string, double> ComputePValues(List<AggregatedAnalysisResult> allResults);

    /// <summary>
    /// Check if this test can run given the available data
    /// </summary>
    bool CanRun(List<AggregatedAnalysisResult> allResults);
}