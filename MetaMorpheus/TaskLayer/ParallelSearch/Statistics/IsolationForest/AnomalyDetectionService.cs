#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Statistics.IsolationForest;

public sealed class AnomalyDetectionService
{
    private const int DefaultTreeCount = 100;
    private const int DefaultSampleSize = 256;

    public AnomalyDetectionResult Run(
        IReadOnlyDictionary<string, TransientDatabaseMetrics> metricsDict,
        List<StatisticalTestResult> statResults,
        int? seed = null)
    {
        var summaryFeatures = AnomalyDetectionFeatureBuilder.BuildSummaryFeatures(metricsDict, statResults);
        var fullFeatures = AnomalyDetectionFeatureBuilder.BuildFullPerTestFeatures(statResults);

        var summaryScores = RunForest(summaryFeatures, seed);
        var fullScores = RunForest(fullFeatures, seed.HasValue ? seed.Value + 1 : null);

        return new AnomalyDetectionResult(summaryScores, fullScores);
    }

    public void UpdateMetrics(
        IReadOnlyDictionary<string, TransientDatabaseMetrics> metricsDict,
        AnomalyDetectionResult result)
    {
        int rank = 1;
        foreach (var score in result.SummaryScores.OrderByDescending(s => s.AnomalyScore))
        {
            if (metricsDict.TryGetValue(score.Id, out var m))
            {
                m.SummaryAnomalyScore = score.AnomalyScore;
                m.AnomalyRank = rank++;
            }
        }

        foreach (var score in result.FullFeatureScores)
        {
            if (metricsDict.TryGetValue(score.Id, out var m))
            {
                m.FullAnomalyScore = score.AnomalyScore;
            }
        }
    }

    private static List<IsolationForestResult> RunForest(
        List<IsolationForestInput> inputs,
        int? seed)
    {
        if (inputs.Count < 2)
        {
            return inputs.Select(i => new IsolationForestResult(i.Id, 0.0, 0.5)).ToList();
        }

        int effectiveSampleSize = Math.Min(DefaultSampleSize, inputs.Count);

        var forest = new IsolationForest(
            DefaultTreeCount,
            effectiveSampleSize,
            seed);

        forest.Fit(inputs);
        return forest.ScoreAll(inputs);
    }
}
