#nullable enable
using EngineLayer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Analysis.Collectors;
using TaskLayer.ParallelSearch.IO;
using TaskLayer.ParallelSearch.Statistics;
using TaskLayer.ParallelSearch.Statistics.Calibration;
using TaskLayer.ParallelSearch.Statistics.IsolationForest;

namespace TaskLayer.ParallelSearch;

/// <summary>
/// Unified manager for transient database analysis and statistical testing
/// Encapsulates analysis workflow with transparent caching
/// Provides a clean interface that hides cache management complexity from callers
/// 
/// WORKFLOW:
/// 1. ProcessDatabase: Runs analysis on each database, caches TransientDatabaseMetrics
/// 2. RunStatisticalAnalysis: Calculates p-values and q-values from all cached results
/// </summary>
public class TransientDatabaseResultsManager
{
    private bool _finalized;
    private readonly double _alpha;
    private readonly MetricAggregator _metricAggregator;
    private readonly ParallelSearchResultCache _analysisCache;
    private readonly List<IStatisticalTest> _tests;
    private readonly StatisticalTestExecutor _testExecutor;
    private readonly StatisticalSummaryBuilder _summaryBuilder;
    private readonly HierarchicalCombinedScoringService _combinedScoringService;
    private readonly CalibrationService _calibrationService;
    private readonly string _outputFolder;

    public const string StatResultFileName = "StatisticalAnalysis_Results.csv";
    public const string SummaryResultsFileName = "ManySearchSummary.csv";
    public const string TestSummaryFileName = "Test_Results.csv";
    public const string CalibrationReportFileName = "CalibrationReport.txt";

    /// <summary>
    /// Gets the number of databases with cached analysis results
    /// </summary>
    public int CachedAnalysisCount => _analysisCache.Count;
    public int StatisticalTestCount => _tests.Count;
    
    /// <summary>
    /// Gets all cached analysis results
    /// </summary>
    public Dictionary<string, TransientDatabaseMetrics> TransientDatabaseMetricsDictionary => _analysisCache.AllResultsDictionary;
    public List<TestSummary> TestSummaryResultsList => _testSummaryCache.Values.ToList();
    public List<StatisticalTestResult> StatisticalTestResultList => _materializedStatResults;
    public CalibrationResult? CalibrationResult { get; private set; }

    /// <summary>
    /// Initializes the unified results manager with analysis and optional statistical aggregators
    /// </summary>
    /// <param name="metricAggregator">Aggregator for computing metrics (counts, organism specificity, etc.)</param>
    /// <param name="tests">List of statistical tests to run</param>
    /// <param name="analysisCachePath">Path to CSV cache file for analysis results</param>
    /// <param name="alpha"></param>
    public TransientDatabaseResultsManager(
        MetricAggregator metricAggregator,
        List<IStatisticalTest> tests,
        string analysisCachePath, double alpha = 0.05)
    {
        _alpha = alpha;
        _metricAggregator = metricAggregator ?? throw new ArgumentNullException(nameof(metricAggregator));
        _tests = tests ?? throw new ArgumentNullException(nameof(tests));
        _testExecutor = new StatisticalTestExecutor(alpha, Warn);
        _summaryBuilder = new StatisticalSummaryBuilder(alpha);
        _combinedScoringService = new HierarchicalCombinedScoringService();
        _calibrationService = new CalibrationService();
        _outputFolder = Path.GetDirectoryName(analysisCachePath) ?? ".";

        _analysisCache = new ParallelSearchResultCache(analysisCachePath);
        _analysisCache.InitializeCache();
    }

    public List<string> WriteAllResults(string outputDirectory)
    {
        if (!_finalized)
            throw new MetaMorpheusException("Write Results Failed.", new InvalidOperationException("Cannot write results before finalizing analysis."));

        var outputFiles = new List<string>();

        // Write analysis summary results
        string summaryPath = Path.Combine(outputDirectory, SummaryResultsFileName);
        _analysisCache.WriteAllToFile(summaryPath);
        outputFiles.Add(summaryPath);

        // Write statistical results if enabled
        var statTestFile = new StatisticalTestResultFile(_alpha)
        {
            Results = StatisticalTestResultList
        };

        string statTestOutPath = Path.Combine(outputDirectory, StatResultFileName);
        statTestFile.WriteResults(statTestOutPath, _analysisCache.AllResultsDictionary);
        outputFiles.Add(statTestOutPath);

        // Write Test Summary File
        var testSummaryFile = new TestSummaryResultFile()
        {
            Results = TestSummaryResultsList.OrderByDescending(p => p.ValidDatabases)
                .ThenByDescending(p => p.SignificantByP)
                .ThenByDescending(p => p.SignificantByQ)
                .ThenBy(p => p.TestName)
                .ThenBy(p => p.MetricName)
                .ToList()
        };
        string testSummaryOutPath = Path.Combine(outputDirectory, TestSummaryFileName);
        testSummaryFile.WriteResults(testSummaryOutPath);
        outputFiles.Add(testSummaryOutPath);

        if (CalibrationResult != null)
        {
            string calOutPath = Path.Combine(outputDirectory, CalibrationReportFileName);
            CalibrationReportWriter.WriteReport(CalibrationResult, calOutPath);
            outputFiles.Add(calOutPath);
        }

        return outputFiles;
    }

    #region Search Result Analysis Cache Methods

    /// <summary>
    /// Checks if a database has cached analysis results
    /// </summary>
    /// <param name="databaseName">Name of the database to check</param>
    /// <returns>True if results are cached, false otherwise</returns>
    public bool HasCachedResults(string databaseName)
    {
        return _analysisCache.Contains(databaseName);
    }

    /// <summary>
    /// Attempts to retrieve cached analysis results for a database
    /// </summary>
    /// <param name="databaseName">Name of the database</param>
    /// <param name="result">Cached result if found, null otherwise</param>
    /// <returns>True if results were found in cache</returns>
    public bool TryGetCachedResult(string databaseName, out TransientDatabaseMetrics? result)
    {
        return _analysisCache.TryGetValue(databaseName, out result);
    }

    /// <summary>
    /// Removes a database's results from cache
    /// Use this when overwriting results for a specific database
    /// </summary>
    /// <param name="databaseName">Name of the database to remove</param>
    /// <returns>True if the database was in cache and removed</returns>
    public bool RemoveFromCache(string databaseName)
    {
        if (!_analysisCache.TryGetValue(databaseName, out var result) || result == null)
            return false;

        return _analysisCache.Remove(result);
    }

    /// <summary>
    /// Processes a single database: runs analysis and caches results
    /// Statistical tests are deferred until RunStatisticalAnalysis
    /// This method is thread-safe and can be called in parallel for multiple databases
    /// </summary>
    /// <param name="context">Analysis context containing PSMs, peptides, and protein groups</param>
    /// <param name="forceRecompute">If true, recomputes even if cached (removes old cache entry)</param>
    /// <returns>Analysis results (either cached or newly computed)</returns>
    public TransientDatabaseMetrics ProcessDatabase(
        TransientDatabaseContext context,
        bool forceRecompute = false)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        string dbName = context.DatabaseName;

        // Check cache first (unless forcing recompute)
        if (!forceRecompute && TryGetCachedResult(dbName, out var cachedResult) && cachedResult != null)
        {
            return cachedResult;
        }

        // If forcing recompute, remove old cache entry
        if (forceRecompute)
        {
            RemoveFromCache(dbName);
        }

        // Run analysis
        var analysisResult = _metricAggregator.RunAnalysis(context);

        // Cache analysis results immediately (thread-safe)
        _analysisCache.AddAndWrite(analysisResult);

        return analysisResult;
    }

    /// <summary>
    /// Gets summary statistics about cache status
    /// </summary>
    public CacheSummary GetCacheSummary(List<string> allDatabaseNames)
    {
        int cached = allDatabaseNames.Count(HasCachedResults);
        int needsProcessing = allDatabaseNames.Count - cached;

        return new CacheSummary
        {
            TotalDatabases = allDatabaseNames.Count,
            CachedDatabases = cached,
            DatabasesNeedingProcessing = needsProcessing,
            CacheHitRate = allDatabaseNames.Count > 0
                ? (double)cached / allDatabaseNames.Count
                : 0.0
        };
    }

    #endregion

    #region Statistical Tests 

    private readonly Dictionary<string, List<StatisticalTestResult>> _statTestResultCache = new();
    private readonly Dictionary<string, TestSummary> _testSummaryCache = new();
    private List<StatisticalTestResult> _materializedStatResults = null!;

    /// <summary>
    /// Finalizes statistical analysis by computing p-values and q-values across ALL databases
    /// This applies multiple testing correction (Benjamini-Hochberg) and computes combined p-values
    /// Must be called after all databases have been processed
    /// </summary>
    /// <returns>List of statistical results with both p-values and q-values, or empty list if stats disabled</returns>
    public void RunStatisticalAnalysis()
    {
        if (_finalized)
            throw new MetaMorpheusException("Finalizing Analysis Failed.", new InvalidOperationException("Analysis has already been finalized."));

        var resultsDictionary = _analysisCache.AllResultsDictionary;

        if (resultsDictionary.Count == 0)
            throw new MetaMorpheusException("Finalizing Analysis Failed.", new InvalidOperationException("No analysis results available to finalize."));

        // Backfill protein group metrics from TSV files if the collector didn't run (re-run scenario)
        var backfill = new ProteinGroupTsvBackfillService();
        if (backfill.BackfillIfNeeded(_outputFolder, _analysisCache.AllResultsList))
            _analysisCache.WriteAllToFile();

        // Compute p-values for each test and database 
        var statisticalResults = ComputePValuesForAllDatabases(_analysisCache.AllResultsList);

        // Build grouped lookups once, reuse across BH correction and summaries
        var byKey = statisticalResults.ToLookup(p => p.Key);

        // Cache and apply multiple testing correction
        foreach (var grouping in byKey)
        {
            var pValues = grouping
                .Where(p => !double.IsNaN(p.PValue))
                .ToDictionary(r => r.DatabaseName, r => r.PValue);

            var qValues = MultipleTestingCorrection.BenjaminiHochberg(pValues);

            // Update q-values in results
            foreach (var result in grouping)
                if (qValues.TryGetValue(result.DatabaseName, out var qValue))
                    result.QValue = qValue;

            // Store results
            _statTestResultCache[grouping.Key] = grouping.ToList();
        }

        foreach (var testSummary in _summaryBuilder.BuildPerTestSummaries(statisticalResults))
        {
            _testSummaryCache[testSummary.Key] = testSummary.Value;
        }

        foreach (var familySummary in _summaryBuilder.BuildFamilySummaries(statisticalResults))
        {
            _testSummaryCache[familySummary.Key] = familySummary.Value;
        }

        ApplyCombinedPValues(statisticalResults);

        // Materialize the combined flat list once, after all results (including combined) are cached
        _materializedStatResults = _statTestResultCache.Values.SelectMany(list => list).ToList();

        CalibrationResult = _calibrationService.Calibrate(_materializedStatResults, _alpha);

        RunAnomalyDetection();

        _finalized = true;
    }

    /// <summary>
    /// Compute p-values for all tests across all databases
    /// Tests are only run if they can execute on the provided data
    /// </summary>
    private List<StatisticalTestResult> ComputePValuesForAllDatabases(List<TransientDatabaseMetrics> searchResults)
    {
        var executionResult = _testExecutor.Execute(_tests, searchResults);

        foreach (var test in executionResult.TestsToRemove)
        {
            _tests.Remove(test);
        }

        _summaryBuilder.UpdatePerDatabaseMetrics(_analysisCache.AllResultsDictionary, executionResult.Results);

        return executionResult.Results;
    }

    private void ApplyCombinedPValues(List<StatisticalTestResult> statResults)
    {
        var combinedScoringResult = _combinedScoringService.BuildCombinedResults(statResults);
        foreach (var cacheEntry in combinedScoringResult.ResultsByCacheKey)
        {
            _statTestResultCache[cacheEntry.Key] = cacheEntry.Value;
        }

        _combinedScoringService.UpdateMetricsSummary(_analysisCache.AllResultsDictionary, combinedScoringResult);
    }

    private void RunAnomalyDetection()
    {
        if (_analysisCache.AllResultsDictionary.Count < 2)
            return;

        var anomalyService = new AnomalyDetectionService();
        var anomalyResult = anomalyService.Run(
            _analysisCache.AllResultsDictionary,
            StatisticalTestResultList);

        anomalyService.UpdateMetrics(_analysisCache.AllResultsDictionary, anomalyResult);
    }

    #endregion

    #region Events

    public static event EventHandler<StringEventArgs>? WarnHandler;

    public static event EventHandler<StringEventArgs>? LogHandler;

    protected static void Warn(string v)
    {
        WarnHandler?.Invoke(null, new StringEventArgs(v, null));
    }

    protected void Log(string v, List<string> nestedIds)
    {
        LogHandler?.Invoke(this, new StringEventArgs(v, nestedIds));
    }

    #endregion
}

/// <summary>
/// Summary of cache status for reporting
/// </summary>
public class CacheSummary
{
    public int TotalDatabases { get; init; }
    public int CachedDatabases { get; init; }
    public int DatabasesNeedingProcessing { get; init; }
    public double CacheHitRate { get; init; }

    public override string ToString()
    {
        return $"Cache Status: {CachedDatabases}/{TotalDatabases} databases cached " +
               $"({CacheHitRate:P1} hit rate), {DatabasesNeedingProcessing} need processing";
    }
}
