#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskLayer.ParallelSearchTask.Analysis;


/// <summary>
/// Aggregates results from multiple analyzers into a single result object
/// compatible with ITransientDbResults for caching
/// </summary>
public class AnalysisResultAggregator
{
    private readonly List<ITransientDatabaseAnalyzer> _analyzers = [];

    public AnalysisResultAggregator(IEnumerable<ITransientDatabaseAnalyzer> analyzers)
    {
        _analyzers.AddRange(analyzers);
    }

    /// <summary>
    /// Gets all column headers from all registered analyzers
    /// </summary>
    public IEnumerable<string> GetAllOutputColumns()
    {
        // Always include base columns
        yield return nameof(ITransientDbResults.DatabaseName);

        // Add columns from each analyzer
        foreach (var analyzer in _analyzers)
        {
            foreach (var column in analyzer.GetOutputColumns())
            {
                yield return column;
            }
        }
    }

    /// <summary>
    /// Runs all analyzers on the context and produces an aggregated result
    /// </summary>
    public AggregatedAnalysisResult RunAnalysis(TransientDatabaseAnalysisContext context)
    {
        var result = new AggregatedAnalysisResult
        {
            DatabaseName = context.DatabaseName
        };

        foreach (var analyzer in _analyzers)
        {
            try
            {
                if (!analyzer.CanAnalyze(context))
                {
                    // Skip this analyzer or log warning
                    continue;
                }

                var analysisResults = analyzer.Analyze(context);

                // Merge results into the aggregated result
                foreach (var kvp in analysisResults)
                {
                    result.Results[kvp.Key] = kvp.Value;
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with other analyzers
                result.Errors.Add($"{analyzer.AnalyzerName}: {ex.Message}");
            }
        }

        // After running all analyzers, populate the typed properties for CSV serialization
        result.PopulatePropertiesFromResults();

        return result;
    }

    /// <summary>
    /// Re-runs additional (non-core) analyzers on cached results
    /// Core analyzer results are preserved from the cache
    /// </summary>
    public AggregatedAnalysisResult ReAnalyze(AggregatedAnalysisResult cachedResult, 
        TransientDatabaseAnalysisContext context,
        IEnumerable<ITransientDatabaseAnalyzer> additionalAnalyzers)
    {
        // Start with the cached results
        var result = new AggregatedAnalysisResult
        {
            DatabaseName = cachedResult.DatabaseName,
            Results = new Dictionary<string, object>(cachedResult.Results)
        };

        // Run only the additional analyzers
        foreach (var analyzer in additionalAnalyzers)
        {
            try
            {
                if (!analyzer.CanAnalyze(context))
                {
                    continue;
                }

                var analysisResults = analyzer.Analyze(context);

                // Merge new results (don't overwrite core metrics)
                foreach (var kvp in analysisResults)
                {
                    // Only add new metrics, don't overwrite existing ones from core analyzers
                    if (!result.Results.ContainsKey(kvp.Key))
                    {
                        result.Results[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{analyzer.AnalyzerName}: {ex.Message}");
            }
        }

        // Update typed properties
        result.PopulatePropertiesFromResults();

        return result;
    }
}