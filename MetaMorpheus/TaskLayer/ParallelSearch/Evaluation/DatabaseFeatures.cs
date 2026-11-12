#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Evaluation;

/// <summary>
/// One per-database feature built from raw summary metrics. Every feature is oriented so that a higher value is more
/// evidence that the organism is present; NaN means the metric is undefined for that database (no hits to take a
/// median of, no transient peptides to divide by).
/// </summary>
public sealed record DatabaseFeature(string Name, Func<TransientDatabaseMetrics, double> Extract);

/// <summary>
/// The raw-metric features shared by the model-based evaluators (isolation forest, proteome-level PEP). They are
/// deliberately not built from statistical-test p-values, so these evaluators do not re-score the statistical tests.
/// </summary>
public static class DatabaseFeatures
{
    public static readonly IReadOnlyList<DatabaseFeature> All =
    [
        // how much was found, absolute and per unit of database size
        new("Log1pUnambiguousPsms", m => Math.Log(1 + m.PsmBacterialUnambiguousTargets)),
        new("Log1pUnambiguousPeptides", m => Math.Log(1 + m.PeptideBacterialUnambiguousTargets)),
        new("Log1pUnambiguousProteinGroups", m => Math.Log(1 + m.ProteinGroupBacterialUnambiguousTargets)),
        new("UnambiguousPsmsPerTransientPeptide", m => Ratio(m.PsmBacterialUnambiguousTargets, m.TransientPeptideCount)),
        new("UnambiguousPeptidesPerTransientPeptide", m => Ratio(m.PeptideBacterialUnambiguousTargets, m.TransientPeptideCount)),
        new("UnambiguousPeptideFraction", m => Ratio(m.PeptideBacterialUnambiguousTargets, m.PeptideBacterialTargets)),

        // how good the hits are
        new("MedianPeptideScore", m => Median(m.PeptideBacterialUnambiguousTargetScores)),
        new("MedianPeptideDeltaScore", m => Median(m.PeptideBacterialTargetDeltaScores)),
        new("NegMedianAbsPeptideRtError", m => -MedianAbs(m.Peptide_AllRtErrors)),
        new("MedianPeptideFragmentCoverage", m => Median(m.Peptide_SequenceCoverageFractionTargets)),
        new("MedianPeptideBidirectional", m => Median(m.Peptide_BidirectionalTargets)),
        new("MedianPeptideComplementaryCount", m => Median(m.Peptide_ComplementaryCountTargets)),

        // protein groups; undefined without parsimony
        new("MedianUniquePeptidesPerProteinGroup", m => Median(m.AllUniquePeptidesPerProteinGroup)),
        new("MedianProteinSequenceCoverage", m => Median(m.AllSequenceCoverageFractions)),

        // precursor deconvolution; undefined when it is off
        new("MedianPeptidePrecursorDeconScore", m => Median(m.PeptidePrecursorDeconScores)),
        new("NegMedianAbsPeptidePrecursorMassError", m => -MedianAbs(m.PeptidePrecursorMassErrors)),
    ];

    private static double Ratio(double numerator, double denominator) => denominator > 0 ? numerator / denominator : double.NaN;

    private static double Median(double[]? values)
    {
        if (values == null || values.Length == 0)
            return double.NaN;
        var finite = values.Where(double.IsFinite).ToArray();
        return finite.Length == 0 ? double.NaN : finite.Median();
    }

    private static double MedianAbs(double[]? values) =>
        values == null ? double.NaN : Median(values.Select(Math.Abs).ToArray());
}

/// <summary>
/// Robust scaling fitted on the targets only and then applied unchanged to any database, so twins are scored on the
/// targets' scale. Per feature: centre on the median and divide by the IQR; when the IQR is 0, fall back to the
/// range (as the ManySearchTask branch's scaling did); a feature constant over the targets maps to 0. A feature
/// undefined for every target is dropped. An undefined value is imputed with the targets' minimum, the
/// low-evidence end, since missing evidence is not evidence.
/// </summary>
public sealed class FittedDatabaseFeatures
{
    private readonly DatabaseFeature[] _features;
    private readonly double[] _center;
    private readonly double[] _scale;
    private readonly double[] _missing;

    private FittedDatabaseFeatures(DatabaseFeature[] features, double[] center, double[] scale, double[] missing)
    {
        _features = features;
        _center = center;
        _scale = scale;
        _missing = missing;
    }

    /// <summary>Names of the features kept, in vector order.</summary>
    public IReadOnlyList<string> FeatureNames => _features.Select(f => f.Name).ToArray();

    public int FeatureCount => _features.Length;

    public static FittedDatabaseFeatures Fit(IReadOnlyList<TransientDatabaseMetrics> targets,
        IReadOnlyList<DatabaseFeature>? features = null)
    {
        ArgumentNullException.ThrowIfNull(targets);
        features ??= DatabaseFeatures.All;

        var kept = new List<DatabaseFeature>();
        var center = new List<double>();
        var scale = new List<double>();
        var missing = new List<double>();
        foreach (var feature in features)
        {
            var defined = targets.Select(feature.Extract).Where(double.IsFinite).ToList();
            if (defined.Count == 0)
                continue;

            double min = defined.Min();
            var values = targets.Select(feature.Extract).Select(v => double.IsFinite(v) ? v : min).OrderBy(v => v).ToList();
            var (c, s) = RobustScale(values);

            kept.Add(feature);
            center.Add(c);
            scale.Add(s);
            missing.Add(min);
        }

        return new FittedDatabaseFeatures(kept.ToArray(), center.ToArray(), scale.ToArray(), missing.ToArray());
    }

    /// <summary>The scaled, imputed feature vector of one database.</summary>
    public double[] Transform(TransientDatabaseMetrics metrics)
    {
        var vector = new double[_features.Length];
        for (int f = 0; f < _features.Length; f++)
        {
            double value = _features[f].Extract(metrics);
            if (!double.IsFinite(value))
                value = _missing[f];
            vector[f] = (value - _center[f]) / _scale[f];
        }
        return vector;
    }

    /// <summary>Centre and scale for sorted values, with the same quantile picks as the branch's <c>ScaleFeature</c>.</summary>
    private static (double Center, double Scale) RobustScale(List<double> sorted)
    {
        int n = sorted.Count;
        double q1 = sorted[n / 4];
        double q3 = sorted[3 * n / 4];
        if (q3 - q1 > 0)
            return (sorted[n / 2], q3 - q1);

        double min = sorted[0], max = sorted[^1];
        if (max > min)
            return (min + (max - min) / 2, max - min);

        return (min, 1.0);
    }
}
