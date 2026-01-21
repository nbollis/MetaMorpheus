#nullable enable
using System.Collections.Generic;

namespace TaskLayer.ParallelSearch.Analysis.ExternalData;

/// <summary>
/// Interface for providing external data to analyzers (e.g., de novo search results)
/// This allows injection of data from sources other than the primary search results
/// </summary>
public interface IExternalDataProvider
{
    /// <summary>
    /// Name of this data source (e.g., "DeNovo", "ExternalDatabase")
    /// </summary>
    string SourceName { get; }

    Dictionary<string, object> GetMetrics(string key);
}