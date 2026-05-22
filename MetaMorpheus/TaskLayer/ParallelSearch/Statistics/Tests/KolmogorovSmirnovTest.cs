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
    StatisticalEvidenceFamily evidenceFamily,
    Func<TransientDatabaseMetrics, double[]> sampleScoresExtractor,
    Func<TransientDatabaseMetrics, bool>? isDefinedFor = null,
    KSAlternative ksMode = KSAlternative.Less)
    : StatisticalTestBase(metricName, evidenceFamily, isDefinedFor)
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

    public override double? GetEffectSize(TransientDatabaseMetrics result, List<TransientDatabaseMetrics> allResults)
    {
        if (!IsDefinedFor(result) || allResults == null)
            return null;

        var sample = (sampleScoresExtractor(result) ?? Array.Empty<double>())
            .Where(s => !double.IsNaN(s) && !double.IsInfinity(s))
            .ToList();

        if (sample.Count == 0)
            return null;

        // Compute global median once from all DB scores, not per-DB leave-one-out
        // This is O(N_all log N_all) total instead of O(D * N_all log N_all) per DB
        var global = allResults
            .Where(r => IsDefinedFor(r))
            .SelectMany(r => sampleScoresExtractor(r) ?? Array.Empty<double>())
            .Where(s => !double.IsNaN(s) && !double.IsInfinity(s))
            .ToList();

        if (global.Count == 0)
            return null;

        global.Sort();
        double globalMedian = global[global.Count / 2];

        sample.Sort();
        double sampleMedian = sample[sample.Count / 2];

        return sampleMedian - globalMedian;
    }

    public override bool IsDefinedFor(TransientDatabaseMetrics result)
    {
        if (!base.IsDefinedFor(result))
            return false;

        var scores = sampleScoresExtractor(result);
        if (scores == null || scores.Length == 0)
            return false;

        for (int i = 0; i < scores.Length; i++)
            if (!double.IsNaN(scores[i]) && !double.IsInfinity(scores[i]))
                return true;
        return false;
    }

    public override string? GetUndefinedReason(TransientDatabaseMetrics result)
    {
        var baseReason = base.GetUndefinedReason(result);
        if (baseReason != null)
            return baseReason;

        var scores = sampleScoresExtractor(result);
        if (scores == null || scores.Length == 0)
            return "NoScoresAvailable";

        for (int i = 0; i < scores.Length; i++)
            if (!double.IsNaN(scores[i]) && !double.IsInfinity(scores[i]))
                return null;
        return "NoFiniteScoresAvailable";
    }

    public override bool CanRun(List<TransientDatabaseMetrics> allResults)
    {
        if (allResults == null || allResults.Count(IsDefinedFor) < 2)
            return false;

        return allResults
            .Where(IsDefinedFor)
            .SelectMany(r => sampleScoresExtractor(r) ?? Array.Empty<double>())
            .Any(s => !double.IsNaN(s) && !double.IsInfinity(s));
    }

    public override Dictionary<string, double> ComputePValues(List<TransientDatabaseMetrics> allResults)
    {
        // Phase 1: Build per-database sorted score arrays (once)
        var databaseScores = allResults
            .Where(IsDefinedFor)
            .Select(r => (
                DbName: r.DatabaseName,
                Scores: (sampleScoresExtractor(r) ?? Array.Empty<double>())
                    .Where(s => !double.IsNaN(s) && !double.IsInfinity(s))
                    .OrderBy(s => s)
                    .ToArray()
            ))
            .Where(x => x.Scores.Length > 0)
            .ToDictionary(x => x.DbName, x => x.Scores);

        if (databaseScores.Count == 0)
        {
            return allResults.ToDictionary(r => r.DatabaseName, _ => double.NaN);
        }

        // Phase 2: Build global sorted array ONCE.
        // The global set lets us compute the KS statistic against the leave-one-out
        // background via the identity: D_loo = N_all / (N_all - N_db) * max|F_db - F_all|
        var globalScores = databaseScores.Values
            .SelectMany(s => s)
            .OrderBy(s => s)
            .ToArray();
        int totalScoreCount = globalScores.Length;

        var pValues = new ConcurrentDictionary<string, double>();

        // Phase 3: Test each database's scores against the leave-one-out background
        // using the global-sweep approach (avoids O(N²) concatenation + re-sort per DB)
        Parallel.ForEach(Partitioner.Create(0, allResults.Count),
            new ParallelOptions { MaxDegreeOfParallelism = 10 },
            (partition) =>
        {
            for (int i = partition.Item1; i < partition.Item2; i++)
            {
                var result = allResults[i];

                if (!databaseScores.TryGetValue(result.DatabaseName, out var dbScores))
                {
                    pValues.TryAdd(result.DatabaseName, double.NaN);
                    continue;
                }

                int nDb = dbScores.Length;
                int nBg = totalScoreCount - nDb;

                if (nBg == 0)
                {
                    pValues.TryAdd(result.DatabaseName, double.NaN);
                    continue;
                }

                // Sweep through global array and DB array simultaneously to compute
                // max|F_db - F_all|, max(F_all - F_db), max(F_db - F_all).
                // Then scale by N_all / (N_all - N_db) for the leave-one-out KS statistic.
                double maxDiff = 0.0, maxLess = 0.0, maxGreater = 0.0;
                int di = 0, gi = 0;

                while (di < nDb || gi < totalScoreCount)
                {
                    // Pick the smaller value; if equal, advance both (handles ties correctly)
                    bool advanceDb = gi >= totalScoreCount || (di < nDb && dbScores[di] <= globalScores[gi]);
                    bool advanceGlobal = di >= nDb || (gi < totalScoreCount && globalScores[gi] <= dbScores[Math.Min(di, nDb - 1)]);

                    if (advanceDb)
                    {
                        double val = dbScores[di];
                        while (di < nDb && dbScores[di] == val) di++;
                    }
                    if (advanceGlobal)
                    {
                        double val = globalScores[gi];
                        while (gi < totalScoreCount && globalScores[gi] == val) gi++;
                    }

                    double fDb = (double)di / nDb;
                    double fAll = (double)gi / totalScoreCount;

                    double absDiff = Math.Abs(fDb - fAll);
                    if (absDiff > maxDiff) maxDiff = absDiff;

                    double diffLess = fAll - fDb;
                    if (diffLess > maxLess) maxLess = diffLess;

                    double diffGreater = fDb - fAll;
                    if (diffGreater > maxGreater) maxGreater = diffGreater;
                }

                // Scale for leave-one-out: D_loo = N_all / (N_all - N_db) * D_db_vs_all
                double scale = (double)totalScoreCount / nBg;
                maxLess *= scale;
                maxGreater *= scale;
                maxDiff *= scale;

                double ksStat = ksMode switch
                {
                    KSAlternative.Less => maxLess,
                    KSAlternative.Greater => maxGreater,
                    _ => maxDiff
                };

                try
                {
                    double pValue = ComputeKSPValue(ksStat, nDb, nBg, ksMode);

                    if (double.IsNaN(pValue) || double.IsInfinity(pValue))
                    {
                        pValues.TryAdd(result.DatabaseName, double.NaN);
                    }
                    else
                    {
                        pValue = Math.Max(1e-300, Math.Min(1.0, pValue));
                        pValues.TryAdd(result.DatabaseName, pValue);
                        result.Results[$"KolmSmir_{MetricName}_KS"] = ksStat;
                    }
                }
                catch
                {
                    pValues.TryAdd(result.DatabaseName, double.NaN);
                }
            }
        });

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
