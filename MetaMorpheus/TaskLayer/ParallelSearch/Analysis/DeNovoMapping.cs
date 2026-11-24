#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EngineLayer.FdrAnalysis;
using Omics;
using Proteomics.ProteolyticDigestion;
using Readers.DeNovoMapping;
using UsefulProteomicsDatabases;

namespace TaskLayer.ParallelSearch.Analysis;

/// <summary>
/// Writes the mapping summary that <see cref="Collectors.DeNovoMappingCollector"/> reads, from Casanovo mzTab files, with
/// mzLib's <see cref="DeNovoMapper"/>. Before mzLib had the mapper, that summary was made outside MetaMorpheus and given
/// to the task as <see cref="ParallelSearchParameters.DeNovoMappingDataFilePath"/>; it still can be.
/// </summary>
/// <remarks>
/// Building the mapper (<see cref="CreateMapper"/>) is most of the work: it reads the mzTabs, fills in and predicts retention
/// times and digests the host. None of that depends on the databases, so the task builds it alongside the searches and only
/// maps the databases (<see cref="WriteMappingSummary(DeNovoMapper, IEnumerable{string}, string)"/>) once they are done.
/// </remarks>
public static class DeNovoMapping
{
    public const string SummaryFileName = "MappingSummary.tsv";

    /// <summary>
    /// Reads the predictions in <paramref name="mzTabPaths"/> and prepares them for mapping: retention times and, with a host,
    /// which predictions the host explains too.
    /// </summary>
    /// <param name="spectraFilePaths">The spectra files Casanovo ran on; only read when an mzTab lacks retention times.</param>
    /// <param name="digestionParams">How database proteins are digested before their peptides are compared to the predictions.</param>
    /// <param name="decoyType">Decoys added to each database as it is read, to count decoy predictions.</param>
    /// <param name="hostProteins">Proteins of the host the sample came from (the foreground organism). A prediction whose
    /// peptide one of them also produces is host-ambiguous, and the summary reports each database's hits with and without
    /// those. Null maps without a host, so every hit counts as unambiguous.</param>
    /// <param name="maxThreads">Threads the mapper may use; null uses every processor.</param>
    public static DeNovoMapper CreateMapper(
        IEnumerable<string> mzTabPaths,
        IEnumerable<string> spectraFilePaths,
        DigestionParams digestionParams,
        DecoyType decoyType,
        IEnumerable<IBioPolymer>? hostProteins = null,
        int? maxThreads = null)
    {
        // The mapper uses its predictor only while it is constructed. It is built here rather than by the mapper so that
        // its construction cannot collide with another Chronologer's (see ChronologerFactory).
        using var predictor = ChronologerFactory.Create();
        var parameters = new DeNovoMappingParameters
        {
            DigestionParams = digestionParams,
            DecoyType = decoyType,
            SpectraFilePaths = spectraFilePaths.ToList(),
            HostProteins = hostProteins?.ToList(),
            MaxThreads = maxThreads ?? System.Environment.ProcessorCount,
            RetentionTimePredictor = predictor,
        };
        return new DeNovoMapper(mzTabPaths, parameters);
    }

    /// <summary>
    /// Maps the mapper's predictions onto each database and writes the summary to <paramref name="outputFolder"/>. A database
    /// file that is missing is skipped with a warning.
    /// </summary>
    /// <returns>The summary's path, and the problems that did not stop the mapping, the mapper's own included.</returns>
    public static (string SummaryPath, IReadOnlyList<string> Warnings) WriteMappingSummary(
        DeNovoMapper mapper,
        IEnumerable<string> databasePaths,
        string outputFolder)
    {
        var (results, warnings) = MapDatabases(mapper, databasePaths);
        string summaryPath = WriteSummary(results, outputFolder);
        return (summaryPath, warnings.Concat(mapper.Warnings).ToList());
    }

    /// <summary>
    /// Reads each database file and maps the mapper's predictions onto it. A database file that is missing is skipped with
    /// a warning. The mapper's own warnings are not included.
    /// </summary>
    public static (List<DeNovoMappingResult> Results, List<string> Warnings) MapDatabases(DeNovoMapper mapper, IEnumerable<string> databasePaths)
    {
        var databases = databasePaths.ToList();
        var missing = databases.Where(path => !File.Exists(path)).ToList();
        var warnings = missing.Select(path => $"De novo mapping skipped {Path.GetFileName(path)}: the database file is missing.").ToList();
        return (mapper.MapDatabases(databases.Except(missing)).ToList(), warnings);
    }

    /// <summary>Writes the mapping summary to <paramref name="outputFolder"/> and returns its path.</summary>
    public static string WriteSummary(IEnumerable<DeNovoMappingResult> results, string outputFolder)
    {
        string summaryPath = Path.Combine(outputFolder, SummaryFileName);
        DeNovoMapper.WriteSummary(results, summaryPath);
        return summaryPath;
    }

    /// <summary>
    /// <see cref="CreateMapper"/> and <see cref="WriteMappingSummary(DeNovoMapper, IEnumerable{string}, string)"/> in one go.
    /// </summary>
    public static (string SummaryPath, IReadOnlyList<string> Warnings) WriteMappingSummary(
        IEnumerable<string> databasePaths,
        IEnumerable<string> mzTabPaths,
        IEnumerable<string> spectraFilePaths,
        DigestionParams digestionParams,
        DecoyType decoyType,
        string outputFolder,
        IEnumerable<IBioPolymer>? hostProteins = null)
    {
        var mapper = CreateMapper(mzTabPaths, spectraFilePaths, digestionParams, decoyType, hostProteins);
        return WriteMappingSummary(mapper, databasePaths, outputFolder);
    }
}
