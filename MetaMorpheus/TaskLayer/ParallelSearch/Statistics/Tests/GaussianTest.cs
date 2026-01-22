#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Statistics;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Gaussian distribution test for count enrichment
/// Tests if observed counts are significantly higher than expected under a normal distribution
/// </summary>
public class GaussianTest<TNumeric>(
    string metricName,
    Func<TransientDatabaseMetrics, TNumeric> countExtractor,
    Func<TransientDatabaseMetrics, bool>? shouldSkip = null,
    bool isLowerTailTest = false)
    : StatisticalTestBase(metricName, shouldSkip: shouldSkip)
    where TNumeric : INumber<TNumeric>
{
    public override string TestName => "Gaussian";
    public override string Description =>
        $"Tests if {MetricName} counts are significantly higher than expected under a Gaussian distribution";

    public override double GetTestValue(TransientDatabaseMetrics result) => ToDouble(countExtractor(result));

    #region Predefined Tests

    // Convenience constructors for common metrics
    public static GaussianTest<double> ForPsm() =>
        new("PSM", r => r.TargetPsmsFromTransientDbAtQValueThreshold / (double)r.TransientPeptideCount);

    public static GaussianTest<double> ForPeptide() =>
        new("Peptide", r => r.TargetPeptidesFromTransientDbAtQValueThreshold / (double)r.TransientPeptideCount);

    public static GaussianTest<double> ForProteinGroup() =>
        new("ProteinGroup", r => r.TargetProteinGroupsFromTransientDbAtQValueThreshold / (double)r.TransientProteinCount);

    #endregion

    protected TNumeric GetObservedCount(TransientDatabaseMetrics result)
    {
        return countExtractor(result);
    }

    public override Dictionary<string, double> ComputePValues(List<TransientDatabaseMetrics> allResults)
    {
        var pValues = new Dictionary<string, double>();

        // Extract all counts and convert to double for statistics
        var counts = allResults
            .Where(r => ShouldSkip == null || !ShouldSkip(r))
            .Select(r => 
        { 
            var val = ToDouble(GetObservedCount(r));
            if (val == null || double.IsNaN(val) || double.IsInfinity(val))
            {
                pValues[r.DatabaseName] = double.NaN;
                return double.NaN;
            }

            return val;
        }).Where(p => !double.IsNaN(p)).ToArray();

        // Fit Gaussian distribution
        double mean = counts.Mean();
        double stdDev = counts.StandardDeviation();

        var normal = new Normal(mean, stdDev);

        // Compute one-sided p-values: P(X >= observed)
        foreach (var result in allResults)
        {
            if (ShouldSkip != null && ShouldSkip(result))
            {
                pValues[result.DatabaseName] = double.NaN;
                continue;
            }

            double observed = ToDouble(GetObservedCount(result));

            // Handle NaN/invalid values - assign p-value of 1.0 (not significant)
            if (double.IsNaN(observed) || double.IsInfinity(observed))
            {
                pValues[result.DatabaseName] = double.NaN;
                continue;
            }

            // Choose tail based on test direction
            double pValue = isLowerTailTest
                ? normal.CumulativeDistribution(observed)     // P(X <= observed) - lower is better
                : 1.0 - normal.CumulativeDistribution(observed); // P(X >= observed) - higher is better

            // Clamp p-value to valid range
            pValue = Math.Max(1e-300, Math.Min(1.0, pValue));

            pValues[result.DatabaseName] = pValue;
        }

        return pValues;
    }
}