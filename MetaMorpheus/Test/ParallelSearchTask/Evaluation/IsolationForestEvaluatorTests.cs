using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Evaluation;

namespace Test.ParallelSearchTask.Evaluation;

[TestFixture]
public class IsolationForestEvaluatorTests
{
    private static TransientDatabaseMetrics Row(string name, int peptides, double score, string twinOf = "", int size = 1000) =>
        new(name)
        {
            TwinOf = twinOf,
            TransientPeptideCount = size,
            PsmBacterialUnambiguousTargets = peptides * 2,
            PeptideBacterialUnambiguousTargets = peptides,
            PeptideBacterialTargets = peptides + 1,
            PeptideBacterialUnambiguousTargetScores = peptides == 0 ? [] : Enumerable.Repeat(score, peptides).ToArray(),
            PeptideBacterialTargetDeltaScores = peptides == 0 ? [] : Enumerable.Repeat(score / 3, peptides).ToArray(),
        };

    /// <summary>Sixty absent organisms with a few weak hits, twenty-five present ones with many strong hits.</summary>
    private static List<TransientDatabaseMetrics> Targets()
    {
        var rows = Enumerable.Range(0, 60).Select(i => Row($"Absent{i:D2}", 2 + i % 5, 7 + i % 3)).ToList();
        rows.AddRange(Enumerable.Range(0, 25).Select(i => Row($"Present{i:D2}", 80 + 7 * i, 25 + i % 4)));
        return rows;
    }

    /// <summary>Each twin looks like an absent organism, drawn from the same range, whatever its target.</summary>
    private static List<TransientDatabaseMetrics> TargetsWithTwins()
    {
        var targets = Targets();
        var twins = targets.Select((t, i) => Row(t.DatabaseName + "_entrapment", 2 + i % 5, 7 + i % 3, t.DatabaseName));
        return targets.Concat(twins).ToList();
    }

    [Test]
    public void WithoutTwins_RanksThePresentOrganismsFirst_ButMakesNoCalls()
    {
        var results = new IsolationForestEvaluator().Evaluate(DatabaseEvaluationInput.FromMetrics(Targets()));

        Assert.That(results, Has.Count.EqualTo(85));
        Assert.That(results.All(r => double.IsNaN(r.QValue) && !r.IsCalled));

        var topRanked = results.Where(r => r.Details![IsolationForestEvaluator.RankColumn] <= 25).Select(r => r.DatabaseName);
        Assert.That(topRanked.All(n => n.StartsWith("Present")), Is.True);
        Assert.That(results.Select(r => r.Details![IsolationForestEvaluator.RankColumn]).OrderBy(r => r),
            Is.EqualTo(Enumerable.Range(1, 85).Select(i => (double)i)));
    }

    [Test]
    public void WithTwins_CallsThePresentOrganisms_ThroughTwinCalibratedQValues()
    {
        var results = new IsolationForestEvaluator().Evaluate(DatabaseEvaluationInput.FromMetrics(TargetsWithTwins()));

        var targets = results.Where(r => !r.IsTwin).ToList();
        Assert.That(targets.Where(r => r.IsCalled).Select(r => r.DatabaseName),
            Is.EquivalentTo(Enumerable.Range(0, 25).Select(i => $"Present{i:D2}")));

        var twins = results.Where(r => r.IsTwin).ToList();
        Assert.That(twins, Has.Count.EqualTo(85));
        Assert.That(twins.All(t => !t.IsCalled && double.IsNaN(t.QValue)));
        Assert.That(twins.Max(t => t.Score), Is.LessThan(targets.Where(t => t.IsCalled).Min(t => t.Score)));
    }

    [Test]
    public void Twins_DoNotChangeTheTargetScores()
    {
        var withoutTwins = new IsolationForestEvaluator().Evaluate(DatabaseEvaluationInput.FromMetrics(Targets()))
            .ToDictionary(r => r.DatabaseName, r => r.Score);
        var withTwins = new IsolationForestEvaluator().Evaluate(DatabaseEvaluationInput.FromMetrics(TargetsWithTwins()))
            .Where(r => !r.IsTwin).ToDictionary(r => r.DatabaseName, r => r.Score);

        Assert.That(withTwins, Is.EquivalentTo(withoutTwins));
    }

    [Test]
    public void ADatabaseThatStandsOutForTooLittleEvidence_ScoresZero()
    {
        var rows = Targets();
        rows.Add(Row("Empty", 0, 0));

        var empty = new IsolationForestEvaluator().Evaluate(DatabaseEvaluationInput.FromMetrics(rows))
            .Single(r => r.DatabaseName == "Empty");

        Assert.That(empty.Details![IsolationForestEvaluator.EvidenceIndexColumn], Is.LessThan(0));
        Assert.That(empty.Details[IsolationForestEvaluator.AnomalyScoreColumn], Is.GreaterThan(0));
        Assert.That(empty.Score, Is.EqualTo(0));
    }

    [Test]
    public void NeedsAtLeastTwoTargets()
    {
        Assert.Throws<EvaluatorNotApplicableException>(() =>
            new IsolationForestEvaluator().Evaluate(DatabaseEvaluationInput.FromMetrics([Row("Only", 5, 10)])));
    }

    [Test]
    public void Runner_ReportsTooFewTargetsAsSkipped_WithoutAWarning()
    {
        var warnings = new List<string>();
        var report = new DatabaseEvaluationRunner([new IsolationForestEvaluator()], warnings.Add)
            .Run(DatabaseEvaluationInput.FromMetrics([Row("Only", 5, 10)]));

        Assert.That(report.HasResults, Is.False);
        Assert.That(report.SkippedEvaluators[IsolationForestEvaluator.EvaluatorName], Does.StartWith("Needs at least 2"));
        Assert.That(warnings, Is.Empty);
    }

    [Test]
    public void Features_DropWhatNoTargetHas_AndImputeTheTargetMinimum()
    {
        var targets = new[] { Row("A", 4, 10), Row("B", 8, 20), Row("C", 0, 0) };
        var fitted = FittedDatabaseFeatures.Fit(targets);

        // no parsimony, RT or deconvolution metrics in these rows
        Assert.That(fitted.FeatureNames, Does.Contain("MedianPeptideScore"));
        Assert.That(fitted.FeatureNames, Does.Not.Contain("MedianUniquePeptidesPerProteinGroup"));
        Assert.That(fitted.FeatureNames, Does.Not.Contain("NegMedianAbsPeptideRtError"));
        Assert.That(fitted.FeatureNames, Does.Not.Contain("MedianPeptidePrecursorDeconScore"));

        // C has no peptide scores; it takes the lowest defined one, A's
        int scoreIndex = fitted.FeatureNames.ToList().IndexOf("MedianPeptideScore");
        Assert.That(fitted.Transform(targets[2])[scoreIndex], Is.EqualTo(fitted.Transform(targets[0])[scoreIndex]));

        // more evidence, higher feature
        Assert.That(fitted.Transform(targets[1]).Zip(fitted.Transform(targets[0])).All(p => p.First >= p.Second));
    }
}
