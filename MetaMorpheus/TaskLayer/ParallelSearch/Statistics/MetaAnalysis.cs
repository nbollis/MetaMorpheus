using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Distributions;

namespace TaskLayer.ParallelSearch.Statistics;

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
    private const double MinDegreesOfFreedom = 0.1; // Minimum valid df for chi-squared distribution
    private const double MaxVarianceInflation = 100.0; // Maximum allowed variance inflation factor

    /// <summary>
    /// Combine p-values for each database across multiple tests
    /// </summary>
    public static Dictionary<string, double> CombinePValuesAcrossTests(List<StatisticalResult> results, PValueCombiningMethod combiningMethod = PValueCombiningMethod.Fishers)
    {
        var combined = new Dictionary<string, double>();

        // Group by database name
        var byDatabase = results.GroupBy(r => r.DatabaseName);

        foreach (var group in byDatabase)
        {
            var pValues = group.Select(r => r.PValue)
                .Where(p => !double.IsNaN(p))
                .ToList();

            try
            {
                var pValue = combiningMethod switch
                {
                    PValueCombiningMethod.Fishers => FisherMethod(pValues),
                    PValueCombiningMethod.Brown => BrownMethod(pValues, GetCorrelationMatrix(group.ToList())),
                    PValueCombiningMethod.KostMcDermott => KostMcDermottMethod(pValues, GetCorrelationMatrix(group.ToList())),
                    _ => throw new ArgumentOutOfRangeException(nameof(combiningMethod), combiningMethod, null)
                };

                combined[group.Key] = pValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to combine p-values for {group.Key} using {combiningMethod}. " +
                                $"Falling back to Fisher's method. Error: {ex.Message}");
                
                // Fallback to Fisher's method (assumes independence)
                combined[group.Key] = FisherMethod(pValues);
            }
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

        return Math.Max(1e-300, Math.Min(1.0, combinedPValue));
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
        double fisherStatistic = -2.0 * validPValues.Sum(Math.Log);

        // Mean and variance under dependence
        double mean = 2.0 * k;
        double baseVariance = 4.0 * k;
        double covarianceSum = 0.0;

        for (int i = 0; i < k; i++)
        {
            for (int j = i + 1; j < k; j++)
            {
                double cov = KostMcDermottCovariance(correlationMatrix[i, j]);
                covarianceSum += 2.0 * cov;
            }
        }

        double variance = baseVariance + covarianceSum;

        // Validate variance
        if (variance <= 0 || double.IsNaN(variance) || double.IsInfinity(variance))
        {
            Console.WriteLine($"Warning: Invalid variance ({variance:F3}) in Brown's method. " +
                            $"Base variance: {baseVariance:F3}, Covariance sum: {covarianceSum:F3}. " +
                            $"Falling back to Fisher's method.");
            return FisherMethod(validPValues);
        }

        // Check for extreme variance inflation
        double varianceInflation = variance / baseVariance;
        if (varianceInflation > MaxVarianceInflation || varianceInflation < 1.0 / MaxVarianceInflation)
        {
            Console.WriteLine($"Warning: Extreme variance inflation ({varianceInflation:F3}) in Brown's method. " +
                            $"Falling back to Fisher's method.");
            return FisherMethod(validPValues);
        }

        // Brown scaling
        double df = 2.0 * mean * mean / variance;
        double scale = variance / (2.0 * mean);

        // Validate degrees of freedom
        if (df < MinDegreesOfFreedom || double.IsNaN(df) || double.IsInfinity(df))
        {
            Console.WriteLine($"Warning: Invalid degrees of freedom ({df:F3}) in Brown's method. " +
                            $"Mean: {mean:F3}, Variance: {variance:F3}. Falling back to Fisher's method.");
            return FisherMethod(validPValues);
        }

        var chi2 = new ChiSquared(df);
        double combinedPValue = 1.0 - chi2.CumulativeDistribution(fisherStatistic / scale);

        return Math.Max(1e-300, Math.Min(1.0, combinedPValue));
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
        double baseVariance = 4.0 * k;
        double covarianceSum = 0.0;

        for (int i = 0; i < k; i++)
        {
            for (int j = i + 1; j < k; j++)
            {
                double r = correlationMatrix[i, j];
                double cov = KostMcDermottCovariance(r);
                covarianceSum += 2.0 * cov;
            }
        }

        double variance = baseVariance + covarianceSum;

        // Validate variance
        if (variance <= 0 || double.IsNaN(variance) || double.IsInfinity(variance))
        {
            Console.WriteLine($"Warning: Invalid variance ({variance:F3}) in Kost-McDermott method. " +
                            $"Base variance: {baseVariance:F3}, Covariance sum: {covarianceSum:F3}. " +
                            $"Falling back to Fisher's method.");
            return FisherMethod(validPValues);
        }

        // Check for extreme variance inflation
        double varianceInflation = variance / baseVariance;
        if (varianceInflation > MaxVarianceInflation || varianceInflation < 1.0 / MaxVarianceInflation)
        {
            Console.WriteLine($"Warning: Extreme variance inflation ({varianceInflation:F3}) in Kost-McDermott method. " +
                            $"Falling back to Fisher's method.");
            return FisherMethod(validPValues);
        }

        double df = 2.0 * mean * mean / variance;
        double scale = variance / (2.0 * mean);

        // Validate degrees of freedom
        if (df < MinDegreesOfFreedom || double.IsNaN(df) || double.IsInfinity(df))
        {
            Console.WriteLine($"Warning: Invalid degrees of freedom ({df:F3}) in Kost-McDermott method. " +
                            $"Mean: {mean:F3}, Variance: {variance:F3}. Falling back to Fisher's method.");
            return FisherMethod(validPValues);
        }

        var chi2 = new ChiSquared(df);
        double combinedPValue = 1.0 - chi2.CumulativeDistribution(fisherStatistic / scale);

        return Math.Max(1e-300, Math.Min(1.0, combinedPValue));
    }

    private static double KostMcDermottCovariance(double r)
    {
        // Polynomial approximation from Kost & McDermott (2002)
        // This can produce large values when r is close to 1 or -1
        return 3.263 * r + 0.710 * r * r + 0.027 * r * r * r;
    }

    public static double[,] GetCorrelationMatrix(List<StatisticalResult> results)
    {
        (double pValue, double TestStat)[] validPAndStats = results
            .Where(r => !double.IsNaN(r.PValue) && !double.IsInfinity(r.PValue) && r.TestStatistic.HasValue)
            .Select(r => (r.PValue, r.TestStatistic!.Value))
            .ToArray();

        int k = validPAndStats.Length;
        var matrix = new double[k, k];

        // Check if we have valid test statistics
        if (results.Any(r => !r.TestStatistic.HasValue))
        {
            // If test statistics are missing, assume independence
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

        // Handle degenerate case where all test statistics are identical
        if (variance < 1e-10)
        {
            for (int i = 0; i < k; i++)
            {
                for (int j = 0; j < k; j++)
                {
                    matrix[i, j] = 1.0; // Perfect correlation
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
                    
                    // Clamp correlation to valid range [-1, 1]
                    matrix[i, j] = Math.Max(-1.0, Math.Min(1.0, correlation));
                }
            }
        }

        return matrix;
    }
}