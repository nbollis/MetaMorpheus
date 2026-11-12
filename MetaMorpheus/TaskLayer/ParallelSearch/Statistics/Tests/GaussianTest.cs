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
    StatisticalEvidenceFamily evidenceFamily,
    Func<TransientDatabaseMetrics, TNumeric> countExtractor,
    Func<TransientDatabaseMetrics, bool>? isDefinedFor = null,
    bool isLowerTailTest = false)
    : StatisticalTestBase(metricName, evidenceFamily, isDefinedFor: isDefinedFor)
    where TNumeric : INumber<TNumeric>
{
    public override string TestName => "Gaussian";
    public override string Description =>
        $"Tests if {MetricName} counts are significantly higher than expected under a Gaussian distribution";

    public override double GetTestValue(TransientDatabaseMetrics result) => ToDouble(countExtractor(result));

    public override double? GetEffectSize(TransientDatabaseMetrics result, List<TransientDatabaseMetrics> allResults)
    {
        if (!IsDefinedFor(result) || allResults == null)
            return null;

        var values = allResults
            .Where(IsDefinedFor)
            .Select(r => ToDouble(GetObservedCount(r)))
            .Where(v => !double.IsNaN(v) && !double.IsInfinity(v))
            .ToArray();

        if (values.Length == 0)
            return null;

        return SafeRatio(ToDouble(GetObservedCount(result)), values.Average());
    }

    #region Predefined Tests

    // Convenience constructors for common metrics
    public static GaussianTest<double> ForPsm() =>
        new("PSM", StatisticalEvidenceFamily.CountEnrichment, r => r.TargetPsmsFromTransientDbAtQValueThreshold / (double)r.TransientPeptideCount);

    public static GaussianTest<double> ForPeptide() =>
        new("Peptide", StatisticalEvidenceFamily.CountEnrichment, r => r.TargetPeptidesFromTransientDbAtQValueThreshold / (double)r.TransientPeptideCount);

    public static GaussianTest<double> ForProteinGroup() =>
        new("ProteinGroup", StatisticalEvidenceFamily.ProteinGroup, r => r.TargetProteinGroupsFromTransientDbAtQValueThreshold / (double)r.TransientProteinCount);

    #endregion

    protected TNumeric GetObservedCount(TransientDatabaseMetrics result)
    {
        return countExtractor(result);
    }

    public override bool IsDefinedFor(TransientDatabaseMetrics result)
    {
        if (!base.IsDefinedFor(result))
            return false;

        double observed = ToDouble(GetObservedCount(result));
        return !double.IsNaN(observed) && !double.IsInfinity(observed);
    }

    public override string? GetUndefinedReason(TransientDatabaseMetrics result)
    {
        var baseReason = base.GetUndefinedReason(result);
        if (baseReason != null)
            return baseReason;

        double observed = ToDouble(GetObservedCount(result));
        return double.IsNaN(observed) || double.IsInfinity(observed)
            ? "ObservedValueNotFinite"
            : null;
    }

    public override Dictionary<string, double> ComputePValues(List<TransientDatabaseMetrics> allResults)
    {
        var pValues = new Dictionary<string, double>();

        // Extract all counts and convert to double for statistics
        var counts = allResults
            .Where(IsDefinedFor)
            .Select(r => 
        { 
            var val = ToDouble(GetObservedCount(r));
            if (double.IsNaN(val) || double.IsInfinity(val))
            {
                pValues[r.DatabaseName] = double.NaN;
                return double.NaN;
            }

            return val;
        }).Where(p => !double.IsNaN(p)).ToArray();

        // Fit Gaussian distribution
        double mean = counts.Mean();
        double stdDev = counts.StandardDeviation();

        if (stdDev <= 0 || double.IsNaN(stdDev))
        {
            return allResults.ToDictionary(r => r.DatabaseName, r => 0.5);
        }

        var normal = new Normal(mean, stdDev);

        // Compute one-sided p-values: P(X >= observed)
        foreach (var result in allResults)
        {
            if (!IsDefinedFor(result))
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
