using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Statistics;
using TaskLayer.ParallelSearchTask.Analysis;

namespace TaskLayer.ParallelSearchTask.Statistics;

/// <summary>
/// Gaussian distribution test for count enrichment
/// Tests if observed counts are significantly higher than expected under a normal distribution
/// </summary>
public class GaussianTest<TNumeric> : StatisticalTestBase where TNumeric : INumber<TNumeric>
{
    private readonly string _metricName;
    private readonly Func<AggregatedAnalysisResult, TNumeric> _countExtractor;

    public override string TestName => "Gaussian";
    public override string MetricName => _metricName;
    public override string Description =>
        $"Tests if {_metricName} counts are significantly higher than expected under a Gaussian distribution";

    public GaussianTest(string metricName, Func<AggregatedAnalysisResult, TNumeric> countExtractor)
    {
        _metricName = metricName;
        _countExtractor = countExtractor;
    }

    // Convenience constructors for common metrics
    public static GaussianTest<int> ForPsm() =>
        new("PSM", r => r.TargetPsmsFromTransientDbAtQValueThreshold);

    public static GaussianTest<int> ForPeptide() =>
        new("Peptide", r => r.TargetPeptidesFromTransientDbAtQValueThreshold);

    public static GaussianTest<int> ForProteinGroup() =>
        new("ProteinGroup", r => r.TargetProteinGroupsFromTransientDbAtQValueThreshold);

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

    protected TNumeric GetObservedCount(AggregatedAnalysisResult result)
    {
        return _countExtractor(result);
    }

    public override Dictionary<string, double> ComputePValues(List<AggregatedAnalysisResult> allResults)
    {
        // Extract all counts and convert to double for statistics
        var counts = allResults.Select(r => ToDouble(GetObservedCount(r))).ToArray();

        // Fit Gaussian distribution
        double mean = counts.Mean();
        double stdDev = counts.StandardDeviation();

        var normal = new Normal(mean, stdDev);

        // Compute one-sided p-values: P(X >= observed)
        var pValues = new Dictionary<string, double>();
        foreach (var result in allResults)
        {
            double observed = ToDouble(GetObservedCount(result));
            // P(X >= x) = 1 - CDF(x)
            double pValue = 1.0 - normal.CumulativeDistribution(observed);

            // Clamp p-value to valid range
            pValue = Math.Max(1e-300, Math.Min(1.0, pValue));

            pValues[result.DatabaseName] = pValue;
        }

        return pValues;
    }
}