#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;

namespace TaskLayer;

/// <summary>
/// Helper class for thread-safe reading and writing of database search results to CSV
/// </summary>
public class ManySearchResultCache
{
    private readonly string _csvFilePath;
    private readonly object _writeLock = new object();
    private readonly HashSet<string> _completedDatabases = new();
    private readonly object _cacheLock = new object();

    public ManySearchResultCache(string csvFilePath)
    {
        _csvFilePath = csvFilePath;
    }

    /// <summary>
    /// Loads cached results from the CSV file if it exists
    /// </summary>
    public List<TransientDatabaseSearchResults> LoadCachedResults()
    {
        var results = new List<TransientDatabaseSearchResults>();

        if (!File.Exists(_csvFilePath))
            return results;

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

                results = csv.GetRecords<TransientDatabaseSearchResults>().ToList();

                lock (_cacheLock)
                {
                    foreach (var result in results)
                    {
                        _completedDatabases.Add(result.DatabaseName);
                    }
                }
            }
        }
        catch (Exception e)
        {
            // If there's an error reading the cache, start fresh
            results.Clear();
            lock (_cacheLock)
            {
                _completedDatabases.Clear();
            }
        }

        return results;
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
    /// Writes a single result to the CSV file in a thread-safe manner
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
            }
        }
    }
}