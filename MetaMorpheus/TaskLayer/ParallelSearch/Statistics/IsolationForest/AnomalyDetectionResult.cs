using System.Collections.Generic;
using System.Linq;

namespace TaskLayer.ParallelSearch.Statistics.IsolationForest;

public sealed class AnomalyDetectionResult
{
    public List<IsolationForestResult> SummaryScores { get; }
    public List<IsolationForestResult> FullFeatureScores { get; }

    private readonly Dictionary<string, double> _summaryScoreLookup;
    private readonly Dictionary<string, double> _fullFeatureScoreLookup;

    public AnomalyDetectionResult(
        List<IsolationForestResult> summaryScores,
        List<IsolationForestResult> fullFeatureScores)
    {
        SummaryScores = summaryScores;
        FullFeatureScores = fullFeatureScores;
        _summaryScoreLookup = summaryScores.ToDictionary(s => s.Id, s => s.AnomalyScore);
        _fullFeatureScoreLookup = fullFeatureScores.ToDictionary(s => s.Id, s => s.AnomalyScore);
    }

    public double GetSummaryScore(string dbName)
    {
        return _summaryScoreLookup.TryGetValue(dbName, out var score) ? score : double.NaN;
    }

    public double GetFullFeatureScore(string dbName)
    {
        return _fullFeatureScoreLookup.TryGetValue(dbName, out var score) ? score : double.NaN;
    }
}
