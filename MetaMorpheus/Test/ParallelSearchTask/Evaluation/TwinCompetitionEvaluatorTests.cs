using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Evaluation;

namespace Test.ParallelSearchTask.Evaluation;

[TestFixture]
public class TwinCompetitionEvaluatorTests
{
    [Test]
    public void Compete_GivesTargetDecoyQValues_WithTiesToTheDecoy_AndZeroZeroPairsDroppingOut()
    {
        var pairs = new List<(double, double)>
        {
            (20, 0),  // G  target wins
            (15, 1),  // H  target wins
            (12, 3),  // I  target wins
            (10, 2),  // A  target wins
            (8, 8),   // B  tie goes to the decoy
            (6, 1),   // C  target wins
            (0, 0),   // D  drops out
            (3, 5),   // E  decoy wins
            (4, 0),   // F  target wins
        };

        var results = PairedCompetition.Compete(pairs);

        // ranked winners G20 H15 I12 A10 B8(d) C6 E5(d) F4
        // raw (decoys + 1) / targets: 1, 1/2, 1/3, 1/4, 2/4, 2/5, 3/5, 3/6 ; made monotone from the bottom
        Assert.That(results.Select(r => r.Outcome), Is.EqualTo(new[]
        {
            CompetitionOutcome.TargetWon, CompetitionOutcome.TargetWon, CompetitionOutcome.TargetWon,
            CompetitionOutcome.TargetWon, CompetitionOutcome.DecoyWon, CompetitionOutcome.TargetWon,
            CompetitionOutcome.DroppedOut, CompetitionOutcome.DecoyWon, CompetitionOutcome.TargetWon,
        }));
        Assert.That(results.Select(r => r.QValue),
            Is.EqualTo(new[] { 0.25, 0.25, 0.25, 0.25, 1.0, 0.4, 1.0, 1.0, 0.5 }).Within(1e-12));
    }

    [Test]
    public void Compete_TiedWinningScoresShareOneThreshold()
    {
        // at score 5 a target and a decoy win together: (1 + 1) / 2 = 1, not the 1 / 2 the target would get alone
        var results = PairedCompetition.Compete(new List<(double, double)> { (9, 0), (5, 0), (0, 5) });

        Assert.That(results[1].Outcome, Is.EqualTo(CompetitionOutcome.TargetWon));
        Assert.That(results[1].QValue, Is.EqualTo(1.0));
    }

    [Test]
    public void Compete_HandlesNoPairs_AndRejectsNaN()
    {
        Assert.That(PairedCompetition.Compete(new List<(double, double)>()), Is.Empty);
        Assert.Throws<ArgumentException>(() => PairedCompetition.Compete(new List<(double, double)> { (double.NaN, 1) }));
    }

    private static TransientDatabaseMetrics Row(string name, int peptides, int decoys = 0, string twinOf = "") =>
        new(name) { TwinOf = twinOf, PeptideBacterialUnambiguousTargets = peptides, PeptideBacterialUnambiguousDecoys = decoys };

    /// <summary>Twenty present organisms well above their twins, three absent ones level with theirs, one without a twin row.</summary>
    private static List<TransientDatabaseMetrics> Rows()
    {
        var rows = new List<TransientDatabaseMetrics>();
        for (int i = 0; i < 20; i++)
        {
            rows.Add(Row($"Present{i}", 50 + i, decoys: 1));
            rows.Add(Row($"Present{i}_entrapment", 2, twinOf: $"Present{i}"));
        }
        rows.Add(Row("Absent0", 3, decoys: 3));
        rows.Add(Row("Absent0_entrapment", 3, twinOf: "Absent0"));
        rows.Add(Row("Absent1", 2, decoys: 4));
        rows.Add(Row("Absent1_entrapment", 4, twinOf: "Absent1"));
        rows.Add(Row("Absent2", 0));
        rows.Add(Row("Absent2_entrapment", 0, twinOf: "Absent2"));
        rows.Add(Row("NoTwin", 40));
        return rows;
    }

    [Test]
    public void Evaluate_CallsTargetsThatBeatTheirTwins_AndNotThoseThatDoNot()
    {
        var results = new TwinCompetitionEvaluator().Evaluate(DatabaseEvaluationInput.FromMetrics(Rows()))
            .ToDictionary(r => r.DatabaseName);

        // 20 target wins, 2 twin wins (tie and loss), 1 pair dropped out
        Assert.That(results.Values.Where(r => !r.IsTwin && r.IsCalled).Select(r => r.DatabaseName),
            Is.EquivalentTo(Enumerable.Range(0, 20).Select(i => $"Present{i}")));

        var present = results["Present19"];
        Assert.That(present.Score, Is.EqualTo(69));
        Assert.That(present.QValue, Is.EqualTo(0.05).Within(1e-12), "(0 twin wins + 1) / 20 target wins");
        Assert.That(present.Details![TwinCompetitionEvaluator.OutcomeColumn], Is.EqualTo(1));
        Assert.That(present.Details[TwinCompetitionEvaluator.TwinScoreColumn], Is.EqualTo(2));
        Assert.That(present.Details[TwinCompetitionEvaluator.EstimatedFdpColumn], Is.EqualTo(2.0 / 69).Within(1e-12));

        var tie = results["Absent0"];
        Assert.That(tie.QValue, Is.EqualTo(1.0));
        Assert.That(tie.Details![TwinCompetitionEvaluator.OutcomeColumn], Is.EqualTo(0), "a tie goes to the twin");
        Assert.That(tie.Details[TwinCompetitionEvaluator.EstimatedFdpColumn], Is.EqualTo(1.0));

        Assert.That(results["Absent1"].Details![TwinCompetitionEvaluator.EstimatedFdpColumn], Is.EqualTo(1.0), "capped at 1");
        Assert.That(results["Absent2"].Details![TwinCompetitionEvaluator.OutcomeColumn], Is.EqualTo(-1), "0-0 drops out");
        Assert.That(results["Absent2"].QValue, Is.EqualTo(1.0));
    }

    [Test]
    public void Evaluate_ReportsTwinsScoreOnly_AndATargetWithoutATwinUncalled()
    {
        var results = new TwinCompetitionEvaluator().Evaluate(DatabaseEvaluationInput.FromMetrics(Rows()));

        var twins = results.Where(r => r.IsTwin).ToList();
        Assert.That(twins, Has.Count.EqualTo(23));
        Assert.That(twins.All(t => !t.IsCalled && double.IsNaN(t.QValue)));
        Assert.That(twins.Single(t => t.DatabaseName == "Absent1_entrapment").Score, Is.EqualTo(4));

        var noTwin = results.Single(r => r.DatabaseName == "NoTwin");
        Assert.That(double.IsNaN(noTwin.QValue));
        Assert.That(noTwin.IsCalled, Is.False);
        Assert.That(noTwin.Details!.ContainsKey(TwinCompetitionEvaluator.OutcomeColumn), Is.False);
        Assert.That(noTwin.Details.ContainsKey(TwinCompetitionEvaluator.DecoyQValueColumn), "the reverse-decoy column needs no twin");
    }

    [Test]
    public void Evaluate_AlsoCompetesEachTargetAgainstItsReverseDecoys()
    {
        var results = new TwinCompetitionEvaluator().Evaluate(DatabaseEvaluationInput.FromMetrics(Rows()))
            .ToDictionary(r => r.DatabaseName);

        Assert.That(results["Present0"].Details![TwinCompetitionEvaluator.DecoyOutcomeColumn], Is.EqualTo(1));
        Assert.That(results["Absent1"].Details![TwinCompetitionEvaluator.DecoyOutcomeColumn], Is.EqualTo(0));
        Assert.That(results["Absent1"].Details![TwinCompetitionEvaluator.DecoyQValueColumn], Is.EqualTo(1.0));
    }

    [Test]
    public void Runner_SkipsTheCompetitionWithoutTwins()
    {
        var report = new DatabaseEvaluationRunner([new TwinCompetitionEvaluator()])
            .Run(DatabaseEvaluationInput.FromMetrics([Row("A", 5), Row("B", 7)]));

        Assert.That(report.HasResults, Is.False);
        Assert.That(report.SkippedEvaluators.Keys, Is.EqualTo(new[] { TwinCompetitionEvaluator.EvaluatorName }));
    }
}
