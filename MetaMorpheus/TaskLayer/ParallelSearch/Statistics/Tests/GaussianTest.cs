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
public class GaussianTest<TNumeric> : StatisticalTestBase where TNumeric : INumber<TNumeric>
{
    private readonly bool _isLowerTailTest;
    private readonly Func<AggregatedAnalysisResult, TNumeric> _dataPointExtractor;

    public override string TestName => "Gaussian";
    public override string Description =>
        $"Tests if {MetricName} counts are significantly higher than expected under a Gaussian distribution";

    public GaussianTest(string metricName, Func<AggregatedAnalysisResult, TNumeric> countExtractor, bool isLowerTailTest = false, Func<AggregatedAnalysisResult, bool>? shouldSkip = null) : base(metricName, shouldSkip: shouldSkip)
    {
        _dataPointExtractor = countExtractor;
        _isLowerTailTest = isLowerTailTest;
    }

    public override double GetTestValue(AggregatedAnalysisResult result) => ToDouble(_dataPointExtractor(result));

    #region Predefined Tests

    // Convenience constructors for common metrics
    public static GaussianTest<double> ForPsm() =>
        new("PSM", r => r.TargetPsmsFromTransientDbAtQValueThreshold / (double)r.TransientPeptideCount);

    public static GaussianTest<double> ForPeptide() =>
        new("Peptide", r => r.TargetPeptidesFromTransientDbAtQValueThreshold / (double)r.TransientPeptideCount);

    public static GaussianTest<double> ForProteinGroup() =>
        new("ProteinGroup", r => r.TargetProteinGroupsFromTransientDbAtQValueThreshold / (double)r.TransientProteinCount);

    public static GaussianTest<double> ForPsmComplementary() =>
        new("PSM-Complementary", r => r.Psm_ComplementaryCount_MedianTargets);

    public static GaussianTest<double> ForPsmBidirectional() =>
        new("PSM-Bidirectional", r => r.Psm_Bidirectional_MedianTargets);

    public static GaussianTest<double> ForPsmSequenceCoverage() =>
        new("PSM-SequenceCoverage", r => r.Psm_SequenceCoverageFraction_MedianTargets);

    public static GaussianTest<double> ForPeptideComplementary() =>
        new("Peptide-Complementary", r => r.Peptide_ComplementaryCount_MedianTargets);

    public static GaussianTest<double> ForPeptideBidirectional() =>
        new("Peptide-Bidirectional", r => r.Peptide_Bidirectional_MedianTargets);

    public static GaussianTest<double> ForPeptideSequenceCoverage() =>
        new("Peptide-SequenceCoverage", r => r.Peptide_SequenceCoverageFraction_MedianTargets);

    public static GaussianTest<double> ForPsmMeanAbsoluteRtError() =>
        new("PSM-MeanAbsoluteRtError",
            r => r.Psm_MeanAbsoluteRtError, isLowerTailTest: true,
            r => r.TargetPsmsFromTransientDbAtQValueThreshold == 0);

    public static GaussianTest<double> ForPeptideMeanAbsoluteRtError() =>
        new("Peptide-MeanAbsoluteRtError",
            r => r.Peptide_MeanAbsoluteRtError, isLowerTailTest: true,
            r => r.TargetPeptidesFromTransientDbAtQValueThreshold == 0);

    #endregion

    protected TNumeric GetObservedCount(AggregatedAnalysisResult result)
    {
        return _dataPointExtractor(result);
    }

    public override Dictionary<string, double> ComputePValues(List<AggregatedAnalysisResult> allResults)
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
            double pValue = _isLowerTailTest
                ? normal.CumulativeDistribution(observed)     // P(X <= observed) - lower is better
                : 1.0 - normal.CumulativeDistribution(observed); // P(X >= observed) - higher is better

            // Clamp p-value to valid range
            pValue = Math.Max(1e-300, Math.Min(1.0, pValue));

            pValues[result.DatabaseName] = pValue;
        }

        return pValues;
    }
}