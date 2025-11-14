#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using EngineLayer;

namespace TaskLayer;

/// <summary>
/// Helper class for thread-safe reading and writing of database search results to CSV
/// Manages caching, validation, and tracking of database search results
/// </summary>
public class ManySearchResultCache
{
    private readonly string _csvFilePath;
    private readonly object _writeLock = new object();
    private readonly HashSet<string> _completedDatabases = new();
    private readonly object _cacheLock = new object();
    private readonly ConcurrentDictionary<string, TransientDatabaseSearchResults> _databaseResults = new();
    private int _completedCount = 0;

    public ManySearchResultCache(string csvFilePath)
    {
        _csvFilePath = csvFilePath;
    }

    /// <summary>
    /// Gets the number of completed databases
    /// </summary>
    public int CompletedCount
    {
        get
        {
            lock (_cacheLock)
            {
                return _completedCount;
            }
        }
    }

    /// <summary>
    /// Gets all results currently in cache
    /// </summary>
    public IReadOnlyDictionary<string, TransientDatabaseSearchResults> AllResults => _databaseResults;

    /// <summary>
    /// Loads cached results from the CSV file if it exists and returns both the list and count
    /// </summary>
    public void InitializeCache()
    {
        if (!File.Exists(_csvFilePath))
            return;

        try
        {
            lock (_writeLock)
            {
                using var reader = new StreamReader(_csvFilePath);
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    MissingFieldFound = null
                });

                var results = csv.GetRecords<TransientDatabaseSearchResults>().ToList();

                lock (_cacheLock)
                {
                    foreach (var result in results)
                    {
                        _completedDatabases.Add(result.DatabaseName);
                        _databaseResults[result.DatabaseName] = result;
                    }
                    _completedCount = results.Count;
                }
            }
        }
        catch (Exception)
        {
            // If there's an error reading the cache, start fresh
            lock (_cacheLock)
            {
                _completedDatabases.Clear();
                _databaseResults.Clear();
                _completedCount = 0;
            }
        }
    }

    /// <summary>
    /// Checks if a database result already exists in cache
    /// </summary>
    public bool HasResult(string databaseName)
    {
        lock (_cacheLock)
        {
            return _completedDatabases.Contains(databaseName);
        }
    }

    /// <summary>
    /// Checks if output files exist and are complete for a given database
    /// </summary>
    public bool ValidateOutputFiles(string outputFolder, string dbName, bool doParsimony)
    {
        string dbOutputFolder = Path.Combine(outputFolder, dbName);
        
        // Check for results file
        string resultsFile = Path.Combine(dbOutputFolder, "results.txt");
        if (!File.Exists(resultsFile))
            return false;

        // Check for transient-specific PSMs
        string transientPsmFile = Path.Combine(dbOutputFolder,
            $"{dbName}_All{EngineLayer.GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s.{EngineLayer.GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        if (!File.Exists(transientPsmFile))
            return false;

        // Check for transient-specific peptides
        string transientPeptideFile = Path.Combine(dbOutputFolder,
            $"{dbName}_All{EngineLayer.GlobalVariables.AnalyteType}s.{EngineLayer.GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        if (!File.Exists(transientPeptideFile))
            return false;

        // Check for transient-specific protein groups if parsimony is enabled
        if (doParsimony)
        {
            string transientProteinFile = Path.Combine(dbOutputFolder,
                $"{dbName}_All{EngineLayer.GlobalVariables.AnalyteType.GetBioPolymerLabel()}Groups.tsv");
            if (!File.Exists(transientProteinFile))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Writes a single result to the CSV file in a thread-safe manner and tracks it
    /// </summary>
    public void WriteResult(TransientDatabaseSearchResults result)
    {
        lock (_writeLock)
        {
            bool fileExists = File.Exists(_csvFilePath);

            using var stream = new FileStream(_csvFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream);
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = !fileExists
            });

            // Write header if file is new
            if (!fileExists)
            {
                csv.WriteHeader<TransientDatabaseSearchResults>();
                csv.NextRecord();
            }

            csv.WriteRecord(result);
            csv.NextRecord();
            csv.Flush();

            lock (_cacheLock)
            {
                _completedDatabases.Add(result.DatabaseName);
                _databaseResults[result.DatabaseName] = result;
                _completedCount++;
            }
        }
    }

    /// <summary>
    /// Adds or updates a result in memory without writing to disk
    /// </summary>
    public void TrackResult(string databaseName, TransientDatabaseSearchResults result)
    {
        lock (_cacheLock)
        {
            _databaseResults[databaseName] = result;
        }
    }
}