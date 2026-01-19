#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
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
    Func<AggregatedAnalysisResult, double[]> sampleScoresExtractor,
    Func<AggregatedAnalysisResult, double[]> buildDistributionScoresExtractor,
    int minScores = 5,
    KSAlternative ksMode = KSAlternative.Less,
    Func<AggregatedAnalysisResult, bool>? shouldSkip = null)
    : StatisticalTestBase(metricName, minScores, shouldSkip)
{
    private readonly int _minScores = minScores;

    public override string TestName => "KolmogorovSmirnov";
    public override string Description => $"Tests if {MetricName} score distributions are significantly {GetAlternativeDescription()} than decoy-based null distribution";

    private string GetAlternativeDescription() => ksMode switch
    {
        KSAlternative.TwoSided => "different",
        KSAlternative.Less => "higher",
        KSAlternative.Greater => "lower",
        _ => "different"
    };

    public override double GetTestValue(AggregatedAnalysisResult result) => result.Results.TryGetValue($"KolmSmir_{MetricName}_KS", out var ksValue) ? (double)ksValue : -1;

    #region Predefined Tests

    public static KolmogorovSmirnovTest ForPsm(int minScores = 2, KSAlternative ksMode = KSAlternative.Less) =>
     new("PSMScoreDistribution",
         r => r.PsmBacterialUnambiguousTargetScores,
         r => r.PsmBacterialUnambiguousTargetScores,
         minScores,
         ksMode);

    public static KolmogorovSmirnovTest ForPeptide(int minScores = 2, KSAlternative ksMode = KSAlternative.Less) =>
        new("PeptideScoreDistribution",
            r => r.PeptideBacterialUnambiguousTargetScores,
            r => r.PeptideBacterialUnambiguousTargetScores,
            minScores,
            ksMode);

    public static KolmogorovSmirnovTest ForPsmComplementary(int minScores = 2, KSAlternative ksMode = KSAlternative.Less) =>
        new("PSM-Complementary",
            r => r.Psm_ComplementaryCountTargets,
            r => r.Psm_ComplementaryCountTargets,
            minScores,
            ksMode);

    public static KolmogorovSmirnovTest ForPsmBidirectional(int minScores = 2, KSAlternative ksMode = KSAlternative.Less) =>
        new("PSM-Bidirectional",
            r => r.Psm_BidirectionalTargets,
            r => r.Psm_BidirectionalTargets,
            minScores,
            ksMode);

    public static KolmogorovSmirnovTest ForPsmSequenceCoverage(int minScores = 2, KSAlternative ksMode = KSAlternative.Less) =>
        new("PSM-SequenceCoverage",
            r => r.Psm_SequenceCoverageFractionTargets,
            r => r.Psm_SequenceCoverageFractionTargets,
            minScores,
            ksMode);

    public static KolmogorovSmirnovTest ForPeptideComplementary(int minScores = 2, KSAlternative ksMode = KSAlternative.Less) =>
        new("Peptide-Complementary",
            r => r.Peptide_ComplementaryCountTargets,
            r => r.Peptide_ComplementaryCountTargets,
            minScores,
            ksMode);

    public static KolmogorovSmirnovTest ForPeptideBidirectional(int minScores = 2, KSAlternative ksMode = KSAlternative.Less) =>
        new("Peptide-Bidirectional",
            r => r.Peptide_BidirectionalTargets,
            r => r.Peptide_BidirectionalTargets,
            minScores,
            ksMode);

    public static KolmogorovSmirnovTest ForPeptideSequenceCoverage(int minScores = 2, KSAlternative ksMode = KSAlternative.Less) =>
        new("Peptide-SequenceCoverage",
            r => r.Peptide_SequenceCoverageFractionTargets,
            r => r.Peptide_SequenceCoverageFractionTargets,
            minScores,
            ksMode);

    public static KolmogorovSmirnovTest ForPsmRetentionTimeErrors(int minScores = 2, KSAlternative ksMode = KSAlternative.Greater) =>
        new("PSM-RtErrors",
        r => r.Psm_AllRtErrors,
        r => r.Psm_AllRtErrors,
        minScores,
        ksMode);

    public static KolmogorovSmirnovTest ForPeptideRetentionTimeErrors(int minScores = 2, KSAlternative ksMode = KSAlternative.Greater) =>
        new("Peptide-RtErrors",
            r => r.Peptide_AllRtErrors,
            r => r.Peptide_AllRtErrors,
            minScores,
            ksMode);

    #endregion

    public override bool CanRun(List<AggregatedAnalysisResult> allResults)
    {
        if (allResults == null || allResults.Count < 2)
            return false;

        // Need at least some decoy scores to build null distribution
        int totalDecoyScores = allResults.Sum(r => buildDistributionScoresExtractor(r)?.Length ?? 0);
        return totalDecoyScores >= _minScores;
    }

    public override Dictionary<string, double> ComputePValues(List<AggregatedAnalysisResult> allResults)
    {
        // Aggregate all DECOY scores as the background/null distribution
        var allDecoyScores = allResults
            .SelectMany(r => buildDistributionScoresExtractor(r) ?? Array.Empty<double>())
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

        // Test each organism's score distribution against decoy null
        foreach (var result in allResults)
        {
            if (ShouldSkip != null && ShouldSkip(result))
            {
                pValues[result.DatabaseName] = double.NaN;
                continue;
            }

            var organismScores = sampleScoresExtractor(result);

            // Handle null or empty arrays - assign p-value of 1 (not significant)
            if (organismScores == null || organismScores.Length == 0)
            {
                pValues[result.DatabaseName] = double.NaN;
                continue;
            }

            // Filter out invalid scores
            var validScores = organismScores
                .Where(s => !double.IsNaN(s) && !double.IsInfinity(s))
                .OrderBy(s => s)
                .ToArray();

            // Check minimum sample size after filtering
            if (validScores.Length < _minScores)
            {
                // Insufficient data - assign p-value of 1 (not significant)
                pValues[result.DatabaseName] = double.NaN;
                continue;
            }

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
                    Console.WriteLine($"  Warning: Invalid p-value for {result.DatabaseName}, setting to 1.0");
                    pValues[result.DatabaseName] = double.NaN;
                }
                else
                {
                    // Clamp p-value to valid range
                    pValue = Math.Max(1e-300, Math.Min(1.0, pValue));

                    pValues[result.DatabaseName] = pValue;
                    result.Results[$"KolmSmir_{MetricName}_KS"] = ksStatistic;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error computing K-S test for {result.DatabaseName}: {ex.Message}");
                pValues[result.DatabaseName] = double.NaN; // Default to non-significant on error
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
        double maxDiffLess = 0.0;    // For one-sided test: sample1 CDF < sample2 CDF (sample1 has higher values)
        double maxDiffGreater = 0.0; // For one-sided test: sample1 CDF > sample2 CDF (sample1 has lower values)

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
            return (0.0, 1.0);
        }

        // Compute p-value using asymptotic approximation
        double pValue = ComputeKSPValue(ksStatistic, n1, n2, alternative);

        // Final validation
        if (double.IsNaN(pValue) || double.IsInfinity(pValue))
        {
            return (ksStatistic, 1.0);
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
