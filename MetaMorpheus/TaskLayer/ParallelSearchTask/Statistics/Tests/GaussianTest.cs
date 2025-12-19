using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Statistics;
using TaskLayer.ParallelSearchTask.Analysis;

namespace TaskLayer.ParallelSearchTask.Statistics;

/// <summary>
/// Gaussian distribution test for count enrichment
/// Tests if observed counts are significantly higher than expected under a normal distribution
/// </summary>
public class GaussianTest : StatisticalTestBase
{
    private readonly string _metricName;
    private readonly Func<AggregatedAnalysisResult, int> _countExtractor;

    public override string TestName => "Gaussian";
    public override string MetricName => _metricName;
    public override string Description =>
        $"Tests if {_metricName} counts are significantly higher than expected under a Gaussian distribution";

    public GaussianTest(string metricName, Func<AggregatedAnalysisResult, int> countExtractor)
    {
        _metricName = metricName;
        _countExtractor = countExtractor;
    }

    // Convenience constructors for common metrics
    public static GaussianTest ForPsm() =>
        new("PSM", r => r.TargetPsmsFromTransientDbAtQValueThreshold);

    public static GaussianTest ForPeptide() =>
        new("Peptide", r => r.TargetPeptidesFromTransientDbAtQValueThreshold);

    public static GaussianTest ForProteinGroup() =>
        new("ProteinGroup", r => r.TargetProteinGroupsFromTransientDbAtQValueThreshold);

    protected override int GetObservedCount(AggregatedAnalysisResult result)
    {
        return _countExtractor(result);
    }

    public override Dictionary<string, double> ComputePValues(List<AggregatedAnalysisResult> allResults)
    {
        // Extract all counts
        var counts = allResults.Select(r => (double)GetObservedCount(r)).ToArray();

        // Fit Gaussian distribution
        double mean = counts.Mean();
        double stdDev = counts.StandardDeviation();

        var normal = new Normal(mean, stdDev);

        // Compute one-sided p-values: P(X >= observed)
        var pValues = new Dictionary<string, double>();
        foreach (var result in allResults)
        {
            int observed = GetObservedCount(result);
            // P(X >= x) = 1 - CDF(x)
            double pValue = 1.0 - normal.CumulativeDistribution(observed);
            pValues[result.DatabaseName] = pValue;
        }

        return pValues;
    }
}