#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskLayer.ParallelSearch.Evaluation;

/// <summary>
/// Posterior error probabilities from a score that targets and twins share. Twins follow the null distribution;
/// targets are a mixture of null and present. With π₀ the null fraction among targets,
/// PEP(s) = π₀ · f_twin(s) / f_target(s).
/// </summary>
public static class LocalFdr
{
    /// <summary>
    /// π₀ from the low-score region: below the twins' median, only nulls are expected, and half of them fall there.
    /// So π₀ ≈ (fraction of targets at or below the twin median) / 0.5, capped at 1.
    /// </summary>
    public static double EstimatePi0(IReadOnlyList<double> targetScores, IReadOnlyList<double> twinScores)
    {
        if (targetScores.Count == 0 || twinScores.Count == 0)
            return 1.0;

        var sortedTwins = twinScores.OrderBy(s => s).ToArray();
        double twinMedian = sortedTwins.Length % 2 == 1
            ? sortedTwins[sortedTwins.Length / 2]
            : (sortedTwins[sortedTwins.Length / 2 - 1] + sortedTwins[sortedTwins.Length / 2]) / 2;

        double below = targetScores.Count(s => s <= twinMedian) / (double)targetScores.Count;
        return Math.Clamp(below / 0.5, 0.0, 1.0);
    }

    /// <summary>
    /// PEP for each target. Densities are Gaussian kernel estimates with one Silverman bandwidth computed from all
    /// scores, so the two densities are smoothed alike. The ratio is capped at 1 and made non-increasing in the
    /// score, so a better score never gets a larger PEP.
    /// </summary>
    /// <returns>One PEP per target score, in the order given.</returns>
    public static double[] PosteriorErrorProbabilities(IReadOnlyList<double> targetScores, IReadOnlyList<double> twinScores,
        double pi0)
    {
        ArgumentNullException.ThrowIfNull(targetScores);
        ArgumentNullException.ThrowIfNull(twinScores);

        var peps = new double[targetScores.Count];
        if (targetScores.Count == 0)
            return peps;
        if (twinScores.Count == 0)
        {
            Array.Fill(peps, 1.0);
            return peps;
        }

        double bandwidth = SilvermanBandwidth(targetScores.Concat(twinScores).ToArray());
        var raw = new double[targetScores.Count];
        for (int i = 0; i < targetScores.Count; i++)
        {
            double fTarget = Density(targetScores, targetScores[i], bandwidth);
            double fTwin = Density(twinScores, targetScores[i], bandwidth);
            raw[i] = fTarget <= 0 ? (fTwin > 0 ? 1.0 : 0.0) : Math.Min(1.0, pi0 * fTwin / fTarget);
        }

        // non-increasing in the score, made conservatively: walking down from the best score, a PEP may only stay
        // or grow. (Walking up with a running minimum would let one low-score outlier, where the twin density's
        // tail is near zero, pull every better score down to that near-zero PEP.)
        double running = 0.0;
        foreach (int i in Enumerable.Range(0, targetScores.Count).OrderByDescending(i => targetScores[i]))
        {
            running = Math.Max(running, raw[i]);
            peps[i] = running;
        }
        return peps;
    }

    /// <summary>
    /// Database-level q-values: the mean PEP of all targets at least as good. Targets with equal PEP share one value.
    /// </summary>
    public static double[] QValuesFromPeps(IReadOnlyList<double> peps)
    {
        ArgumentNullException.ThrowIfNull(peps);
        var q = new double[peps.Count];
        int[] order = Enumerable.Range(0, peps.Count).OrderBy(i => peps[i]).ToArray();

        double sum = 0;
        int start = 0;
        while (start < order.Length)
        {
            int end = start;
            while (end + 1 < order.Length && peps[order[end + 1]] == peps[order[start]])
                end++;
            for (int k = start; k <= end; k++)
                sum += peps[order[k]];
            double mean = sum / (end + 1);
            for (int k = start; k <= end; k++)
                q[order[k]] = mean;
            start = end + 1;
        }
        return q;
    }

    private static double SilvermanBandwidth(double[] values)
    {
        int n = values.Length;
        double mean = values.Average();
        double sd = Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / Math.Max(n - 1, 1));
        var sorted = values.OrderBy(v => v).ToArray();
        double iqr = sorted[3 * n / 4] - sorted[n / 4];
        double spread = iqr > 0 ? Math.Min(sd, iqr / 1.34) : sd;
        double h = 0.9 * spread * Math.Pow(n, -0.2);
        return h > 0 ? h : 1e-3;
    }

    private static double Density(IReadOnlyList<double> sample, double x, double bandwidth)
    {
        double sum = 0;
        foreach (double v in sample)
        {
            double z = (x - v) / bandwidth;
            sum += Math.Exp(-0.5 * z * z);
        }
        return sum / (sample.Count * bandwidth * Math.Sqrt(2 * Math.PI));
    }
}
