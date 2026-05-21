using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Statistics;
using TaskLayer.ParallelSearch.Statistics.IsolationForest;

namespace Test.ParallelSearchTask.StatisticalAnalysis;

[TestFixture]
public class AnomalyDetectionIntegrationTests
{
    [Test]
    public void BuildSummaryFeatures_AllNull_AllHaveSameSigCount()
    {
        var metrics = new Dictionary<string, TransientDatabaseMetrics>();
        var statResults = new List<StatisticalTestResult>();

        for (int i = 0; i < 50; i++)
        {
            string db = $"Db{i:D3}";
            metrics[db] = MakeNullMetrics(db);
            statResults.Add(MakeNullStatResult(db, "Gaussian", "PSM-All",
                StatisticalEvidenceFamily.CountEnrichment, 0.5));
            statResults.Add(MakeNullStatResult(db, "FisherExact", "PSM",
                StatisticalEvidenceFamily.AmbiguityOrTargetDecoy, 0.5));
        }

        var inputs = AnomalyDetectionFeatureBuilder.BuildSummaryFeatures(metrics, statResults);

        Assert.Multiple(() =>
        {
            Assert.That(inputs, Has.Count.EqualTo(50));
            foreach (var input in inputs)
            {
                Assert.That(input.Features, Has.Length.EqualTo(21));
                Assert.That(input.Features.Any(double.IsNaN), Is.False);
                Assert.That(input.Features.Any(double.IsInfinity), Is.False);
            }
        });
    }

    [Test]
    public void BuildFullPerTestFeatures_AllNull_NoNaNFeatures()
    {
        var statResults = new List<StatisticalTestResult>();

        for (int i = 0; i < 50; i++)
        {
            string db = $"Db{i:D3}";
            statResults.Add(MakeNullStatResult(db, "Gaussian", "PSM-All",
                StatisticalEvidenceFamily.CountEnrichment, 0.5));
            statResults.Add(MakeNullStatResult(db, "FisherExact", "PSM",
                StatisticalEvidenceFamily.AmbiguityOrTargetDecoy, 0.5));
        }

        var inputs = AnomalyDetectionFeatureBuilder.BuildFullPerTestFeatures(statResults);

        Assert.Multiple(() =>
        {
            Assert.That(inputs, Has.Count.EqualTo(50));
            Assert.That(inputs[0].Features.Length, Is.EqualTo(2 * 3));
            foreach (var input in inputs)
            {
                Assert.That(input.Features.Any(double.IsNaN), Is.False);
                Assert.That(input.Features.Any(double.IsInfinity), Is.False);
            }
        });
    }

    [Test]
    public void AnomalyDetectionService_MixedData_HighSignalGetsHigherScore()
    {
        var metrics = new Dictionary<string, TransientDatabaseMetrics>();
        var statResults = new List<StatisticalTestResult>();

        for (int i = 0; i < 95; i++)
        {
            string db = $"NullDb{i:D3}";
            metrics[db] = MakeNullMetrics(db);
            statResults.Add(MakeNullStatResult(db, "Gaussian", "PSM-All",
                StatisticalEvidenceFamily.CountEnrichment, 0.5));
            statResults.Add(MakeNullStatResult(db, "FisherExact", "PSM",
                StatisticalEvidenceFamily.AmbiguityOrTargetDecoy, 0.5));
        }
        for (int i = 0; i < 5; i++)
        {
            string db = $"PosDb{i:D3}";
            metrics[db] = MakePositiveMetrics(db);
            statResults.Add(MakeSignificantStatResult(db, "Gaussian", "PSM-All",
                StatisticalEvidenceFamily.CountEnrichment, 0.001, 0.5));
            statResults.Add(MakeSignificantStatResult(db, "FisherExact", "PSM",
                StatisticalEvidenceFamily.AmbiguityOrTargetDecoy, 0.002, 0.5));
        }

        var service = new AnomalyDetectionService();
        var result = service.Run(metrics, statResults, seed: 42);
        service.UpdateMetrics(metrics, result);

        Assert.Multiple(() =>
        {
            double maxNullSummary = metrics.Where(kvp => kvp.Key.StartsWith("NullDb"))
                .Max(kvp => kvp.Value.SummaryAnomalyScore);
            double minPosSummary = metrics.Where(kvp => kvp.Key.StartsWith("PosDb"))
                .Min(kvp => kvp.Value.SummaryAnomalyScore);

            Assert.That(minPosSummary, Is.GreaterThan(maxNullSummary));

            double maxNullFull = metrics.Where(kvp => kvp.Key.StartsWith("NullDb"))
                .Max(kvp => kvp.Value.FullAnomalyScore);
            double minPosFull = metrics.Where(kvp => kvp.Key.StartsWith("PosDb"))
                .Min(kvp => kvp.Value.FullAnomalyScore);

            Assert.That(minPosFull, Is.GreaterThan(maxNullFull));

            var topRanked = metrics.OrderBy(kvp => kvp.Value.AnomalyRank).First();
            Assert.That(topRanked.Key, Does.StartWith("PosDb"));
        });
    }

    [Test]
    public void AnomalyDetectionService_AllNull_ScoresNearPointFive()
    {
        var metrics = new Dictionary<string, TransientDatabaseMetrics>();
        var statResults = new List<StatisticalTestResult>();

        for (int i = 0; i < 50; i++)
        {
            string db = $"Db{i:D3}";
            metrics[db] = MakeNullMetrics(db);
            statResults.Add(MakeNullStatResult(db, "Gaussian", "PSM-All",
                StatisticalEvidenceFamily.CountEnrichment, 0.5));
        }

        var service = new AnomalyDetectionService();
        var result = service.Run(metrics, statResults, seed: 42);
        service.UpdateMetrics(metrics, result);

        Assert.Multiple(() =>
        {
            double maxScore = metrics.Values.Max(m => m.SummaryAnomalyScore);
            double minScore = metrics.Values.Min(m => m.SummaryAnomalyScore);
            double meanScore = metrics.Values.Average(m => m.SummaryAnomalyScore);

            Assert.That(meanScore, Is.EqualTo(0.5).Within(0.2));
            Assert.That(maxScore, Is.LessThan(0.85));
            Assert.That(minScore, Is.GreaterThan(0.15));
        });
    }

    [Test]
    public void AnomalyDetectionService_SingleDatabase_ScoresDefault()
    {
        var metrics = new Dictionary<string, TransientDatabaseMetrics>
        {
            ["OnlyDb"] = MakeNullMetrics("OnlyDb")
        };
        var statResults = new List<StatisticalTestResult>
        {
            MakeNullStatResult("OnlyDb", "Gaussian", "PSM-All",
                StatisticalEvidenceFamily.CountEnrichment, 0.5)
        };

        var service = new AnomalyDetectionService();
        var result = service.Run(metrics, statResults, seed: 42);
        service.UpdateMetrics(metrics, result);

        Assert.Multiple(() =>
        {
            Assert.That(metrics["OnlyDb"].SummaryAnomalyScore, Is.EqualTo(0.5));
            Assert.That(metrics["OnlyDb"].FullAnomalyScore, Is.EqualTo(0.5));
            Assert.That(metrics["OnlyDb"].AnomalyRank, Is.EqualTo(1));
        });
    }

    [Test]
    public void BuildSummaryFeatures_EmptyInput_ReturnsEmpty()
    {
        var inputs = AnomalyDetectionFeatureBuilder.BuildSummaryFeatures(
            new Dictionary<string, TransientDatabaseMetrics>(),
            new List<StatisticalTestResult>());

        Assert.That(inputs, Is.Empty);
    }

    [Test]
    public void BuildFullPerTestFeatures_EmptyInput_ReturnsEmpty()
    {
        var inputs = AnomalyDetectionFeatureBuilder.BuildFullPerTestFeatures(
            new List<StatisticalTestResult>());

        Assert.That(inputs, Is.Empty);
    }

    [Test]
    public void BuildSummaryFeatures_HandlesMissingFamilies_UsesSentinels()
    {
        var metrics = new Dictionary<string, TransientDatabaseMetrics>
        {
            ["Db1"] = MakeNullMetrics("Db1")
        };
        var statResults = new List<StatisticalTestResult>
        {
            MakeNullStatResult("Db1", "Gaussian", "PSM-All",
                StatisticalEvidenceFamily.CountEnrichment, 0.5)
        };

        var inputs = AnomalyDetectionFeatureBuilder.BuildSummaryFeatures(metrics, statResults);

        Assert.Multiple(() =>
        {
            Assert.That(inputs, Has.Count.EqualTo(1));
            for (int i = 0; i < inputs[0].Features.Length; i++)
            {
                Assert.That(double.IsNaN(inputs[0].Features[i]), Is.False);
                Assert.That(double.IsInfinity(inputs[0].Features[i]), Is.False);
            }
        });
    }

    [Test]
    public void BuildFullPerTestFeatures_HandlesMissingTests_Gracefully()
    {
        var statResults = new List<StatisticalTestResult>();

        for (int i = 0; i < 10; i++)
        {
            string db = $"Db{i:D3}";
            statResults.Add(MakeNullStatResult(db, "Gaussian", "PSM-All",
                StatisticalEvidenceFamily.CountEnrichment, 0.5));
        }
        for (int i = 0; i < 10; i++)
        {
            string db = $"Db{i:D3}";
            if (i >= 5)
                continue;
            statResults.Add(MakeNullStatResult(db, "FisherExact", "PSM",
                StatisticalEvidenceFamily.AmbiguityOrTargetDecoy, 0.5));
        }

        var inputs = AnomalyDetectionFeatureBuilder.BuildFullPerTestFeatures(statResults);

        Assert.Multiple(() =>
        {
            Assert.That(inputs, Has.Count.EqualTo(10));
            int expectedFeatureCount = 2 * 3;
            Assert.That(inputs[0].Features.Length, Is.EqualTo(expectedFeatureCount));

            foreach (var input in inputs)
            {
                Assert.That(input.Features.Any(double.IsNaN), Is.False,
                    $"{input.Id} has NaN features");
                Assert.That(input.Features.Any(double.IsInfinity), Is.False,
                    $"{input.Id} has Inf features");
            }
        });
    }

    [Test]
    public void AnomalyRank_OrderedBySummaryScore_Descending()
    {
        var metrics = new Dictionary<string, TransientDatabaseMetrics>();
        var statResults = new List<StatisticalTestResult>();

        for (int i = 0; i < 40; i++)
        {
            string db = $"NullDb{i:D3}";
            metrics[db] = MakeNullMetrics(db);
            statResults.Add(MakeNullStatResult(db, "Gaussian", "PSM-All",
                StatisticalEvidenceFamily.CountEnrichment, 0.5));
        }
        for (int i = 0; i < 3; i++)
        {
            string db = $"PosDb{i:D3}";
            metrics[db] = MakePositiveMetrics(db);
            statResults.Add(MakeSignificantStatResult(db, "Gaussian", "PSM-All",
                StatisticalEvidenceFamily.CountEnrichment, 0.001, 0.5));
            statResults.Add(MakeSignificantStatResult(db, "FisherExact", "PSM",
                StatisticalEvidenceFamily.AmbiguityOrTargetDecoy, 0.002, 0.5));
        }

        var service = new AnomalyDetectionService();
        var result = service.Run(metrics, statResults, seed: 42);
        service.UpdateMetrics(metrics, result);

        var ranked = metrics.OrderBy(kvp => kvp.Value.AnomalyRank).ToList();

        Assert.Multiple(() =>
        {
            for (int i = 0; i < ranked.Count - 1; i++)
            {
                Assert.That(
                    ranked[i].Value.SummaryAnomalyScore,
                    Is.GreaterThanOrEqualTo(ranked[i + 1].Value.SummaryAnomalyScore),
                    $"Rank {ranked[i].Value.AnomalyRank} ({ranked[i].Key}, score={ranked[i].Value.SummaryAnomalyScore}) " +
                    $"should be >= rank {ranked[i + 1].Value.AnomalyRank} ({ranked[i + 1].Key}, score={ranked[i + 1].Value.SummaryAnomalyScore})");
            }
        });
    }

    private static TransientDatabaseMetrics MakeNullMetrics(string dbName)
    {
        int id = ExtractNumericId(dbName);
        double noise = (id % 7) * 0.01;

        return new TransientDatabaseMetrics(dbName)
        {
            PassedTestCount = id % 5 == 0 ? 1 : 0,
            ValidTestCount = 1 + (id % 3),
            PassedFamilyCount = id % 5 == 0 ? 1 : 0,
            ValidFamilyCount = 1 + (id % 2),
            TestPassedRatio = id % 5 == 0 ? 1.0 / (1 + id % 3) : 0.0,
            CombinedPValue = 0.3 + noise,
            CombinedQValue = 0.6 + noise,
            CountEnrichmentBestPValue = 0.3 + noise,
            CountEnrichmentCombinedPValue = 0.3 + noise,
            AmbiguityOrTargetDecoyBestPValue = 0.4 + noise,
            AmbiguityOrTargetDecoyCombinedPValue = 0.4 + noise,
        };
    }

    private static TransientDatabaseMetrics MakePositiveMetrics(string dbName)
    {
        return new TransientDatabaseMetrics(dbName)
        {
            PassedTestCount = 4,
            ValidTestCount = 4,
            PassedFamilyCount = 2,
            ValidFamilyCount = 2,
            TestPassedRatio = 1.0,
            CombinedPValue = 0.001,
            CombinedQValue = 0.005,
            CountEnrichmentBestPValue = 0.001,
            CountEnrichmentCombinedPValue = 0.002,
            AmbiguityOrTargetDecoyBestPValue = 0.002,
            AmbiguityOrTargetDecoyCombinedPValue = 0.003,
            FragmentationBestPValue = 0.01,
            FragmentationCombinedPValue = 0.02,
        };
    }

    private static int ExtractNumericId(string dbName)
    {
        var digits = new string(dbName.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var id) ? id : 0;
    }

    private static StatisticalTestResult MakeNullStatResult(string dbName, string testName, string metricName,
        StatisticalEvidenceFamily family, double pValue)
    {
        return new StatisticalTestResultBuilder()
            .WithDatabaseName(dbName)
            .WithTestName(testName)
            .WithMetricName(metricName)
            .WithEvidenceFamily(family)
            .WithIsDefined(true)
            .WithPValue(pValue)
            .WithQValue(1.0)
            .WithTestStatistic(0.0)
            .WithEffectSize(0.0)
            .Build();
    }

    private static StatisticalTestResult MakeSignificantStatResult(string dbName, string testName, string metricName,
        StatisticalEvidenceFamily family, double pValue, double effectSize)
    {
        return new StatisticalTestResultBuilder()
            .WithDatabaseName(dbName)
            .WithTestName(testName)
            .WithMetricName(metricName)
            .WithEvidenceFamily(family)
            .WithIsDefined(true)
            .WithPValue(pValue)
            .WithQValue(pValue)
            .WithTestStatistic(2.0)
            .WithEffectSize(effectSize)
            .Build();
    }
}
