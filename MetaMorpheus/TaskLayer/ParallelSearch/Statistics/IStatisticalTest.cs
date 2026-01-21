using System;
using System.Collections.Generic;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Interface for statistical significance tests on transient database results
/// </summary>
public interface IStatisticalTest : IEquatable<IStatisticalTest>
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
    /// Count of databases where the result was statistically significant
    /// </summary>
    int SignificantResults { get; }

    /// <summary>
    /// Run the statistical test on all databases
    /// </summary>
    /// <param name="allResults">All database results</param>
    /// <param name="alpha">Significance threshold</param>
    /// <returns>Dictionary of database name to p-value</returns>
    Dictionary<string, double> RunTest(List<TransientDatabaseMetrics> allResults, double alpha = 0.05);

    /// <summary>
    /// Check if this test can run given the available data
    /// </summary>
    bool CanRun(List<TransientDatabaseMetrics> allResults);

    public double GetTestValue(TransientDatabaseMetrics result);
}