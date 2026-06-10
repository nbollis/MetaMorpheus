#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Statistics.IsolationForest;

public static class AnomalyDetectionFeatureBuilder
{
    private const int SummaryFeatureCount = 37;

    public static List<IsolationForestInput> BuildSummaryFeatures(
        IReadOnlyDictionary<string, TransientDatabaseMetrics> metricsDict,
        List<StatisticalTestResult> statResults)
    {
        if (metricsDict == null || metricsDict.Count == 0)
            return new List<IsolationForestInput>();

        var rawFeatures = new List<(string Id, double[] Features)>();

        foreach (var kvp in metricsDict)
        {
            var m = kvp.Value;
            var vec = new double[SummaryFeatureCount];

            vec[0] = m.PassedTestCount;
            vec[1] = m.ValidTestCount;
            vec[2] = m.PassedFamilyCount;
            vec[3] = m.ValidFamilyCount;
            vec[4] = m.TestPassedRatio;
            vec[5] = NegLog10WithSentinel(m.CombinedPValue, AnomalySentinels.MissingPValue);
            vec[6] = NegLog10WithSentinel(m.CombinedQValue, AnomalySentinels.MissingQValue);

            SetFamilyFeature(vec, 7, m.CountEnrichmentBestPValue);
            SetFamilyFeature(vec, 8, m.CountEnrichmentCombinedPValue);
            SetFamilyFeature(vec, 9, m.AmbiguityOrTargetDecoyBestPValue);
            SetFamilyFeature(vec, 10, m.AmbiguityOrTargetDecoyCombinedPValue);
            SetFamilyFeature(vec, 11, m.FragmentationBestPValue);
            SetFamilyFeature(vec, 12, m.FragmentationCombinedPValue);
            SetFamilyFeature(vec, 13, m.RetentionTimeBestPValue);
            SetFamilyFeature(vec, 14, m.RetentionTimeCombinedPValue);
            SetFamilyFeature(vec, 15, m.ScoreDistributionBestPValue);
            SetFamilyFeature(vec, 16, m.ScoreDistributionCombinedPValue);
            SetFamilyFeature(vec, 17, m.ProteinGroupBestPValue);
            SetFamilyFeature(vec, 18, m.ProteinGroupCombinedPValue);
            SetFamilyFeature(vec, 19, m.DeNovoBestPValue);
            SetFamilyFeature(vec, 20, m.DeNovoCombinedPValue);
            SetFamilyFeature(vec, 21, m.PrecursorDeconvolutionBestPValue);
            SetFamilyFeature(vec, 22, m.PrecursorDeconvolutionCombinedPValue);

            rawFeatures.Add((kvp.Key, vec));
        }

        return ScaleAndBuild(rawFeatures);
    }

    public static List<IsolationForestInput> BuildFullPerTestFeatures(
        List<StatisticalTestResult> statResults)
    {
        if (statResults == null || statResults.Count == 0)
            return new List<IsolationForestInput>();

        var canonicalPairs = statResults
            .Where(r => !r.IsCombinedResult)
            .Select(r => (r.TestName, r.MetricName))
            .Distinct()
            .OrderBy(p => p.TestName)
            .ThenBy(p => p.MetricName)
            .ToList();

        if (canonicalPairs.Count == 0)
            return new List<IsolationForestInput>();

        int featuresPerPair = 3;
        int featureCount = canonicalPairs.Count * featuresPerPair;

        var resultsByDb = statResults
            .Where(r => !r.IsCombinedResult)
            .GroupBy(r => r.DatabaseName)
            .ToList();

        var rawFeatures = new List<(string Id, double[] Features)>();

        foreach (var dbGroup in resultsByDb)
        {
            var resultLookup = dbGroup
                .GroupBy(r => (r.TestName, r.MetricName))
                .ToDictionary(g => g.Key, g => g.First());

            var vec = new double[featureCount];
            int idx = 0;

            foreach (var pair in canonicalPairs)
            {
                if (resultLookup.TryGetValue(pair, out var result) && result.IsDefined)
                {
                    vec[idx] = NegLog10WithSentinel(result.PValue, AnomalySentinels.MissingPValue);
                    vec[idx + 1] = result.EffectSize ?? AnomalySentinels.MissingEffectSize;
                    vec[idx + 2] = result.TestStatistic ?? AnomalySentinels.MissingTestStatistic;
                }
                else
                {
                    vec[idx] = NegLog10WithSentinel(double.NaN, AnomalySentinels.MissingPValue);
                    vec[idx + 1] = AnomalySentinels.MissingEffectSize;
                    vec[idx + 2] = AnomalySentinels.MissingTestStatistic;
                }

                idx += featuresPerPair;
            }

            rawFeatures.Add((dbGroup.Key, vec));
        }

        return ScaleAndBuild(rawFeatures);
    }

    private static void SetFamilyFeature(double[] vec, int index, double pValue)
    {
        vec[index] = NegLog10WithSentinel(pValue, AnomalySentinels.MissingPValue);
    }

    /// <summary>Replaces NaN/Infinity with 0 so feature vectors are always valid IsolationForest input.</summary>
    private static double SanitizeFinite(double v) => double.IsNaN(v) || double.IsInfinity(v) ? 0.0 : v;

    private static double NegLog10WithSentinel(double value, double sentinel)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            return sentinel;
        if (value >= 1.0)
            return 0.0;
        return -Math.Log10(value);
    }

    private static List<IsolationForestInput> ScaleAndBuild(
        List<(string Id, double[] RawFeatures)> rawFeatures)
    {
        if (rawFeatures.Count == 0)
            return new List<IsolationForestInput>();

        int featureCount = rawFeatures[0].RawFeatures.Length;
        var scaled = new double[rawFeatures.Count][];

        for (int i = 0; i < rawFeatures.Count; i++)
            scaled[i] = new double[featureCount];

        for (int f = 0; f < featureCount; f++)
        {
            // Sanitize raw values first (a non-null Inf effect size, or a 0/0 ratio, can slip through the
            // per-feature sentinels) so the robust-scaling statistics aren't corrupted by NaN/Infinity.
            var values = rawFeatures.Select(r => SanitizeFinite(r.RawFeatures[f])).ToList();
            var scaledValues = ScaleFeature(values).ToList();

            // And sanitize the scaled output — IsolationForestInput rejects any NaN/Infinity, and with
            // thousands of (mostly empty) databases a single stray value would abort the whole analysis.
            for (int i = 0; i < rawFeatures.Count; i++)
                scaled[i][f] = SanitizeFinite(scaledValues[i]);
        }

        var result = new List<IsolationForestInput>(rawFeatures.Count);
        for (int i = 0; i < rawFeatures.Count; i++)
            result.Add(new IsolationForestInput(rawFeatures[i].Id, scaled[i]));

        return result;
    }

    private static IEnumerable<double> ScaleFeature(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        int n = sorted.Count;

        double q1 = sorted[n / 4];
        double q3 = sorted[3 * n / 4];
        double iqr = q3 - q1;

        if (iqr > 0)
        {
            double median = sorted[n / 2];
            foreach (var v in values)
                yield return (v - median) / iqr;
            yield break;
        }

        double min = sorted[0];
        double max = sorted[^1];

        if (max == min)
        {
            foreach (var _ in values)
                yield return 0.0;
            yield break;
        }

        double range = max - min;
        foreach (var v in values)
            yield return (v - min) / range - 0.5;
    }
}
