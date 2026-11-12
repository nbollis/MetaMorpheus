#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Statistics.IsolationForest;
using Forest = TaskLayer.ParallelSearch.Statistics.IsolationForest.IsolationForest;

namespace TaskLayer.ParallelSearch.Evaluation;

/// <summary>
/// Multivariate anomaly ranking with the isolation forest copied from the ManySearchTask branch, over the raw-metric
/// features in <see cref="DatabaseFeatures"/>. The forest and the feature scaling are fitted on the targets only;
/// twins are then scored with the same forest and scaling.
/// <para>
/// An isolation forest flags databases that stand out in either direction, but only standing out on the high-evidence
/// side means anything here. A database whose features average below the targets' median (evidence index ≤ 0) gets
/// score 0, so an organism with unusually little evidence is not ranked as present.
/// </para>
/// <para>
/// Without twins the evaluator ranks only: no q-value and no call. With twins, q at score t = (twins ≥ t + 1) /
/// (targets ≥ t), made monotone (<see cref="TwinCalibration"/>), and a target is called at q ≤ alpha.
/// </para>
/// </summary>
public sealed class IsolationForestEvaluator : IDatabaseEvaluator
{
    public const string EvaluatorName = "IsolationForest";
    public const double DefaultAlpha = 0.05;
    public const int DefaultTreeCount = 100;
    public const int DefaultSampleSize = 256;
    public const int DefaultSeed = 42;

    public const string AnomalyScoreColumn = "AnomalyScore";
    public const string EvidenceIndexColumn = "EvidenceIndex";
    public const string RankColumn = "Rank";

    private readonly int _treeCount;
    private readonly int _sampleSize;
    private readonly int _seed;

    public IsolationForestEvaluator(double alpha = DefaultAlpha, int treeCount = DefaultTreeCount,
        int sampleSize = DefaultSampleSize, int seed = DefaultSeed)
    {
        Alpha = alpha;
        _treeCount = treeCount;
        _sampleSize = sampleSize;
        _seed = seed;
    }

    public string Name => EvaluatorName;
    public bool RequiresTwins => false;
    public double Alpha { get; }

    /// <summary>Feature names used by the last <see cref="Evaluate"/>, for reporting.</summary>
    public IReadOnlyList<string> FeatureNames { get; private set; } = [];

    public IReadOnlyList<DatabaseEvaluationResult> Evaluate(DatabaseEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var targets = input.Targets;
        var twins = input.Twins;

        var fitted = FittedDatabaseFeatures.Fit(targets);
        FeatureNames = fitted.FeatureNames;
        if (targets.Count < 2 || fitted.FeatureCount == 0)
            throw new EvaluatorNotApplicableException(
                $"Needs at least 2 transient databases and one defined feature; found {targets.Count} and {fitted.FeatureCount}.");

        var forest = new Forest(_treeCount, Math.Min(_sampleSize, targets.Count), _seed);
        forest.Fit(targets.Select(t => new IsolationForestInput(t.DatabaseName, fitted.Transform(t))).ToList());

        var targetScores = targets.Select(t => ScoreOne(forest, fitted, t)).ToList();
        var twinScores = twins.Select(t => ScoreOne(forest, fitted, t)).ToList();

        double[]? qValues = input.HasTwins
            ? TwinCalibration.EmpiricalQValues(targetScores.Select(s => s.Score).ToList(), twinScores.Select(s => s.Score).ToList())
            : null;

        // rank 1 is the most anomalous on the high-evidence side; ties keep name order
        int[] rank = new int[targets.Count];
        int next = 1;
        foreach (int i in Enumerable.Range(0, targets.Count).OrderByDescending(i => targetScores[i].Score))
            rank[i] = next++;

        var results = new List<DatabaseEvaluationResult>(targets.Count + twins.Count);
        for (int i = 0; i < targets.Count; i++)
        {
            var s = targetScores[i];
            var details = Details(s);
            details[RankColumn] = rank[i];
            double q = qValues?[i] ?? double.NaN;
            results.Add(new DatabaseEvaluationResult(targets[i].DatabaseName, string.Empty, s.Score, double.NaN, q,
                !double.IsNaN(q) && q <= Alpha, details));
        }

        for (int i = 0; i < twins.Count; i++)
            results.Add(DatabaseEvaluationResult.ScoreOnly(twins[i].DatabaseName, twins[i].TwinOf, twinScores[i].Score,
                Details(twinScores[i])));

        return results;
    }

    private static (double Score, double AnomalyScore, double EvidenceIndex) ScoreOne(Forest forest,
        FittedDatabaseFeatures fitted, TransientDatabaseMetrics metrics)
    {
        double[] features = fitted.Transform(metrics);
        double anomaly = forest.Score(new IsolationForestInput(metrics.DatabaseName, features)).AnomalyScore;
        double evidence = features.Average();
        return (evidence > 0 ? anomaly : 0.0, anomaly, evidence);
    }

    private static Dictionary<string, double> Details((double Score, double AnomalyScore, double EvidenceIndex) s) => new()
    {
        [AnomalyScoreColumn] = s.AnomalyScore,
        [EvidenceIndexColumn] = s.EvidenceIndex,
    };
}
