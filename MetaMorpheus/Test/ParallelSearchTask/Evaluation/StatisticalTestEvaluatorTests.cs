using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EngineLayer;
using NUnit.Framework;
using TaskLayer.ParallelSearch;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Evaluation;
using TaskLayer.ParallelSearch.Statistics;

namespace Test.ParallelSearchTask.Evaluation;

[TestFixture]
public class StatisticalTestEvaluatorTests
{
    private const string Present = "Present";

    /// <summary>Two count tests and one score test, so both stages of the combination have something to combine.</summary>
    private static IEnumerable<IStatisticalTest> SmallSuite() =>
    [
        new GaussianTest<double>("Peptides", StatisticalEvidenceFamily.CountEnrichment,
            r => r.PeptideBacterialUnambiguousTargets),
        new GaussianTest<double>("Psms", StatisticalEvidenceFamily.CountEnrichment,
            r => r.TargetPsmsFromTransientDbAtQValueThreshold),
        new GaussianTest<double>("Proteins", StatisticalEvidenceFamily.ScoreDistribution,
            r => r.TargetProteinGroupsFromTransientDbAtQValueThreshold),
    ];

    private static TransientDatabaseMetrics Row(string name, int count, string twinOf = "") =>
        new(name)
        {
            TwinOf = twinOf,
            PeptideBacterialUnambiguousTargets = count,
            TargetPsmsFromTransientDbAtQValueThreshold = count * 3,
            TargetProteinGroupsFromTransientDbAtQValueThreshold = count / 2,
        };

    /// <summary>Twenty absent organisms with small counts, one present organism far above them.</summary>
    private static List<TransientDatabaseMetrics> Targets()
    {
        var rows = Enumerable.Range(0, 20).Select(i => Row($"Absent{i:D2}", 10 + i % 7)).ToList();
        rows.Add(Row(Present, 200));
        return rows;
    }

    private static List<TransientDatabaseMetrics> TargetsWithTwins()
    {
        var targets = Targets();
        var twins = targets.Select((t, i) => Row(t.DatabaseName + "_twin", 8 + i % 5, t.DatabaseName)).ToList();
        return targets.Concat(twins).ToList();
    }

    private static StatisticalTestEvaluator NewEvaluator() => new(SmallSuite);

    [Test]
    public void WithoutTwins_ScoresTargetsOnly_AndCallsThePresentOrganism()
    {
        var evaluator = NewEvaluator();
        var results = evaluator.Evaluate(DatabaseEvaluationInput.FromMetrics(Targets()));

        Assert.That(results, Has.Count.EqualTo(21));
        Assert.That(results.Any(r => r.IsTwin), Is.False);
        Assert.That(evaluator.TwinProfiles, Is.Null);

        var present = results.Single(r => r.DatabaseName == Present);
        Assert.That(present.IsCalled, Is.True);
        Assert.That(present.QValue, Is.LessThanOrEqualTo(StatisticalTestEvaluator.DefaultAlpha));
        Assert.That(present.Score, Is.EqualTo(-Math.Log10(present.PValue)).Within(1e-9));
        Assert.That(results.Where(r => r.DatabaseName != Present).Count(r => r.IsCalled), Is.Zero);
        Assert.That(results.Max(r => r.Score), Is.EqualTo(present.Score));

        Assert.That(present.Details!.ContainsKey(StatisticalTestEvaluator.TwinCalibratedQValueColumn), Is.False);
        Assert.That(present.Details[StatisticalTestEvaluator.TestsRunColumn], Is.EqualTo(3));
        Assert.That(present.Details.ContainsKey(StatisticalTestEvaluator.FamilyQValueColumn(StatisticalEvidenceFamily.CountEnrichment)));
    }

    [Test]
    public void OfficialRun_FillsTheTargetsStatisticsFields()
    {
        var targets = Targets();
        var results = NewEvaluator().Evaluate(DatabaseEvaluationInput.FromMetrics(targets));

        var present = targets.Single(t => t.DatabaseName == Present);
        var presentResult = results.Single(r => r.DatabaseName == Present);
        Assert.That(present.StatisticalTestsRun, Is.EqualTo(3));
        Assert.That(present.CombinedPValue, Is.EqualTo(presentResult.PValue));
        Assert.That(present.CombinedQValue, Is.EqualTo(presentResult.QValue));
    }

    [Test]
    public void Twins_DoNotChangeTheOfficialResults()
    {
        var withoutTwins = NewEvaluator().Evaluate(DatabaseEvaluationInput.FromMetrics(Targets()));
        var withTwins = NewEvaluator().Evaluate(DatabaseEvaluationInput.FromMetrics(TargetsWithTwins()));

        var official = withTwins.Where(r => !r.IsTwin).ToDictionary(r => r.DatabaseName);
        Assert.That(official.Keys, Is.EquivalentTo(withoutTwins.Select(r => r.DatabaseName)));
        foreach (var expected in withoutTwins)
        {
            var actual = official[expected.DatabaseName];
            Assert.That(actual.PValue, Is.EqualTo(expected.PValue), expected.DatabaseName);
            Assert.That(actual.QValue, Is.EqualTo(expected.QValue), expected.DatabaseName);
            Assert.That(actual.IsCalled, Is.EqualTo(expected.IsCalled), expected.DatabaseName);
        }
    }

    [Test]
    public void WithTwins_TheCalibrationRunScoresTwins_AndNeverOverwritesTheMetrics()
    {
        var rows = TargetsWithTwins();
        var evaluator = NewEvaluator();
        var results = evaluator.Evaluate(DatabaseEvaluationInput.FromMetrics(rows));

        var twins = results.Where(r => r.IsTwin).ToList();
        Assert.That(twins, Has.Count.EqualTo(21));
        Assert.That(twins.All(t => !t.IsCalled && double.IsNaN(t.QValue)));
        Assert.That(twins.All(t => t.PValue == t.Details![StatisticalTestEvaluator.CalibrationRunPValueColumn]));

        var present = results.Single(r => r.DatabaseName == Present);
        Assert.That(present.Details![StatisticalTestEvaluator.TwinCalibratedQValueColumn], Is.LessThan(1.0));
        Assert.That(present.Details.ContainsKey(StatisticalTestEvaluator.CalibrationRunPValueColumn));

        // the calibration run ran last, but the metrics still hold the official values
        var presentRow = rows.Single(r => r.DatabaseName == Present);
        Assert.That(presentRow.CombinedPValue, Is.EqualTo(present.PValue));
        Assert.That(rows.Where(r => r.IsTwin).All(r => r.StatisticalTestsRun == 0));

        Assert.That(evaluator.TwinProfiles, Is.Not.Null);
        var combined = evaluator.TwinProfiles!.First();
        Assert.That(combined.Family, Is.Null, "the combined row comes first");
        Assert.That(combined.TwinCount, Is.EqualTo(21));
        Assert.That(combined.TwinHistogram.Sum(), Is.EqualTo(21));
        Assert.That(evaluator.TwinProfiles!.Count(p => p.Family.HasValue), Is.EqualTo(3));
    }

    [Test]
    public void WriteFiles_WritesTheBranchOutputs_PlusATwinSectionWhenThereAreTwins()
    {
        string folder = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", nameof(StatisticalTestEvaluatorTests));
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);

        try
        {
            var evaluator = NewEvaluator();
            Assert.That(evaluator.WriteFiles(folder), Is.Empty, "nothing to write before Evaluate");

            evaluator.Evaluate(DatabaseEvaluationInput.FromMetrics(TargetsWithTwins()));
            var written = evaluator.WriteFiles(folder);

            Assert.That(written.Select(Path.GetFileName), Is.EquivalentTo(new[]
            {
                StatisticalTestEvaluator.StatResultFileName,
                StatisticalTestEvaluator.TestSummaryFileName,
                StatisticalTestEvaluator.CalibrationReportFileName,
            }));
            Assert.That(written.All(File.Exists));

            string report = File.ReadAllText(Path.Combine(folder, StatisticalTestEvaluator.CalibrationReportFileName));
            Assert.That(report, Does.Contain("CALIBRATION REPORT"));
            Assert.That(report, Does.Contain("TWIN CALIBRATION"));

            // the per-test table holds the official run only, so no twin rows
            string table = File.ReadAllText(Path.Combine(folder, StatisticalTestEvaluator.StatResultFileName));
            Assert.That(table, Does.Contain(Present));
            Assert.That(table, Does.Not.Contain("_twin"));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Test]
    public void RunAndWrite_AlsoWritesTheEvaluatorsOwnFiles()
    {
        string folder = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", nameof(RunAndWrite_AlsoWritesTheEvaluatorsOwnFiles));
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);

        try
        {
            var rows = Targets();
            var written = DatabaseEvaluationRunner.RunAndWrite([NewEvaluator()], rows, rows.Select(r => r.DatabaseName), folder);

            Assert.That(written.Select(Path.GetFileName), Is.EquivalentTo(new[]
            {
                DatabaseEvaluationFiles.GetEvaluatorFileName(StatisticalTestEvaluator.EvaluatorName),
                DatabaseEvaluationFiles.SummaryFileName,
                StatisticalTestEvaluator.StatResultFileName,
                StatisticalTestEvaluator.TestSummaryFileName,
                StatisticalTestEvaluator.CalibrationReportFileName,
            }));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Test]
    public void DefaultSuite_AddsProteinGroupAndDeconvolutionFamiliesOnlyWhenAsked()
    {
        var families = StatisticalTestEvaluator.CreateDefaultTests(false, false).Select(t => t.EvidenceFamily).ToHashSet();
        Assert.That(families, Is.EquivalentTo(new[]
        {
            StatisticalEvidenceFamily.CountEnrichment, StatisticalEvidenceFamily.AmbiguityOrTargetDecoy,
            StatisticalEvidenceFamily.ScoreDistribution, StatisticalEvidenceFamily.RetentionTime,
            StatisticalEvidenceFamily.Fragmentation,
        }));

        var all = StatisticalTestEvaluator.CreateDefaultTests(true, true).Select(t => t.EvidenceFamily).ToHashSet();
        Assert.That(all, Does.Contain(StatisticalEvidenceFamily.ProteinGroup));
        Assert.That(all, Does.Contain(StatisticalEvidenceFamily.PrecursorDeconvolution));
        Assert.That(all, Does.Not.Contain(StatisticalEvidenceFamily.DeNovo));
    }

    [Test]
    public void Factory_RegistersTheImplementedEvaluators()
    {
        var evaluators = DatabaseEvaluatorFactory.Create(new ParallelSearchParameters(), new CommonParameters());

        Assert.That(evaluators.Select(e => e.Name),
            Is.EqualTo(new[] { StatisticalTestEvaluator.EvaluatorName, TwinCompetitionEvaluator.EvaluatorName }));
        Assert.That(evaluators.Select(e => e.RequiresTwins), Is.EqualTo(new[] { false, true }));
    }
}
