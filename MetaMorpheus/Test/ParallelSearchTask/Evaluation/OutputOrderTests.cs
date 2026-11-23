using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TaskLayer.ParallelSearch;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Evaluation;
using TaskLayer.ParallelSearch.IO;
using TaskLayer.ParallelSearch.Statistics;

namespace Test.ParallelSearchTask.Evaluation;

/// <summary>Every per-database output is written most confident first, twins after targets, name as the last tie-break.</summary>
[TestFixture]
public class OutputOrderTests
{
    private string _folder = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(TestContext.CurrentContext.TestDirectory, $"output_order_{Path.GetRandomFileName()}");
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, true);
    }

    private static DatabaseEvaluationResult Result(string name, double score, double q, bool called, string twinOf = "") =>
        new(name, twinOf, score, double.NaN, q, called, null);

    private static DatabaseEvaluationReport Report(params (string Evaluator, DatabaseEvaluationResult[] Results)[] evaluators) =>
        new(evaluators.ToDictionary(e => e.Evaluator, e => (IReadOnlyList<DatabaseEvaluationResult>)e.Results),
            new Dictionary<string, string>());

    private static string[] FirstColumn(string path) =>
        File.ReadAllLines(path).Skip(1).Select(l => l.Split(',')[0]).ToArray();

    [Test]
    public void EvaluatorFile_CalledFirst_ThenQ_ThenScore_NoQLast_TwinsLast()
    {
        string path = Path.Combine(_folder, "eval.csv");
        DatabaseEvaluationFiles.WriteEvaluatorFile(new[]
        {
            Result("A_uncalled", 9, 0.5, false),
            Result("Z_called_lowq", 1, 0.001, true),
            Result("Y_called_highq", 5, 0.01, true),
            Result("C_noq_highscore", 8, double.NaN, false),
            Result("B_noq_lowscore", 2, double.NaN, false),
            Result("A_twin", 100, double.NaN, false, twinOf: "A_uncalled"),
            Result("D_called_lowq_lowscore", 0.5, 0.001, true),
        }, path);

        Assert.That(FirstColumn(path), Is.EqualTo(new[]
        {
            "Z_called_lowq", "D_called_lowq_lowscore", "Y_called_highq", "A_uncalled",
            "C_noq_highscore", "B_noq_lowscore", "A_twin",
        }));
    }

    [Test]
    public void SummaryFile_ByCalledByCount_ThenBestQ_ThenName_TwinsLast()
    {
        var report = Report(
            ("E1", new[] { Result("A", 1, 0.2, false), Result("B", 1, 0.01, true), Result("C", 1, 0.001, true), Result("A_twin", 1, double.NaN, false, "A") }),
            ("E2", new[] { Result("A", 1, 0.3, false), Result("B", 1, 0.04, true), Result("C", 1, 0.5, false), Result("A_twin", 1, double.NaN, false, "A") }));

        string path = Path.Combine(_folder, "summary.csv");
        DatabaseEvaluationFiles.WriteSummaryFile(report, path);

        // B is called by two evaluators; C by one, with the best q of all; A by none
        Assert.That(FirstColumn(path), Is.EqualTo(new[] { "B", "C", "A", "A_twin" }));
    }

    [Test]
    public void ManySearchSummary_FollowsTheEvaluation_ThenTargetPsms_AndWithoutOneTargetPsmsWithTwinsLast()
    {
        string path = Path.Combine(_folder, TransientDatabaseResultsManager.SummaryResultsFileName);
        // writing recomputes the counts from the (here empty) collector results, so each write gets fresh rows
        ParallelSearchResultCache NewCache()
        {
            if (File.Exists(path))
                File.Delete(path);
            var c = new ParallelSearchResultCache(path);
            c.Add(new TransientDatabaseMetrics("Many") { TargetPsmsFromTransientDbAtQValueThreshold = 100 });
            c.Add(new TransientDatabaseMetrics("Few") { TargetPsmsFromTransientDbAtQValueThreshold = 3 });
            c.Add(new TransientDatabaseMetrics("Tied1") { TargetPsmsFromTransientDbAtQValueThreshold = 7 });
            c.Add(new TransientDatabaseMetrics("Tied2") { TargetPsmsFromTransientDbAtQValueThreshold = 9 });
            c.Add(new TransientDatabaseMetrics("Many_twin") { TwinOf = "Many", TargetPsmsFromTransientDbAtQValueThreshold = 500 });
            return c;
        }

        var cache = NewCache();
        cache.WriteAllToFile();
        Assert.That(FirstColumn(path), Is.EqualTo(new[] { "Many", "Tied2", "Tied1", "Few", "Many_twin" }));

        var report = Report(("E", new[]
        {
            Result("Many", 1, 0.5, false), Result("Few", 1, 0.001, true),
            Result("Tied1", 1, 0.2, false), Result("Tied2", 1, 0.2, false),
            Result("Many_twin", 1, double.NaN, false, "Many"),
        }));
        NewCache().WriteAllToFile(report: report);
        Assert.That(FirstColumn(path), Is.EqualTo(new[] { "Few", "Tied2", "Tied1", "Many", "Many_twin" }));
    }

    [Test]
    public void StatisticalTestResults_RowsFollowCombinedAllQValue_NoneLast_ThenName()
    {
        StatisticalTestResult Combined(string db, double q) => new()
        {
            DatabaseName = db, TestName = CombinedResultNames.TestName, MetricName = CombinedResultNames.AllMetricName,
            PValue = q, QValue = q,
        };
        StatisticalTestResult Test(string db, double q) => new()
        {
            DatabaseName = db, TestName = "Gaussian", MetricName = "PSM", EvidenceFamily = StatisticalEvidenceFamily.CountEnrichment,
            PValue = q, QValue = q,
        };

        var file = new StatisticalTestResultFile(0.05)
        {
            Results = new List<StatisticalTestResult>
            {
                Test("A_manyTests", 0.001), Combined("A_manyTests", 0.3),
                Test("B", 0.5), Combined("B", 1e-6),
                Test("C_nocombined", 0.001),
                Test("D", 0.5), Combined("D", 1e-6),
            },
        };
        string path = Path.Combine(_folder, "stats.csv");
        file.WriteResults(path, null);

        Assert.That(FirstColumn(path), Is.EqualTo(new[] { "B", "D", "A_manyTests", "C_nocombined" }));
    }
}
