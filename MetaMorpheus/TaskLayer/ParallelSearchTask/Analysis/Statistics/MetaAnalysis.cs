using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Distributions;

namespace TaskLayer.ParallelSearchTask.Analysis.Statistics;

/// <summary>
/// Fisher's method for combining p-values from multiple tests
/// </summary>
public static class MetaAnalysis
{
    /// <summary>
    /// Combine p-values using Fisher's method
    /// </summary>
    /// <param name="pValues">List of p-values to combine</param>
    /// <returns>Combined p-value</returns>
    public static double FisherMethod(IEnumerable<double> pValues)
    {
        var validPValues = pValues
            .Where(p => !double.IsNaN(p) && !double.IsInfinity(p))
            .Select(p => Math.Max(p, 1e-300)) // Avoid log(0)
            .ToList();

        if (validPValues.Count == 0)
            return 1.0;

        int k = validPValues.Count;
        double testStatistic = -2.0 * validPValues.Sum(p => Math.Log(p));

        // Use chi-squared distribution with 2*k degrees of freedom
        var chi2Dist = new ChiSquared(2 * k);
        double combinedPValue = 1.0 - chi2Dist.CumulativeDistribution(testStatistic);

        return combinedPValue;
    }

    /// <summary>
    /// Combine p-values for each database across multiple tests
    /// </summary>
    public static Dictionary<string, double> CombinePValuesAcrossTests(List<StatisticalResult> results)
    {
        var combined = new Dictionary<string, double>();

        // Group by database name
        var byDatabase = results.GroupBy(r => r.DatabaseName);

        foreach (var group in byDatabase)
        {
            var pValues = group.Select(r => r.PValue).ToList();
            combined[group.Key] = FisherMethod(pValues);
        }

        return combined;
    }
}