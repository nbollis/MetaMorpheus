#nullable enable
using MathNet.Numerics.Statistics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Statistics;
public enum KSAlternative
{
    TwoSided,
    Less,    // CDF1 < CDF2 (sample1 has higher values)
    Greater  // CDF1 > CDF2 (sample1 has lower values)
}

/// <summary>
/// Kolmogorov-Smirnov test for score distribution quality
/// Tests if organism's PSM score distribution is significantly shifted to HIGHER values
/// compared to decoy scores (random noise)
/// </summary>
public class KolmogorovSmirnovTest(
    string metricName,
    Func<TransientDatabaseMetrics, double[]> sampleScoresExtractor,
    Func<TransientDatabaseMetrics, bool>? shouldSkip = null,
    KSAlternative ksMode = KSAlternative.Less)
    : StatisticalTestBase(metricName, shouldSkip)
{
    public override string TestName => "KolmogorovSmirnov";
    public override string Description => $"Tests if {MetricName} score distributions are significantly {GetAlternativeDescription()} than decoy-based null distribution";

    private string GetAlternativeDescription() => ksMode switch
    {
        KSAlternative.TwoSided => "different",
        KSAlternative.Less => "higher",
        KSAlternative.Greater => "lower",
        _ => "different"
    };

    public override double GetTestValue(TransientDatabaseMetrics result) => result.Results.TryGetValue($"KolmSmir_{MetricName}_KS", out var ksValue) ? (double)ksValue : -1;

    public override bool CanRun(List<TransientDatabaseMetrics> allResults)
    {
        if (allResults == null || allResults.Count < 2)
            return false;

        // Need at least some decoy scores to build null distribution
        return true;
    }

    public override Dictionary<string, double> ComputePValues(List<TransientDatabaseMetrics> allResults)
    {
        // Aggregate all DECOY scores as the background/null distribution
        var allDecoyScores = allResults
            .SelectMany(r => sampleScoresExtractor(r) ?? Array.Empty<double>())
            .Where(s => !double.IsNaN(s) && !double.IsInfinity(s))
            .OrderBy(s => s)
            .ToArray();

        if (allDecoyScores.Length == 0)
        {
            Console.WriteLine($"{MetricName} K-S Test: No decoy scores available");
            return new Dictionary<string, double>();
        }

        double backgroundMean = allDecoyScores.Average();
        double backgroundStdDev = allDecoyScores.StandardDeviation();

        Console.WriteLine($"{MetricName} Kolmogorov-Smirnov Test:");
        Console.WriteLine($"  Background (DECOY) scores: {allDecoyScores.Length} total");
        Console.WriteLine($"  Background mean: {backgroundMean:F2}");
        Console.WriteLine($"  Background std dev: {backgroundStdDev:F2}");

        var pValues = new ConcurrentDictionary<string, double>();

        // Test each organism's score distribution against background null
        Parallel.ForEach(Partitioner.Create(0, allResults.Count), 
            new ParallelOptions() { MaxDegreeOfParallelism = 10 }, 
            (partition) =>
        {
            for (int i = partition.Item1; i < partition.Item2; i++)
            {
                var result = allResults[i];

                if (ShouldSkip != null && ShouldSkip(result))
                {
                    pValues.TryAdd(result.DatabaseName, double.NaN);
                    continue;
                }

                var organismScores = sampleScoresExtractor(result);

                // Handle null or empty arrays - assign p-value of 1 (not significant)
                if (organismScores == null || organismScores.Length == 0)
                {
                    pValues.TryAdd(result.DatabaseName, double.NaN);
                    continue;
                }

                // Filter out invalid scores
                var validScores = organismScores
                    .Where(s => !double.IsNaN(s) && !double.IsInfinity(s))
                    .OrderBy(s => s)
                    .ToArray();

                try
                {
                    // Perform two-sample K-S test (one-sided: organism scores > decoy scores)
                    // K-S statistic: maximum vertical distance between CDFs
                    // One-sided alternative: organism CDF is LESS than decoy CDF
                    // (which means more probability mass at higher values)
                    var (ksStatistic, pValue) = KolmogorovSmirnovTwoSample(
                        validScores,
                        allDecoyScores,
                        alternative: ksMode);

                    // Validate p-value
                    if (double.IsNaN(pValue) || double.IsInfinity(pValue))
                    {
                        pValues.TryAdd(result.DatabaseName, double.NaN);
                    }
                    else
                    {
                        // Clamp p-value to valid range
                        pValue = Math.Max(1e-300, Math.Min(1.0, pValue));

                        pValues.TryAdd(result.DatabaseName, pValue);
                        result.Results[$"KolmSmir_{MetricName}_KS"] = ksStatistic;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Error computing K-S test for {result.DatabaseName}: {ex.Message}");
                    pValues.TryAdd(result.DatabaseName, double.NaN); // Default to non-significant on error
                }
            }
        });

        Console.WriteLine($"  Tested organisms: {pValues.Count}");

        return pValues.ToDictionary();
    }

    /// <summary>
    /// Compute two-sample Kolmogorov-Smirnov test
    /// </summary>
    private (double KSStatistic, double PValue) KolmogorovSmirnovTwoSample(
        double[] sample1, 
        double[] sample2, 
        KSAlternative alternative = KSAlternative.TwoSided)
    {
        int n1 = sample1.Length;
        int n2 = sample2.Length;

        // Samples are already sorted from caller - no need to sort again
        // If not sorted, sort once here:
        // Array.Sort(sample1);
        // Array.Sort(sample2);

        double maxDiff = 0.0;
        double maxDiffLess = 0.0;    // For one-sided test: sample1 CDF < sample2 CDF (sample1 has higher values)
        double maxDiffGreater = 0.0; // For one-sided test: sample1 CDF > sample2 CDF (sample1 has lower values)

        int i = 0; // index for sample1
        int j = 0; // index for sample2

        // Sweep through both sorted arrays simultaneously - O(n1 + n2) instead of O(n²)
        while (i < n1 || j < n2)
        {
            // Get current point from whichever array has the smaller value
            double point;
            if (i >= n1)
                point = sample2[j++];
            else if (j >= n2)
                point = sample1[i++];
            else if (sample1[i] <= sample2[j])
                point = sample1[i++];
            else
                point = sample2[j++];

            // CDF is just the proportion of values we've seen so far
            double cdf1 = i / (double)n1;
            double cdf2 = j / (double)n2;

            double diff = Math.Abs(cdf1 - cdf2);
            maxDiff = Math.Max(maxDiff, diff);

            // For "less" alternative: we want sample1 CDF < sample2 CDF
            // which means sample1 has more mass to the right (higher values)
            double diffLess = cdf2 - cdf1;
            maxDiffLess = Math.Max(maxDiffLess, diffLess);

            // For "greater" alternative: we want sample1 CDF > sample2 CDF
            // which means sample1 has more mass to the left (lower values)
            double diffGreater = cdf1 - cdf2;
            maxDiffGreater = Math.Max(maxDiffGreater, diffGreater);
        }

        double ksStatistic = alternative switch
        {
            KSAlternative.Less => maxDiffLess,
            KSAlternative.Greater => maxDiffGreater,
            _ => maxDiff
        };

        // Handle edge case where no difference was found
        if (ksStatistic <= 0.0 || double.IsNaN(ksStatistic))
        {
            return (0.0, double.NaN);
        }

        // Compute p-value using asymptotic approximation
        double pValue = ComputeKSPValue(ksStatistic, n1, n2, alternative);

        // Final validation
        if (double.IsNaN(pValue) || double.IsInfinity(pValue))
        {
            return (ksStatistic, double.NaN);
        }

        return (ksStatistic, pValue);
    }

    /// <summary>
    /// Compute p-value for K-S test using asymptotic distribution
    /// </summary>
    private double ComputeKSPValue(double ksStatistic, int n1, int n2, KSAlternative alternative)
    {
        if (ksStatistic <= 0)
            return 1.0;

        // Compute effective sample size
        double n = Math.Sqrt(n1 * n2 / (double)(n1 + n2));

        if (double.IsNaN(n) || double.IsInfinity(n) || n <= 0)
        {
            return 1.0;
        }

        if (alternative == KSAlternative.TwoSided)
        {
            // Two-sided test: use Kolmogorov distribution
            double lambda = (n + 0.12 + 0.11 / n) * ksStatistic;

            if (double.IsNaN(lambda) || double.IsInfinity(lambda))
            {
                return 1.0;
            }

            // Sum first 100 terms
            double pValue = 0.0;
            for (int k = 1; k <= 100; k++)
            {
                double exponent = -2.0 * k * k * lambda * lambda;

                // Prevent underflow
                if (exponent < -700) // e^-700 ≈ 0
                    break;

                double term = Math.Pow(-1.0, k - 1) * Math.Exp(exponent);
                pValue += term;

                if (Math.Abs(term) < 1e-10)
                    break;
            }
            pValue *= 2.0;

            return Math.Max(0.0, Math.Min(1.0, pValue));
        }
        else
        {
            // One-sided test: use adjusted approximation
            double exponent = -2.0 * n * n * ksStatistic * ksStatistic;

            // Prevent underflow
            if (exponent < -700)
            {
                return 0.0; // Very small p-value
            }

            double pValue = Math.Exp(exponent);

            if (double.IsNaN(pValue) || double.IsInfinity(pValue))
            {
                return 1.0;
            }

            return Math.Max(0.0, Math.Min(1.0, pValue));
        }
    }
}
