#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskLayer.ParallelSearch.Evaluation;

/// <summary>
/// What every evaluator produced for one run, plus the evaluators that did not run and why.
/// </summary>
public sealed class DatabaseEvaluationReport
{
    public DatabaseEvaluationReport(IReadOnlyDictionary<string, IReadOnlyList<DatabaseEvaluationResult>> resultsByEvaluator,
        IReadOnlyDictionary<string, string> skippedEvaluators)
    {
        ResultsByEvaluator = resultsByEvaluator;
        SkippedEvaluators = skippedEvaluators;
    }

    /// <summary>Results per evaluator name, for the evaluators that ran, in the order they were given.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<DatabaseEvaluationResult>> ResultsByEvaluator { get; }

    /// <summary>Evaluator name to the reason it did not run or failed.</summary>
    public IReadOnlyDictionary<string, string> SkippedEvaluators { get; }

    public bool HasResults => ResultsByEvaluator.Count > 0;
}

/// <summary>
/// Runs each evaluator on the same input. One evaluator failing does not stop the others; its failure is reported
/// through the warning callback and in <see cref="DatabaseEvaluationReport.SkippedEvaluators"/>.
/// </summary>
public sealed class DatabaseEvaluationRunner
{
    private readonly List<IDatabaseEvaluator> _evaluators;
    private readonly Action<string> _warn;

    public DatabaseEvaluationRunner(IEnumerable<IDatabaseEvaluator> evaluators, Action<string>? warn = null)
    {
        ArgumentNullException.ThrowIfNull(evaluators);
        _evaluators = evaluators.ToList();
        _warn = warn ?? (_ => { });

        var duplicateNames = _evaluators.GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateNames.Count > 0)
            throw new ArgumentException($"Evaluator names must be unique: {string.Join(", ", duplicateNames)}", nameof(evaluators));
    }

    public IReadOnlyList<IDatabaseEvaluator> Evaluators => _evaluators;

    /// <summary>
    /// Runs the evaluators on the summary rows of <paramref name="databaseNames"/> and writes their files to
    /// <paramref name="outputFolder"/>. Evaluators that did not run are reported through <paramref name="log"/>.
    /// </summary>
    /// <returns>Paths of the files written; none when there are no evaluators.</returns>
    public static List<string> RunAndWrite(IEnumerable<IDatabaseEvaluator> evaluators,
        IEnumerable<Analysis.TransientDatabaseMetrics> metrics, IEnumerable<string> databaseNames, string outputFolder,
        Action<string>? warn = null, Action<string>? log = null)
    {
        var runner = new DatabaseEvaluationRunner(evaluators, warn);
        if (runner.Evaluators.Count == 0)
            return [];

        var report = runner.Run(DatabaseEvaluationInput.FromMetrics(metrics, databaseNames));
        foreach (var (evaluatorName, reason) in report.SkippedEvaluators)
            log?.Invoke($"Database evaluation {evaluatorName} did not run: {reason}");

        return DatabaseEvaluationFiles.WriteAll(report, outputFolder);
    }

    public DatabaseEvaluationReport Run(DatabaseEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var results = new Dictionary<string, IReadOnlyList<DatabaseEvaluationResult>>();
        var skipped = new Dictionary<string, string>();

        if (input.UnpairedTwins.Count > 0)
            _warn($"{input.UnpairedTwins.Count} twin entrapment database(s) have no target row and are left out of the evaluations: "
                + string.Join(", ", input.UnpairedTwins.Select(t => t.DatabaseName)));

        foreach (var evaluator in _evaluators)
        {
            if (input.Targets.Count == 0)
            {
                skipped[evaluator.Name] = "No transient databases to evaluate.";
                continue;
            }

            if (evaluator.RequiresTwins && !input.HasTwins)
            {
                skipped[evaluator.Name] = "Needs twin entrapment databases; turn on UseTwinEntrapment.";
                continue;
            }

            try
            {
                results[evaluator.Name] = evaluator.Evaluate(input);
            }
            catch (Exception e)
            {
                skipped[evaluator.Name] = $"Failed: {e.Message}";
                _warn($"Database evaluation {evaluator.Name} failed and was skipped: {e.Message}");
            }
        }

        return new DatabaseEvaluationReport(results, skipped);
    }
}
