#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Estimates correlation from test-statistic covariance, identical to the
/// logic previously inlined in MetaAnalysis.GetCorrelationMatrix.
/// Falls back to the identity matrix (independence assumption) when test
/// statistics are missing or degenerate.
/// </summary>
public sealed class TestStatisticCorrelationEstimator : ICorrelationEstimator
{
    public double[,] EstimateCorrelationMatrix(List<StatisticalTestResult> results)
    {
        (double pValue, double TestStat)[] validPAndStats = results
            .Where(r => !double.IsNaN(r.PValue) && !double.IsInfinity(r.PValue) && r.TestStatistic.HasValue)
            .Select(r => (r.PValue, r.TestStatistic!.Value))
            .ToArray();

        int k = validPAndStats.Length;
        var matrix = new double[k, k];

        if (results.Any(r => !r.TestStatistic.HasValue))
        {
            for (int i = 0; i < k; i++)
            {
                for (int j = 0; j < k; j++)
                {
                    matrix[i, j] = (i == j) ? 1.0 : 0.0;
                }
            }
            return matrix;
        }

        var stats = validPAndStats.Select(r => r.TestStat).ToArray();
        double mean = stats.Average();
        double variance = stats.Select(x => (x - mean) * (x - mean)).Sum();

        if (variance < 1e-10)
        {
            for (int i = 0; i < k; i++)
            {
                for (int j = 0; j < k; j++)
                {
                    matrix[i, j] = 1.0;
                }
            }
            return matrix;
        }

        for (int i = 0; i < k; i++)
        {
            for (int j = 0; j < k; j++)
            {
                if (i == j)
                {
                    matrix[i, j] = 1.0;
                }
                else
                {
                    double cov = (stats[i] - mean) * (stats[j] - mean);
                    double correlation = cov / variance;
                    matrix[i, j] = Math.Max(-1.0, Math.Min(1.0, correlation));
                }
            }
        }

        return matrix;
    }
}
