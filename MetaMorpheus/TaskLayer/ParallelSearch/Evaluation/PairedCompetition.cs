#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskLayer.ParallelSearch.Evaluation;

/// <summary>Who won one target-versus-decoy pair.</summary>
public enum CompetitionOutcome
{
    /// <summary>Both scored 0; the pair takes no part in the competition.</summary>
    DroppedOut,
    TargetWon,
    /// <summary>The decoy scored higher, or the two tied.</summary>
    DecoyWon,
}

/// <summary>The outcome of one pair and the target's q-value.</summary>
/// <param name="QValue">The target's q-value; 1 when the target lost or the pair dropped out.</param>
public readonly record struct CompetitionResult(CompetitionOutcome Outcome, double QValue);

/// <summary>
/// Picked target-decoy competition at database level: each target competes only with its own decoy (a twin, or the
/// reverse decoys of the same search), and the winners are ranked to give q-values as in ordinary target-decoy FDR.
/// </summary>
public static class PairedCompetition
{
    /// <summary>
    /// Runs the competition. The higher score wins and a tie goes to the decoy, which is the conservative choice and
    /// matches mzLib's convention. Pairs where both score 0 drop out. Winners are ranked by their winning score;
    /// at rank k, q_k = min over j ≥ k of (decoy wins at or above j + 1) / max(target wins at or above j, 1). Equal
    /// winning scores share one threshold. A target that loses its pair, or whose pair drops out, gets q = 1.
    /// </summary>
    /// <returns>One result per pair, in the order given.</returns>
    public static CompetitionResult[] Compete(IReadOnlyList<(double Target, double Decoy)> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);

        var outcomes = new CompetitionOutcome[pairs.Count];
        var winningScores = new double[pairs.Count];
        for (int i = 0; i < pairs.Count; i++)
        {
            var (target, decoy) = pairs[i];
            if (double.IsNaN(target) || double.IsNaN(decoy))
                throw new ArgumentException($"Pair {i} has a NaN score.", nameof(pairs));

            if (target <= 0 && decoy <= 0)
                outcomes[i] = CompetitionOutcome.DroppedOut;
            else
                outcomes[i] = target > decoy ? CompetitionOutcome.TargetWon : CompetitionOutcome.DecoyWon;
            winningScores[i] = Math.Max(target, decoy);
        }

        int[] order = Enumerable.Range(0, pairs.Count)
            .Where(i => outcomes[i] != CompetitionOutcome.DroppedOut)
            .OrderByDescending(i => winningScores[i])
            .ToArray();

        // raw q at each tied block of winning scores, best first
        var raw = new double[order.Length];
        int targetWins = 0, decoyWins = 0, start = 0;
        while (start < order.Length)
        {
            int end = start;
            while (end + 1 < order.Length && winningScores[order[end + 1]] == winningScores[order[start]])
                end++;

            for (int k = start; k <= end; k++)
            {
                if (outcomes[order[k]] == CompetitionOutcome.TargetWon)
                    targetWins++;
                else
                    decoyWins++;
            }

            double q = Math.Min(1.0, (decoyWins + 1.0) / Math.Max(targetWins, 1));
            for (int k = start; k <= end; k++)
                raw[k] = q;
            start = end + 1;
        }

        var results = new CompetitionResult[pairs.Count];
        for (int i = 0; i < pairs.Count; i++)
            results[i] = new CompetitionResult(outcomes[i], 1.0);

        // monotone: walking from the worst winner up, a q may only stay or fall
        double running = 1.0;
        for (int k = order.Length - 1; k >= 0; k--)
        {
            running = Math.Min(running, raw[k]);
            int i = order[k];
            if (outcomes[i] == CompetitionOutcome.TargetWon)
                results[i] = new CompetitionResult(CompetitionOutcome.TargetWon, running);
        }

        return results;
    }
}
