#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Distributions;
using TaskLayer.ParallelSearchTask.Analysis;

namespace TaskLayer.ParallelSearchTask.Statistics;

/// <summary>
/// Negative Binomial distribution test for count data
/// Tests for overdispersion and uses appropriate distribution (Negative Binomial or Poisson)
/// Normalizes by proteome size to account for database size differences
/// </summary>
public class NegativeBinomialTest : StatisticalTestBase
{
    private readonly string _metricName;
    private readonly Func<AggregatedAnalysisResult, int> _countExtractor;
    private readonly string _proteomeSizeColumn;

    public override string TestName => "NegativeBinomial";
    public override string MetricName => _metricName;
    public override string Description =>
        $"Tests if {_metricName} counts show overdispersion and exceed expected rates (normalized by proteome size)";

    public NegativeBinomialTest(
        string metricName,
        Func<AggregatedAnalysisResult, int> countExtractor,
        string proteomeSizeColumn = "TransientProteinCount")
    {
        _metricName = metricName;
        _countExtractor = countExtractor;
        _proteomeSizeColumn = proteomeSizeColumn;
    }

    // Convenience constructors for common metrics
    public static NegativeBinomialTest ForPsm(string proteomeSizeColumn = "TransientProteinCount") =>
        new("PSM", r => r.PsmBacterialUnambiguousTargets, proteomeSizeColumn);

    public static NegativeBinomialTest ForPeptide(string proteomeSizeColumn = "TransientProteinCount") =>
        new("Peptide", r => r.PeptideBacterialUnambiguousTargets, proteomeSizeColumn);

    public static NegativeBinomialTest ForProteinGroup(string proteomeSizeColumn = "TransientProteinCount") =>
        new("ProteinGroup", r => r.ProteinGroupBacterialUnambiguousTargets, proteomeSizeColumn);

    protected override int GetObservedCount(AggregatedAnalysisResult result)
    {
        return _countExtractor(result);
    }

    private int GetProteomeSize(AggregatedAnalysisResult result)
    {
        return _proteomeSizeColumn switch
        {
            "TransientProteinCount" => result.TransientProteinCount,
            "TransientPeptideCount" => result.TransientPeptideCount,
            "TotalProteins" => result.TotalProteins,
            _ => result.TransientProteinCount
        };
    }

    public override Dictionary<string, double> ComputePValues(List<AggregatedAnalysisResult> allResults)
    {
        // Extract counts and proteome sizes
        var counts = allResults.Select(r => (double)GetObservedCount(r)).ToArray();
        var proteomeSizes = allResults.Select(r => (double)GetProteomeSize(r)).ToArray();

        // Normalize counts by proteome size to get rates
        var rates = counts.Zip(proteomeSizes, (count, size) => count / Math.Max(size, 1.0)).ToArray();

        // Calculate mean and variance of rates
        double meanRate = rates.Average();
        double varianceRate = rates.Select(r => Math.Pow(r - meanRate, 2)).Average();

        Console.WriteLine($"{MetricName} Negative Binomial Test:");
        Console.WriteLine($"  Mean rate: {meanRate:F6}");
        Console.WriteLine($"  Variance rate: {varianceRate:F6}");
        Console.WriteLine($"  Dispersion: {varianceRate / meanRate:F2}x");

        var pValues = new Dictionary<string, double>();

        // Check for overdispersion
        if (varianceRate > meanRate * 1.1) // Allow 10% tolerance for numerical stability
        {
            Console.WriteLine($"  Using Negative Binomial (overdispersed)");

            // Estimate Negative Binomial parameters
            // For NB: E[X] = r*p/(1-p), Var[X] = r*p/(1-p)^2
            // Solving: p = mean/variance, r = mean*p/(1-p)
            double p = Math.Min(0.999, meanRate / varianceRate); // Clip p to avoid r -> infinity
            double r = meanRate * p / (1 - p);

            // Validate parameters
            if (double.IsNaN(r) || double.IsInfinity(r) || r <= 0 || p <= 0 || p >= 1)
            {
                Console.WriteLine($"  WARNING: Invalid NB parameters (r={r:F2}, p={p:F4}). Falling back to Poisson.");
                return ComputePoissonPValues(allResults, meanRate, proteomeSizes);
            }

            Console.WriteLine($"  NB parameters: r={r:F2}, p={p:F4}");

            // Compute p-values for each organism
            var nb = new NegativeBinomial(r, p);

            for (int i = 0; i < allResults.Count; i++)
            {
                int observedCount = (int)counts[i];
                // P(X >= observed) = 1 - P(X <= observed-1)
                double pValue = 1.0 - nb.CumulativeDistribution(observedCount - 1);
                pValues[allResults[i].DatabaseName] = Math.Max(pValue, 1e-300); // Avoid exactly 0
            }
        }
        else
        {
            Console.WriteLine($"  Using Poisson (not overdispersed)");
            return ComputePoissonPValues(allResults, meanRate, proteomeSizes);
        }

        return pValues;
    }

    /// <summary>
    /// Compute p-values using Poisson distribution
    /// </summary>
    private Dictionary<string, double> ComputePoissonPValues(
        List<AggregatedAnalysisResult> allResults,
        double meanRate,
        double[] proteomeSizes)
    {
        var pValues = new Dictionary<string, double>();

        for (int i = 0; i < allResults.Count; i++)
        {
            int observedCount = GetObservedCount(allResults[i]);
            double expectedCount = meanRate * proteomeSizes[i];

            var poisson = new Poisson(expectedCount);
            // P(X >= observed) = 1 - P(X <= observed-1)
            double pValue = 1.0 - poisson.CumulativeDistribution(observedCount - 1);
            pValues[allResults[i].DatabaseName] = Math.Max(pValue, 1e-300); // Avoid exactly 0
        }

        return pValues;
    }
}
