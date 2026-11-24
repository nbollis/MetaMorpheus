#nullable enable
using EngineLayer;
using Omics.SpectralMatch.MslSpectralLibrary;
using Readers.SpectralLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Util;

/// <summary>
/// The spectral library of confident transient matches: one .msl per transient database, then one library for
/// the whole run. An entry holds the best scan of one peptide and charge. Every entry stores
/// <c>dbName|accession</c> as its accession and the database name as its gene name, so two databases that share
/// an accession each keep their own protein record (the .msl stores a protein's name and gene once per accession).
/// </summary>
public static class TransientSpectralLibrary
{
    public const string DatabaseFileSuffix = "_SpectralLibrary.msl";
    public const string CombinedFileName = "TransientSpectralLibrary.msl";

    /// <summary>Separates the database name from the accession in <see cref="MslLibraryEntry.ProteinAccession"/>.</summary>
    public const char AccessionSeparator = '|';

    /// <summary>
    /// One entry per peptide and charge from the transient matches that pass the cutoff: the one with the lowest
    /// aligned q-value, then the highest score. Decoys, contaminants and matches whose full sequence did not
    /// resolve are left out, because an .msl entry holds one sequence. <see cref="MslLibraryEntry.IsProteotypic"/> is
    /// true when every protein the match maps to is in the transient database, and false when it also maps to
    /// the background.
    /// </summary>
    public static List<MslLibraryEntry> BuildEntries(IEnumerable<SpectralMatch> transientPsms, string databaseName,
        IReadOnlySet<string> transientAccessions, ConfidenceCutoff cutoff)
    {
        var best = new Dictionary<(string Sequence, int Charge), SpectralMatch>();
        foreach (var psm in transientPsms)
        {
            if (psm.IsDecoy || psm.IsContaminant || psm.FullSequence is null || psm.ScanPrecursorCharge <= 0
                || !cutoff.Passes(psm))
            {
                continue;
            }

            var key = (psm.FullSequence, psm.ScanPrecursorCharge);
            if (!best.TryGetValue(key, out var current) || IsBetter(psm, current, cutoff))
            {
                best[key] = psm;
            }
        }

        var entries = new List<MslLibraryEntry>(best.Count);
        foreach (var psm in best.Values.OrderBy(p => p.FullSequence, StringComparer.Ordinal).ThenBy(p => p.ScanPrecursorCharge))
        {
            var parents = psm.BestMatchingBioPolymersWithSetMods
                .Select(h => h.SpecificBioPolymer.Parent)
                .GroupBy(p => p.Accession)
                .Select(g => g.First())
                .OrderBy(p => p.Accession, StringComparer.Ordinal)
                .ToList();
            var transientParent = parents.FirstOrDefault(p => transientAccessions.Contains(p.Accession));
            if (transientParent is null)
            {
                continue;
            }

            var library = psm.ToLibrarySpectrum();
            var entry = library is null ? null : MslLibraryEntry.FromLibrarySpectrum(library);
            if (entry is null)
            {
                continue;
            }

            entry.RetentionTime = psm.ScanRetentionTime;
            entry.RtIsCalibrated = true;
            entry.ProteinAccession = databaseName + AccessionSeparator + transientParent.Accession;
            entry.ProteinName = transientParent.FullName ?? string.Empty;
            entry.GeneName = databaseName;
            entry.IsProteotypic = parents.All(p => transientAccessions.Contains(p.Accession));
            entry.QValue = (float)QValueOf(psm, cutoff);
            entry.Source = MslFormat.SourceType.Empirical;
            entries.Add(entry);
        }

        return entries;
    }

    /// <summary>Writes a database's library; nothing is written when it has no entries. Returns whether a file was written.</summary>
    public static bool WriteDatabaseLibrary(string path, IReadOnlyList<MslLibraryEntry> entries)
    {
        if (entries.Count == 0)
        {
            return false;
        }

        MslWriter.Write(path, entries);
        return true;
    }

    /// <summary>
    /// Combines the databases' libraries into one, keeping for each peptide and charge the entry with the lowest
    /// q-value. The files go in by name, so a tie goes to the same database on every run. Returns null when there
    /// are no libraries to combine.
    /// </summary>
    public static MslMergeResult? Combine(string outputFolder, string outputPath, int filesPerBatch = 200)
    {
        var sources = Directory.EnumerateDirectories(outputFolder)
            .SelectMany(d => Directory.EnumerateFiles(d, "*" + DatabaseFileSuffix))
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();
        if (sources.Count == 0)
        {
            return null;
        }

        // the merge holds every source open until it finishes, so a long list is merged in batches first
        var tempFiles = new List<string>();
        try
        {
            while (sources.Count > filesPerBatch)
            {
                var next = new List<string>();
                foreach (var batch in sources.Chunk(filesPerBatch))
                {
                    string temp = Path.Combine(outputFolder, $"combine_{tempFiles.Count}.msl.tmp");
                    tempFiles.Add(temp);
                    MslMerger.Merge(batch, temp, MslMergeConflictPolicy.KeepLowestQValue);
                    next.Add(temp);
                }

                sources = next;
            }

            return MslMerger.Merge(sources, outputPath, MslMergeConflictPolicy.KeepLowestQValue);
        }
        finally
        {
            foreach (string temp in tempFiles)
            {
                try { File.Delete(temp); } catch (IOException) { }
            }
        }
    }

    private static double QValueOf(SpectralMatch psm, ConfidenceCutoff cutoff)
    {
        var info = psm.GetFdrInfo(cutoff.PeptideLevel);
        return info is null ? double.NaN : info.QValueNotch;
    }

    private static bool IsBetter(SpectralMatch candidate, SpectralMatch current, ConfidenceCutoff cutoff)
    {
        double a = QValueOf(candidate, cutoff);
        double b = QValueOf(current, cutoff);
        if (a != b)
        {
            return a < b;
        }

        return candidate.Score > current.Score;
    }
}
