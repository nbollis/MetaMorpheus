#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Evaluation;

/// <summary>
/// The per-database metrics an evaluator sees, split into real transient databases (targets) and their twin
/// entrapment databases. A twin is matched to its target through <see cref="TransientDatabaseMetrics.TwinOf"/>.
/// </summary>
public sealed class DatabaseEvaluationInput
{
    private readonly Dictionary<string, TransientDatabaseMetrics> _twinByTarget;

    private DatabaseEvaluationInput(List<TransientDatabaseMetrics> targets, List<TransientDatabaseMetrics> twins,
        Dictionary<string, TransientDatabaseMetrics> twinByTarget, List<TransientDatabaseMetrics> unpairedTwins)
    {
        Targets = targets;
        Twins = twins;
        _twinByTarget = twinByTarget;
        UnpairedTwins = unpairedTwins;
    }

    /// <summary>Real transient databases, ordered by name.</summary>
    public IReadOnlyList<TransientDatabaseMetrics> Targets { get; }

    /// <summary>Twins whose target is among <see cref="Targets"/>, ordered by name.</summary>
    public IReadOnlyList<TransientDatabaseMetrics> Twins { get; }

    /// <summary>Twins whose target has no row; left out of <see cref="Twins"/> so pairs stay one to one.</summary>
    public IReadOnlyList<TransientDatabaseMetrics> UnpairedTwins { get; }

    public bool HasTwins => Twins.Count > 0;

    /// <summary>Targets that have a twin, each with its twin, in target-name order.</summary>
    public IEnumerable<(TransientDatabaseMetrics Target, TransientDatabaseMetrics Twin)> Pairs =>
        Targets.Where(t => _twinByTarget.ContainsKey(t.DatabaseName))
            .Select(t => (t, _twinByTarget[t.DatabaseName]));

    public bool TryGetTwin(string targetName, out TransientDatabaseMetrics twin) =>
        _twinByTarget.TryGetValue(targetName, out twin!);

    /// <summary>
    /// Splits summary rows into targets and twins. When <paramref name="databaseNames"/> is given, rows for
    /// databases outside it (left over from an earlier run with a different list) are ignored.
    /// </summary>
    public static DatabaseEvaluationInput FromMetrics(IEnumerable<TransientDatabaseMetrics> metrics,
        IEnumerable<string>? databaseNames = null)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        HashSet<string>? keep = databaseNames == null ? null : new HashSet<string>(databaseNames, StringComparer.Ordinal);

        var rows = metrics
            .Where(m => m != null && !string.IsNullOrEmpty(m.DatabaseName))
            .Where(m => keep == null || keep.Contains(m.DatabaseName))
            .GroupBy(m => m.DatabaseName, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(m => m.DatabaseName, StringComparer.Ordinal)
            .ToList();

        var targets = rows.Where(m => !m.IsTwin).ToList();
        var targetNames = new HashSet<string>(targets.Select(t => t.DatabaseName), StringComparer.Ordinal);

        var twinByTarget = new Dictionary<string, TransientDatabaseMetrics>(StringComparer.Ordinal);
        var unpaired = new List<TransientDatabaseMetrics>();
        foreach (var twin in rows.Where(m => m.IsTwin))
        {
            if (targetNames.Contains(twin.TwinOf) && twinByTarget.TryAdd(twin.TwinOf, twin))
                continue;
            unpaired.Add(twin);
        }

        var twins = twinByTarget.Values.OrderBy(m => m.DatabaseName, StringComparer.Ordinal).ToList();
        return new DatabaseEvaluationInput(targets, twins, twinByTarget, unpaired);
    }
}
