#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Base class providing common functionality for statistical tests
/// </summary>
public abstract class StatisticalTestBase(
    string metricName,
    StatisticalEvidenceFamily evidenceFamily,
    Func<TransientDatabaseMetrics, bool>? isDefinedFor = null)
    : IStatisticalTest
{
    public abstract string TestName { get; }
    public abstract string Description { get; }
    public string MetricName { get; } = metricName;
    public StatisticalEvidenceFamily EvidenceFamily { get; } = evidenceFamily;
    public int SignificantResults { get; protected set; }

    protected readonly Func<TransientDatabaseMetrics, bool>? BaseIsDefinedFor = isDefinedFor;

    public Dictionary<string, double> RunTest(List<TransientDatabaseMetrics> allResults, double alpha = 0.05)
    {
        var rawResults = ComputePValues(allResults);
        var normalizedResults = new Dictionary<string, double>(allResults.Count);

        foreach (var result in allResults)
        {
            if (!IsDefinedFor(result))
            {
                normalizedResults[result.DatabaseName] = double.NaN;
                continue;
            }

            if (rawResults.TryGetValue(result.DatabaseName, out var pValue))
            {
                normalizedResults[result.DatabaseName] = pValue;
            }
            else
            {
                normalizedResults[result.DatabaseName] = double.NaN;
            }
        }

        SignificantResults = normalizedResults.Values.Count(p => !double.IsNaN(p) && p <= alpha);
        return normalizedResults;
    }

    public abstract Dictionary<string, double> ComputePValues(List<TransientDatabaseMetrics> allResults);

    public abstract double GetTestValue(TransientDatabaseMetrics result);

    public virtual double? GetEffectSize(TransientDatabaseMetrics result, List<TransientDatabaseMetrics> allResults)
    {
        return null;
    }

    public virtual bool CanRun(List<TransientDatabaseMetrics> allResults)
    {
        return allResults != null && allResults.Count(IsDefinedFor) >= 2;
    }

    public virtual bool IsDefinedFor(TransientDatabaseMetrics result)
    {
        return result != null && (BaseIsDefinedFor == null || BaseIsDefinedFor(result));
    }

    public virtual string? GetUndefinedReason(TransientDatabaseMetrics result)
    {
        if (result == null)
            return "NullResult";

        if (BaseIsDefinedFor != null && !BaseIsDefinedFor(result))
            return "BelowEligibilityThreshold";

        return null;
    }

    #region Numeric Helpers

    /// <summary>
    /// Convert TNumeric to double for statistical calculations
    /// </summary>
    protected static double ToDouble<TNumeric>(TNumeric value) where TNumeric : System.Numerics.INumber<TNumeric>
    {
        return Convert.ToDouble(value);
    }

    protected static int ToInt32<TNumeric>(TNumeric value) where TNumeric : System.Numerics.INumber<TNumeric>
    {
        return Convert.ToInt32(value);
    }

    protected static TNumeric Sum<TNumeric>(IEnumerable<TNumeric> values) where TNumeric : System.Numerics.INumber<TNumeric>
    {
        return values.Aggregate(TNumeric.Zero, (sum, val) => sum + val);

    }

    protected static double SafeRatio(double numerator, double denominator)
    {
        if (double.IsNaN(numerator) || double.IsInfinity(numerator) || double.IsNaN(denominator) || double.IsInfinity(denominator))
            return double.NaN;

        if (Math.Abs(denominator) < 1e-12)
        {
            if (Math.Abs(numerator) < 1e-12)
                return 1.0;

            return numerator > 0 ? double.PositiveInfinity : double.NegativeInfinity;
        }

        return numerator / denominator;
    }

    #endregion

    public override string ToString()
    {
        return $"{EvidenceFamily}|{TestName}: {MetricName}";
    }

    protected bool Equals(StatisticalTestBase other)
    {
        return TestName == other.TestName && MetricName == other.MetricName && Description == other.Description && EvidenceFamily == other.EvidenceFamily;
    }

    public bool Equals(IStatisticalTest? other)
    {
        if (other is null) 
            return false;
        if (other is StatisticalTestBase baseT)
            return Equals(baseT);
        return TestName == other.TestName && MetricName == other.MetricName && Description == other.Description && EvidenceFamily == other.EvidenceFamily;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((StatisticalTestBase)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TestName, MetricName, Description, EvidenceFamily);
    }
}
