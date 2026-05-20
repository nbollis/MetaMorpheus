using System;
using System.Linq;

namespace TaskLayer.ParallelSearch.Statistics.IsolationForest;

/// <summary>
/// One input row for anomaly detection.
/// One row should represent one transient database / proteome.
/// </summary>
public sealed class IsolationForestInput
{
    public string Id { get; }
    public double[] Features { get; }

    public IsolationForestInput(string id, double[] features)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Features = features ?? throw new ArgumentNullException(nameof(features));

        if (features.Length == 0)
            throw new ArgumentException("Feature vector cannot be empty.", nameof(features));

        if (features.Any(x => double.IsNaN(x) || double.IsInfinity(x)))
            throw new ArgumentException("Feature vector cannot contain NaN or Infinity.", nameof(features));
    }
}
