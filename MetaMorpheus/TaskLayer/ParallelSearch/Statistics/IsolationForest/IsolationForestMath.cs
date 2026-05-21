using System;
using System.Collections.Generic;

namespace TaskLayer.ParallelSearch.Statistics.IsolationForest;

internal static class IsolationForestMath
{
    /// <summary>
    /// Scales values by subtracting median and dividing by the interquartile range (IQR).
    /// This is better than z-score normalization for skewed data, which is common in anomaly detection contexts.
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<double> ScaleValues(IList<double> values)
    {
        if (values == null || values.Count == 0)
            yield break;

        var sortedValues = new List<double>(values);
        sortedValues.Sort();

        double median = sortedValues[sortedValues.Count / 2];
        double q1 = sortedValues[sortedValues.Count / 4];
        double q3 = sortedValues[3 * sortedValues.Count / 4];
        double iqr = q3 - q1;

        foreach (var value in values)
        {
            yield return (value - median) / iqr;
        }
    }

    /// <summary>
    /// Expected path length of an unsuccessful search in a binary search tree.
    /// This is the c(n) normalization term from the Isolation Forest paper.
    /// </summary>
    public static double ExpectedPathLength(int n)
    {
        if (n <= 1)
            return 0.0;

        if (n == 2)
            return 1.0;

        return 2.0 * HarmonicNumber(n - 1) - (2.0 * (n - 1) / n);
    }

    private static double HarmonicNumber(int n)
    {
        if (n < 1)
            return 0.0;

        // Good approximation for n > ~50, exact sum for smaller n.
        if (n <= 50)
        {
            double sum = 0.0;

            for (int i = 1; i <= n; i++)
                sum += 1.0 / i;

            return sum;
        }

        const double gamma = 0.5772156649015329;

        double x = n;

        return Math.Log(x)
               + gamma
               + 1.0 / (2.0 * x)
               - 1.0 / (12.0 * x * x);
    }
}


