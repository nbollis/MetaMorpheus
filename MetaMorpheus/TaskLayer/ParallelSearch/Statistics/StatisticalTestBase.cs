using System;
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Base class providing common functionality for statistical tests
/// </summary>
public abstract class StatisticalTestBase : IStatisticalTest 
{
    public abstract string TestName { get; }
    public abstract string MetricName { get; }
    public abstract string Description { get; }
    public int SignificantResults { get; protected set; }
    protected int MinimumSampleSize { get; set; } = 5;


    public Dictionary<string, double> RunTest(List<AggregatedAnalysisResult> allResults, double alpha = 0.05)
    {
        var results = ComputePValues(allResults);
        SignificantResults = results.Values.Count(p => p < alpha);
        return results;
    }

    public abstract Dictionary<string, double> ComputePValues(List<AggregatedAnalysisResult> allResults);

    public abstract double GetTestValue(AggregatedAnalysisResult result);

    public virtual bool CanRun(List<AggregatedAnalysisResult> allResults)
    {
        return allResults != null && allResults.Count >= MinimumSampleSize;
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

    #endregion

    public override string ToString()
    {
        return $"{TestName}: {MetricName}";
    }

    protected bool Equals(StatisticalTestBase other)
    {
        return TestName == other.TestName && MetricName == other.MetricName && Description == other.Description && MinimumSampleSize == other.MinimumSampleSize;
    }

    public bool Equals(IStatisticalTest other)
    {
        if (other is null) 
            return false;
        if (other is StatisticalTestBase baseT)
            return Equals(baseT);
        return TestName == other.TestName && MetricName == other.MetricName && Description == other.Description;
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((StatisticalTestBase)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TestName, MetricName, Description, MinimumSampleSize);
    }
}