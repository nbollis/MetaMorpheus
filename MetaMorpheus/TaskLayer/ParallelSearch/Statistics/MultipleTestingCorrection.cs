using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Implements Benjamini-Hochberg FDR correction
/// </summary>
public static class MultipleTestingCorrection
{
    /// <summary>
    /// Apply Benjamini-Hochberg correction to p-values
    /// </summary>
    /// <param name="pValues">Dictionary of identifier to p-value</param>
    /// <returns>Dictionary of identifier to q-value</returns>
    public static Dictionary<string, double> BenjaminiHochberg(Dictionary<string, double> pValues)
    {
        if (pValues == null || pValues.Count == 0)
            return new Dictionary<string, double>();

        // Sort by p-value
        var sorted = pValues
            .OrderBy(kvp => kvp.Value)
            .ToList();

        int n = sorted.Count;
        var qValues = new Dictionary<string, double>();

        // Compute q-values
        var tempQValues = new double[n];
        for (int i = 0; i < n; i++)
        {
            int rank = i + 1;
            tempQValues[i] = sorted[i].Value * n / rank;
        }

        // Enforce monotonicity (q-values should not decrease)
        for (int i = n - 2; i >= 0; i--)
        {
            tempQValues[i] = Math.Min(tempQValues[i], tempQValues[i + 1]);
        }

        // Cap at 1.0
        for (int i = 0; i < n; i++)
        {
            qValues[sorted[i].Key] = Math.Min(1.0, tempQValues[i]);
        }

        return qValues;
    }

    /// <summary>
    /// Apply BH correction to a list of results
    /// </summary>
    public static void ApplyBenjaminiHochberg(List<StatisticalResult> results)
    {
        if (results == null || results.Count == 0)
            return;

        // Group by test name and metric (correct separately for each test)
        var grouped = results.GroupBy(r => (r.TestName, r.MetricName));

        foreach (var group in grouped)
        {
            var pValues = group.ToDictionary(r => r.DatabaseName, r => r.PValue);
            var qValues = BenjaminiHochberg(pValues);

            foreach (var result in group)
            {
                result.QValue = qValues[result.DatabaseName];
            }
        }
    }
}