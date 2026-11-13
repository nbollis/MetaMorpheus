#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Statistics.Calibration;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Everything one pass of the statistical test suite produced.
/// </summary>
public sealed class StatisticalAnalysisRun
{
    public StatisticalAnalysisRun(List<StatisticalTestResult> results, Dictionary<string, TestSummary> testSummaries,
        CalibrationResult calibration, IReadOnlyList<IStatisticalTest> testsRun)
    {
        Results = results;
        TestSummaries = testSummaries;
        Calibration = calibration;
        TestsRun = testsRun;
    }

    /// <summary>Per-test rows with BH q-values, followed by the per-family and overall <c>Combined</c> rows.</summary>
    public List<StatisticalTestResult> Results { get; }

    /// <summary>Per-test and per-family summary rows, keyed as on the ManySearchTask branch.</summary>
    public Dictionary<string, TestSummary> TestSummaries { get; }

    public CalibrationResult Calibration { get; }

    /// <summary>The tests that could run; tests skipped for insufficient data or errors are left out.</summary>
    public IReadOnlyList<IStatisticalTest> TestsRun { get; }

    /// <summary>The overall <c>Combined | All</c> row per database.</summary>
    public Dictionary<string, StatisticalTestResult> OverallCombinedByDatabase() =>
        Results.Where(r => r.IsCombinedResult && r.MetricName == CombinedResultNames.AllMetricName)
            .GroupBy(r => r.DatabaseName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
}

/// <summary>
/// The statistics pass from the ManySearchTask branch's <c>TransientDatabaseResultsManager.RunStatisticalAnalysis</c>,
/// lifted out of the results manager so it can run on any set of summary rows: execute every test, apply
/// Benjamini–Hochberg per test, build test and family summaries, combine p-values within and then across
/// families, and calibrate. Anomaly detection is not part of it; that is the isolation forest evaluator's job.
/// </summary>
public sealed class StatisticalAnalysisPipeline
{
    private readonly Func<IEnumerable<IStatisticalTest>> _createTests;
    private readonly double _alpha;
    private readonly StatisticalSummaryBuilder _summaryBuilder;
    private readonly HierarchicalCombinedScoringService _combinedScoringService;
    private readonly CalibrationService _calibrationService = new();

    /// <param name="createTests">Builds a fresh suite for each run; tests keep per-run state, so a suite is never reused.</param>
    /// <param name="alpha">Significance threshold.</param>
    /// <param name="withinFamilyCorrelationEstimator">Defaults to independence; the Cauchy combination ignores it.</param>
    /// <param name="withinFamilyCombiningMethod">Defaults to the Cauchy combination (see <see cref="HierarchicalCombinedScoringService()"/>).</param>
    /// <param name="acrossFamilyCorrelationEstimator">Defaults to independence; the Cauchy combination ignores it.</param>
    /// <param name="acrossFamilyCombiningMethod">Defaults to the Cauchy combination.</param>
    public StatisticalAnalysisPipeline(
        Func<IEnumerable<IStatisticalTest>> createTests,
        double alpha = 0.05,
        ICorrelationEstimator? withinFamilyCorrelationEstimator = null,
        PValueCombiningMethod withinFamilyCombiningMethod = PValueCombiningMethod.Cauchy,
        ICorrelationEstimator? acrossFamilyCorrelationEstimator = null,
        PValueCombiningMethod acrossFamilyCombiningMethod = PValueCombiningMethod.Cauchy)
    {
        _createTests = createTests ?? throw new ArgumentNullException(nameof(createTests));
        _alpha = alpha;
        _summaryBuilder = new StatisticalSummaryBuilder(alpha);
        _combinedScoringService = new HierarchicalCombinedScoringService(
            withinFamilyCorrelationEstimator ?? new IndependenceCorrelationEstimator(),
            withinFamilyCombiningMethod,
            acrossFamilyCorrelationEstimator ?? new IndependenceCorrelationEstimator(),
            acrossFamilyCombiningMethod);
    }

    public double Alpha => _alpha;

    /// <summary>
    /// Runs the suite on <paramref name="metrics"/>. Every test's null is the population passed in, so the same
    /// database can get different p-values in runs over different populations.
    /// </summary>
    /// <param name="updateMetrics">
    /// When true, the per-database statistics fields of <paramref name="metrics"/> (tests passed, combined p and q,
    /// per-family summaries) are filled in, as on the branch. A side run such as a calibration run passes false so
    /// it never overwrites the official values.
    /// </param>
    public StatisticalAnalysisRun Run(IReadOnlyList<TransientDatabaseMetrics> metrics, Action<string> warn, bool updateMetrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(warn);

        var searchResults = metrics.ToList();
        var resultsDictionary = searchResults.ToDictionary(m => m.DatabaseName, StringComparer.Ordinal);

        // Compute p-values for each test and database
        var tests = _createTests().ToList();
        var executionResult = new StatisticalTestExecutor(_alpha, warn).Execute(tests, searchResults);
        foreach (var test in executionResult.TestsToRemove)
            tests.Remove(test);

        var statisticalResults = executionResult.Results;
        if (updateMetrics)
            _summaryBuilder.UpdatePerDatabaseMetrics(resultsDictionary, statisticalResults);

        // Benjamini-Hochberg within each test
        var statTestResultCache = new Dictionary<string, List<StatisticalTestResult>>();
        foreach (var grouping in statisticalResults.ToLookup(p => p.Key))
        {
            var pValues = grouping
                .Where(p => !double.IsNaN(p.PValue))
                .ToDictionary(r => r.DatabaseName, r => r.PValue);

            var qValues = MultipleTestingCorrection.BenjaminiHochberg(pValues);
            foreach (var result in grouping)
                if (qValues.TryGetValue(result.DatabaseName, out var qValue))
                    result.QValue = qValue;

            statTestResultCache[grouping.Key] = grouping.ToList();
        }

        var testSummaries = new Dictionary<string, TestSummary>();
        foreach (var testSummary in _summaryBuilder.BuildPerTestSummaries(statisticalResults))
            testSummaries[testSummary.Key] = testSummary.Value;
        foreach (var familySummary in _summaryBuilder.BuildFamilySummaries(statisticalResults))
            testSummaries[familySummary.Key] = familySummary.Value;

        // Combine within and across families
        var combinedScoringResult = _combinedScoringService.BuildCombinedResults(statisticalResults);
        foreach (var cacheEntry in combinedScoringResult.ResultsByCacheKey)
            statTestResultCache[cacheEntry.Key] = cacheEntry.Value;
        if (updateMetrics)
            _combinedScoringService.UpdateMetricsSummary(resultsDictionary, combinedScoringResult);

        var allResults = statTestResultCache.Values.SelectMany(list => list).ToList();
        var calibration = _calibrationService.Calibrate(allResults, _alpha);

        return new StatisticalAnalysisRun(allResults, testSummaries, calibration, tests);
    }
}
