using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Evaluation;

namespace Test.ParallelSearchTask.Evaluation;

[TestFixture]
public class ProteomePepEvaluatorTests
{
    #region Local FDR

    /// <summary>Evenly spread, deterministic stand-in for draws from a normal-ish distribution.</summary>
    private static List<double> Spread(int count, double center, double width) =>
        Enumerable.Range(0, count).Select(i => center + width * ((i + 0.5) / count - 0.5)).ToList();

    [Test]
    public void LocalFdr_TargetsLikeTwins_GivePepNearOne()
    {
        var targets = Spread(200, 0, 4);
        var twins = Spread(200, 0, 4);

        double pi0 = LocalFdr.EstimatePi0(targets, twins);
        double[] peps = LocalFdr.PosteriorErrorProbabilities(targets, twins, pi0);

        Assert.That(pi0, Is.EqualTo(1.0).Within(0.02));
        Assert.That(peps.Min(), Is.GreaterThan(0.8));
    }

    [Test]
    public void LocalFdr_HalfPresent_EstimatesPi0NearHalf_AndPresentTargetsGetLowPep()
    {
        var targets = Spread(100, 0, 4).Concat(Spread(100, 20, 4)).ToList();
        var twins = Spread(200, 0, 4);

        double pi0 = LocalFdr.EstimatePi0(targets, twins);
        double[] peps = LocalFdr.PosteriorErrorProbabilities(targets, twins, pi0);

        Assert.That(pi0, Is.EqualTo(0.5).Within(0.05));
        Assert.That(peps.Skip(100).Max(), Is.LessThan(0.01));
        Assert.That(peps.Take(100).Min(), Is.GreaterThan(0.5));
    }

    [Test]
    public void LocalFdr_LowScoreOutlier_DoesNotPullBetterScoresDown()
    {
        // the tier-2 twins-on failure: one target far below every twin has a near-zero twin density, so its raw
        // PEP is near 0; that must not become the PEP of every better-scoring null target
        var targets = Spread(200, 0, 4);
        targets.Add(-50);
        var twins = Spread(200, 0, 4);

        double[] peps = LocalFdr.PosteriorErrorProbabilities(targets, twins, 1.0);

        Assert.That(peps.Take(200).Min(), Is.GreaterThan(0.8));
        Assert.That(LocalFdr.QValuesFromPeps(peps).Min(), Is.GreaterThan(0.05), "nothing called");
    }

    [Test]
    public void LocalFdr_PepNeverRisesWithScore()
    {
        var targets = Spread(50, 0, 6).Concat(Spread(10, 5, 2)).ToList();
        var twins = Spread(60, 0, 6);
        double[] peps = LocalFdr.PosteriorErrorProbabilities(targets, twins, 1.0);

        var byScore = Enumerable.Range(0, targets.Count).OrderBy(i => targets[i]).Select(i => peps[i]).ToList();
        for (int k = 1; k < byScore.Count; k++)
            Assert.That(byScore[k], Is.LessThanOrEqualTo(byScore[k - 1]));
    }

    [Test]
    public void QValuesFromPeps_AreTheMeanPepOfAllAtLeastAsGood()
    {
        double[] q = LocalFdr.QValuesFromPeps([0.1, 0.0, 0.5, 0.1]);

        // sorted 0.0, 0.1, 0.1, 0.5: the tied 0.1s share (0 + 0.1 + 0.1) / 3
        Assert.That(q, Is.EqualTo(new[] { 0.2 / 3, 0.0, 0.7 / 4, 0.2 / 3 }).Within(1e-12));
    }

    #endregion

    #region Evaluator

    private static TransientDatabaseMetrics Row(string name, int peptides, double score, string twinOf = "") =>
        new(name)
        {
            TwinOf = twinOf,
            TransientPeptideCount = 1000,
            PsmBacterialUnambiguousTargets = peptides * 2,
            PeptideBacterialUnambiguousTargets = peptides,
            PeptideBacterialTargets = peptides + 1,
            PeptideBacterialUnambiguousTargetScores = peptides == 0 ? [] : Enumerable.Repeat(score, peptides).ToArray(),
            PeptideBacterialTargetDeltaScores = peptides == 0 ? [] : Enumerable.Repeat(score / 3, peptides).ToArray(),
        };

    /// <summary>Sixty absent organisms and twenty-five present ones; every twin looks like an absent organism.</summary>
    private static List<TransientDatabaseMetrics> Rows()
    {
        var targets = Enumerable.Range(0, 60).Select(i => Row($"Absent{i:D2}", 2 + i % 5, 7 + i % 3)).ToList();
        targets.AddRange(Enumerable.Range(0, 25).Select(i => Row($"Present{i:D2}", 80 + 7 * i, 25 + i % 4)));
        var twins = targets.Select((t, i) => Row(t.DatabaseName + "_entrapment", 2 + (i * 3) % 5, 7 + (i * 2) % 3, t.DatabaseName));
        return targets.Concat(twins).ToList();
    }

    [Test]
    public void Evaluate_CallsThePresentOrganisms()
    {
        var results = new ProteomePepEvaluator().Evaluate(DatabaseEvaluationInput.FromMetrics(Rows()));

        var targets = results.Where(r => !r.IsTwin).ToList();
        Assert.That(targets.Where(r => r.IsCalled).Select(r => r.DatabaseName),
            Is.EquivalentTo(Enumerable.Range(0, 25).Select(i => $"Present{i:D2}")));

        double pi0 = targets[0].Details![ProteomePepEvaluator.Pi0Column];
        Assert.That(pi0, Is.InRange(0.5, 1.0), "about 60 of 85 targets are null");
        Assert.That(targets.Where(r => r.DatabaseName.StartsWith("Present")).Max(r => r.Details![ProteomePepEvaluator.PepColumn]),
            Is.LessThan(0.05));

        var twins = results.Where(r => r.IsTwin).ToList();
        Assert.That(twins, Has.Count.EqualTo(85));
        Assert.That(twins.All(t => !t.IsCalled && double.IsNaN(t.QValue)));
    }

    [Test]
    public void EachTwinIsScoredInItsTargetsFold()
    {
        var results = new ProteomePepEvaluator().Evaluate(DatabaseEvaluationInput.FromMetrics(Rows()));
        var foldByTarget = results.Where(r => !r.IsTwin)
            .ToDictionary(r => r.DatabaseName, r => r.Details![ProteomePepEvaluator.FoldColumn]);

        foreach (var twin in results.Where(r => r.IsTwin))
            Assert.That(twin.Details![ProteomePepEvaluator.FoldColumn], Is.EqualTo(foldByTarget[twin.TwinOf]), twin.DatabaseName);

        var foldSizes = foldByTarget.Values.GroupBy(f => f).Select(g => g.Count()).ToList();
        Assert.That(foldSizes, Has.Count.EqualTo(DatabasePepModel.DefaultFoldCount));
        Assert.That(foldSizes.Max() - foldSizes.Min(), Is.LessThanOrEqualTo(1));
    }

    [Test]
    public void Evaluate_IsDeterministic()
    {
        var first = new ProteomePepEvaluator().Evaluate(DatabaseEvaluationInput.FromMetrics(Rows()));
        var second = new ProteomePepEvaluator().Evaluate(DatabaseEvaluationInput.FromMetrics(Rows()));

        Assert.That(second.Select(r => r.Score), Is.EqualTo(first.Select(r => r.Score)));
    }

    [Test]
    public void TooFewDatabases_IsNotApplicable()
    {
        var rows = new List<TransientDatabaseMetrics>();
        for (int i = 0; i < 4; i++)
        {
            rows.Add(Row($"T{i}", 5 + i, 10));
            rows.Add(Row($"T{i}_entrapment", 3, 8, $"T{i}"));
        }

        Assert.Throws<EvaluatorNotApplicableException>(() =>
            new ProteomePepEvaluator().Evaluate(DatabaseEvaluationInput.FromMetrics(rows)));
    }

    #endregion
}
