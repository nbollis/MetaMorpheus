#nullable enable
using System;
using System.Collections.Generic;

namespace TaskLayer.ParallelSearch.Analysis;


/// <summary>
/// Aggregates results from multiple analyzers into a single result object
/// compatible with ITransientDbResults for caching
/// </summary>
public class MetricAggregator
{
    private readonly List<IMetricCollector> _analyzers = [];

    public MetricAggregator(IEnumerable<IMetricCollector> analyzers)
    {
        _analyzers.AddRange(analyzers);
    }

    /// <summary>
    /// Runs all analyzers on the context and produces an aggregated result
    /// </summary>
    public TransientDatabaseMetrics RunAnalysis(TransientDatabaseContext context)
    {
        var result = new TransientDatabaseMetrics(context.DatabaseName);

        foreach (var analyzer in _analyzers)
        {
            try
            {
                if (!analyzer.CanCollectData(context))
                {
                    // Skip this analyzer or log warning
                    Console.WriteLine($"Skipping analyzer {analyzer.AnalyzerName} due to insufficient data.");
                    continue;
                }

                var analysisResults = analyzer.CollectData(context);

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
}