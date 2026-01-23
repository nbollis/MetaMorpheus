#nullable enable
using System;
using System.Collections.Generic;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Container for statistical test results from a single database
/// Contains both p-value and q-value (computed after multiple testing correction)
/// </summary>
public class StatisticalTestResult : IEquatable<StatisticalTestResult>
{
    public string DatabaseName { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public double PValue { get; set; } = double.NaN;
    public double QValue { get; set; } = double.NaN;
    public double? TestStatistic { get; set; }
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();

    private double? _negLogP = null;
    private double? _negLogQ = null;
    public double NegLog10PValue => _negLogP ??= CalculateNegLog10(PValue);
    public double NegLog10QValue => _negLogQ ??= CalculateNegLog10(QValue);
    public string Key => $"{TestName}_{MetricName}";

    /// <summary>
    /// Check if this result is statistically significant at the given alpha level
    /// </summary>
    public bool IsSignificant(double alpha = 0.05, bool useQValue = true) =>  useQValue ? QValue <= alpha : PValue <= alpha;


    private static double CalculateNegLog10(double value)
    {
        if (value <= 0 || double.IsNaN(value))
            return double.NaN;
        if (value >= 1.0)
            return 0.0;
        return -Math.Log10(value);
    }

    public bool Equals(StatisticalTestResult? other)
    {
        if (other is null) return false;
        return DatabaseName == other.DatabaseName &&
               TestName == other.TestName &&
               MetricName == other.MetricName;
    }

    public override string ToString() => 
        $"{DatabaseName}|{TestName}_{MetricName}:p={PValue:E4},q={QValue:E4}";
}