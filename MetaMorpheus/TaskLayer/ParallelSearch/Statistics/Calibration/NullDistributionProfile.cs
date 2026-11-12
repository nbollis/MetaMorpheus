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

        // Single pass for mean and variance (avoids two LINQ passes)
        double sum = 0;
        for (int i = 0; i < sorted.Count; i++)
            sum += sorted[i];
        Mean = sum / Count;

        double sumSqDiff = 0;
        for (int i = 0; i < sorted.Count; i++)
        {
            double diff = sorted[i] - Mean;
            sumSqDiff += diff * diff;
        }
        StdDev = Math.Sqrt(sumSqDiff / Count);

        Median = ComputePercentile(sorted, 0.50);
        Percentile50 = Median;
        Percentile90 = ComputePercentile(sorted, 0.90);
        Percentile95 = ComputePercentile(sorted, 0.95);
        Percentile99 = ComputePercentile(sorted, 0.99);
    }

    public double GetPercentile(double fraction)
    {
        if (Count == 0)
            return double.NaN;
        if (fraction <= 0.0) return Min;
        if (fraction >= 1.0) return Max;
        return ComputePercentile(SortedValues, fraction);
    }

    private static double ComputePercentile(IReadOnlyList<double> sorted, double fraction)
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
