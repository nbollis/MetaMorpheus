#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Evaluation;

/// <summary>
/// Target-versus-twin competition at database level: a traditional target-decoy q-value in which each database's
/// shuffled twin plays its decoy. The score is the number of unambiguous peptides at the aligned q-value cutoff
/// (<see cref="TransientDatabaseMetrics.PeptideBacterialUnambiguousTargets"/>); a twin's entrapment peptides are
/// counted the same way, since they are not decoys. See <see cref="PairedCompetition"/> for the rules.
/// <para>
/// Each target also gets a per-database false-discovery estimate, twin peptides / target peptides capped at 1. Twins
/// are searched on their own rather than alongside their targets, so this simple ratio applies; mzLib's combined and
/// paired estimators assume a joint search. For comparison, the same competition is also run against the target's
/// own reverse-decoy count. That column needs no twins, but those decoys already set the PSM q-values, so it is not
/// an independent check.
/// </para>
/// </summary>
public sealed class TwinCompetitionEvaluator : IDatabaseEvaluator
{
    public const string EvaluatorName = "TwinCompetition";
    public const double DefaultAlpha = 0.05;

    public const string TwinScoreColumn = "TwinScore";
    public const string OutcomeColumn = "Outcome";
    public const string EstimatedFdpColumn = "EstimatedFdp";
    public const string DecoyScoreColumn = "ReverseDecoyScore";
    public const string DecoyOutcomeColumn = "ReverseDecoyOutcome";
    public const string DecoyQValueColumn = "ReverseDecoyQValue";

    public TwinCompetitionEvaluator(double alpha = DefaultAlpha)
    {
        Alpha = alpha;
    }

    public string Name => EvaluatorName;
    public bool RequiresTwins => true;
    public double Alpha { get; }

    /// <summary>The competition score of one database.</summary>
    public static double Score(TransientDatabaseMetrics metrics) => metrics.PeptideBacterialUnambiguousTargets;

    /// <summary>Outcome as written to the CSV: 1 target won, 0 decoy won (including ties), −1 dropped out (both 0).</summary>
    public static double OutcomeCode(CompetitionOutcome outcome) => outcome switch
    {
        CompetitionOutcome.TargetWon => 1,
        CompetitionOutcome.DecoyWon => 0,
        _ => -1,
    };

    /// <summary>Twin peptides over target peptides, capped at 1; 1 when the target has none.</summary>
    public static double EstimatedFdp(double targetScore, double twinScore) =>
        targetScore <= 0 ? 1.0 : Math.Min(1.0, twinScore / targetScore);

    public IReadOnlyList<DatabaseEvaluationResult> Evaluate(DatabaseEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var pairs = input.Pairs.ToList();
        var twinResults = PairedCompetition.Compete(pairs.Select(p => (Score(p.Target), Score(p.Twin))).ToList());
        var byTarget = new Dictionary<string, (TransientDatabaseMetrics Twin, CompetitionResult Result)>(StringComparer.Ordinal);
        for (int i = 0; i < pairs.Count; i++)
            byTarget[pairs[i].Target.DatabaseName] = (pairs[i].Twin, twinResults[i]);

        var targets = input.Targets;
        var decoyResults = PairedCompetition.Compete(
            targets.Select(t => (Score(t), (double)t.PeptideBacterialUnambiguousDecoys)).ToList());

        var results = new List<DatabaseEvaluationResult>(targets.Count + pairs.Count);
        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            double score = Score(target);
            var details = new Dictionary<string, double>
            {
                [DecoyScoreColumn] = target.PeptideBacterialUnambiguousDecoys,
                [DecoyOutcomeColumn] = OutcomeCode(decoyResults[i].Outcome),
                [DecoyQValueColumn] = decoyResults[i].QValue,
            };

            // a target whose twin has no row cannot compete; it is reported but never called
            double q = double.NaN;
            if (byTarget.TryGetValue(target.DatabaseName, out var pair))
            {
                double twinScore = Score(pair.Twin);
                q = pair.Result.QValue;
                details[TwinScoreColumn] = twinScore;
                details[OutcomeColumn] = OutcomeCode(pair.Result.Outcome);
                details[EstimatedFdpColumn] = EstimatedFdp(score, twinScore);
            }

            results.Add(new DatabaseEvaluationResult(target.DatabaseName, string.Empty, score, double.NaN, q,
                !double.IsNaN(q) && q <= Alpha, details));
        }

        foreach (var (target, twin) in pairs)
            results.Add(DatabaseEvaluationResult.ScoreOnly(twin.DatabaseName, twin.TwinOf, Score(twin)));

        return results;
    }
}
