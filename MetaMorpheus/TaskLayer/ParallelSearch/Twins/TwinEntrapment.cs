#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EngineLayer.DatabaseLoading;

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

    /// <summary>Folder, inside the task's output folder, that holds the generated twin FASTA files.</summary>
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

    public static string GetTwinFolder(string outputFolder) => Path.Combine(outputFolder, FolderName);

    public static string GetTwinFastaPath(string outputFolder, string targetName) =>
        Path.Combine(GetTwinFolder(outputFolder), GetTwinName(targetName) + ".fasta");

    /// <summary>
    /// The twin database of each target, in target order. The files may not exist yet; see
    /// <see cref="TwinEntrapmentDatabaseBuilder"/>.
    /// </summary>
    public static List<DbForTask> GetTwinDatabases(IEnumerable<DbForTask> targets, string outputFolder) =>
        targets
            .Select(db => new DbForTask(GetTwinFastaPath(outputFolder, Path.GetFileNameWithoutExtension(db.FilePath)), false) {DerivedFrom = [db] })
            .ToList();

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
