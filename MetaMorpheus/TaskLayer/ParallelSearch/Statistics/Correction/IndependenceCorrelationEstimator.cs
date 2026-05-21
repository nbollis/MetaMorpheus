#nullable enable
using System.Collections.Generic;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Assumes all tests are independent by returning the identity matrix.
/// Appropriate for Fisher's method, which does not use correlation at all,
/// and as a safe default when correlation estimation is not desired.
/// </summary>
public sealed class IndependenceCorrelationEstimator : ICorrelationEstimator
{
    public double[,] EstimateCorrelationMatrix(List<StatisticalTestResult> results)
    {
        int k = results.Count;
        var matrix = new double[k, k];

        for (int i = 0; i < k; i++)
        {
            for (int j = 0; j < k; j++)
            {
                matrix[i, j] = (i == j) ? 1.0 : 0.0;
            }
        }

        return matrix;
    }
}
