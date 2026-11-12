#nullable enable
using System.Collections.Generic;

namespace TaskLayer.ParallelSearch.Evaluation;

/// <summary>
/// One evaluator's verdict on one database.
/// </summary>
/// <param name="DatabaseName">The database scored.</param>
/// <param name="TwinOf">The target this database is the twin of; empty for a real database.</param>
/// <param name="Score">The evaluator's score; higher always means more evidence that the organism is present.</param>
/// <param name="PValue">A p-value when the evaluator produces one; NaN otherwise.</param>
/// <param name="QValue">A q-value when the evaluator produces one; NaN otherwise.</param>
/// <param name="IsCalled">Whether the evaluator calls the organism present at its own threshold.</param>
/// <param name="Details">Extra per-database numbers the evaluator wants written, keyed by column name.</param>
public sealed record DatabaseEvaluationResult(
    string DatabaseName,
    string TwinOf,
    double Score,
    double PValue,
    double QValue,
    bool IsCalled,
    IReadOnlyDictionary<string, double>? Details = null)
{
    public bool IsTwin => !string.IsNullOrEmpty(TwinOf);

    public static DatabaseEvaluationResult ScoreOnly(string databaseName, string twinOf, double score,
        IReadOnlyDictionary<string, double>? details = null) =>
        new(databaseName, twinOf, score, double.NaN, double.NaN, false, details);
}
