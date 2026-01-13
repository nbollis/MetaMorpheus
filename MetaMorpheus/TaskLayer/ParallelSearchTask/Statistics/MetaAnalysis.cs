using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Distributions;

namespace TaskLayer.ParallelSearchTask.Statistics;

public enum PValueCombiningMethod
{
    Fishers, // Assumes independence
    Brown,   // Extension of Fisher's method which adjusts degrees of freedom using covariance of log-p-values
    KostMcDermott // Refinement of Brown's method with better behavior for small sample sizes. 

}

/// <summary>
/// Fisher's method for combining p-values from multiple tests
/// </summary>
public static class MetaAnalysis
{
    /// <summary>
    /// Combine p-values for each database across multiple tests
    /// </summary>
    public static Dictionary<string, double> CombinePValuesAcrossTests(List<StatisticalResult> results, PValueCombiningMethod combiningMethod = PValueCombiningMethod.KostMcDermott)
    {
        var combined = new Dictionary<string, double>();

        // Group by database name
        var byDatabase = results.GroupBy(r => r.DatabaseName);

        foreach (var group in byDatabase)
        {
            var pValues = group.Select(r => r.PValue).ToList();

            var pValue = combiningMethod switch
            {
                PValueCombiningMethod.Fishers => FisherMethod(pValues),
                PValueCombiningMethod.Brown => BrownMethod(pValues, GetCorrelationMatrix(group.ToList())),
                PValueCombiningMethod.KostMcDermott => KostMcDermottMethod(pValues, GetCorrelationMatrix(group.ToList())),
                _ => throw new ArgumentOutOfRangeException(nameof(combiningMethod), combiningMethod, null)
            };

            combined[group.Key] = pValue;
        }

        return combined;
    }

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

    public static double BrownMethod(IEnumerable<double> pValues, double[,] correlationMatrix)
    {
        var validPValues = pValues
            .Where(p => !double.IsNaN(p) && !double.IsInfinity(p))
            .Select(p => Math.Max(p, 1e-300))
            .ToList();

        int k = validPValues.Count;
        if (k == 0)
            return 1.0;

        if (correlationMatrix.GetLength(0) != k ||
            correlationMatrix.GetLength(1) != k)
            throw new ArgumentException("Correlation matrix dimensions must match number of p-values.");

        // Fisher statistic
        double fisherStatistic = -2.0 * validPValues.Sum(p => Math.Log(p));

        // Mean and variance under dependence
        double mean = 2.0 * k;
        double variance = 4.0 * k;

        for (int i = 0; i < k; i++)
        {
            for (int j = i + 1; j < k; j++)
            {
                double cov = KostMcDermottCovariance(correlationMatrix[i, j]);
                variance += 2.0 * cov;
            }
        }

        // Brown scaling
        double df = 2.0 * mean * mean / variance;
        double scale = variance / (2.0 * mean);

        var chi2 = new ChiSquared(df);
        double combinedPValue =
            1.0 - chi2.CumulativeDistribution(fisherStatistic / scale);

        return combinedPValue;
    }

    public static double KostMcDermottMethod(IEnumerable<double> pValues, double[,] correlationMatrix)
    {
        var validPValues = pValues
            .Where(p => !double.IsNaN(p) && !double.IsInfinity(p))
            .Select(p => Math.Max(p, 1e-300))
            .ToList();

        int k = validPValues.Count;
        if (k == 0)
            return 1.0;

        if (correlationMatrix.GetLength(0) != k ||
            correlationMatrix.GetLength(1) != k)
            throw new ArgumentException("Correlation matrix dimensions must match number of p-values.");

        double fisherStatistic = -2.0 * validPValues.Sum(p => Math.Log(p));

        double mean = 2.0 * k;
        double variance = 4.0 * k;

        for (int i = 0; i < k; i++)
        {
            for (int j = i + 1; j < k; j++)
            {
                double r = correlationMatrix[i, j];
                double cov = KostMcDermottCovariance(r);
                variance += 2.0 * cov;
            }
        }

        double df = 2.0 * mean * mean / variance;
        double scale = variance / (2.0 * mean);

        var chi2 = new ChiSquared(df);
        double combinedPValue =
            1.0 - chi2.CumulativeDistribution(fisherStatistic / scale);

        return combinedPValue;
    }

    private static double KostMcDermottCovariance(double r)
    {
        // Polynomial approximation from Kost & McDermott (2002)
        return 3.263 * r + 0.710 * r * r + 0.027 * r * r * r;
    }

    public static double[,] GetCorrelationMatrix(List<StatisticalResult> results)
    {
        int k = results.Count;
        var matrix = new double[k, k];

        var stats = results.Select(r => r.TestStatistic!.Value).ToArray();
        double mean = stats.Average();
        double variance = stats.Select(x => (x - mean) * (x - mean)).Sum();

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
                    double cov =
                        (stats[i] - mean) * (stats[j] - mean);
                    matrix[i, j] = cov / variance;
                }
            }
        }

        return matrix;
    }
}