#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Distributions;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Tests whether a database's values (one per peptide, or per protein group) sit better than the run's pooled values:
/// z = (mean - pooled mean) / (pooled SD / sqrt(n)), one-sided in the better direction.
/// <para>
/// This replaces Gaussian tests on per-database medians, which fitted one spread to databases with 1 value and with 50
/// and so ignored n. A database with one odd value then looked extreme (in a simulation, p ≤ 1e-5 came up 30 times
/// as often as it should). Here the standard error shrinks with n, so one value is judged as one value.
/// </para>
/// <para>
/// The values within a database are taken as independent, so pass one value per peptide (never per PSM or per fragment
/// ion, which are clustered) and, for errors, absolute values so that "better" has one direction.
/// </para>
/// </summary>
public class SampleMeanTest(
    string metricName,
    StatisticalEvidenceFamily evidenceFamily,
    Func<TransientDatabaseMetrics, double[]> valuesExtractor,
    bool higherIsBetter,
    int minValues = 2,
    Func<TransientDatabaseMetrics, bool>? isDefinedFor = null)
    : StatisticalTestBase(metricName, evidenceFamily, isDefinedFor)
{
    public override string TestName => "SampleMean";
    public override string Description =>
        $"Tests if the mean {MetricName} is {(higherIsBetter ? "higher" : "lower")} than the run's pooled values, with a standard error that shrinks with the number of values";

    private double[] FiniteValues(TransientDatabaseMetrics result) =>
        (valuesExtractor(result) ?? Array.Empty<double>()).Where(double.IsFinite).ToArray();

    public override bool IsDefinedFor(TransientDatabaseMetrics result) =>
        base.IsDefinedFor(result) && FiniteValues(result).Length >= Math.Max(1, minValues);

    public override string? GetUndefinedReason(TransientDatabaseMetrics result)
    {
        var baseReason = base.GetUndefinedReason(result);
        if (baseReason != null)
            return baseReason;
        return FiniteValues(result).Length >= Math.Max(1, minValues) ? null : "TooFewValues";
    }

    /// <summary>The database's z-score against the pooled values; NaN before the test has run.</summary>
    public override double GetTestValue(TransientDatabaseMetrics result) => TryGetTestValue(result, out double z) ? z : double.NaN;

    /// <summary>The database's mean minus the pooled mean.</summary>
    public override double? GetEffectSize(TransientDatabaseMetrics result, List<TransientDatabaseMetrics> allResults)
    {
        if (!IsDefinedFor(result) || allResults == null)
            return null;
        var (mean, _) = Pooled(allResults, PooledMoments);
        return double.IsNaN(mean) ? null : FiniteValues(result).Average() - mean;
    }

    /// <summary>Mean and sample SD of every value of every defined database.</summary>
    private (double Mean, double StdDev) PooledMoments(List<TransientDatabaseMetrics> allResults)
    {
        long n = 0;
        double sum = 0, sumSq = 0;
        foreach (var result in allResults.Where(IsDefinedFor))
        {
            foreach (double v in FiniteValues(result))
            {
                n++;
                sum += v;
                sumSq += v * v;
            }
        }
        if (n < 2)
            return (double.NaN, double.NaN);
        double mean = sum / n;
        double variance = Math.Max(0, (sumSq - n * mean * mean) / (n - 1));
        return (mean, Math.Sqrt(variance));
    }

    public override Dictionary<string, double> ComputePValues(List<TransientDatabaseMetrics> allResults)
    {
        ArgumentNullException.ThrowIfNull(allResults);
        var (pooledMean, pooledSd) = Pooled(allResults, PooledMoments);

        var pValues = new Dictionary<string, double>(allResults.Count);
        foreach (var result in allResults)
        {
            if (!IsDefinedFor(result) || double.IsNaN(pooledSd) || pooledSd <= 0)
            {
                pValues[result.DatabaseName] = double.NaN;
                continue;
            }

            double[] values = FiniteValues(result);
            double z = (values.Average() - pooledMean) / (pooledSd / Math.Sqrt(values.Length));
            SetTestValue(result, z);

            // the better tail, taken directly so a far tail keeps its precision
            double pValue = Normal.CDF(0, 1, higherIsBetter ? -z : z);
            pValues[result.DatabaseName] = Math.Max(1e-300, Math.Min(1.0, pValue));
        }

        return pValues;
    }
}
