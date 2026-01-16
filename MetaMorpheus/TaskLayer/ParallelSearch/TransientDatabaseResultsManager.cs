#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Statistics;

namespace TaskLayer.ParallelSearch;

/// <summary>
/// Unified manager for transient database analysis and statistical testing
/// Encapsulates analysis workflow with transparent caching
/// Provides a clean interface that hides cache management complexity from callers
/// 
/// WORKFLOW:
/// 1. ProcessDatabase: Runs analysis on each database, caches AggregatedAnalysisResult
/// 2. FinalizeStatisticalAnalysis: Calculates p-values and q-values from all cached results
/// </summary>
public class TransientDatabaseResultsManager
{
    private readonly AnalysisResultAggregator _analysisAggregator;
    private readonly StatisticalAnalysisAggregator _statisticalAggregator;
    private readonly ParallelSearchResultCache<AggregatedAnalysisResult> _analysisCache;

    public const string StatResultFileName = "StatisticalAnalysis_Results.csv";
    public const string TestResultFileName = "Test_Results.csv";
    public const string SummaryResultsFileName = "ManySearchSummary.csv";

    /// <summary>
    /// Gets the number of databases with cached analysis results
    /// </summary>
    public int CachedAnalysisCount => _analysisCache.Count;
    public string SearchSummaryFilePath => _analysisCache.FilePath;
    public int StatisticalTestCount => _statisticalAggregator.TestCount;

    /// <summary>
    /// Gets all cached analysis results
    /// </summary>
    public IReadOnlyDictionary<string, AggregatedAnalysisResult> AllAnalysisResults => _analysisCache.AllResults;

    /// <summary>
    /// Initializes the unified results manager with analysis and optional statistical aggregators
    /// </summary>
    /// <param name="analysisAggregator">Aggregator for computing metrics (counts, organism specificity, etc.)</param>
    /// <param name="statisticalAggregator">Optional aggregator for statistical tests (null to disable stats)</param>
    /// <param name="analysisCachePath">Path to CSV cache file for analysis results</param>
    public TransientDatabaseResultsManager(
        AnalysisResultAggregator analysisAggregator,
        StatisticalAnalysisAggregator statisticalAggregator,
        string analysisCachePath)
    {
        _analysisAggregator = analysisAggregator ?? throw new ArgumentNullException(nameof(analysisAggregator));
        _statisticalAggregator = statisticalAggregator;
        
        _analysisCache = new ParallelSearchResultCache<AggregatedAnalysisResult>(analysisCachePath);
        _analysisCache.InitializeCache();
    }

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
    public bool TryGetCachedResult(string databaseName, out AggregatedAnalysisResult? result)
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
    /// Statistical tests are deferred until FinalizeStatisticalAnalysis
    /// This method is thread-safe and can be called in parallel for multiple databases
    /// </summary>
    /// <param name="context">Analysis context containing PSMs, peptides, and protein groups</param>
    /// <param name="forceRecompute">If true, recomputes even if cached (removes old cache entry)</param>
    /// <returns>Analysis results (either cached or newly computed)</returns>
    public AggregatedAnalysisResult ProcessDatabase(
        TransientDatabaseAnalysisContext context,
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
        var analysisResult = _analysisAggregator.RunAnalysis(context);

        // Cache analysis results immediately (thread-safe)
        _analysisCache.AddAndWrite(analysisResult);

        return analysisResult;
    }

    /// <summary>
    /// Finalizes statistical analysis by computing p-values and q-values across ALL databases
    /// This applies multiple testing correction (Benjamini-Hochberg) and computes combined p-values
    /// Must be called after all databases have been processed
    /// </summary>
    /// <param name="allResults">All analysis results (optional - uses cached results if not provided)</param>
    /// <returns>List of statistical results with both p-values and q-values, or empty list if stats disabled</returns>
    public List<StatisticalResult> FinalizeStatisticalAnalysis(List<AggregatedAnalysisResult>? allResults = null)
    {
        // Use provided results or get all from cache
        allResults ??= _analysisCache.AllResults.Values.ToList();

        if (allResults.Count == 0)
        {
            Console.WriteLine("Warning: No results to finalize statistical analysis");
            return new List<StatisticalResult>();
        }

        // Compute p-values and q-values across all databases in one pass
        var statisticalResults = _statisticalAggregator.FinalizeAnalysis(allResults);

        return statisticalResults;
    }

    /// <summary>
    /// Writes final analysis results to CSV file
    /// Should be called after all processing is complete
    /// </summary>
    /// <param name="outputPath">Path to output CSV file</param>
    public void WriteSearchSummaryCacheResults(string outputPath)
    {
        _analysisCache.WriteAllToFile(outputPath);
    }

    /// <summary>
    /// Writes statistical results to CSV file in wide format
    /// Only available if statistical analysis is enabled
    /// </summary>
    /// <param name="results">Statistical results to write (from FinalizeStatisticalAnalysis)</param>
    /// <param name="outputPath">Path to output CSV file</param>
    public void WriteStatisticalResults(List<StatisticalResult> results, string outputPath)
    {
        if (_statisticalAggregator == null)
        {
            Console.WriteLine("Warning: Statistical analysis not enabled, cannot write results");
            return;
        }

        StatisticalAnalysisAggregator.WriteResultsToCsv(results, outputPath);
        string outDir = Path.GetDirectoryName(outputPath) ?? ".";
        _statisticalAggregator.WriteTestSummaryToCsv(Path.Combine(outDir, TestResultFileName));
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
