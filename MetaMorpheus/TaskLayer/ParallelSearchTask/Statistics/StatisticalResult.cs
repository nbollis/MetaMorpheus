#nullable enable
using System.Collections.Generic;

namespace TaskLayer.ParallelSearchTask.Statistics;

/// <summary>
/// Container for statistical test results from a single database
/// Contains both p-value and q-value (computed after multiple testing correction)
/// </summary>
public class StatisticalResult
{
    public string DatabaseName { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public double PValue { get; set; }
    public double QValue { get; set; }
    public double? TestStatistic { get; set; }
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();

    /// <summary>
    /// Check if this result is statistically significant at the given alpha level
    /// </summary>
    public bool IsSignificant(double alpha = 0.05) => QValue < alpha;

    public override string ToString() => 
        $"{DatabaseName}|{TestName}_{MetricName}:p={PValue:E4},q={QValue:E4}";
}