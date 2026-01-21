#nullable enable
using System.Collections.Generic;

namespace TaskLayer.ParallelSearch.Analysis.ExternalData.DeNovoMapping;

public class DeNovoMappedDataSource : IExternalDataSource
{
    public const string DeNovoMappedSourceName = "DeNovoMapped";
    public string SourceName => DeNovoMappedSourceName;

    // TODO: Load in mapping file and provide metrics

    public Dictionary<string, object> GetMetrics(string key)
    {
        throw new System.NotImplementedException();
    }
}

public class DeNovoMappedAnalyzer : ExternalDataAnalyzer
{
    public override string AnalyzerName => DeNovoMappedDataSource.DeNovoMappedSourceName;
    public override string ExpectedSourceName => DeNovoMappedDataSource.DeNovoMappedSourceName;
    public override bool CanAnalyze(TransientDatabaseAnalysisContext context)
    {
        // Check if the DeNovoMapped data source is present
        return context.ExternalDataSource.Exists(src => src.SourceName.Equals(ExpectedSourceName, System.StringComparison.OrdinalIgnoreCase));
    }
}