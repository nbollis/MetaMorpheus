using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TaskLayer.ParallelSearch;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Evaluation;

namespace Test.ParallelSearchTask.Evaluation;

[TestFixture]
public class DatabaseEvaluationFrameworkTests
{
    private static TransientDatabaseMetrics Row(string name, string twinOf = "", int peptides = 0) =>
        new(name) { TwinOf = twinOf, PeptideBacterialUnambiguousTargets = peptides };

    /// <summary>Scores every database by its unambiguous peptide count; calls targets with q at most 0.5.</summary>
    private sealed class PeptideCountEvaluator(bool requiresTwins = false) : IDatabaseEvaluator
    {
        public string Name => requiresTwins ? "NeedsTwins" : "PeptideCount";
        public bool RequiresTwins => requiresTwins;

        public IReadOnlyList<DatabaseEvaluationResult> Evaluate(DatabaseEvaluationInput input)
        {
            double[] q = TwinCalibration.EmpiricalQValues(
                input.Targets.Select(t => (double)t.PeptideBacterialUnambiguousTargets).ToList(),
                input.Twins.Select(t => (double)t.PeptideBacterialUnambiguousTargets).ToList());

            var results = input.Targets.Select((t, i) => new DatabaseEvaluationResult(t.DatabaseName, t.TwinOf,
                t.PeptideBacterialUnambiguousTargets, double.NaN, q[i], q[i] <= 0.5,
                new Dictionary<string, double> { ["Peptides"] = t.PeptideBacterialUnambiguousTargets })).ToList();
            results.AddRange(input.Twins.Select(t =>
                DatabaseEvaluationResult.ScoreOnly(t.DatabaseName, t.TwinOf, t.PeptideBacterialUnambiguousTargets)));
            return results;
        }
    }

    private sealed class ThrowingEvaluator : IDatabaseEvaluator
    {
        public string Name => "Throws";
        public bool RequiresTwins => false;
        public IReadOnlyList<DatabaseEvaluationResult> Evaluate(DatabaseEvaluationInput input) =>
            throw new InvalidOperationException("boom");
    }

    [Test]
    public void Input_SplitsTargetsAndTwins_AndPairsThemByTwinOf()
    {
        var input = DatabaseEvaluationInput.FromMetrics(new[]
        {
            Row("B"), Row("A"), Row("A_twin", "A"), Row("Orphan_twin", "Orphan"),
        });

        Assert.That(input.Targets.Select(t => t.DatabaseName), Is.EqualTo(new[] { "A", "B" }));
        Assert.That(input.Twins.Select(t => t.DatabaseName), Is.EqualTo(new[] { "A_twin" }));
        Assert.That(input.UnpairedTwins.Select(t => t.DatabaseName), Is.EqualTo(new[] { "Orphan_twin" }));
        Assert.That(input.Pairs.Select(p => (p.Target.DatabaseName, p.Twin.DatabaseName)), Is.EqualTo(new[] { ("A", "A_twin") }));
        Assert.That(input.TryGetTwin("B", out _), Is.False);
        Assert.That(input.HasTwins, Is.True);
    }

    [Test]
    public void Input_KeepsOnlyTheNamedDatabases()
    {
        var input = DatabaseEvaluationInput.FromMetrics(new[] { Row("A"), Row("Stale") }, new[] { "A" });

        Assert.That(input.Targets.Select(t => t.DatabaseName), Is.EqualTo(new[] { "A" }));
    }

    [Test]
    public void EmpiricalQValues_UseTwinsAsTheNull_AndAreMonotone()
    {
        // targets 10, 8, 3, 3, 1 ; twins 5, 2
        double[] q = TwinCalibration.EmpiricalQValues(new double[] { 3, 10, 1, 8, 3 }, new double[] { 5, 2 });

        // >=10: (0+1)/1 = 1 ; >=8: (0+1)/2 = 0.5 ; >=3: (1+1)/4 = 0.5 ; >=1: (2+1)/5 = 0.6
        // monotone from the bottom: 0.6, 0.5, 0.5, then 10 takes min(1, 0.5) = 0.5
        Assert.That(q, Is.EqualTo(new[] { 0.5, 0.5, 0.6, 0.5, 0.5 }).Within(1e-12));
    }

    [Test]
    public void EmpiricalQValues_GiveNaNForNaNScores_AndOneWithoutSeparation()
    {
        double[] q = TwinCalibration.EmpiricalQValues(new[] { double.NaN, 1.0 }, new[] { 1.0, 1.0 });

        Assert.That(double.IsNaN(q[0]), Is.True);
        Assert.That(q[1], Is.EqualTo(1.0));
    }

    [Test]
    public void Runner_SkipsTwinOnlyEvaluators_WithoutTwins_AndSurvivesAFailingEvaluator()
    {
        var warnings = new List<string>();
        var runner = new DatabaseEvaluationRunner(
            new IDatabaseEvaluator[] { new PeptideCountEvaluator(), new PeptideCountEvaluator(requiresTwins: true), new ThrowingEvaluator() },
            warnings.Add);

        var report = runner.Run(DatabaseEvaluationInput.FromMetrics(new[] { Row("A", peptides: 3) }));

        Assert.That(report.ResultsByEvaluator.Keys, Is.EquivalentTo(new[] { "PeptideCount" }));
        Assert.That(report.SkippedEvaluators.Keys, Is.EquivalentTo(new[] { "NeedsTwins", "Throws" }));
        Assert.That(warnings, Has.Some.Contains("Throws"));
    }

    [Test]
    public void Runner_RejectsDuplicateEvaluatorNames()
    {
        Assert.Throws<ArgumentException>(() =>
            new DatabaseEvaluationRunner(new IDatabaseEvaluator[] { new PeptideCountEvaluator(), new PeptideCountEvaluator() }));
    }

    [Test]
    public void Files_WriteOnePerEvaluatorAndASummary_TargetsBeforeTwins()
    {
        string folder = Path.Combine(TestContext.CurrentContext.TestDirectory, $"db_evaluation_{Path.GetRandomFileName()}");
        try
        {
            var input = DatabaseEvaluationInput.FromMetrics(new[]
            {
                Row("A", peptides: 10), Row("A_twin", "A", peptides: 1),
                Row("B", peptides: 0), Row("B_twin", "B", peptides: 2),
            });
            var report = new DatabaseEvaluationRunner(new IDatabaseEvaluator[]
            {
                new PeptideCountEvaluator(), new PeptideCountEvaluator(requiresTwins: true),
            }).Run(input);

            var written = DatabaseEvaluationFiles.WriteAll(report, folder);

            Assert.That(written.Select(Path.GetFileName), Is.EquivalentTo(new[]
            {
                "DatabaseEvaluation_PeptideCount.csv", "DatabaseEvaluation_NeedsTwins.csv", DatabaseEvaluationFiles.SummaryFileName,
            }));

            string[] evaluatorLines = File.ReadAllLines(Path.Combine(folder, "DatabaseEvaluation_PeptideCount.csv"));
            Assert.That(evaluatorLines[0], Is.EqualTo("DatabaseName,TwinOf,IsTwin,Score,PValue,QValue,IsCalled,Peptides"));
            Assert.That(evaluatorLines.Skip(1).Select(l => l.Split(',')[0]), Is.EqualTo(new[] { "A", "B", "A_twin", "B_twin" }));
            // two targets and two twins cannot get below q = 1 with the +1 correction
            Assert.That(evaluatorLines[1], Is.EqualTo("A,,FALSE,10,NaN,1,FALSE,10"));
            Assert.That(evaluatorLines[3], Is.EqualTo("A_twin,A,TRUE,1,NaN,NaN,FALSE,"));

            string[] summaryLines = File.ReadAllLines(Path.Combine(folder, DatabaseEvaluationFiles.SummaryFileName));
            Assert.That(summaryLines[0], Is.EqualTo("DatabaseName,TwinOf,IsTwin,PeptideCount_Score,PeptideCount_QValue,PeptideCount_IsCalled,"
                + "NeedsTwins_Score,NeedsTwins_QValue,NeedsTwins_IsCalled,CalledByCount"));
            Assert.That(summaryLines[1], Is.EqualTo("A,,FALSE,10,1,FALSE,10,1,FALSE,0"));
            Assert.That(summaryLines[3], Does.StartWith("A_twin,A,TRUE,"));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Test]
    public void Files_WriteNothingWhenNoEvaluatorRan()
    {
        var report = new DatabaseEvaluationRunner(Array.Empty<IDatabaseEvaluator>()).Run(DatabaseEvaluationInput.FromMetrics(new[] { Row("A") }));

        Assert.That(DatabaseEvaluationFiles.WriteAll(report, Path.Combine(TestContext.CurrentContext.TestDirectory, "unused")), Is.Empty);
    }

    [Test]
    public void TwinOf_RoundTripsThroughTheSummaryFile_AndOldFilesStillRead()
    {
        string folder = Path.Combine(TestContext.CurrentContext.TestDirectory, $"twin_summary_{Path.GetRandomFileName()}");
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, TransientDatabaseResultsManager.SummaryResultsFileName);
            var cache = new ParallelSearchResultCache(path);
            cache.Add(Row("A"));
            cache.Add(Row("A_twin", "A"));
            cache.WriteAllToFile();

            var reread = new ParallelSearchResultCache(path);
            reread.InitializeCache();
            Assert.That(reread.UnreadableFileMessage, Is.Null);
            Assert.That(reread.TryGetValue("A_twin", out var twin) && twin!.TwinOf == "A", Is.True);
            Assert.That(reread.TryGetValue("A", out var target) && !target!.IsTwin, Is.True);

            // a summary written before TwinOf existed: drop the column and read it back
            string[] lines = File.ReadAllLines(path);
            int column = Array.IndexOf(lines[0].Split(','), "TwinOf");
            Assert.That(column, Is.GreaterThanOrEqualTo(0));
            File.WriteAllLines(path, lines.Select(l => string.Join(",", l.Split(',').Where((_, i) => i != column))));

            var old = new ParallelSearchResultCache(path);
            old.InitializeCache();
            Assert.That(old.UnreadableFileMessage, Is.Null);
            Assert.That(old.Count, Is.EqualTo(2));
            Assert.That(old.AllResultsList.All(r => r.TwinOf == string.Empty), Is.True);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
