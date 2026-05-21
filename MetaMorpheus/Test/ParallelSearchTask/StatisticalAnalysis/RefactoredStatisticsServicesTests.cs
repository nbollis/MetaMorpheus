using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Statistics;

namespace Test.ParallelSearchTask.StatisticalAnalysis;

[TestFixture]
public class RefactoredStatisticsServicesTests
{
    [Test]
    public void CombinedResultNames_AndSelectionHelpers_NormalizeRuntimeAndLoadedCombinedRows()
    {
        var runtimeCombined = new StatisticalTestResult
        {
            DatabaseName = "Db1",
            TestName = CombinedResultNames.TestName,
            MetricName = CombinedResultNames.AllMetricName,
        };

        var loadedCombined = new StatisticalTestResult
        {
            DatabaseName = "Db1",
            TestName = CombinedResultNames.GetCacheKey(CombinedResultNames.AllMetricName),
            MetricName = CombinedResultNames.AllMetricName,
        };

        var regular = new StatisticalTestResult
        {
            DatabaseName = "Db1",
            TestName = "Gaussian",
            MetricName = "PSM",
        };

        Assert.Multiple(() =>
        {
            Assert.That(runtimeCombined.IsCombinedResult, Is.True);
            Assert.That(loadedCombined.IsCombinedResult, Is.True);
            Assert.That(regular.IsCombinedResult, Is.False);

            Assert.That(runtimeCombined.SelectionKey, Is.EqualTo("Combined_All"));
            Assert.That(loadedCombined.SelectionKey, Is.EqualTo("Combined_All"));
            Assert.That(regular.SelectionKey, Is.EqualTo("Gaussian"));

            Assert.That(runtimeCombined.MatchesSelection("Combined_All"), Is.True);
            Assert.That(loadedCombined.MatchesSelection("Combined_All"), Is.True);
            Assert.That(regular.MatchesSelection("Gaussian"), Is.True);
            Assert.That(regular.MatchesSelection("Combined_All"), Is.False);
        });
    }

    [Test]
    public void StatisticalSummaryBuilder_BuildsPerTestAndFamilySummaries_AndBackfillsDatabaseMetrics()
    {
        var db1 = new TransientDatabaseMetrics("Db1");
        var db2 = new TransientDatabaseMetrics("Db2");
        var builder = new StatisticalSummaryBuilder(alpha: 0.05);

        var results = new List<StatisticalTestResult>
        {
            new()
            {
                DatabaseName = "Db1",
                TestName = "Gaussian",
                MetricName = "CountA",
                EvidenceFamily = StatisticalEvidenceFamily.CountEnrichment,
                IsDefined = true,
                PValue = 0.01,
                QValue = 0.02,
            },
            new()
            {
                DatabaseName = "Db1",
                TestName = "FisherExact",
                MetricName = "AmbiguityA",
                EvidenceFamily = StatisticalEvidenceFamily.AmbiguityOrTargetDecoy,
                IsDefined = false,
                EligibilityReason = "NoEvidence",
                PValue = double.NaN,
                QValue = double.NaN,
            },
            new()
            {
                DatabaseName = "Db2",
                TestName = "Gaussian",
                MetricName = "CountA",
                EvidenceFamily = StatisticalEvidenceFamily.CountEnrichment,
                IsDefined = true,
                PValue = 0.5,
                QValue = 0.6,
            },
            new()
            {
                DatabaseName = "Db2",
                TestName = "FisherExact",
                MetricName = "AmbiguityA",
                EvidenceFamily = StatisticalEvidenceFamily.AmbiguityOrTargetDecoy,
                IsDefined = true,
                PValue = 0.04,
                QValue = 0.04,
            },
        };

        var perTest = builder.BuildPerTestSummaries(results);
        var byFamily = builder.BuildFamilySummaries(results);
        builder.UpdatePerDatabaseMetrics(new Dictionary<string, TransientDatabaseMetrics>
        {
            [db1.DatabaseName] = db1,
            [db2.DatabaseName] = db2,
        }, results);

        Assert.Multiple(() =>
        {
            var gaussianSummary = perTest["Gaussian_CountA"];
            Assert.That(gaussianSummary.ValidDatabases, Is.EqualTo(2));
            Assert.That(gaussianSummary.UndefinedDatabases, Is.EqualTo(0));
            Assert.That(gaussianSummary.SignificantByP, Is.EqualTo(1));
            Assert.That(gaussianSummary.SignificantByQ, Is.EqualTo(1));

            var ambiguitySummary = perTest["FisherExact_AmbiguityA"];
            Assert.That(ambiguitySummary.ValidDatabases, Is.EqualTo(1));
            Assert.That(ambiguitySummary.UndefinedDatabases, Is.EqualTo(1));
            Assert.That(ambiguitySummary.SignificantByP, Is.EqualTo(1));
            Assert.That(ambiguitySummary.SignificantByQ, Is.EqualTo(1));

            var countFamily = byFamily["FamilySummary_CountEnrichment"];
            Assert.That(countFamily.IsFamilySummary, Is.True);
            Assert.That(countFamily.ValidDatabases, Is.EqualTo(2));
            Assert.That(countFamily.UndefinedDatabases, Is.EqualTo(0));
            Assert.That(countFamily.SignificantByQ, Is.EqualTo(1));

            var ambiguityFamily = byFamily["FamilySummary_AmbiguityOrTargetDecoy"];
            Assert.That(ambiguityFamily.ValidDatabases, Is.EqualTo(1));
            Assert.That(ambiguityFamily.UndefinedDatabases, Is.EqualTo(1));
            Assert.That(ambiguityFamily.SignificantByQ, Is.EqualTo(1));

            Assert.That(db1.ValidTestCount, Is.EqualTo(1));
            Assert.That(db1.PassedTestCount, Is.EqualTo(1));
            Assert.That(db1.ValidFamilyCount, Is.EqualTo(1));
            Assert.That(db1.PassedFamilyCount, Is.EqualTo(1));
            Assert.That(db1.CountEnrichmentBestPValue, Is.EqualTo(0.01).Within(1e-12));
            Assert.That(double.IsNaN(db1.AmbiguityOrTargetDecoyBestPValue), Is.True);

            Assert.That(db2.ValidTestCount, Is.EqualTo(2));
            Assert.That(db2.PassedTestCount, Is.EqualTo(1));
            Assert.That(db2.ValidFamilyCount, Is.EqualTo(2));
            Assert.That(db2.PassedFamilyCount, Is.EqualTo(1));
            Assert.That(db2.CountEnrichmentBestQValue, Is.EqualTo(0.6).Within(1e-12));
            Assert.That(db2.AmbiguityOrTargetDecoyBestQValue, Is.EqualTo(0.04).Within(1e-12));
        });
    }

    [Test]
    public void HierarchicalCombinedScoringService_BuildsFamilyAndOverallCombinedResults_AndUpdatesMetrics()
    {
        var service = new HierarchicalCombinedScoringService();
        var db1 = new TransientDatabaseMetrics("Db1");
        var db2 = new TransientDatabaseMetrics("Db2");

        var rawResults = new List<StatisticalTestResult>
        {
            new()
            {
                DatabaseName = "Db1",
                TestName = "Gaussian",
                MetricName = "CountA",
                EvidenceFamily = StatisticalEvidenceFamily.CountEnrichment,
                IsDefined = true,
                PValue = 0.01,
            },
            new()
            {
                DatabaseName = "Db1",
                TestName = "KS",
                MetricName = "FragA",
                EvidenceFamily = StatisticalEvidenceFamily.Fragmentation,
                IsDefined = true,
                PValue = 0.02,
            },
            new()
            {
                DatabaseName = "Db2",
                TestName = "Gaussian",
                MetricName = "CountA",
                EvidenceFamily = StatisticalEvidenceFamily.CountEnrichment,
                IsDefined = false,
                EligibilityReason = "BelowEligibilityThreshold",
                PValue = double.NaN,
            },
            new()
            {
                DatabaseName = "Db2",
                TestName = "KS",
                MetricName = "FragA",
                EvidenceFamily = StatisticalEvidenceFamily.Fragmentation,
                IsDefined = true,
                PValue = 0.5,
            },
        };

        var combined = service.BuildCombinedResults(rawResults);
        service.UpdateMetricsSummary(new Dictionary<string, TransientDatabaseMetrics>
        {
            [db1.DatabaseName] = db1,
            [db2.DatabaseName] = db2,
        }, combined);

        var countFamilyResults = combined.ResultsByCacheKey["Combined_CountEnrichment"];
        var overallResults = combined.ResultsByCacheKey["Combined_All"];
        var db1OverallExpected = MetaAnalysis.CombinePValuesAcrossTests(
        [
            countFamilyResults.Single(p => p.DatabaseName == "Db1"),
            combined.ResultsByCacheKey["Combined_Fragmentation"].Single(p => p.DatabaseName == "Db1")
        ])["Db1"];

        Assert.Multiple(() =>
        {
            Assert.That(combined.ResultsByCacheKey.Keys, Does.Contain("Combined_CountEnrichment"));
            Assert.That(combined.ResultsByCacheKey.Keys, Does.Contain("Combined_Fragmentation"));
            Assert.That(combined.ResultsByCacheKey.Keys, Does.Contain("Combined_All"));

            var db2CountFamily = countFamilyResults.Single(p => p.DatabaseName == "Db2");
            Assert.That(db2CountFamily.IsDefined, Is.False);
            Assert.That(db2CountFamily.EligibilityReason, Is.EqualTo("NoDefinedTestsInFamily"));

            var db1Overall = overallResults.Single(p => p.DatabaseName == "Db1");
            Assert.That(db1Overall.IsDefined, Is.True);
            Assert.That(db1Overall.PValue, Is.EqualTo(db1OverallExpected).Within(1e-12));

            Assert.That(db1.CountEnrichmentCombinedPValue, Is.EqualTo(0.01).Within(1e-12));
            Assert.That(db1.FragmentationCombinedPValue, Is.EqualTo(0.02).Within(1e-12));
            Assert.That(db1.CombinedPValue, Is.EqualTo(db1Overall.PValue).Within(1e-12));

            Assert.That(double.IsNaN(db2.CountEnrichmentCombinedPValue), Is.True);
            Assert.That(db2.FragmentationCombinedPValue, Is.EqualTo(0.5).Within(1e-12));
            Assert.That(db2.CombinedPValue, Is.EqualTo(0.5).Within(1e-12));
        });
    }

    [Test]
    public void StatisticalTestExecutor_HandlesSuccessfulSkippedAndFailingTests()
    {
        var warnings = new List<string>();
        var executor = new StatisticalTestExecutor(0.05, warnings.Add);
        var searchResults = Enumerable.Range(1, 20)
            .Select(i => new TransientDatabaseMetrics($"Db{i}") { TargetPsmsFromTransientDb = i })
            .ToList();

        var successfulTest = new FakeStatisticalTest(
            "GoodMetric",
            StatisticalEvidenceFamily.CountEnrichment,
            canRun: true,
            throwOnCompute: false,
            pValueFactory: metrics => metrics.DatabaseName == "Db1" ? 0.001 : 0.5,
            isDefinedFor: metrics => metrics.DatabaseName != "Db20",
            testValueFactory: metrics => metrics.TargetPsmsFromTransientDb,
            effectSizeFactory: (metrics, _) => metrics.TargetPsmsFromTransientDb / 10.0);

        var skippedTest = new FakeStatisticalTest(
            "SkippedMetric",
            StatisticalEvidenceFamily.Fragmentation,
            canRun: false,
            throwOnCompute: false,
            pValueFactory: _ => 0.5);

        var failingTest = new FakeStatisticalTest(
            "FailingMetric",
            StatisticalEvidenceFamily.RetentionTime,
            canRun: true,
            throwOnCompute: true,
            pValueFactory: _ => 0.5);

        var execution = executor.Execute([successfulTest, skippedTest, failingTest], searchResults);

        Assert.Multiple(() =>
        {
            Assert.That(execution.Results.Count, Is.EqualTo(20));
            Assert.That(execution.TestsToRemove, Has.Count.EqualTo(2));
            Assert.That(execution.TestsToRemove, Does.Contain(skippedTest));
            Assert.That(execution.TestsToRemove, Does.Contain(failingTest));

            var db1 = execution.Results.Single(p => p.DatabaseName == "Db1");
            Assert.That(db1.TestName, Is.EqualTo("Fake"));
            Assert.That(db1.MetricName, Is.EqualTo("GoodMetric"));
            Assert.That(db1.EvidenceFamily, Is.EqualTo(StatisticalEvidenceFamily.CountEnrichment));
            Assert.That(db1.IsDefined, Is.True);
            Assert.That(db1.PValue, Is.EqualTo(0.001).Within(1e-12));
            Assert.That(db1.TestStatistic, Is.EqualTo(1).Within(1e-12));
            Assert.That(db1.EffectSize, Is.EqualTo(0.1).Within(1e-12));

            var db20 = execution.Results.Single(p => p.DatabaseName == "Db20");
            Assert.That(db20.IsDefined, Is.False);
            Assert.That(db20.EligibilityReason, Is.EqualTo("BelowEligibilityThreshold"));

            Assert.That(warnings.Any(p => p.Contains("Skipping Fake - SkippedMetric: insufficient data")), Is.True);
            Assert.That(warnings.Any(p => p.Contains("Error running Fake - FailingMetric")), Is.True);
        });
    }

    [Test]
    public void StatisticalTestResultBuilder_ProducesCompleteResult()
    {
        var result = new StatisticalTestResultBuilder()
            .WithDatabaseName("Db1")
            .WithTestName("Gaussian")
            .WithMetricName("PSM")
            .WithEvidenceFamily(StatisticalEvidenceFamily.CountEnrichment)
            .WithIsDefined(true)
            .WithEligibilityReason(null)
            .WithPValue(0.01)
            .WithQValue(0.05)
            .WithTestStatistic(2.5)
            .WithEffectSize(1.3)
            .Build();

        Assert.Multiple(() =>
        {
            Assert.That(result.DatabaseName, Is.EqualTo("Db1"));
            Assert.That(result.TestName, Is.EqualTo("Gaussian"));
            Assert.That(result.MetricName, Is.EqualTo("PSM"));
            Assert.That(result.EvidenceFamily, Is.EqualTo(StatisticalEvidenceFamily.CountEnrichment));
            Assert.That(result.IsDefined, Is.True);
            Assert.That(result.EligibilityReason, Is.Null);
            Assert.That(result.PValue, Is.EqualTo(0.01).Within(1e-12));
            Assert.That(result.QValue, Is.EqualTo(0.05).Within(1e-12));
            Assert.That(result.TestStatistic, Is.EqualTo(2.5).Within(1e-12));
            Assert.That(result.EffectSize, Is.EqualTo(1.3).Within(1e-12));
            Assert.That(result.IsSignificant(), Is.True);
            Assert.That(result.GetState(), Is.EqualTo(StatisticalResultState.PositiveEvidence));
        });
    }

    [Test]
    public void StatisticalTestResultBuilder_DefaultsUndefinedResult()
    {
        var result = new StatisticalTestResultBuilder()
            .WithDatabaseName("Db1")
            .WithTestName("Gaussian")
            .WithMetricName("PSM")
            .WithIsDefined(false)
            .WithEligibilityReason("NoScoresAvailable")
            .Build();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsDefined, Is.False);
            Assert.That(result.IsSignificant(), Is.False);
            Assert.That(result.GetState(), Is.EqualTo(StatisticalResultState.Undefined));
            Assert.That(result.EligibilityReason, Is.EqualTo("NoScoresAvailable"));
            Assert.That(result.PValue, Is.EqualTo(double.NaN));
        });
    }

    [Test]
    public void TestSuiteBuilder_ComposesTestSuite()
    {
        var suite = new TestSuiteBuilder()
            .AddCountEnrichmentTests()
            .AddAmbiguityOrTargetDecoyTests()
            .AddScoreDistributionTests()
            .Build();

        Assert.Multiple(() =>
        {
            Assert.That(suite, Is.Not.Empty);
            Assert.That(suite.Count(t => t.EvidenceFamily == StatisticalEvidenceFamily.CountEnrichment), Is.EqualTo(12));
            Assert.That(suite.Count(t => t.EvidenceFamily == StatisticalEvidenceFamily.AmbiguityOrTargetDecoy), Is.EqualTo(4));
            Assert.That(suite.Count(t => t.EvidenceFamily == StatisticalEvidenceFamily.ScoreDistribution), Is.EqualTo(2));
            Assert.That(suite.Count, Is.EqualTo(18));
        });
    }

    [Test]
    public void TestSuiteBuilder_WithProteinGroupAndDeNovo_AddsExpectedFamilies()
    {
        var suite = new TestSuiteBuilder()
            .AddCountEnrichmentTests()
            .AddAmbiguityOrTargetDecoyTests()
            .AddProteinGroupTests()
            .AddDeNovoTests()
            .Build();

        Assert.Multiple(() =>
        {
            Assert.That(suite.Any(t => t.EvidenceFamily == StatisticalEvidenceFamily.ProteinGroup), Is.True);
            Assert.That(suite.Any(t => t.EvidenceFamily == StatisticalEvidenceFamily.DeNovo), Is.True);
        });
    }

    [Test]
    public void TestSuiteBuilder_AddFamily_DispatchesToCorrectFamilyMethod()
    {
        var suite = new TestSuiteBuilder()
            .AddFamily(StatisticalEvidenceFamily.CountEnrichment)
            .AddFamily(StatisticalEvidenceFamily.ScoreDistribution)
            .Build();

        Assert.Multiple(() =>
        {
            Assert.That(suite.Count(t => t.EvidenceFamily == StatisticalEvidenceFamily.CountEnrichment), Is.EqualTo(12));
            Assert.That(suite.Count(t => t.EvidenceFamily == StatisticalEvidenceFamily.ScoreDistribution), Is.EqualTo(2));
            Assert.That(suite.Count, Is.EqualTo(14));
        });
    }

    [Test]
    public void TestSuiteBuilder_AddTests_InjectsCustomTests()
    {
        var customTest = new FakeStatisticalTest(
            "CustomMetric", StatisticalEvidenceFamily.Fragmentation,
            canRun: true, throwOnCompute: false,
            pValueFactory: _ => 0.05);

        var suite = new TestSuiteBuilder()
            .AddCountEnrichmentTests()
            .AddAmbiguityOrTargetDecoyTests()
            .AddTests(new[] { customTest })
            .Build();

        Assert.That(suite, Does.Contain(customTest));
        Assert.That(suite.Count, Is.EqualTo(17));
    }

    [Test]
    public void IndependenceCorrelationEstimator_ReturnsIdentityMatrix()
    {
        var results = new List<StatisticalTestResult>
        {
            new StatisticalTestResultBuilder().WithDatabaseName("Db1").WithTestName("T1").WithMetricName("M1").WithPValue(0.01).WithTestStatistic(2.0).Build(),
            new StatisticalTestResultBuilder().WithDatabaseName("Db1").WithTestName("T2").WithMetricName("M2").WithPValue(0.03).WithTestStatistic(1.5).Build(),
            new StatisticalTestResultBuilder().WithDatabaseName("Db1").WithTestName("T3").WithMetricName("M3").WithPValue(0.05).WithTestStatistic(1.2).Build(),
        };

        var estimator = new IndependenceCorrelationEstimator();
        var matrix = estimator.EstimateCorrelationMatrix(results);

        Assert.Multiple(() =>
        {
            Assert.That(matrix.GetLength(0), Is.EqualTo(3));
            Assert.That(matrix.GetLength(1), Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    Assert.That(matrix[i, j], Is.EqualTo(i == j ? 1.0 : 0.0));
        });
    }

    [Test]
    public void TestStatisticCorrelationEstimator_ProducesValidCorrelationMatrix()
    {
        var results = new List<StatisticalTestResult>
        {
            new StatisticalTestResultBuilder().WithDatabaseName("Db1").WithTestName("T1").WithMetricName("M1").WithPValue(0.01).WithTestStatistic(3.0).Build(),
            new StatisticalTestResultBuilder().WithDatabaseName("Db1").WithTestName("T2").WithMetricName("M2").WithPValue(0.03).WithTestStatistic(1.0).Build(),
        };

        var estimator = new TestStatisticCorrelationEstimator();
        var matrix = estimator.EstimateCorrelationMatrix(results);

        Assert.Multiple(() =>
        {
            Assert.That(matrix.GetLength(0), Is.EqualTo(2));
            Assert.That(matrix[0, 0], Is.EqualTo(1.0));
            Assert.That(matrix[1, 1], Is.EqualTo(1.0));
            Assert.That(matrix[0, 1], Is.EqualTo(-0.5).Within(1e-10));
            Assert.That(matrix[1, 0], Is.EqualTo(-0.5).Within(1e-10));
        });
    }

    [Test]
    public void MetaAnalysis_UsesInjectedCorrelationEstimator()
    {
        var results = new List<StatisticalTestResult>
        {
            new StatisticalTestResultBuilder().WithDatabaseName("Db1").WithTestName("T1").WithMetricName("M1").WithPValue(0.01).WithTestStatistic(2.0).Build(),
            new StatisticalTestResultBuilder().WithDatabaseName("Db1").WithTestName("T2").WithMetricName("M2").WithPValue(0.03).WithTestStatistic(1.5).Build(),
        };

        var fisherDefault = MetaAnalysis.CombinePValuesAcrossTests(results, PValueCombiningMethod.Fishers);
        var fisherWithIndependence = MetaAnalysis.CombinePValuesAcrossTests(results, PValueCombiningMethod.Fishers, new IndependenceCorrelationEstimator());

        Assert.Multiple(() =>
        {
            Assert.That(fisherDefault.ContainsKey("Db1"), Is.True);
            Assert.That(fisherWithIndependence.ContainsKey("Db1"), Is.True);
            Assert.That(fisherDefault["Db1"], Is.EqualTo(fisherWithIndependence["Db1"]).Within(1e-12));
        });
    }

    private sealed class FakeStatisticalTest : StatisticalTestBase
    {
        private readonly bool _canRun;
        private readonly bool _throwOnCompute;
        private readonly Func<TransientDatabaseMetrics, double> _pValueFactory;
        private readonly Func<TransientDatabaseMetrics, double> _testValueFactory;
        private readonly Func<TransientDatabaseMetrics, List<TransientDatabaseMetrics>, double?> _effectSizeFactory;

        public FakeStatisticalTest(
            string metricName,
            StatisticalEvidenceFamily evidenceFamily,
            bool canRun,
            bool throwOnCompute,
            Func<TransientDatabaseMetrics, double> pValueFactory,
            Func<TransientDatabaseMetrics, bool>? isDefinedFor = null,
            Func<TransientDatabaseMetrics, double>? testValueFactory = null,
            Func<TransientDatabaseMetrics, List<TransientDatabaseMetrics>, double?>? effectSizeFactory = null)
            : base(metricName, evidenceFamily, isDefinedFor)
        {
            _canRun = canRun;
            _throwOnCompute = throwOnCompute;
            _pValueFactory = pValueFactory;
            _testValueFactory = testValueFactory ?? (_ => 0.0);
            _effectSizeFactory = effectSizeFactory ?? ((_, _) => null);
        }

        public override string TestName => "Fake";

        public override string Description => "Fake test for refactor coverage";

        public override bool CanRun(List<TransientDatabaseMetrics> allResults)
        {
            return _canRun;
        }

        public override Dictionary<string, double> ComputePValues(List<TransientDatabaseMetrics> allResults)
        {
            if (_throwOnCompute)
            {
                throw new InvalidOperationException("Expected test failure");
            }

            return allResults.ToDictionary(p => p.DatabaseName, _pValueFactory);
        }

        public override double GetTestValue(TransientDatabaseMetrics result)
        {
            return _testValueFactory(result);
        }

        public override double? GetEffectSize(TransientDatabaseMetrics result, List<TransientDatabaseMetrics> allResults)
        {
            return _effectSizeFactory(result, allResults);
        }
    }
}
