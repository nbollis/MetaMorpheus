#nullable enable
using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
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
    /// Runs all analyzers on the context and produces an aggregated result
    /// </summary>
    public AggregatedAnalysisResult RunAnalysis(TransientDatabaseAnalysisContext context)
    {
        var result = new AggregatedAnalysisResult(context.DatabaseName);

        foreach (var analyzer in _analyzers)
        {
            try
            {
                if (!analyzer.CanAnalyze(context))
                {
                    // Skip this analyzer or log warning
                    Console.WriteLine($"Skipping analyzer {analyzer.AnalyzerName} due to insufficient data.");
                    continue;
                }

                var analysisResults = analyzer.Analyze(context);

                // Merge results into the aggregated result
                foreach (var kvp in analysisResults)
                {
                    result.Results[kvp.Key] = kvp.Value is double.NaN ? 0 : kvp.Value;
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
        var result = new AggregatedAnalysisResult(cachedResult.DatabaseName)
        {
            Results = new Dictionary<string, object>(cachedResult.Results)
        };

        // Run only the additional analyzers
        foreach (var analyzer in additionalAnalyzers)
        {
            try
            {
                if (!analyzer.CanAnalyze(context))
                {
                    Console.WriteLine($"Skipping analyzer {analyzer.AnalyzerName} due to insufficient data.");
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