#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Readers.DeNovoMapping;

namespace TaskLayer.ParallelSearch.Analysis.Collectors;

/// <summary>
/// Fills each database's de novo columns, either from a mapping summary file or, with a mapper, by mapping the predictions
/// onto the database's proteins while the database is analysed, so nothing is read from disk again.
/// </summary>
public class DeNovoMappingCollector : IMetricCollector
{
    private readonly string? dataFilePath;
    private readonly Func<DeNovoMapper>? _mapperSource;
    private readonly ConcurrentDictionary<string, DeNovoMappingResult> _mapped = new(StringComparer.Ordinal);

    /// <summary>Reads each database's row from a mapping summary (MappingSummary.tsv).</summary>
    public DeNovoMappingCollector(string dataFilePath)
    {
        this.dataFilePath = dataFilePath;
    }

    /// <summary>
    /// Maps each database's proteins (<see cref="TransientDatabaseContext.TransientProteins"/>, decoys included) with the mapper
    /// <paramref name="mapperSource"/> returns; it may block until the mapper is built.
    /// </summary>
    public DeNovoMappingCollector(Func<DeNovoMapper> mapperSource)
    {
        _mapperSource = mapperSource;
    }

    /// <summary>The results this collector mapped itself, by database name; empty when it reads a summary file.</summary>
    public IReadOnlyCollection<DeNovoMappingResult> MappedResults => _mapped.Values.ToList();

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

    // Host ambiguity: hits whose peptide the host (foreground organism) also produces. HostProteinCount is -1 when the
    // summary was made without a host, so the unambiguous columns are unknown and every hit is tested.
    public const string HostProteinCount = "DeNovo_HostProteinCount";
    public const string UnambiguousTargetPredictions = "DeNovo_UnambiguousTargetPredictions";
    public const string AmbiguousTargetPredictions = "DeNovo_AmbiguousTargetPredictions";
    public const string UnambiguousDecoyPredictions = "DeNovo_UnambiguousDecoyPredictions";
    public const string UniqueUnambiguousPeptidesMapped = "DeNovo_UniqueUnambiguousPeptidesMapped";
    public const string UniqueUnambiguousProteinsMapped = "DeNovo_UniqueUnambiguousProteinsMapped";
    public const string UnambiguousRetentionTimeErrors = "DeNovo_UnambiguousRetentionTimeErrors";
    public const string UnambiguousTargetPredictionScores = "DeNovo_UnambiguousTargetPredictionScores";
    public const string UnambiguousDecoyPredictionScores = "DeNovo_UnambiguousDecoyPredictionScores";

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
        yield return HostProteinCount;
        yield return UnambiguousTargetPredictions;
        yield return AmbiguousTargetPredictions;
        yield return UnambiguousDecoyPredictions;
        yield return UniqueUnambiguousPeptidesMapped;
        yield return UniqueUnambiguousProteinsMapped;
        yield return UnambiguousRetentionTimeErrors;
        yield return UnambiguousTargetPredictionScores;
        yield return UnambiguousDecoyPredictionScores;
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
                var file = new DeNovoMappingResultFile(dataFilePath!);
                _dataCache = file.Results.ToDictionary(r => r.DatabaseIdentifier, r => r);
            }
            return _dataCache;
        }
    }

    #endregion

    public Dictionary<string, object> CollectData(TransientDatabaseContext context)
    {
        var result = _mapperSource == null
            ? DataCache[context.DatabaseName]
            : _mapped[context.DatabaseName] = _mapperSource().MapDatabase(context.DatabaseName, context.TransientProteins);

        return ToResults(result);
    }

    /// <summary>One database's mapping as the collector's columns.</summary>
    public static Dictionary<string, object> ToResults(DeNovoMappingResult result)
    {
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

            { HostProteinCount, result.HostProteinCount },
            { UnambiguousTargetPredictions, result.UnambiguousTargetPredictions },
            { AmbiguousTargetPredictions, result.AmbiguousTargetPredictions },
            { UnambiguousDecoyPredictions, result.UnambiguousDecoyPredictions },
            { UniqueUnambiguousPeptidesMapped, result.UniqueUnambiguousPeptidesMapped },
            { UniqueUnambiguousProteinsMapped, result.UniqueUnambiguousProteinsMapped },
            { UnambiguousRetentionTimeErrors, result.UnambiguousRetentionTimeErrors.ToArray() },
            { UnambiguousTargetPredictionScores, result.UnambiguousTargetPredictionScores.ToArray() },
            { UnambiguousDecoyPredictionScores, result.UnambiguousDecoyPredictionScores.ToArray() },
        };
    }

    public bool CanCollectData(TransientDatabaseContext context)
    {
        return _mapperSource == null
            ? DataCache.ContainsKey(context.DatabaseName)
            : context.TransientProteins is { Count: > 0 };
    }

    /// <summary>
    /// Casanovo scores 0-1 for precorsor mass within tolerance and -1 to 0 for outside of tolerance, so we can normalize to a 0-1 scale for easier comparison to other scores and metrics
    /// </summary>
    /// <param name="originalScores"></param>
    /// <returns></returns>
    public static IEnumerable<double> NormalizeScores(IEnumerable<double> originalScores)
    {
        foreach (var ogScore in originalScores)
        {
            if (ogScore < 0)
            {
                yield return (ogScore + 1) / 1; // Map -1 to 0 and 0 to 1
            }
            else
            {
                yield return ogScore; // Keep scores between 0 and 1 unchanged
            }
        }
    }
}
