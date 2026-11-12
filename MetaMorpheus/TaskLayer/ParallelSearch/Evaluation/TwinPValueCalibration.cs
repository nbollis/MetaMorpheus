#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TaskLayer.ParallelSearch.Statistics;

namespace TaskLayer.ParallelSearch.Evaluation;

/// <summary>
/// How one test's p-values fall on the twins. A twin holds no real organism, so an honest test gives twins
/// uniform p-values: about <c>alpha</c> of them at p ≤ alpha and a flat histogram.
/// </summary>
public sealed record TwinPValueProfile(
    string TestKey,
    StatisticalEvidenceFamily? Family,
    int TwinCount,
    int TwinsAtOrBelowAlpha,
    int TargetCount,
    int TargetsAtOrBelowAlpha,
    int[] TwinHistogram)
{
    public double TwinFractionAtOrBelowAlpha => TwinCount == 0 ? double.NaN : TwinsAtOrBelowAlpha / (double)TwinCount;
    public double TargetFractionAtOrBelowAlpha => TargetCount == 0 ? double.NaN : TargetsAtOrBelowAlpha / (double)TargetCount;
}

/// <summary>
/// Summarises the twin calibration run of the statistical tests: per test, and for the combined p-value, the
/// twins' p-value histogram and how many twins a test would have called at <c>alpha</c>.
/// </summary>
public static class TwinPValueCalibration
{
    public const int HistogramBins = 10;

    /// <param name="results">Rows from a run over targets and twins together.</param>
    /// <param name="twinNames">Names of the twin databases in that run; every other database counts as a target.</param>
    public static List<TwinPValueProfile> Build(IEnumerable<StatisticalTestResult> results, ISet<string> twinNames, double alpha)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(twinNames);

        var profiles = new List<TwinPValueProfile>();
        foreach (var group in results
                     .Where(r => r.IsDefined && !double.IsNaN(r.PValue) && !double.IsInfinity(r.PValue))
                     .Where(r => !r.IsCombinedResult || r.MetricName == CombinedResultNames.AllMetricName)
                     .GroupBy(r => r.Key))
        {
            var twins = group.Where(r => twinNames.Contains(r.DatabaseName)).Select(r => r.PValue).ToList();
            var targets = group.Where(r => !twinNames.Contains(r.DatabaseName)).Select(r => r.PValue).ToList();

            var histogram = new int[HistogramBins];
            foreach (double p in twins)
                histogram[Math.Clamp((int)(p * HistogramBins), 0, HistogramBins - 1)]++;

            var first = group.First();
            profiles.Add(new TwinPValueProfile(group.Key, first.IsCombinedResult ? null : first.EvidenceFamily,
                twins.Count, twins.Count(p => p <= alpha), targets.Count, targets.Count(p => p <= alpha), histogram));
        }

        // combined row first, then by family and test
        return profiles
            .OrderBy(p => p.Family.HasValue ? 1 : 0)
            .ThenBy(p => p.Family)
            .ThenBy(p => p.TestKey, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The twin-calibration section appended to <c>CalibrationReport.txt</c>.</summary>
    public static string FormatSection(IReadOnlyList<TwinPValueProfile> profiles, double alpha)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        writer.WriteLine("=".PadRight(70, '='));
        writer.WriteLine("  TWIN CALIBRATION");
        writer.WriteLine("=".PadRight(70, '='));
        writer.WriteLine();
        writer.WriteLine("  Second run of the suite over targets and their shuffled twins together. Twins hold no");
        writer.WriteLine($"  real organism, so an honest test calls about {alpha:P0} of them at p <= {alpha}.");
        writer.WriteLine("  Histogram: twin counts in p-value bins of width 0.1, from [0, 0.1) to [0.9, 1].");
        writer.WriteLine();

        if (profiles.Count == 0)
        {
            writer.WriteLine("  No test produced p-values for twins.");
            writer.WriteLine();
            return writer.ToString();
        }

        writer.WriteLine($"  {"Test",-55} {"Twins",6} {"Twin p<=a",10} {"Target p<=a",12}  Histogram");
        foreach (var profile in profiles)
        {
            writer.WriteLine($"  {Truncate(profile.TestKey, 55),-55} {profile.TwinCount,6} " +
                $"{FormatFraction(profile.TwinFractionAtOrBelowAlpha),10} {FormatFraction(profile.TargetFractionAtOrBelowAlpha),12}  " +
                string.Join(" ", profile.TwinHistogram));
        }
        writer.WriteLine();
        return writer.ToString();
    }

    private static string FormatFraction(double value) =>
        double.IsNaN(value) ? "NA" : value.ToString("P1", CultureInfo.InvariantCulture);

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..(length - 3)] + "...";
}
