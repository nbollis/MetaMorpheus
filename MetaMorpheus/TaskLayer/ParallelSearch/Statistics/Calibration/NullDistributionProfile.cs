#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskLayer.ParallelSearch.Statistics.Calibration;

public sealed class NullDistributionProfile
{
    public string Label { get; }
    public int Count { get; }
    public double Mean { get; }
    public double Median { get; }
    public double StdDev { get; }
    public double Min { get; }
    public double Max { get; }
    public double Percentile50 { get; }
    public double Percentile90 { get; }
    public double Percentile95 { get; }
    public double Percentile99 { get; }
    public IReadOnlyList<double> SortedValues { get; }

    public NullDistributionProfile(string label, IEnumerable<double> values)
    {
        Label = label;
        var sorted = values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v))
            .OrderBy(v => v)
            .ToList();

        SortedValues = sorted.AsReadOnly();
        Count = sorted.Count;

        if (Count == 0)
        {
            Mean = double.NaN;
            Median = double.NaN;
            StdDev = double.NaN;
            Min = double.NaN;
            Max = double.NaN;
            Percentile50 = double.NaN;
            Percentile90 = double.NaN;
            Percentile95 = double.NaN;
            Percentile99 = double.NaN;
            return;
        }

        Min = sorted[0];
        Max = sorted[^1];
        Mean = sorted.Average();
        Median = ComputePercentile(sorted, 0.50);
        StdDev = Math.Sqrt(sorted.Sum(v => (v - Mean) * (v - Mean)) / Count);

        Percentile50 = Median;
        Percentile90 = ComputePercentile(sorted, 0.90);
        Percentile95 = ComputePercentile(sorted, 0.95);
        Percentile99 = ComputePercentile(sorted, 0.99);
    }

    public double GetPercentile(double fraction)
    {
        if (Count == 0)
            return double.NaN;
        return ComputePercentile(SortedValues.ToList(), fraction);
    }

    private static double ComputePercentile(List<double> sorted, double fraction)
    {
        if (sorted.Count == 0)
            return double.NaN;

        double rank = fraction * (sorted.Count - 1);
        int lower = (int)Math.Floor(rank);
        int upper = (int)Math.Ceiling(rank);

        if (lower == upper)
            return sorted[lower];

        double frac = rank - lower;
        return sorted[lower] * (1.0 - frac) + sorted[upper] * frac;
    }
}
