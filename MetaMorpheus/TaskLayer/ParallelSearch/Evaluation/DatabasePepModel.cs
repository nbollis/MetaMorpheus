#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using EngineLayer;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.FastTree;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Evaluation;

/// <summary>
/// A targets-versus-twins classifier over database features: ML.NET FastTree with Platt calibration, on the small-set
/// tree settings of <see cref="PepAnalysisEngine.GetTreeOptions"/>. It is kept apart from <see cref="PepAnalysisEngine"/>,
/// which is tied to spectral matches.
/// <para>
/// Scores are cross-fitted: databases are split into folds, and each fold is scored by a model trained on the others.
/// A target and its twin always share a fold, so no database is scored by a model that saw its own twin.
/// </para>
/// </summary>
public sealed class DatabasePepModel
{
    public const int DefaultFoldCount = 4;

    /// <summary>Fewest targets, and fewest twins, a training set may have.</summary>
    public const int MinimumPerClassForTraining = 4;

    private readonly int _foldCount;
    private readonly int _seed;

    public DatabasePepModel(int foldCount = DefaultFoldCount, int seed = 42)
    {
        if (foldCount < 2)
            throw new ArgumentOutOfRangeException(nameof(foldCount), "Cross-fitting needs at least 2 folds.");
        _foldCount = foldCount;
        _seed = seed;
    }

    /// <summary>One database's cross-fitted score: the classifier margin, its Platt probability, and its fold.</summary>
    public readonly record struct CrossFitScore(double Score, double Probability, int Fold);

    /// <summary>
    /// Fold of each target: the targets in a seeded shuffled order, dealt out in turn. A twin takes its target's fold.
    /// </summary>
    public int[] AssignFolds(int targetCount)
    {
        int[] order = Enumerable.Range(0, targetCount).ToArray();
        new Random(_seed).Shuffle(order);
        var folds = new int[targetCount];
        for (int k = 0; k < order.Length; k++)
            folds[order[k]] = k % _foldCount;
        return folds;
    }

    /// <summary>Cross-fitted scores for every target and every twin, in input order.</summary>
    /// <exception cref="EvaluatorNotApplicableException">A training set would have too few targets or twins.</exception>
    public (CrossFitScore[] Targets, CrossFitScore[] Twins) CrossFit(DatabaseEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var targets = input.Targets;
        var twins = input.Twins;

        int[] targetFolds = AssignFolds(targets.Count);
        var foldByTarget = targets.Select((t, i) => (t.DatabaseName, Fold: targetFolds[i]))
            .ToDictionary(x => x.DatabaseName, x => x.Fold, StringComparer.Ordinal);
        int[] twinFolds = twins.Select(t => foldByTarget[t.TwinOf]).ToArray();

        var targetScores = new CrossFitScore[targets.Count];
        var twinScores = new CrossFitScore[twins.Count];
        var mlContext = new MLContext(seed: _seed);

        for (int fold = 0; fold < _foldCount; fold++)
        {
            var trainTargets = targets.Where((_, i) => targetFolds[i] != fold).ToList();
            var trainTwins = twins.Where((_, i) => twinFolds[i] != fold).ToList();
            if (trainTargets.Count < MinimumPerClassForTraining || trainTwins.Count < MinimumPerClassForTraining)
                throw new EvaluatorNotApplicableException(
                    $"Too few databases to train a classifier: a training fold has {trainTargets.Count} targets and " +
                    $"{trainTwins.Count} twins; each needs at least {MinimumPerClassForTraining}.");

            // features scaled on the training targets only, like every other fit
            var features = FittedDatabaseFeatures.Fit(trainTargets);
            if (features.FeatureCount == 0)
                throw new EvaluatorNotApplicableException("No feature is defined for the training databases.");

            var training = trainTargets.Select(t => Row(features, t, true))
                .Concat(trainTwins.Select(t => Row(features, t, false)))
                .ToList();
            var schema = Schema(features.FeatureCount);
            var trainer = mlContext.BinaryClassification.Trainers.FastTree(TreeOptions(training.Count));
            var model = trainer.Fit(mlContext.Data.LoadFromEnumerable(training, schema));

            Score(mlContext, model, schema, features, targets, targetFolds, fold, targetScores);
            Score(mlContext, model, schema, features, twins, twinFolds, fold, twinScores);
        }

        return (targetScores, twinScores);
    }

    /// <summary>
    /// The small-set settings of <see cref="PepAnalysisEngine.GetTreeOptions"/> (50 trees, 8 leaves, learning rate
    /// 0.1). The minimum examples per leaf (20 there) is lowered for small training sets so a tree can still split.
    /// </summary>
    public FastTreeBinaryTrainer.Options TreeOptions(int trainingExampleCount)
    {
        var options = PepAnalysisEngine.GetTreeOptions("RNA", 0, _seed);
        options.MinimumExampleCountPerLeaf = Math.Clamp(trainingExampleCount / 10, 2, options.MinimumExampleCountPerLeaf);
        return options;
    }

    private static void Score(MLContext mlContext, ITransformer model, SchemaDefinition schema, FittedDatabaseFeatures features,
        IReadOnlyList<TransientDatabaseMetrics> rows, int[] folds, int fold, CrossFitScore[] output)
    {
        var indices = Enumerable.Range(0, rows.Count).Where(i => folds[i] == fold).ToList();
        if (indices.Count == 0)
            return;

        var data = mlContext.Data.LoadFromEnumerable(indices.Select(i => Row(features, rows[i], false)).ToList(), schema);
        var predictions = mlContext.Data.CreateEnumerable<Prediction>(model.Transform(data), reuseRowObject: false).ToList();
        for (int k = 0; k < indices.Count; k++)
            output[indices[k]] = new CrossFitScore(predictions[k].Score, predictions[k].Probability, fold);
    }

    private static PepRow Row(FittedDatabaseFeatures features, TransientDatabaseMetrics metrics, bool label) =>
        new() { Label = label, Features = features.Transform(metrics).Select(v => (float)v).ToArray() };

    private static SchemaDefinition Schema(int featureCount)
    {
        var schema = SchemaDefinition.Create(typeof(PepRow));
        schema[nameof(PepRow.Features)].ColumnType = new VectorDataViewType(NumberDataViewType.Single, featureCount);
        return schema;
    }

    private sealed class PepRow
    {
        public bool Label { get; set; }
        public float[] Features { get; set; } = [];
    }

    private sealed class Prediction
    {
        public float Score { get; set; }
        public float Probability { get; set; }
    }
}
