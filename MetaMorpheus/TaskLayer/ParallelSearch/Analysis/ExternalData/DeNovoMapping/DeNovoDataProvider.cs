#nullable enable
using System.Collections.Generic;

namespace TaskLayer.ParallelSearch.Analysis.ExternalData.DeNovoMapping;

public class DeNovoDataProvider : IExternalDataProvider
{
    public const string DeNovoMappedSourceName = "DeNovoMapped";
    public string SourceName => DeNovoMappedSourceName;

    // TODO: Load in mapping file and provide metrics

    public Dictionary<string, object> GetMetrics(string key)
    {
        throw new System.NotImplementedException();
    }
}

public class DeNovoMappedCollector : ExternalDataCollector
{
    public override string AnalyzerName => DeNovoDataProvider.DeNovoMappedSourceName;
    public override string ExpectedSourceName => DeNovoDataProvider.DeNovoMappedSourceName;
    public override bool CanAnalyze(TransientDatabaseContext context)
    {
        // Check if the DeNovoMapped data source is present
        return context.ExternalDataSource.Exists(src => src.SourceName.Equals(ExpectedSourceName, System.StringComparison.OrdinalIgnoreCase));
    }
}