using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TaskLayer.ParallelSearch.Statistics;
using TaskLayer.ParallelSearch.Statistics.Calibration;

namespace Test.ParallelSearchTask.StatisticalAnalysis;

[TestFixture]
public class CalibrationServiceTests
{
    [Test]
    public void Calibrate_AllNullData_ReturnsLowExpectedPassCounts()
    {
        var results = new List<StatisticalTestResult>();
        for (int i = 0; i < 100; i++)
        {
            results.Add(MakeNullResult($"Db{i:D3}", "Gaussian", "PSM-All",
                StatisticalEvidenceFamily.CountEnrichment, 0.5));
            results.Add(MakeNullResult($"Db{i:D3}", "Gaussian", "Peptide-All",
                StatisticalEvidenceFamily.CountEnrichment, 0.5));
            results.Add(MakeNullResult($"Db{i:D3}", "FisherExact", "PSM",
                StatisticalEvidenceFamily.AmbiguityOrTargetDecoy, 0.5));
            results.Add(MakeNullResult($"Db{i:D3}", "FisherExact", "Peptide-TD",
                StatisticalEvidenceFamily.AmbiguityOrTargetDecoy, 0.5));
        }

        var service = new CalibrationService();
        var calResult = service.Calibrate(results, alpha: 0.05);

        Assert.Multiple(() =>
        {
            Assert.That(calResult.TotalDatabases, Is.EqualTo(100));
            Assert.That(calResult.NullBulkDatabaseCount, Is.GreaterThanOrEqualTo(95));
            Assert.That(calResult.DatabasesRemovedAsOutliers, Is.LessThanOrEqualTo(5));

            Assert.That(calResult.OverallTestPassCountProfile, Is.Not.Null);
            Assert.That(calResult.OverallTestPassCountProfile!.Mean, Is.LessThanOrEqualTo(0.5));
            Assert.That(calResult.OverallTestPassCountProfile.Percentile95, Is.LessThanOrEqualTo(1));

            Assert.That(calResult.PerTestPValueProfiles.Count, Is.GreaterThan(0));
            Assert.That(calResult.PerTestEffectSizeProfiles.Count, Is.GreaterThan(0));

            foreach (var kvp in calResult.PerTestPValueProfiles)
            {
                Assert.That(kvp.Value.Count, Is.GreaterThanOrEqualTo(95));
                Assert.That(kvp.Value.Mean, Is.InRange(0.40, 0.60));
            }
        });
    }

    [Test]
    public void Calibrate_MixedData_RemovesHighSignalOutliers()
    {
        var results = new List<StatisticalTestResult>();
        for (int i = 0; i < 95; i++)
        {
            results.Add(MakeNullResult($"NullDb{i:D3}", "Gaussian", "PSM-All",
                StatisticalEvidenceFamily.CountEnrichment, 0.5));
            results.Add(MakeNullResult($"NullDb{i:D3}", "FisherExact", "PSM",
                StatisticalEvidenceFamily.AmbiguityOrTargetDecoy, 0.5));
            results.Add(MakeNullResult($"NullDb{i:D3}", "KS", "PSMScoreDistribution",
                StatisticalEvidenceFamily.ScoreDistribution, 0.5));
        }
        for (int i = 0; i < 5; i++)
        {
            results.Add(MakeSignificantResult($"PosDb{i:D3}", "Gaussian", "PSM-All",
                StatisticalEvidenceFamily.CountEnrichment, 0.001, 0.5));
            results.Add(MakeSignificantResult($"PosDb{i:D3}", "FisherExact", "PSM",
                StatisticalEvidenceFamily.AmbiguityOrTargetDecoy, 0.002, 0.5));
            results.Add(MakeSignificantResult($"PosDb{i:D3}", "KS", "PSMScoreDistribution",
                StatisticalEvidenceFamily.ScoreDistribution, 0.003, 0.5));
        }

        var service = new CalibrationService();
        var calResult = service.Calibrate(results, alpha: 0.05);

        Assert.Multiple(() =>
        {
            Assert.That(calResult.TotalDatabases, Is.EqualTo(100));
            Assert.That(calResult.NullBulkDatabaseCount, Is.EqualTo(95));
            Assert.That(calResult.DatabasesRemovedAsOutliers, Is.EqualTo(5));
            Assert.That(calResult.IterationsUsed, Is.GreaterThanOrEqualTo(1));

            Assert.That(calResult.OverallFamilyPassCountProfile, Is.Not.Null);
            Assert.That(calResult.OverallFamilyPassCountProfile!.Mean, Is.LessThanOrEqualTo(0.1));
        });
    }

    [Test]
    public void Calibrate_EmptyData_ReturnsEmptyResult()
    {
        var service = new CalibrationService();
        var calResult = service.Calibrate(new List<StatisticalTestResult>(), alpha: 0.05);

        Assert.Multiple(() =>
        {
            Assert.That(calResult.TotalDatabases, Is.EqualTo(0));
            Assert.That(calResult.NullBulkDatabaseCount, Is.EqualTo(0));
            Assert.That(calResult.DatabasesRemovedAsOutliers, Is.EqualTo(0));
            Assert.That(calResult.IterationsUsed, Is.EqualTo(0));
        });
    }

    [Test]
    public void Calibrate_SingleDatabase_ReturnsSingleResult()
    {
        var results = new List<StatisticalTestResult>
        {
            MakeNullResult("OnlyDb", "Gaussian", "PSM-All",
                StatisticalEvidenceFamily.CountEnrichment, 0.5),
        };

        var service = new CalibrationService();
        var calResult = service.Calibrate(results, alpha: 0.05);

        Assert.Multiple(() =>
        {
            Assert.That(calResult.TotalDatabases, Is.EqualTo(1));
            Assert.That(calResult.NullBulkDatabaseCount, Is.EqualTo(1));
            Assert.That(calResult.OverallTestPassCountProfile, Is.Not.Null);
            Assert.That(calResult.OverallTestPassCountProfile!.Mean, Is.EqualTo(0));
        });
    }

    [Test]
    public void Calibrate_AllDatabasesSignificant_IterationsZeroIndicatesInconclusive()
    {
        var results = new List<StatisticalTestResult>();
        for (int i = 0; i < 10; i++)
        {
            results.Add(MakeSignificantResult($"Db{i:D3}", "Gaussian", "PSM-All",
                StatisticalEvidenceFamily.CountEnrichment, 0.001, 0.5));
            results.Add(MakeSignificantResult($"Db{i:D3}", "FisherExact", "PSM",
                StatisticalEvidenceFamily.AmbiguityOrTargetDecoy, 0.002, 0.5));
            results.Add(MakeSignificantResult($"Db{i:D3}", "KS", "PSMScoreDistribution",
                StatisticalEvidenceFamily.ScoreDistribution, 0.003, 0.5));
        }

        var service = new CalibrationService();
        var calResult = service.Calibrate(results, alpha: 0.05);

        Assert.Multiple(() =>
        {
            Assert.That(calResult.IterationsUsed, Is.EqualTo(0));
            Assert.That(calResult.NullBulkDatabaseCount, Is.EqualTo(10));
            Assert.That(calResult.DatabasesRemovedAsOutliers, Is.EqualTo(0));
        });
    }

    [Test]
    public void Calibrate_NullData_PerFamilyProfilesAreSensible()
    {
        var results = new List<StatisticalTestResult>();
        for (int i = 0; i < 100; i++)
        {
            results.Add(MakeNullResult($"Db{i:D3}", "Gaussian", "PSM-All",
                StatisticalEvidenceFamily.CountEnrichment, 0.5));
            results.Add(MakeNullResult($"Db{i:D3}", "FisherExact", "PSM",
                StatisticalEvidenceFamily.AmbiguityOrTargetDecoy, 0.5));
        }

        var service = new CalibrationService();
        var calResult = service.Calibrate(results, alpha: 0.05);

        Assert.Multiple(() =>
        {
            Assert.That(calResult.PerFamilyTestPassCountProfiles, Does.ContainKey(StatisticalEvidenceFamily.CountEnrichment));
            Assert.That(calResult.PerFamilyTestPassCountProfiles, Does.ContainKey(StatisticalEvidenceFamily.AmbiguityOrTargetDecoy));

            var countEnrichmentProfile = calResult.PerFamilyTestPassCountProfiles[StatisticalEvidenceFamily.CountEnrichment];
            Assert.That(countEnrichmentProfile.Mean, Is.LessThanOrEqualTo(0.1));
            Assert.That(countEnrichmentProfile.Percentile95, Is.LessThanOrEqualTo(1));
        });
    }

    [Test]
    public void Calibrate_WithCombinedResults_ComputesCombinedPValueProfile()
    {
        var results = new List<StatisticalTestResult>();
        for (int i = 0; i < 100; i++)
        {
            string db = $"Db{i:D3}";
            results.Add(MakeNullResult(db, "Gaussian", "PSM-All",
                StatisticalEvidenceFamily.CountEnrichment, 0.5));
            results.Add(MakeNullResult(db, "FisherExact", "PSM",
                StatisticalEvidenceFamily.AmbiguityOrTargetDecoy, 0.5));

            results.Add(new StatisticalTestResultBuilder()
                .WithDatabaseName(db)
                .WithTestName(CombinedResultNames.TestName)
                .WithMetricName(CombinedResultNames.AllMetricName)
                .WithEvidenceFamily(null)
                .WithIsDefined(true)
                .WithPValue(0.5)
                .WithQValue(0.5)
                .Build());
        }

        var service = new CalibrationService();
        var calResult = service.Calibrate(results, alpha: 0.05);

        Assert.Multiple(() =>
        {
            Assert.That(calResult.CombinedPValueProfile, Is.Not.Null);
            Assert.That(calResult.CombinedPValueProfile!.Count, Is.EqualTo(100));
            Assert.That(calResult.CombinedPValueProfile.Mean, Is.InRange(0.40, 0.60));
        });
    }

    [Test]
    public void Calibrate_UndefinedResults_ExcludedFromDistributions()
    {
        var results = new List<StatisticalTestResult>();
        for (int i = 0; i < 100; i++)
        {
            results.Add(MakeNullResult($"Db{i:D3}", "Gaussian", "PSM-All",
                StatisticalEvidenceFamily.CountEnrichment, 0.5));
            results.Add(new StatisticalTestResultBuilder()
                .WithDatabaseName($"Db{i:D3}")
                .WithTestName("Gaussian")
                .WithMetricName("UndefinedMetric")
                .WithEvidenceFamily(StatisticalEvidenceFamily.CountEnrichment)
                .WithIsDefined(false)
                .WithEligibilityReason("NoData")
                .Build());
        }

        var service = new CalibrationService();
        var calResult = service.Calibrate(results, alpha: 0.05);

        Assert.Multiple(() =>
        {
            Assert.That(calResult.PerTestPValueProfiles, Does.ContainKey("Gaussian_PSM-All"));
            Assert.That(calResult.PerTestPValueProfiles, Does.Not.ContainKey("Gaussian_UndefinedMetric"));
        });
    }

    [Test]
    public void NullDistributionProfile_BasicStatistics()
    {
        var values = new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0 };
        var profile = new NullDistributionProfile("Test", values);

        Assert.Multiple(() =>
        {
            Assert.That(profile.Count, Is.EqualTo(10));
            Assert.That(profile.Mean, Is.EqualTo(5.5).Within(1e-12));
            Assert.That(profile.Median, Is.EqualTo(5.5).Within(1e-12));
            Assert.That(profile.Min, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(profile.Max, Is.EqualTo(10.0).Within(1e-12));
            Assert.That(profile.Percentile50, Is.EqualTo(5.5).Within(1e-12));
            Assert.That(profile.Percentile90, Is.EqualTo(9.1).Within(1e-12));
            Assert.That(profile.Percentile95, Is.EqualTo(9.55).Within(1e-12));
            Assert.That(profile.Percentile99, Is.EqualTo(9.91).Within(1e-12));
            Assert.That(profile.Label, Is.EqualTo("Test"));
        });
    }

    [Test]
    public void NullDistributionProfile_Empty_ReturnsNaN()
    {
        var profile = new NullDistributionProfile("Empty", Array.Empty<double>());

        Assert.Multiple(() =>
        {
            Assert.That(profile.Count, Is.EqualTo(0));
            Assert.That(profile.Mean, Is.NaN);
            Assert.That(profile.Median, Is.NaN);
            Assert.That(profile.Percentile95, Is.NaN);
        });
    }

    [Test]
    public void NullDistributionProfile_SingleValue()
    {
        var profile = new NullDistributionProfile("Single", new[] { 42.0 });

        Assert.Multiple(() =>
        {
            Assert.That(profile.Count, Is.EqualTo(1));
            Assert.That(profile.Mean, Is.EqualTo(42.0).Within(1e-12));
            Assert.That(profile.Median, Is.EqualTo(42.0).Within(1e-12));
            Assert.That(profile.Percentile95, Is.EqualTo(42.0).Within(1e-12));
        });
    }

    [Test]
    public void NullDistributionProfile_FiltersNaN()
    {
        var profile = new NullDistributionProfile("WithNaN", new double[] { 1.0, double.NaN, 3.0, double.PositiveInfinity, 5.0 });

        Assert.Multiple(() =>
        {
            Assert.That(profile.Count, Is.EqualTo(3));
            Assert.That(profile.Mean, Is.EqualTo(3.0).Within(1e-12));
            Assert.That(profile.Min, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(profile.Max, Is.EqualTo(5.0).Within(1e-12));
        });
    }

    [Test]
    public void CalibrationReportWriter_FormatReport_ContainsExpectedSections()
    {
        var result = new CalibrationResult
        {
            TotalDatabases = 1000,
            NullBulkDatabaseCount = 985,
            DatabasesRemovedAsOutliers = 15,
            IterationsUsed = 3,
            Alpha = 0.05,
            OverallTestPassCountProfile = new NullDistributionProfile(
                "TestsPassedPerDatabase",
                Enumerable.Repeat(0.0, 985)),
            PerTestPValueProfiles = new Dictionary<string, NullDistributionProfile>
            {
                ["Gaussian_PSM-All"] = new NullDistributionProfile("pValue_Gaussian_PSM-All", Enumerable.Repeat(0.5, 985))
            }
        };

        var report = CalibrationReportWriter.FormatReport(result);

        Assert.Multiple(() =>
        {
            Assert.That(report, Does.Contain("CALIBRATION REPORT"));
            Assert.That(report, Does.Contain("1000"));
            Assert.That(report, Does.Contain("985"));
            Assert.That(report, Does.Contain("15"));
            Assert.That(report, Does.Contain("RECOMMENDED THRESHOLDS"));
            Assert.That(report, Does.Contain("END CALIBRATION REPORT"));
        });
    }

    [Test]
    public void CalibrationReportWriter_EmptyResult_ProducesWarning()
    {
        var result = new CalibrationResult
        {
            TotalDatabases = 0,
            NullBulkDatabaseCount = 0,
            DatabasesRemovedAsOutliers = 0,
            IterationsUsed = 0,
            Alpha = 0.05,
        };

        var report = CalibrationReportWriter.FormatReport(result);

        Assert.Multiple(() =>
        {
            Assert.That(report, Does.Contain("CALIBRATION REPORT"));
            Assert.That(report, Does.Contain("WARNING"));
        });
    }

    private static StatisticalTestResult MakeNullResult(string dbName, string testName, string metricName,
        StatisticalEvidenceFamily family, double pValue)
    {
        return new StatisticalTestResultBuilder()
            .WithDatabaseName(dbName)
            .WithTestName(testName)
            .WithMetricName(metricName)
            .WithEvidenceFamily(family)
            .WithIsDefined(true)
            .WithPValue(pValue)
            .WithQValue(pValue * 2)
            .WithTestStatistic(0.0)
            .WithEffectSize(0.0)
            .Build();
    }

    private static StatisticalTestResult MakeSignificantResult(string dbName, string testName, string metricName,
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
