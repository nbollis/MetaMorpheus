#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;

namespace TaskLayer;

/// <summary>
/// Helper class for thread-safe reading and writing of database search results to CSV
/// Manages caching, validation, and tracking of database search results
/// </summary>
public class ParallelSearchResultCache<TDbResults> where TDbResults : ITransientDbResults
{
    private readonly object _writeLock = new();
    private readonly object _cacheLock = new();
    private readonly HashSet<string> _completedDatabases = new();
    private readonly ConcurrentDictionary<string, TDbResults> _databaseResults = new();
    private readonly string _csvFilePath;

    /// <summary>
    /// Helper class for thread-safe reading and writing of database search results to CSV
    /// Manages caching, validation, and tracking of database search results
    /// </summary>
    public ParallelSearchResultCache(string csvFilePath)
    {
        _csvFilePath = csvFilePath;
    }

    /// <summary>
    /// Gets the number of completed databases
    /// </summary>
    public int Count
    {
        get
        {
            lock (_cacheLock)
            {
                return _completedDatabases.Count;
            }
        }
    }

    /// <summary>
    /// Gets all results currently in cache
    /// </summary>
    public IReadOnlyDictionary<string, TDbResults> AllResults => _databaseResults;


    #region Dictionary Like Methods

    public bool Add(TDbResults? result)
    {
        if (result is null) return false;

        string key = result.DatabaseName;


        lock (_cacheLock)
        {
            if (!_completedDatabases.Add(key))
                return false;

            _databaseResults[key] = result;
        }
        return true;
    }

    public bool Remove(TDbResults? result)
    {
        if (result is null) return false;

        string key = result.DatabaseName;
        if (!_databaseResults.ContainsKey(key))
            return false;

        lock (_cacheLock)
        {
            if (!_completedDatabases.Contains(key))
                return false;

            _completedDatabases.Remove(key);
            _databaseResults.Remove(key, out _);
        }
        return true;
    }

    public bool AddAndWrite(TDbResults? result)
    {
        if (result is null) return false;

        if (!Add(result))
            return false;

        AppendToFile(result);
        return true;
    }

    public bool TryGetValue(string key, out TDbResults? result) => _databaseResults.TryGetValue(key, out result);

    /// <summary>
    /// Checks if a database result already exists in cache
    /// </summary>
    public bool Contains(string databaseName)
    {
        lock (_cacheLock)
        {
            return _completedDatabases.Contains(databaseName);
        }
    }

    #endregion

    #region IO 

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

                var results = csv.GetRecords<TDbResults>().ToList();

                lock (_cacheLock)
                {
                    foreach (var result in results)
                    {
                        _completedDatabases.Add(result.DatabaseName);
                        _databaseResults[result.DatabaseName] = result;
                    }
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
            }
        }
    }

    /// <summary>
    /// Writes a single result to the CSV file in a thread-safe manner and tracks it
    /// </summary>
    public void AppendToFile(TDbResults result)
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
                csv.WriteHeader<TDbResults>();
                csv.NextRecord();
            }

            csv.WriteRecord(result);
            csv.NextRecord();
            csv.Flush();
        }
    }

    public void WriteAllToFile(string outputPath)
    {
        lock (_writeLock)
        {
            using var writer = new StreamWriter(outputPath);
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            });
            csv.WriteHeader<TDbResults>();
            csv.NextRecord();
            foreach (var result in _databaseResults.Values)
            {
                csv.WriteRecord(result);
                csv.NextRecord();
            }
            csv.Flush();
        }
    }

    #endregion
}