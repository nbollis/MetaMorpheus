#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MathNet.Numerics;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Negative Binomial distribution test for count data
/// Tests for overdispersion and uses appropriate distribution (Negative Binomial or Poisson).
/// The counts are raw: database size is not taken into account, so a large proteome sits higher by size alone. For
/// counts that scale with size use <see cref="SizeOffsetPoissonTest"/>.
/// </summary>
public class NegativeBinomialTest<TNumeric>(
    string metricName,
    StatisticalEvidenceFamily evidenceFamily,
    Func<TransientDatabaseMetrics, TNumeric> countExtractor,
    Func<TransientDatabaseMetrics, bool>? isDefinedFor = null,
    bool isLowerTailTest = false)
    : StatisticalTestBase(metricName, evidenceFamily, isDefinedFor)
    where TNumeric : INumber<TNumeric>
{
    public override string TestName => "NegativeBinomial";
    public override string Description =>
        $"Tests if {MetricName} counts exceed a negative binomial (or Poisson) fitted to the raw counts of all databases";

    public override double GetTestValue(TransientDatabaseMetrics result) => ToDouble(countExtractor(result));

    public override double? GetEffectSize(TransientDatabaseMetrics result, List<TransientDatabaseMetrics> allResults)
    {
        if (!IsDefinedFor(result) || allResults == null)
            return null;

        double? mean = Pooled(allResults, results =>
        {
            var values = results
                .Where(IsDefinedFor)
                .Select(r =>
                {
                    try
                    {
                        return Convert.ToDouble(countExtractor(r));
                    }
                    catch
                    {
                        return double.NaN;
                    }
                })
                .Where(v => !double.IsNaN(v) && !double.IsInfinity(v) && v >= 0)
                .ToArray();
            return values.Length == 0 ? (double?)null : values.Average();
        });

        if (mean is not double m)
            return null;

        double observed = Convert.ToDouble(countExtractor(result));
        return SafeRatio(observed, m);
    }

    public override bool IsDefinedFor(TransientDatabaseMetrics result)
    {
        if (!base.IsDefinedFor(result))
            return false;

        try
        {
            double value = Convert.ToDouble(countExtractor(result));
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
        }
        catch
        {
            return false;
        }
    }

    public override string? GetUndefinedReason(TransientDatabaseMetrics result)
    {
        var baseReason = base.GetUndefinedReason(result);
        if (baseReason != null)
            return baseReason;

        try
        {
            double value = Convert.ToDouble(countExtractor(result));
            return double.IsNaN(value) || double.IsInfinity(value) || value < 0
                ? "CountNotFiniteOrNegative"
                : null;
        }
        catch
        {
            return "CountExtractionFailed";
        }
    }

    public override Dictionary<string, double> ComputePValues(List<TransientDatabaseMetrics> allResults)
    {
        if (allResults == null) throw new ArgumentNullException(nameof(allResults));

        // 1) Extract counts and decide skips (NaN output) up-front.
        var extracted = new (TransientDatabaseMetrics item, double value, bool skip)[allResults.Count];

        for (int i = 0; i < allResults.Count; i++)
        {
            var r = allResults[i];
            bool skip = !IsDefinedFor(r);

            double v;
            try
            {
                v = Convert.ToDouble(countExtractor(r));
            }
            catch
            {
                v = double.NaN;
                skip = true;
            }

            if (double.IsNaN(v) || double.IsInfinity(v) || v < 0)
                skip = true;

            extracted[i] = (r, v, skip);
        }

        // 2) Fit NB parameters (method-of-moments) on non-skipped counts.
        var fitValues = extracted.Where(t => !t.skip).Select(t => t.value).ToArray();

        if (fitValues.Length < 2)
        {
            return extracted.ToDictionary(t => t.item.DatabaseName, t => double.NaN);
        }

        double mean = fitValues.Average();
        double variance = SampleVariance(fitValues, mean);

        if (mean <= 0 || double.IsNaN(mean) || double.IsInfinity(mean))
        {
            return extracted.ToDictionary(t => t.item.DatabaseName, t => double.NaN);
        }

        // NB (r,p): mean = r*(1-p)/p ; var = mean + mean^2/r. With variance <= mean there is no overdispersion
        // and the count is Poisson. (This used to set r = 1e12 instead, where the hand-written incomplete beta broke
        // down and returned a CDF of about 1, so the upper tail came out 0 and was floored at 1e-300: databases with
        // two or three hits against a null of mean ~1.5 scored 1e-300 on the regression panel.)
        bool poisson = !(variance > mean);
        double rParam = poisson ? double.PositiveInfinity : (mean * mean) / (variance - mean);
        if (!poisson && (double.IsNaN(rParam) || double.IsInfinity(rParam) || rParam <= 0))
        {
            return extracted.ToDictionary(t => t.item.DatabaseName, t => double.NaN);
        }

        double pParam = poisson ? 1.0 : rParam / (rParam + mean);
        if (!poisson && (pParam <= 0 || pParam >= 1 || double.IsNaN(pParam)))
        {
            return extracted.ToDictionary(t => t.item.DatabaseName, t => double.NaN);
        }

        // 3) Compute one-sided p-values per database.
        var output = new Dictionary<string, double>(allResults.Count);

        foreach (var t in extracted)
        {
            if (t.skip)
            {
                output[t.item.DatabaseName] = double.NaN;
                continue;
            }

            // Treat as integer count.
            int k = (int)Math.Round(t.value);
            if (k < 0)
            {
                output[t.item.DatabaseName] = double.NaN;
                continue;
            }

            double pValue = isLowerTailTest
                ? (poisson ? PoissonLowerTail(k, mean) : NegativeBinomialLowerTail(k, rParam, pParam))
                : (poisson ? PoissonUpperTail(k, mean) : NegativeBinomialUpperTail(k, rParam, pParam));

            output[t.item.DatabaseName] = Math.Max(1e-300, Math.Min(1.0, pValue));
        }

        return output;
    }

    private static double SampleVariance(double[] values, double mean)
    {
        if (values.Length < 2) return 0.0;
        double sumSq = 0.0;
        for (int i = 0; i < values.Length; i++)
        {
            double d = values[i] - mean;
            sumSq += d * d;
        }
        return sumSq / (values.Length - 1);
    }

    // Tails computed directly with MathNet's regularized special functions, never as 1 - CDF, so small tail
    // probabilities keep their precision.

    /// <summary>NB P(X ≥ k) = I_{1-p}(k, r) for k ≥ 1.</summary>
    internal static double NegativeBinomialUpperTail(int k, double r, double p) =>
        k <= 0 ? 1.0 : SpecialFunctions.BetaRegularized(k, r, 1.0 - p);

    /// <summary>NB P(X ≤ k) = I_p(r, k + 1).</summary>
    internal static double NegativeBinomialLowerTail(int k, double r, double p) =>
        k < 0 ? 0.0 : SpecialFunctions.BetaRegularized(r, k + 1.0, p);

    /// <summary>Poisson P(X ≥ k) = P(k, λ), the regularized lower incomplete gamma, for k ≥ 1.</summary>
    internal static double PoissonUpperTail(int k, double lambda) =>
        k <= 0 ? 1.0 : SpecialFunctions.GammaLowerRegularized(k, lambda);

    /// <summary>Poisson P(X ≤ k) = Q(k + 1, λ), the regularized upper incomplete gamma.</summary>
    internal static double PoissonLowerTail(int k, double lambda) =>
        k < 0 ? 0.0 : SpecialFunctions.GammaUpperRegularized(k + 1.0, lambda);
}
