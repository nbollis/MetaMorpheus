using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskLayer.ParallelSearch.Statistics.IsolationForest;

/// <summary>
/// Isolation forest for anomaly ranking.
/// Higher score means more anomalous.
/// </summary>
public sealed class IsolationForest
{
    private readonly int _treeCount;
    private readonly int _sampleSize;
    private readonly int _maxDepth;
    private readonly Random _random;
    private readonly List<IsolationTree> _trees = new();

    private int _effectiveSampleSize;

    public IsolationForest(
        int treeCount = 100,
        int sampleSize = 256,
        int? seed = null)
    {
        if (treeCount < 1)
            throw new ArgumentOutOfRangeException(nameof(treeCount), "Tree count must be at least 1.");

        if (sampleSize < 2)
            throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be at least 2.");

        _treeCount = treeCount;
        _sampleSize = sampleSize;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();

        _maxDepth = (int)Math.Ceiling(Math.Log2(sampleSize));
    }

    public void Fit(IReadOnlyList<IsolationForestInput> inputs)
    {
        if (inputs is null)
            throw new ArgumentNullException(nameof(inputs));

        if (inputs.Count == 0)
            throw new ArgumentException("At least one input is required.", nameof(inputs));

        int featureCount = inputs[0].Features.Length;

        foreach (IsolationForestInput input in inputs)
        {
            if (input.Features.Length != featureCount)
                throw new ArgumentException("All feature vectors must have the same length.", nameof(inputs));
        }

        _trees.Clear();

        _effectiveSampleSize = Math.Min(_sampleSize, inputs.Count);

        List<double[]> allSamples = inputs.Select(x => x.Features).ToList();

        for (int i = 0; i < _treeCount; i++)
        {
            List<double[]> subsample = SampleWithoutReplacement(allSamples, _effectiveSampleSize);

            var tree = new IsolationTree(_maxDepth, _random);
            tree.Fit(subsample);

            _trees.Add(tree);
        }
    }

    public IsolationForestResult Score(IsolationForestInput input)
    {
        if (_trees.Count == 0)
            throw new InvalidOperationException("Forest must be fitted before scoring.");

        double averagePathLength = _trees.Average(tree => tree.PathLength(input.Features));

        double normalizer = IsolationForestMath.ExpectedPathLength(_effectiveSampleSize);

        double anomalyScore;

        if (normalizer <= 0)
        {
            anomalyScore = 0.0;
        }
        else
        {
            anomalyScore = Math.Pow(2.0, -averagePathLength / normalizer);
        }

        return new IsolationForestResult(
            id: input.Id,
            averagePathLength: averagePathLength,
            anomalyScore: anomalyScore);
    }

    public List<IsolationForestResult> ScoreAll(IReadOnlyList<IsolationForestInput> inputs)
    {
        return inputs
            .Select(Score)
            .OrderByDescending(x => x.AnomalyScore)
            .ToList();
    }

    private List<double[]> SampleWithoutReplacement(IReadOnlyList<double[]> samples, int count)
    {
        int[] indices = Enumerable.Range(0, samples.Count).ToArray();

        for (int i = 0; i < count; i++)
        {
            int j = _random.Next(i, samples.Count);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        List<double[]> result = new(count);

        for (int i = 0; i < count; i++)
        {
            result.Add(samples[indices[i]]);
        }

        return result;
    }
}
