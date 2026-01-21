#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskLayer.ParallelSearch.Analysis.ExternalData;

/// <summary>
/// Analyzer that injects external data into the analysis results
/// This allows data from de novo searches, external databases, or other sources
/// by implementing IExternalDataSource and providing it in the context
/// </summary>
public abstract class ExternalDataAnalyzer : ITransientDatabaseAnalyzer
{
    public abstract string AnalyzerName { get; }
    public abstract string ExpectedSourceName { get; }

    protected List<string> RequiredColumns = new();

    public IEnumerable<string> GetOutputColumns()
    {
        // We don't know columns ahead of time - they're dynamic based on external data
        return RequiredColumns;
    }

    public Dictionary<string, object> Analyze(TransientDatabaseAnalysisContext context)
    {
        var results = new Dictionary<string, object>();

        // If no external data source, return empty results
        if (context.ExternalDataSource.Count == 0)
            return results;

        var sourceToUse = context.ExternalDataSource
            .FirstOrDefault(src => src.SourceName.Equals(ExpectedSourceName, StringComparison.OrdinalIgnoreCase));

        // If specified external data source not found, return empty results
        if (sourceToUse == null)
            return results;

        // Get all metrics from external data source
        var externalMetrics = sourceToUse.GetMetrics(context.DatabaseName);

        // Prefix external metrics with the source name to avoid collisions
        foreach (var kvp in externalMetrics)
        {
            string prefixedKey = $"{sourceToUse.SourceName}_{kvp.Key}";
            results[prefixedKey] = kvp.Value;
            RequiredColumns.Add(prefixedKey);
        }

        return results;
    }

    public virtual bool CanAnalyze(TransientDatabaseAnalysisContext context)
    {
        // Can always run, even if there's no external data (will just return empty)
        return true;
    }
}