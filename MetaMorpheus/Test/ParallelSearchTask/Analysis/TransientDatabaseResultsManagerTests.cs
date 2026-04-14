using System;
using System.Collections.Generic;
using System.IO;
using EngineLayer;
using NUnit.Framework;
using TaskLayer.ParallelSearch;
using TaskLayer.ParallelSearch.Statistics;

namespace Test.ParallelSearchTask.Analysis;

public class TransientDatabaseResultsManagerTests
{
    private string _testOutputDir = null!;

    [SetUp]
    public void SetUp()
    {
        _testOutputDir = Path.Combine(Path.GetTempPath(), $"ParallelSearchResults_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testOutputDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testOutputDir))
            Directory.Delete(_testOutputDir, true);
    }

    [Test]
    public void Constructor_WithNullAggregator_Throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new TransientDatabaseResultsManager(
            null!,
            new List<IStatisticalTest>(),
            Path.Combine(_testOutputDir, "cache.csv")));

        Assert.That(ex.ParamName, Is.EqualTo("metricAggregator"));
    }

    [Test]
    public void Constructor_WithNullTests_Throws()
    {
        var aggregator = new TaskLayer.ParallelSearch.Analysis.MetricAggregator(new List<TaskLayer.ParallelSearch.Analysis.IMetricCollector>());

        var ex = Assert.Throws<ArgumentNullException>(() => new TransientDatabaseResultsManager(
            aggregator,
            null!,
            Path.Combine(_testOutputDir, "cache.csv")));

        Assert.That(ex.ParamName, Is.EqualTo("tests"));
    }

    [Test]
    public void HasCachedResults_WithNoCache_ReturnsFalse()
    {
        var aggregator = new TaskLayer.ParallelSearch.Analysis.MetricAggregator(new List<TaskLayer.ParallelSearch.Analysis.IMetricCollector>());
        var manager = new TransientDatabaseResultsManager(
            aggregator,
            new List<IStatisticalTest>(),
            Path.Combine(_testOutputDir, "cache.csv"));

        bool hasCache = manager.HasCachedResults("TestDb");

        Assert.That(hasCache, Is.False);
    }

    [Test]
    public void TryGetCachedResult_WithNoCache_ReturnsFalse()
    {
        var aggregator = new TaskLayer.ParallelSearch.Analysis.MetricAggregator(new List<TaskLayer.ParallelSearch.Analysis.IMetricCollector>());
        var manager = new TransientDatabaseResultsManager(
            aggregator,
            new List<IStatisticalTest>(),
            Path.Combine(_testOutputDir, "cache.csv"));

        bool found = manager.TryGetCachedResult("TestDb", out var result);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.False);
            Assert.That(result, Is.Null);
        });
    }

    [Test]
    public void ProcessDatabase_WithNullContext_Throws()
    {
        var aggregator = new TaskLayer.ParallelSearch.Analysis.MetricAggregator(new List<TaskLayer.ParallelSearch.Analysis.IMetricCollector>());
        var manager = new TransientDatabaseResultsManager(
            aggregator,
            new List<IStatisticalTest>(),
            Path.Combine(_testOutputDir, "cache.csv"));

        var ex = Assert.Throws<ArgumentNullException>(() => manager.ProcessDatabase(null!));

        Assert.That(ex.ParamName, Is.EqualTo("context"));
    }

    [Test]
    public void GetCacheSummary_EmptyList_ReturnsZeroCounts()
    {
        var aggregator = new TaskLayer.ParallelSearch.Analysis.MetricAggregator(new List<TaskLayer.ParallelSearch.Analysis.IMetricCollector>());
        var manager = new TransientDatabaseResultsManager(
            aggregator,
            new List<IStatisticalTest>(),
            Path.Combine(_testOutputDir, "cache.csv"));

        var summary = manager.GetCacheSummary(new List<string>());

        Assert.Multiple(() =>
        {
            Assert.That(summary.TotalDatabases, Is.EqualTo(0));
            Assert.That(summary.CachedDatabases, Is.EqualTo(0));
            Assert.That(summary.DatabasesNeedingProcessing, Is.EqualTo(0));
            Assert.That(summary.CacheHitRate, Is.EqualTo(0.0));
        });
    }

    [Test]
    public void GetCacheSummary_MultipleDatabases_ReturnsCorrectCounts()
    {
        var aggregator = new TaskLayer.ParallelSearch.Analysis.MetricAggregator(new List<TaskLayer.ParallelSearch.Analysis.IMetricCollector>());
        var manager = new TransientDatabaseResultsManager(
            aggregator,
            new List<IStatisticalTest>(),
            Path.Combine(_testOutputDir, "cache.csv"));

        var summary = manager.GetCacheSummary(new List<string> { "Db1", "Db2", "Db3" });

        Assert.Multiple(() =>
        {
            Assert.That(summary.TotalDatabases, Is.EqualTo(3));
            Assert.That(summary.CachedDatabases, Is.EqualTo(0));
            Assert.That(summary.DatabasesNeedingProcessing, Is.EqualTo(3));
            Assert.That(summary.CacheHitRate, Is.EqualTo(0.0));
        });
    }

    [Test]
    public void CachedAnalysisCount_InitiallyZero()
    {
        var aggregator = new TaskLayer.ParallelSearch.Analysis.MetricAggregator(new List<TaskLayer.ParallelSearch.Analysis.IMetricCollector>());
        var manager = new TransientDatabaseResultsManager(
            aggregator,
            new List<IStatisticalTest>(),
            Path.Combine(_testOutputDir, "cache.csv"));

        Assert.That(manager.CachedAnalysisCount, Is.EqualTo(0));
    }

    [Test]
    public void StatisticalTestCount_InitiallyZero()
    {
        var aggregator = new TaskLayer.ParallelSearch.Analysis.MetricAggregator(new List<TaskLayer.ParallelSearch.Analysis.IMetricCollector>());
        var manager = new TransientDatabaseResultsManager(
            aggregator,
            new List<IStatisticalTest>(),
            Path.Combine(_testOutputDir, "cache.csv"));

        Assert.That(manager.StatisticalTestCount, Is.EqualTo(0));
    }

    [Test]
    public void RemoveFromCache_WithNoCache_ReturnsFalse()
    {
        var aggregator = new TaskLayer.ParallelSearch.Analysis.MetricAggregator(new List<TaskLayer.ParallelSearch.Analysis.IMetricCollector>());
        var manager = new TransientDatabaseResultsManager(
            aggregator,
            new List<IStatisticalTest>(),
            Path.Combine(_testOutputDir, "cache.csv"));

        bool removed = manager.RemoveFromCache("NonExistent");

        Assert.That(removed, Is.False);
    }

    [Test]
    public void WriteAllResults_BeforeFinalize_Throws()
    {
        var aggregator = new TaskLayer.ParallelSearch.Analysis.MetricAggregator(new List<TaskLayer.ParallelSearch.Analysis.IMetricCollector>());
        var manager = new TransientDatabaseResultsManager(
            aggregator,
            new List<IStatisticalTest>(),
            Path.Combine(_testOutputDir, "cache.csv"));

        var ex = Assert.Throws<MetaMorpheusException>(() => manager.WriteAllResults(_testOutputDir));

        Assert.That(ex.Message, Does.Contain("Write Results Failed"));
    }

    [Test]
    public void RunStatisticalAnalysis_WithoutProcessing_Throws()
    {
        var aggregator = new TaskLayer.ParallelSearch.Analysis.MetricAggregator(new List<TaskLayer.ParallelSearch.Analysis.IMetricCollector>());
        var manager = new TransientDatabaseResultsManager(
            aggregator,
            new List<IStatisticalTest>(),
            Path.Combine(_testOutputDir, "cache.csv"));

        var ex = Assert.Throws<MetaMorpheusException>(() => manager.RunStatisticalAnalysis());

        Assert.That(ex.Message, Does.Contain("Finalizing Analysis Failed"));
    }

    [Test]
    public void RunStatisticalAnalysis_CalledTwice_Throws()
    {
        var aggregator = new TaskLayer.ParallelSearch.Analysis.MetricAggregator(new List<TaskLayer.ParallelSearch.Analysis.IMetricCollector>());
        var manager = new TransientDatabaseResultsManager(
            aggregator,
            new List<IStatisticalTest>(),
            Path.Combine(_testOutputDir, "cache.csv"));

        var ex = Assert.Throws<MetaMorpheusException>(() => manager.RunStatisticalAnalysis());

        Assert.That(ex.Message, Does.Contain("Finalizing Analysis Failed"));
    }
}