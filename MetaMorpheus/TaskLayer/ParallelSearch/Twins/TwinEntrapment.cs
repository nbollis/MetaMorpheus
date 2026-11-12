#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using EngineLayer.DatabaseLoading;
using Omics.Digestion;
using Proteomics.ProteolyticDigestion;
using UsefulProteomicsDatabases.EntrapmentGeneration;

namespace TaskLayer.ParallelSearch.Twins;

/// <summary>
/// Names and locations of twin entrapment databases. A twin is a shuffled entrapment copy of one transient
/// database, searched as a transient database of its own and named <c>&lt;target&gt;_twin</c>, so every twin
/// name is known before anything is generated.
/// </summary>
public static class TwinEntrapment
{
    /// <summary>Appended to a transient database's name to name its twin.</summary>
    public const string TwinSuffix = "_entrapment";

    /// <summary>
    /// Folder that holds generated twin FASTA files: beside each target database, or in the task's output folder
    /// when the target's folder is read-only. Under it, one subfolder per <see cref="TwinSettings.Key"/>.
    /// </summary>
    public const string FolderName = "TwinEntrapment";

    /// <summary>One entrapment partner per target entry, so target and twin are the same size.</summary>
    public const int FoldCount = 1;

    /// <summary>Fixed so the same target always gives the same twin, and a twin file can be reused on reruns.</summary>
    public const int Seed = 42;

    public static string GetTwinName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
            throw new ArgumentException("A twin needs a target database name.", nameof(targetName));

        return targetName + TwinSuffix;
    }

    public static bool IsTwinName(string? databaseName) =>
        databaseName != null
        && databaseName.Length > TwinSuffix.Length
        && databaseName.EndsWith(TwinSuffix, StringComparison.Ordinal);

    /// <summary>The target a twin was made from; false when the name is not a twin name.</summary>
    public static bool TryGetTargetName(string? databaseName, out string targetName)
    {
        if (IsTwinName(databaseName))
        {
            targetName = databaseName![..^TwinSuffix.Length];
            return true;
        }

        targetName = string.Empty;
        return false;
    }

    /// <summary>
    /// What a twin depends on besides its own target: the generator and its settings, the digestion, and the
    /// baseline (no twin peptide may equal a baseline peptide). <see cref="Key"/> names the folder the twins live in,
    /// so twins made under different settings sit side by side and are never mixed up.
    /// </summary>
    /// <param name="Key">Short hash of <see cref="Description"/>.</param>
    /// <param name="Description">Human-readable list of the settings, written beside the twins.</param>
    public sealed record TwinSettings(string Key, string Description);

    public const string SettingsFileName = "twin_settings.txt";

    public static TwinSettings DescribeSettings(IDigestionParams digestionParams, IEnumerable<DbForTask> baselineDatabases)
    {
        ArgumentNullException.ThrowIfNull(digestionParams);
        ArgumentNullException.ThrowIfNull(baselineDatabases);

        var lines = new List<string>
        {
            $"Generator: mzLib EntrapmentProteinGenerator, construction version {EntrapmentProteinGenerator.ConstructionVersion}",
            $"Fold count: {FoldCount}",
            $"Seed: {Seed}",
            $"Digestion agent: {digestionParams.DigestionAgent?.Name}",
            $"Cleavage specificity: {digestionParams.SearchModeType}",
            $"Fragmentation terminus: {digestionParams.FragmentationTerminus}",
            $"Max missed cleavages: {digestionParams.MaxMissedCleavages}",
            $"Min peptide length: {digestionParams.MinLength}",
            $"Max peptide length: {digestionParams.MaxLength}",
            $"Initiator methionine: {(digestionParams as DigestionParams)?.InitiatorMethionineBehavior.ToString() ?? "n/a"}",
        };

        // identified by name, size and time rather than full path, so moving the baseline does not orphan twins
        lines.AddRange(baselineDatabases
            .Where(db => !db.IsSpectralLibrary)
            .Select(db =>
            {
                var file = new FileInfo(db.FilePath);
                string identity = file.Exists ? $"{file.Length} bytes, modified {file.LastWriteTimeUtc:O}" : "missing";
                return $"Baseline{(db.IsContaminant ? " contaminant" : string.Empty)}: {file.Name} ({identity})";
            })
            .OrderBy(line => line, StringComparer.Ordinal));

        string description = string.Join(Environment.NewLine, lines);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(description));
        return new TwinSettings(Convert.ToHexString(hash, 0, 6).ToLowerInvariant(), description);
    }

    /// <summary>Preferred home of a target's twins: beside the target, so later searches can reuse them.</summary>
    public static string GetTwinFolderBesideTarget(string targetDatabasePath, string settingsKey) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(targetDatabasePath)) ?? string.Empty, FolderName, settingsKey);

    /// <summary>Used only when the target's folder cannot be written to.</summary>
    public static string GetFallbackTwinFolder(string outputFolder, string settingsKey) =>
        Path.Combine(outputFolder, FolderName, settingsKey);

    public static string GetTwinFileName(string targetDatabasePath) =>
        GetTwinName(Path.GetFileNameWithoutExtension(targetDatabasePath)) + ".fasta";

    /// <summary>
    /// The twin database of each target, in target order, beside the target when possible. A twin already beside
    /// its target is used; otherwise the target's folder must be writable, else the twin goes in
    /// <paramref name="fallbackFolder"/>. The files may not exist yet; see <see cref="TwinEntrapmentDatabaseBuilder"/>.
    /// </summary>
    public static List<DbForTask> GetTwinDatabases(IEnumerable<DbForTask> targets, string settingsKey, string fallbackFolder) =>
        targets
            .Select(db => new DbForTask(ChooseTwinPath(db.FilePath, settingsKey, fallbackFolder), false) { DerivedFrom = [db] })
            .ToList();

    private static string ChooseTwinPath(string targetDatabasePath, string settingsKey, string fallbackFolder)
    {
        string fileName = GetTwinFileName(targetDatabasePath);
        string besideTarget = Path.Combine(GetTwinFolderBesideTarget(targetDatabasePath, settingsKey), fileName);
        string fallback = Path.Combine(GetFallbackTwinFolder(fallbackFolder, settingsKey), fileName);

        if (File.Exists(besideTarget))
            return besideTarget;
        if (File.Exists(fallback))
            return fallback;
        return CanWriteTo(Path.GetDirectoryName(besideTarget)!) ? besideTarget : fallback;
    }

    /// <summary>Creates the folder if needed and checks a file can be written there.</summary>
    internal static bool CanWriteTo(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            string probe = Path.Combine(folder, $".write_probe_{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Transient database names that cannot be used with twins: a name that already looks like a twin, or one
    /// that equals another database's twin name. Results are stored by name, so either would overwrite a twin.
    /// </summary>
    public static List<string> FindNameConflicts(IEnumerable<string> transientDatabaseNames)
    {
        var names = transientDatabaseNames.ToList();
        var nameSet = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        return names
            .Where(name => IsTwinName(name) || nameSet.Contains(GetTwinName(name)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
