#nullable enable
using System.Collections.Generic;

namespace TaskLayer.ParallelSearchTask.Analysis;

/// <summary>
/// Interface for post-hoc analysis of transient database search results
/// Each analyzer produces one or more named metrics that can be aggregated across databases
/// </summary>
public interface ITransientDatabaseAnalyzer
{
    /// <summary>
    /// Q-value cutoff for organism specificity analysis
    /// </summary>
    const double QCutoff = 0.01;

    /// <summary>
    /// Unique name for this analyzer (e.g., "OrganismSpecificity", "FdrMetrics")
    /// </summary>
    string AnalyzerName { get; }

    /// <summary>
    /// Returns the column headers this analyzer will produce
    /// </summary>
    IEnumerable<string> GetOutputColumns();

    /// <summary>
    /// Performs analysis on the context and returns key-value pairs for each metric
    /// Keys should match the output columns returned by GetOutputColumns()
    /// </summary>
    Dictionary<string, object> Analyze(TransientDatabaseAnalysisContext context);

    /// <summary>
    /// Validates that all required data is present in the context
    /// Returns true if the analyzer can run, false otherwise
    /// </summary>
    bool CanAnalyze(TransientDatabaseAnalysisContext context);
}