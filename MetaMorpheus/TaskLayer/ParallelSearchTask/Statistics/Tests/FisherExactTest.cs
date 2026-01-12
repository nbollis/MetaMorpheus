#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearchTask.Analysis;

namespace TaskLayer.ParallelSearchTask.Statistics;

/// <summary>
/// Fisher's Exact Test for Odds Ratio analysis
/// Tests if organisms show enrichment for UNAMBIGUOUS peptides compared to the dataset average
/// High OR = disproportionately more unambiguous evidence (stronger biological signal vs random noise)
/// </summary>
public class FisherExactTest : StatisticalTestBase
{
    private readonly string _metricName;
    private readonly Func<AggregatedAnalysisResult, int> _unambiguousExtractor;
    private readonly Func<AggregatedAnalysisResult, int> _ambiguousExtractor;

    public override string TestName => "FisherExact";
    public override string MetricName => _metricName;
    public override string Description =>
        $"Tests if {_metricName} show enrichment for unambiguous evidence (Odds Ratio analysis)";

    public FisherExactTest(
        string metricName,
        Func<AggregatedAnalysisResult, int> unambiguousExtractor,
        Func<AggregatedAnalysisResult, int> ambiguousExtractor)
    {
        _metricName = metricName;
        _unambiguousExtractor = unambiguousExtractor;
        _ambiguousExtractor = ambiguousExtractor;
        MinimumSampleSize = 1; // Need at least 1 peptide
    }

    public static FisherExactTest ForPsm() =>
        new("PSM",
            r => r.PsmBacterialUnambiguousTargets,
            r => r.PsmBacterialAmbiguous);

    public static FisherExactTest ForPeptide() =>
        new("Peptide",
            r => r.PeptideBacterialUnambiguousTargets,
            r => r.PeptideBacterialAmbiguous);

    public override bool CanRun(List<AggregatedAnalysisResult> allResults)
    {
        if (allResults == null || allResults.Count < 2)
            return false;

        // Need some observations
        int totalUnambiguous = allResults.Sum(r => _unambiguousExtractor(r));
        int totalAmbiguous = allResults.Sum(r => _ambiguousExtractor(r));
        
        return totalUnambiguous > 0 || totalAmbiguous > 0;
    }

    public override Dictionary<string, double> ComputePValues(List<AggregatedAnalysisResult> allResults)
    {
        // Calculate dataset-wide baseline ratio
        int totalUnambiguous = allResults.Sum(r => _unambiguousExtractor(r));
        int totalAmbiguous = allResults.Sum(r => _ambiguousExtractor(r));
        double baselineRatio = totalUnambiguous / (double)Math.Max(totalAmbiguous, 1);

        Console.WriteLine($"{MetricName} Fisher's Exact Test:");
        Console.WriteLine($"  Total unambiguous: {totalUnambiguous}");
        Console.WriteLine($"  Total ambiguous: {totalAmbiguous}");
        Console.WriteLine($"  Baseline ratio: {baselineRatio:F3}");

        var pValues = new Dictionary<string, double>();

        foreach (var result in allResults)
        {
            // Get counts for this organism (with pseudocounts to handle zeros)
            int orgUnambig = _unambiguousExtractor(result);
            int orgAmbig = _ambiguousExtractor(result);

            // Skip if no evidence and set p to 1
            if (orgUnambig == 0 && orgAmbig == 0)
            {
                pValues[result.DatabaseName] = 1;
                continue;
            }

            // Calculate "other" counts (rest of dataset)
            int otherUnambig = totalUnambiguous - orgUnambig;
            int otherAmbig = totalAmbiguous - orgAmbig;

            // 2x2 contingency table with pseudocounts:
            // [[organism_unambig, organism_ambig],
            //  [other_unambig, other_ambig]]

            // Fisher's exact test p-value (one-sided: enrichment for unambiguous)
            double pValue = ComputeFisherExactPValue(
                orgUnambig, orgAmbig,
                otherUnambig, otherAmbig,
                alternative: FisherAlternative.Greater);

            // Clamp p-value to valid range
            pValue = Math.Max(1e-300, Math.Min(1.0, pValue));

            // Apply Haldane-Anscombe correction (add 0.5 to all cells) to handle zeros
            double a = orgUnambig + 0.5;
            double b = orgAmbig + 0.5;
            double c = otherUnambig + 0.5;
            double d = otherAmbig + 0.5;

            // Calculate odds ratio manually to avoid infinity
            // OR = (a*d) / (b*c)
            double oddsRatio = (a * d) / (b * c);

            pValues[result.DatabaseName] = pValue;
            result.Results[$"FisherExact_{MetricName}_OddsRatio"] = oddsRatio;
        }

        Console.WriteLine($"  Tested organisms: {pValues.Count}");

        return pValues;
    }

    /// <summary>
    /// Compute Fisher's Exact Test p-value for 2x2 contingency table
    /// Uses hypergeometric distribution
    /// </summary>
    private double ComputeFisherExactPValue(
        int a, int b, int c, int d, 
        FisherAlternative alternative = FisherAlternative.Greater)
    {
        // Contingency table:
        // | a  b | row1
        // | c  d | row2
        //  col1 col2

        int n11 = a;
        int n12 = b;
        int n21 = c;
        int n22 = d;

        int row1Total = n11 + n12;
        int row2Total = n21 + n22;
        int col1Total = n11 + n21;
        int col2Total = n12 + n22;
        int grandTotal = row1Total + row2Total;

        // Hypergeometric probability: P(X = n11 | row and column margins fixed)
        // P(n11) = C(col1Total, n11) * C(col2Total, n12) / C(grandTotal, row1Total)

        if (alternative == FisherAlternative.Greater)
        {
            // One-sided: P(X >= a)
            // Sum probabilities for all tables with n11 >= a
            double pValue = 0.0;
            int maxN11 = Math.Min(row1Total, col1Total);

            for (int x = a; x <= maxN11; x++)
            {
                pValue += HypergeometricProbability(x, row1Total, col1Total, grandTotal);
            }

            return Math.Max(0.0, Math.Min(1.0, pValue));
        }
        else if (alternative == FisherAlternative.Less)
        {
            // One-sided: P(X <= a)
            double pValue = 0.0;
            int minN11 = Math.Max(0, row1Total + col1Total - grandTotal);

            for (int x = minN11; x <= a; x++)
            {
                pValue += HypergeometricProbability(x, row1Total, col1Total, grandTotal);
            }

            return Math.Max(0.0, Math.Min(1.0, pValue));
        }
        else
        {
            // Two-sided: sum probabilities for all tables as or more extreme
            double observedProb = HypergeometricProbability(a, row1Total, col1Total, grandTotal);
            double pValue = 0.0;

            int minN11 = Math.Max(0, row1Total + col1Total - grandTotal);
            int maxN11 = Math.Min(row1Total, col1Total);

            for (int x = minN11; x <= maxN11; x++)
            {
                double prob = HypergeometricProbability(x, row1Total, col1Total, grandTotal);
                if (prob <= observedProb + 1e-10) // Tolerance for floating point
                {
                    pValue += prob;
                }
            }

            return Math.Max(0.0, Math.Min(1.0, pValue));
        }
    }

    /// <summary>
    /// Compute hypergeometric probability using log-space to avoid overflow
    /// P(X = k) = C(K, k) * C(N-K, n-k) / C(N, n)
    /// where N = total, K = col1Total, n = row1Total, k = n11
    /// </summary>
    private double HypergeometricProbability(int k, int n, int K, int N)
    {
        // P(X = k) = C(K, k) * C(N-K, n-k) / C(N, n)
        // Using log space: log(P) = log(C(K,k)) + log(C(N-K,n-k)) - log(C(N,n))

        if (k < 0 || k > n || k > K || (n - k) > (N - K))
            return 0.0;

        try
        {
            double logProb = LogBinomialCoefficient(K, k) 
                           + LogBinomialCoefficient(N - K, n - k)
                           - LogBinomialCoefficient(N, n);

            return Math.Exp(logProb);
        }
        catch
        {
            return 0.0;
        }
    }

    /// <summary>
    /// Compute log of binomial coefficient C(n, k) = n! / (k! * (n-k)!)
    /// Uses log-gamma function for numerical stability
    /// </summary>
    private double LogBinomialCoefficient(int n, int k)
    {
        if (k < 0 || k > n)
            return double.NegativeInfinity;

        if (k == 0 || k == n)
            return 0.0;

        // log(C(n,k)) = log(n!) - log(k!) - log((n-k)!)
        return LogFactorial(n) - LogFactorial(k) - LogFactorial(n - k);
    }

    /// <summary>
    /// Compute log(n!) using Stirling's approximation for large n
    /// </summary>
    private double LogFactorial(int n)
    {
        if (n <= 1)
            return 0.0;

        // Use lookup table for small values
        if (n <= 20)
        {
            double result = 0.0;
            for (int i = 2; i <= n; i++)
                result += Math.Log(i);
            return result;
        }

        // Stirling's approximation: log(n!) ≈ n*log(n) - n + 0.5*log(2*pi*n)
        return n * Math.Log(n) - n + 0.5 * Math.Log(2 * Math.PI * n);
    }

    private enum FisherAlternative
    {
        TwoSided,
        Greater, // Enrichment (OR > 1)
        Less     // Depletion (OR < 1)
    }
}
