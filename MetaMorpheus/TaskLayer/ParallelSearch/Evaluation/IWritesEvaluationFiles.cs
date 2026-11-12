#nullable enable
using System.Collections.Generic;

namespace TaskLayer.ParallelSearch.Evaluation;

/// <summary>
/// An evaluator with outputs of its own beyond the per-database rows, such as the statistical test tables.
/// The runner calls <see cref="WriteFiles"/> after a successful <see cref="IDatabaseEvaluator.Evaluate"/>.
/// </summary>
public interface IWritesEvaluationFiles
{
    /// <summary>Writes the files for the most recent <see cref="IDatabaseEvaluator.Evaluate"/> call.</summary>
    /// <returns>Paths of the files written.</returns>
    IReadOnlyList<string> WriteFiles(string outputFolder);
}
