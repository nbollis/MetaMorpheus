#nullable enable
using System.Collections.Generic;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Estimates pairwise correlations between statistical test results for use
/// in dependent p-value combination methods (Brown, Kost-McDermott).
/// Implementations may use test statistics, empirical covariances, or
/// assume independence (identity matrix).
/// </summary>
public interface ICorrelationEstimator
{
    /// <summary>
    /// Estimate a correlation matrix from the given test results.
    /// The matrix must be k×k where k is the number of results with
    /// valid (finite, non-NaN) p-values and test statistics.
    /// </summary>
    double[,] EstimateCorrelationMatrix(List<StatisticalTestResult> results);
}
