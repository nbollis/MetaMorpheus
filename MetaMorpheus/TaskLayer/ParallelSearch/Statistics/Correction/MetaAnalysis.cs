using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Distributions;

namespace TaskLayer.ParallelSearch.Statistics;

public enum PValueCombiningMethod
{
    Fishers, // Assumes independence
    Brown,   // Extension of Fisher's method which adjusts degrees of freedom using covariance of log-p-values
    KostMcDermott, // Refinement of Brown's method with better behavior for small sample sizes. 
    Cauchy // Cauchy combination (ACAT, Liu & Xie 2020); stays valid under arbitrary dependence, needs no correlation estimate
}

/// <summary>
/// Methods for combining p-values from multiple tests.
/// Brown and Kost-McDermott accept an optional ICorrelationEstimator; it defaults to independence, under which both
/// reduce to Fisher. The Cauchy combination needs no correlation estimate.
/// </summary>
public static class MetaAnalysis
{
    private static readonly ICorrelationEstimator DefaultCorrelationEstimator = new IndependenceCorrelationEstimator();

    private const double MinDegreesOfFreedom = 0.1;
    private const double MaxVarianceInflation = 100.0;

    /// <summary>
    /// Combine p-values for each database across multiple tests
    /// </summary>
    public static Dictionary<string, double> CombinePValuesAcrossTests(
        List<StatisticalTestResult> results,
        PValueCombiningMethod combiningMethod = PValueCombiningMethod.Fishers,
        ICorrelationEstimator? correlationEstimator = null)
    {
        var estimator = correlationEstimator ?? DefaultCorrelationEstimator;
        var combined = new Dictionary<string, double>();

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
                    PValueCombiningMethod.Brown => BrownMethod(pValues, estimator.EstimateCorrelationMatrix(group.ToList())),
                    PValueCombiningMethod.KostMcDermott => KostMcDermottMethod(pValues, estimator.EstimateCorrelationMatrix(group.ToList())),
                    PValueCombiningMethod.Cauchy => CauchyMethod(pValues),
                    _ => throw new ArgumentOutOfRangeException(nameof(combiningMethod), combiningMethod, null)
                };

                combined[group.Key] = pValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to combine p-values for {group.Key} using {combiningMethod}. " +
                                $"Falling back to Fisher's method. Error: {ex.Message}");
                
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
                double cov = BrownCovariance(correlationMatrix[i, j]);
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

    /// <summary>
    /// Cauchy combination test (ACAT; Liu &amp; Xie 2020, JASA 115:393) with equal weights:
    /// T = mean of tan((0.5 - p_i) * pi), combined p = 0.5 - arctan(T) / pi. Its tail is accurate whatever the
    /// dependence between the tests, so tests that share data (as every test on one database does) need no
    /// correlation estimate.
    /// <para>
    /// p-values are capped at <see cref="CauchyUpperCap"/> first. The transform of p near 1 is about
    /// -1 / (pi (1 - p)), so a single test at p = 1 - 1e-14 adds about -3e13 and outweighs any number of strong
    /// tests: on the regression panel, a true virus with family p-values down to 8e-8 combined to 0.99999999999998.
    /// Large p-values carry no evidence for presence, so capping them loses nothing the test is for, and the
    /// result hardly depends on the cap (0.9, 0.99 and 0.999 gave 5.4e-7 for that virus).
    /// </para>
    /// </summary>
    public static double CauchyMethod(IEnumerable<double> pValues)
    {
        var validPValues = pValues
            .Where(p => !double.IsNaN(p) && !double.IsInfinity(p))
            .Select(p => Math.Clamp(p, 1e-300, CauchyUpperCap))
            .ToList();

        if (validPValues.Count == 0)
            return 1.0;

        double statistic = validPValues.Average(CauchyTransform);

        // 0.5 - arctan(T)/pi loses precision for large T; for T > 0 it equals arctan(1/T)/pi, which keeps it
        double combinedPValue = statistic > 0
            ? Math.Atan(1.0 / statistic) / Math.PI
            : 0.5 - Math.Atan(statistic) / Math.PI;

        return Math.Max(1e-300, Math.Min(1.0, combinedPValue));
    }

    /// <summary>The largest p-value the Cauchy combination uses; larger ones are set to it.</summary>
    public const double CauchyUpperCap = 0.99;

    /// <summary>tan((0.5 - p) * pi), using 1/(pi p) for tiny p, where the tangent overflows.</summary>
    private static double CauchyTransform(double p)
    {
        if (p < 1e-15)
            return 1.0 / (p * Math.PI);
        return Math.Tan((0.5 - p) * Math.PI);
    }

    /// <summary>Brown's (1975) approximation of cov(-2 ln p_i, -2 ln p_j) from the correlation r.</summary>
    private static double BrownCovariance(double r)
    {
        return 3.25 * r + 0.75 * r * r;
    }

    /// <summary>Kost &amp; McDermott's (2002) refinement of Brown's covariance approximation.</summary>
    private static double KostMcDermottCovariance(double r)
    {
        return 3.263 * r + 0.710 * r * r + 0.027 * r * r * r;
    }
}