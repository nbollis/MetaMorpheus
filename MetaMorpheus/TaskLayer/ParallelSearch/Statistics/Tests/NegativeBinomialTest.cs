#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Negative Binomial distribution test for count data
/// Tests for overdispersion and uses appropriate distribution (Negative Binomial or Poisson)
/// Normalizes by proteome size to account for database size differences
/// </summary>
public class NegativeBinomialTest<TNumeric>(
    string metricName,
    Func<TransientDatabaseMetrics, TNumeric> countExtractor,
    Func<TransientDatabaseMetrics, bool>? shouldSkip = null,
    bool isLowerTailTest = false)
    : StatisticalTestBase(metricName, shouldSkip)
    where TNumeric : INumber<TNumeric>
{
    public override string TestName => "NegativeBinomial";
    public override string Description =>
        $"Tests if {MetricName} counts show overdispersion and exceed expected rates (normalized by proteome size)";

    public override double GetTestValue(TransientDatabaseMetrics result) => ToDouble(countExtractor(result));

    public override Dictionary<string, double> ComputePValues(List<TransientDatabaseMetrics> allResults)
    {
        if (allResults == null) throw new ArgumentNullException(nameof(allResults));

        // 1) Extract counts and decide skips (NaN output) up-front.
        var extracted = new (TransientDatabaseMetrics item, double value, bool skip)[allResults.Count];

        for (int i = 0; i < allResults.Count; i++)
        {
            var r = allResults[i];
            bool skip = ShouldSkip?.Invoke(r) ?? false;

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

        // NB (r,p): mean = r*(1-p)/p ; var = mean + mean^2/r
        double rParam;
        if (variance > mean)
        {
            rParam = (mean * mean) / (variance - mean);
        }
        else
        {
            // Variance <= mean => approx Poisson; NB approaches Poisson as r->infty
            rParam = 1e12;
        }

        if (double.IsNaN(rParam) || double.IsInfinity(rParam) || rParam <= 0)
        {
            return extracted.ToDictionary(t => t.item.DatabaseName, t => double.NaN);
        }

        double pParam = rParam / (rParam + mean);
        if (pParam <= 0 || pParam >= 1 || double.IsNaN(pParam))
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

            // Treat as integer count for NB CDF.
            int k = (int)Math.Round(t.value);
            if (k < 0)
            {
                output[t.item.DatabaseName] = double.NaN;
                continue;
            }

            double pValue;
            if (!isLowerTailTest)
            {
                // P(X >= k) = 1 - P(X <= k-1)
                pValue = 1.0 - NegativeBinomialCdf(k - 1, rParam, pParam);
            }
            else
            {
                // P(X <= k)
                pValue = NegativeBinomialCdf(k, rParam, pParam);
            }

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

    // ---------------------------
    // Negative binomial CDF
    // ---------------------------
    // With r>0 and 0<p<1, and k integer >= 0:
    //   P(X <= k) = I_p(r, k+1)
    // where I is the regularized incomplete beta.
    private static double NegativeBinomialCdf(int k, double r, double p)
    {
        if (k < 0) return 0.0;
        return RegularizedIncompleteBeta(p, r, k + 1.0);
    }

    // ---------------------------
    // Regularized incomplete beta I_x(a,b)
    // ---------------------------
    private static double RegularizedIncompleteBeta(double x, double a, double b)
    {
        if (x <= 0.0) return 0.0;
        if (x >= 1.0) return 1.0;

        double lnBt = LogGamma(a + b) - LogGamma(a) - LogGamma(b)
                      + a * Math.Log(x) + b * Math.Log(1.0 - x);
        double bt = Math.Exp(lnBt);

        bool useDirect = x < (a + 1.0) / (a + b + 2.0);

        if (useDirect)
            return bt * BetaContinuedFraction(x, a, b) / a;

        return 1.0 - (bt * BetaContinuedFraction(1.0 - x, b, a) / b);
    }

    private static double BetaContinuedFraction(double x, double a, double b)
    {
        const int MAX_IT = 200;
        const double EPS = 3.0e-14;
        const double FPMIN = 1.0e-300;

        double qab = a + b;
        double qap = a + 1.0;
        double qam = a - 1.0;

        double c = 1.0;
        double d = 1.0 - qab * x / qap;
        if (Math.Abs(d) < FPMIN) d = FPMIN;
        d = 1.0 / d;
        double h = d;

        for (int m = 1; m <= MAX_IT; m++)
        {
            int m2 = 2 * m;

            // even step
            double aa = m * (b - m) * x / ((qam + m2) * (a + m2));
            d = 1.0 + aa * d;
            if (Math.Abs(d) < FPMIN) d = FPMIN;
            c = 1.0 + aa / c;
            if (Math.Abs(c) < FPMIN) c = FPMIN;
            d = 1.0 / d;
            h *= d * c;

            // odd step
            aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
            d = 1.0 + aa * d;
            if (Math.Abs(d) < FPMIN) d = FPMIN;
            c = 1.0 + aa / c;
            if (Math.Abs(c) < FPMIN) c = FPMIN;
            d = 1.0 / d;

            double del = d * c;
            h *= del;

            if (Math.Abs(del - 1.0) < EPS)
                break;
        }

        return h;
    }

    // ---------------------------
    // Log Gamma (Lanczos)
    // ---------------------------
    private static double LogGamma(double z)
    {
        double[] p =
        {
            0.99999999999980993,
            676.5203681218851,
            -1259.1392167224028,
            771.32342877765313,
            -176.61502916214059,
            12.507343278686905,
            -0.13857109526572012,
            9.9843695780195716e-6,
            1.5056327351493116e-7
        };

        if (z < 0.5)
            return Math.Log(Math.PI) - Math.Log(Math.Sin(Math.PI * z)) - LogGamma(1.0 - z);

        z -= 1.0;
        double x = p[0];
        for (int i = 1; i < p.Length; i++)
            x += p[i] / (z + i);

        double t = z + p.Length - 0.5;
        return 0.5 * Math.Log(2.0 * Math.PI) + (z + 0.5) * Math.Log(t) - t + Math.Log(x);
    }
}
