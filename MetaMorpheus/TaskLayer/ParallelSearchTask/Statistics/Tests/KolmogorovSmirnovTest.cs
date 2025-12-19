#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using TaskLayer.ParallelSearchTask.Analysis;

namespace TaskLayer.ParallelSearchTask.Statistics;

/// <summary>
/// Kolmogorov-Smirnov test for score distribution quality
/// Tests if organism's PSM score distribution is significantly shifted to HIGHER values
/// compared to decoy scores (random noise)
/// </summary>
public class KolmogorovSmirnovTest : StatisticalTestBase
{
    private readonly string _metricName;
    private readonly Func<AggregatedAnalysisResult, double[]> _targetScoresExtractor;
    private readonly Func<AggregatedAnalysisResult, double[]> _decoyScoresExtractor;
    private readonly int _minScores;

    public override string TestName => "KolmogorovSmirnov";
    public override string MetricName => _metricName;
    public override string Description =>
        $"Tests if {_metricName} score distributions are significantly higher than decoy-based null distribution";

    public KolmogorovSmirnovTest(
        string metricName,
        Func<AggregatedAnalysisResult, double[]> targetScoresExtractor,
        Func<AggregatedAnalysisResult, double[]> decoyScoresExtractor,
        int minScores = 5)
    {
        _metricName = metricName;
        _targetScoresExtractor = targetScoresExtractor;
        _decoyScoresExtractor = decoyScoresExtractor;
        _minScores = minScores;
        MinimumSampleSize = minScores;
    }

    // Convenience constructors for common metrics
    public static KolmogorovSmirnovTest ForPsm(int minScores = 5) =>
        new("PSM Score Distribution",
            r => r.PsmBacterialUnambiguousTargetScores,
            r => r.PsmBacterialUnambiguousDecoyScores,
            minScores);

    public static KolmogorovSmirnovTest ForPeptide(int minScores = 5) =>
        new("Peptide Score Distribution",
            r => r.PeptideBacterialUnambiguousTargetScores,
            r => r.PeptideBacterialUnambiguousDecoyScores,
            minScores);

    protected override int GetObservedCount(AggregatedAnalysisResult result)
    {
        return _targetScoresExtractor(result)?.Length ?? 0;
    }

    public override bool CanRun(List<AggregatedAnalysisResult> allResults)
    {
        if (allResults == null || allResults.Count < 2)
            return false;

        // Need at least some decoy scores to build null distribution
        int totalDecoyScores = allResults.Sum(r => _decoyScoresExtractor(r)?.Length ?? 0);
        return totalDecoyScores >= _minScores;
    }

    public override Dictionary<string, double> ComputePValues(List<AggregatedAnalysisResult> allResults)
    {
        // Aggregate all DECOY scores as the background/null distribution
        var allDecoyScores = allResults
            .SelectMany(r => _decoyScoresExtractor(r) ?? Array.Empty<double>())
            .Where(s => !double.IsNaN(s) && !double.IsInfinity(s))
            .OrderBy(s => s)
            .ToArray();

        if (allDecoyScores.Length == 0)
        {
            Console.WriteLine($"{MetricName} K-S Test: No decoy scores available");
            return new Dictionary<string, double>();
        }

        double decoyMean = allDecoyScores.Average();
        double decoyStdDev = allDecoyScores.StandardDeviation();

        Console.WriteLine($"{MetricName} Kolmogorov-Smirnov Test:");
        Console.WriteLine($"  Background (DECOY) scores: {allDecoyScores.Length} total");
        Console.WriteLine($"  Background mean: {decoyMean:F2}");
        Console.WriteLine($"  Background std dev: {decoyStdDev:F2}");

        var pValues = new Dictionary<string, double>();
        var ksStatistics = new Dictionary<string, double>();

        // Test each organism's score distribution against decoy null
        foreach (var result in allResults)
        {
            var organismScores = _targetScoresExtractor(result);

            if (organismScores == null || organismScores.Length < _minScores)
                continue;

            // Filter out invalid scores
            organismScores = organismScores
                .Where(s => !double.IsNaN(s) && !double.IsInfinity(s))
                .OrderBy(s => s)
                .ToArray();

            if (organismScores.Length < _minScores)
                continue;

            // Perform two-sample K-S test (one-sided: organism scores > decoy scores)
            // K-S statistic: maximum vertical distance between CDFs
            // One-sided alternative: organism CDF is LESS than decoy CDF
            // (which means more probability mass at higher values)
            var (ksStatistic, pValue) = KolmogorovSmirnovTwoSample(
                organismScores, 
                allDecoyScores, 
                alternative: KSAlternative.Less);

            pValues[result.DatabaseName] = pValue;
            ksStatistics[result.DatabaseName] = ksStatistic;
        }

        // Store K-S statistics as additional metrics in results
        // This will be available through StatisticalResult.AdditionalMetrics
        foreach (var kvp in ksStatistics)
        {
            if (pValues.ContainsKey(kvp.Key))
            {
                // Note: We'll need to store this differently since we can't modify
                // the StatisticalResult here. For now, just compute p-values.
            }
        }

        Console.WriteLine($"  Tested organisms: {pValues.Count}");

        return pValues;
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

        // Compute empirical CDFs at all unique points
        var allPoints = sample1.Concat(sample2).Distinct().OrderBy(x => x).ToArray();

        double maxDiff = 0.0;
        double maxDiffLess = 0.0;  // For one-sided test: sample1 CDF < sample2 CDF

        foreach (var point in allPoints)
        {
            // CDF value = proportion of values <= point
            double cdf1 = sample1.Count(x => x <= point) / (double)n1;
            double cdf2 = sample2.Count(x => x <= point) / (double)n2;

            double diff = Math.Abs(cdf1 - cdf2);
            maxDiff = Math.Max(maxDiff, diff);

            // For "less" alternative: we want sample1 CDF < sample2 CDF
            // which means sample1 has more mass to the right (higher values)
            double diffLess = cdf2 - cdf1;
            maxDiffLess = Math.Max(maxDiffLess, diffLess);
        }

        double ksStatistic = alternative == KSAlternative.Less ? maxDiffLess : maxDiff;

        // Compute p-value using asymptotic approximation
        double pValue = ComputeKSPValue(ksStatistic, n1, n2, alternative);

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

        if (alternative == KSAlternative.TwoSided)
        {
            // Two-sided test: use Kolmogorov distribution
            // Approximation: P(D > d) ≈ 2 * sum_{k=1}^∞ (-1)^(k-1) * exp(-2k^2 * d^2)
            double lambda = (n + 0.12 + 0.11 / n) * ksStatistic;
            
            // Sum first 100 terms
            double pValue = 0.0;
            for (int k = 1; k <= 100; k++)
            {
                double term = Math.Pow(-1.0, k - 1) * Math.Exp(-2.0 * k * k * lambda * lambda);
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
            // P(D+ > d) ≈ exp(-2 * n * d^2)
            double pValue = Math.Exp(-2.0 * n * n * ksStatistic * ksStatistic);
            return Math.Max(0.0, Math.Min(1.0, pValue));
        }
    }

    private enum KSAlternative
    {
        TwoSided,
        Less,    // CDF1 < CDF2 (sample1 has higher values)
        Greater  // CDF1 > CDF2 (sample1 has lower values)
    }
}
