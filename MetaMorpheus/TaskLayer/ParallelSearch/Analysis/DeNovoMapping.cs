#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Proteomics.ProteolyticDigestion;
using Readers.DeNovoMapping;
using UsefulProteomicsDatabases;

namespace TaskLayer.ParallelSearch.Analysis;

/// <summary>
/// Writes the mapping summary that <see cref="Collectors.DeNovoMappingCollector"/> reads, from Casanovo mzTab files, with
/// mzLib's <see cref="DeNovoMapper"/>. Before mzLib had the mapper, that summary was made outside MetaMorpheus and given
/// to the task as <see cref="ParallelSearchParameters.DeNovoMappingDataFilePath"/>; it still can be.
/// </summary>
public static class DeNovoMapping
{
    public const string SummaryFileName = "MappingSummary.tsv";

    /// <summary>
    /// Maps the predictions in <paramref name="mzTabPaths"/> onto each database and writes the summary to
    /// <paramref name="outputFolder"/>. A database file that is missing is skipped with a warning.
    /// </summary>
    /// <param name="spectraFilePaths">The spectra files Casanovo ran on; only read when an mzTab lacks retention times.</param>
    /// <param name="digestionParams">How database proteins are digested before their peptides are compared to the predictions.</param>
    /// <param name="decoyType">Decoys added to each database as it is read, to count decoy predictions.</param>
    /// <returns>The summary's path, and the problems that did not stop the mapping.</returns>
    public static (string SummaryPath, IReadOnlyList<string> Warnings) WriteMappingSummary(
        IEnumerable<string> databasePaths,
        IEnumerable<string> mzTabPaths,
        IEnumerable<string> spectraFilePaths,
        DigestionParams digestionParams,
        DecoyType decoyType,
        string outputFolder)
    {
        var databases = databasePaths.ToList();
        var missing = databases.Where(path => !File.Exists(path)).ToList();
        var warnings = missing.Select(path => $"De novo mapping skipped {Path.GetFileName(path)}: the database file is missing.").ToList();

        var parameters = new DeNovoMappingParameters
        {
            DigestionParams = digestionParams,
            DecoyType = decoyType,
            SpectraFilePaths = spectraFilePaths.ToList(),
        };
        var mapper = new DeNovoMapper(mzTabPaths, parameters);
        var results = mapper.MapDatabases(databases.Except(missing)).ToList();
        warnings.AddRange(mapper.Warnings);

        string summaryPath = Path.Combine(outputFolder, SummaryFileName);
        DeNovoMapper.WriteSummary(results, summaryPath);
        return (summaryPath, warnings);
    }
}
