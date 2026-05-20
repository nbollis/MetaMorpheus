using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskLayer.ParallelSearch.Statistics.IsolationForest;

/// <summary>
/// A single isolation tree.
/// </summary>
public sealed class IsolationTree
{
    private readonly Random _random;
    private readonly int _maxDepth;
    private IsolationTreeNode? _root;

    public IsolationTree(int maxDepth, Random random)
    {
        if (maxDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Max depth must be at least 1.");

        _maxDepth = maxDepth;
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public void Fit(IReadOnlyList<double[]> samples)
    {
        ValidateSamples(samples);
        _root = Build(samples.ToList(), currentDepth: 0);
    }

    public double PathLength(double[] sample)
    {
        if (_root is null)
            throw new InvalidOperationException("Tree must be fitted before scoring.");

        return PathLength(sample, _root, currentDepth: 0);
    }

    private IsolationTreeNode Build(List<double[]> samples, int currentDepth)
    {
        if (samples.Count <= 1 || currentDepth >= _maxDepth)
            return IsolationTreeNode.External(samples.Count);

        int featureCount = samples[0].Length;

        List<int> splittableFeatures = new();

        for (int featureIndex = 0; featureIndex < featureCount; featureIndex++)
        {
            double min = samples.Min(x => x[featureIndex]);
            double max = samples.Max(x => x[featureIndex]);

            if (min < max)
                splittableFeatures.Add(featureIndex);
        }

        if (splittableFeatures.Count == 0)
            return IsolationTreeNode.External(samples.Count);

        int selectedFeature = splittableFeatures[_random.Next(splittableFeatures.Count)];

        double featureMin = samples.Min(x => x[selectedFeature]);
        double featureMax = samples.Max(x => x[selectedFeature]);

        double splitValue = featureMin + _random.NextDouble() * (featureMax - featureMin);

        List<double[]> left = new();
        List<double[]> right = new();

        foreach (double[] sample in samples)
        {
            if (sample[selectedFeature] < splitValue)
                left.Add(sample);
            else
                right.Add(sample);
        }

        if (left.Count == 0 || right.Count == 0)
            return IsolationTreeNode.External(samples.Count);

        IsolationTreeNode leftNode = Build(left, currentDepth + 1);
        IsolationTreeNode rightNode = Build(right, currentDepth + 1);

        return IsolationTreeNode.Internal(
            size: samples.Count,
            splitFeatureIndex: selectedFeature,
            splitValue: splitValue,
            left: leftNode,
            right: rightNode);
    }

    private static double PathLength(double[] sample, IsolationTreeNode node, int currentDepth)
    {
        if (node.IsExternal)
        {
            return currentDepth + IsolationForestMath.ExpectedPathLength(node.Size);
        }

        if (sample[node.SplitFeatureIndex] < node.SplitValue)
            return PathLength(sample, node.Left!, currentDepth + 1);

        return PathLength(sample, node.Right!, currentDepth + 1);
    }

    private static void ValidateSamples(IReadOnlyList<double[]> samples)
    {
        if (samples is null)
            throw new ArgumentNullException(nameof(samples));

        if (samples.Count == 0)
            throw new ArgumentException("At least one sample is required.", nameof(samples));

        int featureCount = samples[0].Length;

        if (featureCount == 0)
            throw new ArgumentException("Feature vectors cannot be empty.", nameof(samples));

        foreach (double[] sample in samples)
        {
            if (sample.Length != featureCount)
                throw new ArgumentException("All samples must have the same number of features.", nameof(samples));

            if (sample.Any(x => double.IsNaN(x) || double.IsInfinity(x)))
                throw new ArgumentException("Samples cannot contain NaN or Infinity.", nameof(samples));
        }
    }
}
