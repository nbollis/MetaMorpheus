#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Tests whether a database's count is higher than its size predicts: k ~ Poisson(λ × size), where λ is the run's
/// pooled rate, (Σk + 0.5) / Σsize over every database the test is defined for, zero counts included.
/// <para>
/// This replaces Gaussian tests on count/size ratios, which fit one spread across all databases. A ratio from a tiny
/// database varies far more than one from a large database, so on the Fisher panel 1 mapped protein out of 2 scored
/// p ≈ 1e-16 while SARS-CoV-2 at C1 (30 peptides of 6,651) scored 0.002. Here the same hit in a 2,000-peptide database
/// gives about 0.02, and the virus about 1e-50.
/// </para>
/// <para>
/// Use it only for counts of distinct peptides or proteins. PSM and de novo prediction counts are clustered (one
/// peptide gives many PSMs or predictions), so a Poisson null makes them look far too significant.
/// The test is defined for every database with a positive size: a database with no hits scores 1 and counts in the
/// multiple-testing correction. The 0.5 pseudocount keeps λ above 0 when no database has a hit, where p would be 0.
/// </para>
/// </summary>
public class SizeOffsetPoissonTest(
    string metricName,
    StatisticalEvidenceFamily evidenceFamily,
    Func<TransientDatabaseMetrics, double> countExtractor,
    Func<TransientDatabaseMetrics, double> sizeExtractor,
    Func<TransientDatabaseMetrics, bool>? isDefinedFor = null)
    : StatisticalTestBase(metricName, evidenceFamily, isDefinedFor)
{
    /// <summary>Added to the pooled count so λ is never 0.</summary>
    public const double RatePseudocount = 0.5;

    public override string TestName => "SizeOffsetPoisson";
    public override string Description =>
        $"Tests if {MetricName} counts exceed the run's pooled rate times the database's size (Poisson)";

    public override double GetTestValue(TransientDatabaseMetrics result) => countExtractor(result);

    public override bool IsDefinedFor(TransientDatabaseMetrics result)
    {
        if (!base.IsDefinedFor(result))
            return false;

        double count = countExtractor(result);
        double size = sizeExtractor(result);
        return IsValidCount(count) && double.IsFinite(size) && size > 0;
    }

    public override string? GetUndefinedReason(TransientDatabaseMetrics result)
    {
        var baseReason = base.GetUndefinedReason(result);
        if (baseReason != null)
            return baseReason;

        if (!IsValidCount(countExtractor(result)))
            return "CountNotFiniteOrNegative";
        double size = sizeExtractor(result);
        return double.IsFinite(size) && size > 0 ? null : "SizeNotPositive";
    }

    /// <summary>The run's pooled rate per unit size; NaN when no database is defined.</summary>
    public double PooledRate(List<TransientDatabaseMetrics> allResults)
    {
        double totalCount = 0, totalSize = 0;
        foreach (var result in allResults.Where(IsDefinedFor))
        {
            totalCount += Math.Round(countExtractor(result));
            totalSize += sizeExtractor(result);
        }
        return totalSize > 0 ? (totalCount + RatePseudocount) / totalSize : double.NaN;
    }

    /// <summary>Observed count over the count the pooled rate expects for this database.</summary>
    public override double? GetEffectSize(TransientDatabaseMetrics result, List<TransientDatabaseMetrics> allResults)
    {
        if (!IsDefinedFor(result) || allResults == null)
            return null;

        double rate = Pooled(allResults, PooledRate);
        if (double.IsNaN(rate))
            return null;
        return SafeRatio(Math.Round(countExtractor(result)), rate * sizeExtractor(result));
    }

    public override Dictionary<string, double> ComputePValues(List<TransientDatabaseMetrics> allResults)
    {
        ArgumentNullException.ThrowIfNull(allResults);

        double rate = PooledRate(allResults);
        var pValues = new Dictionary<string, double>(allResults.Count);
        foreach (var result in allResults)
        {
            if (double.IsNaN(rate) || !IsDefinedFor(result))
            {
                pValues[result.DatabaseName] = double.NaN;
                continue;
            }

            int k = (int)Math.Round(countExtractor(result));
            double expected = rate * sizeExtractor(result);
            double pValue = NegativeBinomialTest<double>.PoissonUpperTail(k, expected);
            pValues[result.DatabaseName] = Math.Max(1e-300, Math.Min(1.0, pValue));
        }

        return pValues;
    }

    private static bool IsValidCount(double count) => double.IsFinite(count) && count >= 0;
}
