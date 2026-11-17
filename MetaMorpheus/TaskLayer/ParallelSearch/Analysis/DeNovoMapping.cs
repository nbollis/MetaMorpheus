#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Proteomics.ProteolyticDigestion;
using Readers.DeNovoMapping;
using UsefulProteomicsDatabases;

namespace TaskLayer.ParallelSearch.Analysis;

/// <summary>
/// Maps Casanovo de novo predictions onto each searched database with mzLib's <see cref="DeNovoMapper"/> and stores
/// the result in that database's metrics. The predictions do not depend on the search, so this runs once, after
/// the searches, over every database in the summary, including rows carried over from an earlier run.
/// </summary>
public static class DeNovoMapping
{
    public const string TotalPredictions = "DeNovo_TotalPredictions";
    public const string TargetPredictions = "DeNovo_TargetPredictions";
    public const string DecoyPredictions = "DeNovo_DecoyPredictions";
    public const string UniquePeptidesMapped = "DeNovo_UniquePeptidesMapped";
    public const string UniqueProteinsMapped = "DeNovo_UniqueProteinsMapped";
    public const string MeanRtError = "DeNovo_MeanRtError";
    public const string RetentionTimeErrors = "DeNovo_RetentionTimeErrors";
    public const string MeanPredictionScore = "DeNovo_MeanPredictionScore";
    public const string PredictionScores = "DeNovo_PredictionScores";
    public const string TargetPredictionScores = "DeNovo_TargetPredictionScores";
    public const string DecoyPredictionScores = "DeNovo_DecoyPredictionScores";

    /// <summary>
    /// Maps the predictions in <paramref name="mzTabPaths"/> onto every database in <paramref name="databasePaths"/>
    /// that has a row in <paramref name="metricsByDatabase"/>, and writes the de novo columns of those rows.
    /// </summary>
    /// <param name="spectraFilePaths">The spectra files Casanovo ran on; only read when an mzTab lacks retention times.</param>
    /// <param name="digestionParams">How database proteins are digested before their peptides are compared to the predictions.</param>
    /// <param name="decoyType">Decoys added to each database as it is read, to count decoy predictions.</param>
    /// <returns>The number of databases mapped, and the mapper's warnings.</returns>
    public static (int Mapped, IReadOnlyList<string> Warnings) MapInto(
        IReadOnlyDictionary<string, TransientDatabaseMetrics> metricsByDatabase,
        IEnumerable<string> databasePaths,
        IEnumerable<string> mzTabPaths,
        IEnumerable<string> spectraFilePaths,
        DigestionParams digestionParams,
        DecoyType decoyType)
    {
        var toMap = databasePaths
            .Where(path => metricsByDatabase.ContainsKey(Path.GetFileNameWithoutExtension(path)))
            .ToList();
        var warnings = new List<string>();
        var missing = toMap.Where(path => !File.Exists(path)).ToList();
        warnings.AddRange(missing.Select(path => $"De novo mapping skipped {Path.GetFileName(path)}: the database file is missing."));
        toMap = toMap.Except(missing).ToList();
        if (toMap.Count == 0)
        {
            return (0, warnings);
        }

        var parameters = new DeNovoMappingParameters
        {
            DigestionParams = digestionParams,
            DecoyType = decoyType,
            SpectraFilePaths = spectraFilePaths.ToList(),
        };
        var mapper = new DeNovoMapper(mzTabPaths, parameters);
        var results = mapper.MapDatabases(toMap);
        warnings.AddRange(mapper.Warnings);

        int mapped = 0;
        foreach (var result in results)
        {
            if (!metricsByDatabase.TryGetValue(result.DatabaseIdentifier, out var metrics))
            {
                continue;
            }

            foreach (var (key, value) in ToResults(result))
            {
                metrics.Results[key] = value;
            }
            metrics.PopulatePropertiesFromResults();
            mapped++;
        }

        return (mapped, warnings);
    }

    /// <summary>One database's mapping, keyed by the de novo column names.</summary>
    public static Dictionary<string, object> ToResults(DeNovoMappingResult result) => new()
    {
        [TotalPredictions] = result.TotalPredictions,
        [TargetPredictions] = result.TargetPredictions,
        [DecoyPredictions] = result.DecoyPredictions,
        [UniquePeptidesMapped] = result.UniquePeptidesMapped,
        [UniqueProteinsMapped] = result.UniqueProteinsMapped,
        [MeanRtError] = result.MeanRtError,
        [RetentionTimeErrors] = result.RetentionTimeErrors.ToArray(),
        [MeanPredictionScore] = result.MeanPredictionScore,
        [PredictionScores] = result.PredictionScores.ToArray(),
        [TargetPredictionScores] = result.TargetPredictionScores.ToArray(),
        [DecoyPredictionScores] = result.DecoyPredictionScores.ToArray(),
    };
}
