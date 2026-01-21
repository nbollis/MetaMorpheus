#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MathNet.Numerics.Distributions;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Negative Binomial distribution test for count data
/// Tests for overdispersion and uses appropriate distribution (Negative Binomial or Poisson)
/// Normalizes by proteome size to account for database size differences
/// </summary>
public class NegativeBinomialTest<TNumeric> : StatisticalTestBase where TNumeric : INumber<TNumeric>
{
    private readonly Func<TransientDatabaseMetrics, TNumeric> _dataPointExtractor;
    private readonly string _proteomeSizeColumn;

    public override string TestName => "NegativeBinomial";
    public override string Description =>
        $"Tests if {MetricName} counts show overdispersion and exceed expected rates (normalized by proteome size)";

    public NegativeBinomialTest(
        string metricName,
        Func<TransientDatabaseMetrics, TNumeric> countExtractor,
        string proteomeSizeColumn = "", 
        Func<TransientDatabaseMetrics, bool>? shouldSkip = null) : base(metricName, shouldSkip: shouldSkip)
    {
        _dataPointExtractor = countExtractor;
        _proteomeSizeColumn = proteomeSizeColumn;
    }

    public override double GetTestValue(TransientDatabaseMetrics result) => ToDouble(_dataPointExtractor(result)) / Math.Max(GetProteomeSize(result), 1.0);

    #region Predefined Tests

    public static NegativeBinomialTest<double> ForPsm(string proteomeSizeColumn = "TransientProteinCount") =>
        new("PSM", r => r.PsmBacterialUnambiguousTargets, proteomeSizeColumn);

    public static NegativeBinomialTest<double> ForPeptide(string proteomeSizeColumn = "TransientProteinCount") =>
        new("Peptide", r => r.PeptideBacterialUnambiguousTargets, proteomeSizeColumn);

    public static NegativeBinomialTest<double> ForProteinGroup(string proteomeSizeColumn = "TransientProteinCount") =>
        new("ProteinGroup", r => r.ProteinGroupBacterialUnambiguousTargets, proteomeSizeColumn);


    // Fragment ion tests need at least 2 results per database. 

    public static NegativeBinomialTest<double> ForPsmComplementary() =>
        new("PSM-Complementary", r => r.Psm_ComplementaryCount_MedianTargets,
            shouldSkip: r => r.TargetPsmsFromTransientDbAtQValueThreshold < 2);

    public static NegativeBinomialTest<double> ForPsmBidirectional() =>
        new("PSM-Bidirectional", r => r.Psm_Bidirectional_MedianTargets,
            shouldSkip: r => r.TargetPsmsFromTransientDbAtQValueThreshold < 2);

    public static NegativeBinomialTest<double> ForPsmSequenceCoverage() =>
        new("PSM-SequenceCoverage", r => r.Psm_SequenceCoverageFraction_MedianTargets, 
            shouldSkip: r => r.TargetPsmsFromTransientDbAtQValueThreshold < 2);

    public static NegativeBinomialTest<double> ForPeptideComplementary() =>
        new("Peptide-Complementary", r => r.Peptide_ComplementaryCount_MedianTargets,
            shouldSkip: r => r.TargetPeptidesFromTransientDbAtQValueThreshold < 2);

    public static NegativeBinomialTest<double> ForPeptideBidirectional() =>
        new("Peptide-Bidirectional", r => r.Peptide_Bidirectional_MedianTargets, 
            shouldSkip: r => r.TargetPeptidesFromTransientDbAtQValueThreshold < 2);
    public static NegativeBinomialTest<double> ForPeptideSequenceCoverage() =>
        new("Peptide-SequenceCoverage", r => r.Peptide_SequenceCoverageFraction_MedianTargets, 
            shouldSkip: r => r.TargetPeptidesFromTransientDbAtQValueThreshold < 2);

    #endregion

    protected TNumeric GetObservedCount(TransientDatabaseMetrics result)
    {
        return _dataPointExtractor(result);
    }

    private int GetProteomeSize(TransientDatabaseMetrics result)
    {
        return _proteomeSizeColumn switch
        {
            "TransientProteinCount" => result.TransientProteinCount,
            "TransientPeptideCount" => result.TransientPeptideCount,
            "TotalProteins" => result.TotalProteins,
            _ => 1
        };
    }

    public override Dictionary<string, double> ComputePValues(List<TransientDatabaseMetrics> allResults)
    {
        // Extract counts and proteome sizes
        var counts = allResults.Select(r => ToDouble(GetObservedCount(r))).ToArray();
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
                double observedCount = counts[i];

                // Handle zero or negative counts
                if (observedCount <= 0 || (ShouldSkip != null && ShouldSkip(allResults[i])))
                {
                    pValues[allResults[i].DatabaseName] = double.NaN;
                    continue;
                }

                try
                {
                    // P(X >= observed) = 1 - P(X <= observed-1)
                    double cdf = nb.CumulativeDistribution(Math.Floor(observedCount) - 1);

                    if (double.IsNaN(cdf) || double.IsInfinity(cdf))
                    {
                        Console.WriteLine($"  Warning: Invalid CDF for {allResults[i].DatabaseName}, setting p-value = 1.0");
                        pValues[allResults[i].DatabaseName] = double.NaN;
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
                    pValues[allResults[i].DatabaseName] = double.NaN;
                }
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
        List<TransientDatabaseMetrics> allResults,
        double meanRate,
        double[] proteomeSizes)
    {
        var pValues = new Dictionary<string, double>();

        for (int i = 0; i < allResults.Count; i++)
        {
            if (ShouldSkip != null && ShouldSkip(allResults[i]))
            {
                pValues[allResults[i].DatabaseName] = double.NaN;
                continue;
            }

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
