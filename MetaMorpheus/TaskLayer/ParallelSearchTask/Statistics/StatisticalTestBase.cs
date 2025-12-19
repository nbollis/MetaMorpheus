using System;
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearchTask.Analysis;

namespace TaskLayer.ParallelSearchTask.Statistics;

/// <summary>
/// Base class providing common functionality for statistical tests
/// </summary>
public abstract class StatisticalTestBase : IStatisticalTest 
{
    public abstract string TestName { get; }
    public abstract string MetricName { get; }
    public abstract string Description { get; }

    protected int MinimumSampleSize { get; set; } = 5;

    public abstract Dictionary<string, double> ComputePValues(List<AggregatedAnalysisResult> allResults);

    public virtual bool CanRun(List<AggregatedAnalysisResult> allResults)
    {
        // Default: need at least 2 databases and some non-zero observations
        return allResults != null && allResults.Count >= 2;
    }

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
}