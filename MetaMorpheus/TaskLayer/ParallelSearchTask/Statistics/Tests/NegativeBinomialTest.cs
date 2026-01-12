#nullable enable
using MathNet.Numerics.Distributions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TaskLayer.ParallelSearchTask.Analysis;

namespace TaskLayer.ParallelSearchTask.Statistics;

/// <summary>
/// Negative Binomial distribution test for count data
/// Tests for overdispersion and uses appropriate distribution (Negative Binomial or Poisson)
/// Normalizes by proteome size to account for database size differences
/// </summary>
public class NegativeBinomialTest<TNumeric> : StatisticalTestBase where TNumeric : INumber<TNumeric>
{
    private readonly string _metricName;
    private readonly Func<AggregatedAnalysisResult, TNumeric> _countExtractor;
    private readonly string _proteomeSizeColumn;

    public override string TestName => "NegativeBinomial";
    public override string MetricName => _metricName;
    public override string Description =>
        $"Tests if {_metricName} counts show overdispersion and exceed expected rates (normalized by proteome size)";

    public NegativeBinomialTest(
        string metricName,
        Func<AggregatedAnalysisResult, TNumeric> countExtractor,
        string proteomeSizeColumn = "TransientProteinCount")
    {
        _metricName = metricName;
        _countExtractor = countExtractor;
        _proteomeSizeColumn = proteomeSizeColumn;
    }

    // Convenience constructors for common metrics
    public static NegativeBinomialTest<int> ForPsm(string proteomeSizeColumn = "TransientProteinCount") =>
        new("PSM", r => r.PsmBacterialUnambiguousTargets, proteomeSizeColumn);

    public static NegativeBinomialTest<int> ForPeptide(string proteomeSizeColumn = "TransientProteinCount") =>
        new("Peptide", r => r.PeptideBacterialUnambiguousTargets, proteomeSizeColumn);

    public static NegativeBinomialTest<int> ForProteinGroup(string proteomeSizeColumn = "TransientProteinCount") =>
        new("ProteinGroup", r => r.ProteinGroupBacterialUnambiguousTargets, proteomeSizeColumn);

    public static NegativeBinomialTest<double> ForPsmComplementary(string proteomeSizeColumn = "TransientProteinCount") =>
        new("PSM-Complementary", r => r.Psm_ComplementaryCount_MedianTargets);

    public static NegativeBinomialTest<double> ForPsmBidirectional(string proteomeSizeColumn = "TransientProteinCount") =>
        new("PSM-Bidirectional", r => r.Psm_Bidirectional_MedianTargets);

    public static NegativeBinomialTest<double> ForPsmSequenceCoverage(string proteomeSizeColumn = "TransientProteinCount") =>
        new("PSM-SequenceCoverage", r => r.Psm_SequenceCoverageFraction_MedianTargets);

    public static NegativeBinomialTest<double> ForPeptideComplementary(string proteomeSizeColumn = "TransientProteinCount") =>
        new("Peptide-Complementary", r => r.Peptide_ComplementaryCount_MedianTargets);

    public static NegativeBinomialTest<double> ForPeptideBidirectional(string proteomeSizeColumn = "TransientProteinCount") =>
        new("Peptide-Bidirectional", r => r.Peptide_Bidirectional_MedianTargets);

    public static NegativeBinomialTest<double> ForPeptideSequenceCoverage(string proteomeSizeColumn = "TransientProteinCount") =>
        new("Peptide-SequenceCoverage", r => r.Peptide_SequenceCoverageFraction_MedianTargets);

    protected TNumeric GetObservedCount(AggregatedAnalysisResult result)
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
        var counts = allResults.Select(r => ToDouble(GetObservedCount(r))).ToArray();
        var proteomeSizes = allResults.Select(r => (double)GetProteomeSize(r)).ToArray();

        // Validate that we have meaningful data
        if (counts.All(c => c == 0))
        {
            Console.WriteLine($"{MetricName} Negative Binomial Test: All counts are zero, assigning p-value = 1.0 for all databases");
            return allResults.ToDictionary(r => r.DatabaseName, r => 1.0);
        }

        if (proteomeSizes.All(s => s == 0))
        {
            Console.WriteLine($"{MetricName} Negative Binomial Test: All proteome sizes are zero, cannot normalize - assigning p-value = 1.0");
            return allResults.ToDictionary(r => r.DatabaseName, r => 1.0);
        }

        // Normalize counts by proteome size to get rates
        // Filter out databases with 0 proteome size for rate calculation
        var validIndices = proteomeSizes
            .Select((size, idx) => (size, idx))
            .Where(x => x.size > 0)
            .Select(x => x.idx)
            .ToArray();

        if (validIndices.Length < 2)
        {
            Console.WriteLine($"{MetricName} Negative Binomial Test: Insufficient valid proteome sizes, assigning p-value = 1.0");
            return allResults.ToDictionary(r => r.DatabaseName, r => 1.0);
        }

        var rates = validIndices
            .Select(i => counts[i] / proteomeSizes[i])
            .Where(r => !double.IsNaN(r) && !double.IsInfinity(r))
            .ToArray();

        if (rates.Length < 2)
        {
            Console.WriteLine($"{MetricName} Negative Binomial Test: Insufficient valid rates, assigning p-value = 1.0");
            return allResults.ToDictionary(r => r.DatabaseName, r => 1.0);
        }

        // Calculate mean and variance of rates
        double meanRate = rates.Average();
        double varianceRate = rates.Select(r => Math.Pow(r - meanRate, 2)).Average();

        // Handle edge case where mean rate is effectively zero
        if (meanRate < 1e-10)
        {
            Console.WriteLine($"{MetricName} Negative Binomial Test: Mean rate too close to zero ({meanRate:E2}), assigning p-value = 1.0");
            return allResults.ToDictionary(r => r.DatabaseName, r => 1.0);
        }

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
            try
            {
                var nb = new NegativeBinomial(r, p);

                for (int i = 0; i < allResults.Count; i++)
                {
                    double observedCount = counts[i];
                    double proteomeSize = proteomeSizes[i];

                    // Handle databases with 0 proteome size
                    if (proteomeSize <= 0)
                    {
                        pValues[allResults[i].DatabaseName] = 1.0;
                        continue;
                    }

                    // Handle zero or negative counts
                    if (observedCount <= 0)
                    {
                        pValues[allResults[i].DatabaseName] = 1.0;
                        continue;
                    }

                    try
                    {
                        // P(X >= observed) = 1 - P(X <= observed-1)
                        double cdf = nb.CumulativeDistribution(Math.Floor(observedCount) - 1);

                        if (double.IsNaN(cdf) || double.IsInfinity(cdf))
                        {
                            Console.WriteLine($"  Warning: Invalid CDF for {allResults[i].DatabaseName}, setting p-value = 1.0");
                            pValues[allResults[i].DatabaseName] = 1.0;
                            continue;
                        }

                        double pValue = 1.0 - cdf;

                        // Clamp p-value to valid range
                        pValue = Math.Max(1e-300, Math.Min(1.0, pValue));

                        pValues[allResults[i].DatabaseName] = pValue;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Error computing NB CDF for {allResults[i].DatabaseName}: {ex.Message}");
                        pValues[allResults[i].DatabaseName] = 1.0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR creating NegativeBinomial distribution: {ex.Message}. Falling back to Poisson.");
                return ComputePoissonPValues(allResults, meanRate, proteomeSizes);
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
            double observedCount = ToDouble(GetObservedCount(allResults[i]));
            double expectedCount = meanRate * proteomeSizes[i];

            var poisson = new Poisson(expectedCount);
            // P(X >= observed) = 1 - P(X <= observed-1)
            double pValue = 1.0 - poisson.CumulativeDistribution(Math.Floor(observedCount) - 1);
            pValues[allResults[i].DatabaseName] = Math.Max(pValue, 1e-300); // Avoid exactly 0
        }

        return pValues;
    }
}
