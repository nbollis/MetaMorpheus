#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskLayer.ParallelSearch.Evaluation;

/// <summary>
/// Turns any per-database score into an empirical q-value by using the twins' scores as the null. An absent
/// organism and its twin are exchangeable, so among targets scoring at least t, about as many are false as there
/// are twins scoring at least t. Shared by every evaluator that scores twins.
/// </summary>
public static class TwinCalibration
{
    /// <summary>
    /// q(t) = (twins with score ≥ t + 1) / (targets with score ≥ t), capped at 1 and made monotone so a higher
    /// score never gets a larger q. The +1 keeps the estimate conservative when counts are small. Targets with a
    /// NaN score get NaN.
    /// </summary>
    /// <returns>One q-value per target score, in the order given.</returns>
    public static double[] EmpiricalQValues(IReadOnlyList<double> targetScores, IReadOnlyList<double> twinScores)
    {
        ArgumentNullException.ThrowIfNull(targetScores);
        ArgumentNullException.ThrowIfNull(twinScores);

        var qValues = new double[targetScores.Count];
        double[] sortedTwins = twinScores.Where(s => !double.IsNaN(s)).OrderByDescending(s => s).ToArray();

        // targets best first; ties share one threshold, so they are handled as a block
        int[] order = Enumerable.Range(0, targetScores.Count)
            .Where(i => !double.IsNaN(targetScores[i]))
            .OrderByDescending(i => targetScores[i])
            .ToArray();
        foreach (int i in Enumerable.Range(0, targetScores.Count).Where(i => double.IsNaN(targetScores[i])))
            qValues[i] = double.NaN;

        var raw = new double[order.Length];
        int twinsAtOrAbove = 0;
        int start = 0;
        while (start < order.Length)
        {
            double threshold = targetScores[order[start]];
            int end = start;
            while (end + 1 < order.Length && targetScores[order[end + 1]] == threshold)
                end++;

            while (twinsAtOrAbove < sortedTwins.Length && sortedTwins[twinsAtOrAbove] >= threshold)
                twinsAtOrAbove++;

            int targetsAtOrAbove = end + 1;
            double q = Math.Min(1.0, (twinsAtOrAbove + 1.0) / targetsAtOrAbove);
            for (int k = start; k <= end; k++)
                raw[k] = q;
            start = end + 1;
        }

        // monotone: walking from the worst score up, a q may only stay or fall
        double running = 1.0;
        for (int k = order.Length - 1; k >= 0; k--)
        {
            running = Math.Min(running, raw[k]);
            qValues[order[k]] = running;
        }

        return qValues;
    }
}
