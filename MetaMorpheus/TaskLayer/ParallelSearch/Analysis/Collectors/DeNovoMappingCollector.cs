#nullable enable
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearch.Util;

namespace TaskLayer.ParallelSearch.Analysis.Collectors;

public class DeNovoMappingCollector(string dataFilePath) : IMetricCollector
{
    public string CollectorName => "DeNovoMapped";
    
    #region Column Information 

    public const string TotalPredictions = "DeNovo_TotalPredictions";
    public const string TargetPeptidesMapped = "DeNovo_TargetPredictions";
    public const string DecoyPeptidesMapped = "DeNovo_DecoyPredictions";

    public const string UniquePeptidesMapped = "DeNovo_UniquePeptidesMapped";
    public const string UniqueProteinsMapped = "DeNovo_UniqueProteinsMapped";

    public const string MeanRtError = "DeNovo_MeanRtError";
    public const string RetentionTimeErrors = "DeNovo_RetentionTimeErrors";

    public const string MeanPredictionScore = "DeNovo_MeanPredictionScore";
    public const string PredictionScores = "DeNovo_PredictionScores";
    public const string TargetPredictionScores = "DeNovo_TargetPredictionScores";
    public const string DecoyPredictionScores = "DeNovo_DecoyPredictionScores";

    public IEnumerable<string> GetOutputColumns()
    {
        yield return TotalPredictions;
        yield return TargetPeptidesMapped;
        yield return DecoyPeptidesMapped;
        yield return UniquePeptidesMapped;
        yield return UniqueProteinsMapped;
        yield return MeanRtError;
        yield return RetentionTimeErrors;
        yield return MeanPredictionScore;
        yield return PredictionScores;
        yield return TargetPredictionScores;
        yield return DecoyPredictionScores;
    }

    #endregion

    #region DeNovo Mapping Data 

    private Dictionary<string, DeNovoMappingResult>? _dataCache = null;
    public Dictionary<string, DeNovoMappingResult> DataCache
    {
        get
        {
            if (_dataCache == null)
            {
                var file = new DeNovoMappingResultFile(dataFilePath);
                _dataCache = file.Results.ToDictionary(r => r.DatabaseIdentifier, r => r);
            }
            return _dataCache;
        }
    }

    #endregion

    public Dictionary<string, object> CollectData(TransientDatabaseContext context)
    {
        var result = DataCache[context.DatabaseName];

        return new Dictionary<string, object>
        {
            { TotalPredictions, result.TotalPredictions },
            { TargetPeptidesMapped, result.TargetPredictions },
            { DecoyPeptidesMapped, result.DecoyPredictions },

            { UniquePeptidesMapped, result.UniquePeptidesMapped },
            { UniqueProteinsMapped, result.UniqueProteinsMapped },

            { MeanRtError, result.MeanRtError },
            { RetentionTimeErrors, result.RetentionTimeErrors.ToArray() },

            { MeanPredictionScore, result.MeanPredictionScore },
            { PredictionScores, result.PredictionScores.ToArray() },
            { TargetPredictionScores, result.TargetPredictionScores.ToArray() },
            { DecoyPredictionScores, result.DecoyPredictionScores.ToArray() },
        };
    }

    public bool CanCollectData(TransientDatabaseContext context)
    {
        return DataCache.ContainsKey(context.DatabaseName);
    }
}