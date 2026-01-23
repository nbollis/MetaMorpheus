#nullable enable
using EngineLayer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.IO;
using TaskLayer.ParallelSearch.Statistics;

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

    public const string StatResultFileName = "StatisticalAnalysis_Results.csv";
    public const string SummaryResultsFileName = "ManySearchSummary.csv";
    public const string TestSummaryFileName = "Test_Results.csv";

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
    public List<StatisticalTestResult> StatisticalTestResultList => _statTestResultCache.Values.SelectMany(list => list).ToList();

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
        statTestFile.WriteResults(statTestOutPath);
        outputFiles.Add(statTestOutPath);

        // Write Test Summary File
        var testSummaryFile = new TestSummaryResultFile()
        {
            Results = TestSummaryResultsList
        };
        string testSummaryOutPath = Path.Combine(outputDirectory, TestSummaryFileName);
        testSummaryFile.WriteResults(testSummaryOutPath);
        outputFiles.Add(testSummaryOutPath);

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

        // Compute p-values for each test and database 
        var statisticalResults = ComputePValuesForAllDatabases(_analysisCache.AllResultsList);

        // Cache and apply multiple testing correction
        foreach (var testMetricGrouping in statisticalResults.GroupBy(p => p.Key))
        {
            var pValues = testMetricGrouping
                .Where(p => !double.IsNaN(p.PValue))
                .ToDictionary(r => r.DatabaseName, r => r.PValue);

            var qValues = MultipleTestingCorrection.BenjaminiHochberg(pValues);

            // Update q-values in results
            foreach (var result in testMetricGrouping)
                if (qValues.TryGetValue(result.DatabaseName, out var qValue))
                    result.QValue = qValue;

            // Store results
            _statTestResultCache[testMetricGrouping.Key] = testMetricGrouping.ToList();

            // Collect summary information
            _testSummaryCache[$"{testMetricGrouping.Key}"] = new TestSummary
            {
                TestName = testMetricGrouping.First().TestName,
                MetricName = testMetricGrouping.First().MetricName,
                ValidDatabases = testMetricGrouping.Count(p => !double.IsNaN(p.PValue)),
                SignificantByP = testMetricGrouping.Count(p => !double.IsNaN(p.PValue) && p.PValue <= _alpha),
                SignificantByQ = testMetricGrouping.Count(p => !double.IsNaN(p.QValue) && p.QValue <= _alpha)
            };
        }

        ApplyCombinedPValues(statisticalResults);

        _finalized = true;
    }

    /// <summary>
    /// Compute p-values for all tests across all databases
    /// Tests are only run if they can execute on the provided data
    /// </summary>
    private List<StatisticalTestResult> ComputePValuesForAllDatabases(List<TransientDatabaseMetrics> searchResults)
    {
        int resultCount = searchResults.Count;
        var statisticalResults = new ConcurrentBag<StatisticalTestResult>();
        var toRemove = new ConcurrentBag<IStatisticalTest>();

        // Run each test on all databases
        Parallel.ForEach(_tests, test =>
        {
            if (!test.CanRun(searchResults))
            {
                Warn($"Skipping {test.TestName} - {test.MetricName}: insufficient data");
                toRemove.Add(test);
                return;
            }

            try
            {
                Console.WriteLine($"Running {test.TestName} - {test.MetricName} on {resultCount} databases...");
                var pValues = test.RunTest(searchResults, _alpha);

                if (resultCount != pValues.Count)
                    Debugger.Break();

                // Reject tests if they are bad (many sig findings). 
                if (test.SignificantResults >= resultCount / 10)
                {
                    toRemove.Add(test);
                    Warn($"Removing {test.TestName} - {test.MetricName} due to excessive (>=10%) significant p-values.");
                    return;
                }

                // Reject tests if they are bad (many non-significant findings).
                if (test.SignificantResults == 0)
                {
                    toRemove.Add(test);
                    Warn($"Removing {test.TestName} - {test.MetricName} due to no significant values.");
                    return;
                }

                // Convert p-values to StatisticalTestResult format
                HashSet<TransientDatabaseMetrics> unmapped = searchResults.ToHashSet();
                foreach (var (dbName, pValue) in pValues)
                {
                    var result = unmapped.First(r => r.DatabaseName == dbName);
                    unmapped.Remove(result);

                    var testStat = test.GetTestValue(result);
                    statisticalResults.Add(new StatisticalTestResult
                    {
                        DatabaseName = dbName,
                        TestName = test.TestName,
                        MetricName = test.MetricName,
                        PValue = pValue,
                        QValue = double.NaN, // Will be filled by Benjamini-Hochberg
                        TestStatistic = testStat
                    });
                }
            }
            catch (Exception ex)
            {
                var stackTrace = new StackTrace(ex, true);
                var frame = stackTrace.GetFrame(0);
                var lineNumber = frame?.GetFileLineNumber() ?? 0;
                var fileName = frame?.GetFileName() ?? "Unknown";

                Warn($"Error running {test.TestName} - {test.MetricName}: {ex.Message} at {fileName}:line {lineNumber}");
                toRemove.Add(test);
            }
        });

        foreach (var test in toRemove)
        {
            _tests.Remove(test);
        }

        // Updates the StatisticalTestsPassed count in all analysis results
        var lookupTable = _analysisCache.AllResultsDictionary;
        foreach (var dbGrouping in statisticalResults.GroupBy(p => p.DatabaseName))
        {
            if (!lookupTable.TryGetValue(dbGrouping.Key, out var analysisResult))
                continue;

            int testsRun = dbGrouping.Count(p => !double.IsNaN(p.PValue));
            int testsPassed = dbGrouping.Count(r => r.IsSignificant(_alpha));

            analysisResult.StatisticalTestsRun = testsRun;
            analysisResult.StatisticalTestsPassed = testsPassed;
            analysisResult.TestPassedRatio = testsRun > 0 ? testsPassed / (double)testsRun : 0.0;
        }

        return statisticalResults.ToList();
    }

    private void ApplyCombinedPValues(List<StatisticalTestResult> statResults)
    {
        var combinedPValues = MetaAnalysis.CombinePValuesAcrossTests(statResults);
        var combinedQValues = MultipleTestingCorrection.BenjaminiHochberg(combinedPValues);

        _statTestResultCache["Combined"] = new List<StatisticalTestResult>();

        // Add combined results
        foreach (var dbName in combinedPValues.Keys)
        {
            _statTestResultCache["Combined"].Add(new StatisticalTestResult
            {
                DatabaseName = dbName,
                TestName = "Combined",
                MetricName = "All",
                PValue = combinedPValues[dbName],
                QValue = combinedQValues[dbName]
            });
        }
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
