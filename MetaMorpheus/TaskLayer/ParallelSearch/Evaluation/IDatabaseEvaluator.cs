#nullable enable
using System.Collections.Generic;

namespace TaskLayer.ParallelSearch.Evaluation;

/// <summary>
/// One way of deciding, per transient database, whether its organism is really present. Evaluators read only the
/// per-database summary metrics, so they can be re-run on an existing output folder without searching again.
/// Each evaluator stands on its own; the runner reports them side by side and never combines them.
/// </summary>
public interface IDatabaseEvaluator
{
    /// <summary>Short name used in column headers and file names; letters, digits and underscores only.</summary>
    string Name { get; }

    /// <summary>True when the evaluator cannot run without twin entrapment databases.</summary>
    bool RequiresTwins { get; }

    /// <summary>
    /// Scores every target, and every twin when there are twins. Twins are scored the same way as targets so
    /// their scores can serve as a null; a result for a twin is never a call about a real organism.
    /// </summary>
    IReadOnlyList<DatabaseEvaluationResult> Evaluate(DatabaseEvaluationInput input);
}
