using System.Collections.Generic;

namespace TaskLayer.ParallelSearchTask.Statistics;

/// <summary>
/// Container for statistical test results
/// </summary>
public class StatisticalResult
{
    public string DatabaseName { get; set; }
    public string TestName { get; set; }
    public string MetricName { get; set; }
    public double PValue { get; set; }
    public double QValue { get; set; }
    public double? TestStatistic { get; set; } // Optional: store test statistic (e.g., K-S, OR)
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();

    public bool IsSignificant(double alpha = 0.05) => QValue < alpha;
}