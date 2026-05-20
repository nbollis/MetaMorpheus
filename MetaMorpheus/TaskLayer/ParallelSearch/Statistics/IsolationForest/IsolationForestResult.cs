namespace TaskLayer.ParallelSearch.Statistics.IsolationForest;

/// <summary>
/// Scored anomaly result.
/// Higher AnomalyScore means more anomalous.
/// </summary>
public sealed class IsolationForestResult
{
    public string Id { get; }
    public double AveragePathLength { get; }
    public double AnomalyScore { get; }

    public IsolationForestResult(string id, double averagePathLength, double anomalyScore)
    {
        Id = id;
        AveragePathLength = averagePathLength;
        AnomalyScore = anomalyScore;
    }
}
