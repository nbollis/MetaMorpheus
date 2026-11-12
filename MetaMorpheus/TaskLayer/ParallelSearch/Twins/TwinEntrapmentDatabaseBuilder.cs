#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EngineLayer;
using EngineLayer.DatabaseLoading;
using Omics;
using Omics.Digestion;
using Proteomics;
using UsefulProteomicsDatabases;
using UsefulProteomicsDatabases.EntrapmentGeneration;

namespace TaskLayer.ParallelSearch.Twins;

/// <summary>
/// Builds twin entrapment databases with mzLib's <see cref="EntrapmentProteinGenerator"/>: one shuffled partner
/// per target entry, same composition and length, cleavage sites kept in place. No twin peptide may equal a
/// baseline peptide or a peptide of its own target, so a twin hit is never a real identification.
/// </summary>
public static class TwinEntrapmentDatabaseBuilder
{
    /// <summary>
    /// The peptides every twin must avoid because the baseline already holds them. Built once per run and shared.
    /// </summary>
    public static HashSet<string> CollectBaselinePeptides(IEnumerable<IBioPolymer> baselineBioPolymers, IDigestionParams digestionParams)
    {
        ArgumentNullException.ThrowIfNull(baselineBioPolymers);
        // contaminants are real sequences too, so their peptides are forbidden as well
        return EntrapmentProteinGenerator.TargetPeptides(
            baselineBioPolymers.OfType<Protein>().Where(p => !p.IsDecoy), digestionParams);
    }

    /// <summary>
    /// The twin of one transient database. <paramref name="targets"/> are that database's proteins; decoys and
    /// non-protein entries are ignored.
    /// </summary>
    public static List<Protein> GenerateTwin(IEnumerable<IBioPolymer> targets, IReadOnlySet<string> baselinePeptides,
        IDigestionParams digestionParams)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(baselinePeptides);

        List<Protein> targetProteins = TargetProteins(targets).ToList();
        HashSet<string> ownPeptides = EntrapmentProteinGenerator.TargetPeptides(targetProteins, digestionParams);
        var forbidden = new UnionReadOnlySet(baselinePeptides, ownPeptides);

        return EntrapmentProteinGenerator.GenerateEntrapment(targetProteins, digestionParams, forbidden,
            TwinEntrapment.FoldCount, TwinEntrapment.Seed);
    }

    /// <summary>
    /// Writes a twin as a UniProt-style FASTA. The file is written beside its destination and then moved, so an
    /// interrupted run never leaves a partial twin that a rerun would reuse.
    /// </summary>
    public static void WriteTwin(List<Protein> twinProteins, string fastaPath)
    {
        ArgumentNullException.ThrowIfNull(twinProteins);
        string directory = Path.GetDirectoryName(fastaPath)
            ?? throw new MetaMorpheusException($"Cannot write a twin database to {fastaPath}: it has no folder.");
        Directory.CreateDirectory(directory);

        string partialPath = fastaPath + ".partial";
        ProteinDbWriter.WriteFastaDatabase(twinProteins, partialPath, "|");
        File.Move(partialPath, fastaPath, overwrite: true);
    }

    /// <summary>
    /// Generates and writes the twin of one database unless its file already exists. Generation is deterministic,
    /// so an existing file is the same twin. Returns true when a new file was written.
    /// </summary>
    public static bool EnsureTwin(string fastaPath, Func<IEnumerable<IBioPolymer>> loadTargets,
        IReadOnlySet<string> baselinePeptides, IDigestionParams digestionParams)
    {
        ArgumentNullException.ThrowIfNull(loadTargets);
        if (File.Exists(fastaPath))
            return false;

        List<Protein> twin = GenerateTwin(loadTargets(), baselinePeptides, digestionParams);
        if (twin.Count == 0)
            throw new MetaMorpheusException($"No twin entrapment proteins could be made for {Path.GetFileName(fastaPath)}; "
                + "its database holds no protein entries.");

        WriteTwin(twin, fastaPath);
        return true;
    }

    /// <summary>
    /// Makes sure every target in <paramref name="targetDatabases"/> has its twin file in
    /// <paramref name="outputFolder"/>. Missing twins are built in parallel; existing files are reused, and when
    /// none is missing the baseline is not even digested.
    /// </summary>
    /// <param name="loadTargets">Loads one target database's entries, without decoys.</param>
    /// <returns>How many twin files were written; the rest were reused.</returns>
    public static int EnsureTwins(IEnumerable<DbForTask> targetDatabases, string outputFolder,
        IEnumerable<IBioPolymer> baselineBioPolymers, IDigestionParams digestionParams,
        Func<DbForTask, IEnumerable<IBioPolymer>> loadTargets, int maxDegreeOfParallelism)
    {
        ArgumentNullException.ThrowIfNull(targetDatabases);
        ArgumentNullException.ThrowIfNull(loadTargets);

        var missing = targetDatabases
            .Select(db => (Database: db, Name: Path.GetFileNameWithoutExtension(db.FilePath)))
            .Select(t => (t.Database, t.Name, Path: TwinEntrapment.GetTwinFastaPath(outputFolder, t.Name)))
            .Where(t => !File.Exists(t.Path))
            .ToList();
        if (missing.Count == 0)
            return 0;

        HashSet<string> baselinePeptides = CollectBaselinePeptides(baselineBioPolymers, digestionParams);
        int written = 0;
        Parallel.ForEach(missing, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, maxDegreeOfParallelism) }, target =>
        {
            if (GlobalVariables.StopLoops)
                return;

            try
            {
                if (EnsureTwin(target.Path, () => loadTargets(target.Database), baselinePeptides, digestionParams))
                    Interlocked.Increment(ref written);
            }
            catch (Exception e) when (e is not MetaMorpheusException)
            {
                throw new MetaMorpheusException($"Could not build the twin entrapment database of {target.Name}: {e.Message}", e);
            }
        });

        return written;
    }

    private static IEnumerable<Protein> TargetProteins(IEnumerable<IBioPolymer> bioPolymers) =>
        bioPolymers.OfType<Protein>().Where(p => !p.IsDecoy && !p.IsContaminant);
}
