#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace TaskLayer.ParallelSearch.Evaluation;

/// <summary>
/// Writes the evaluation outputs: one <c>DatabaseEvaluation_&lt;Evaluator&gt;.csv</c> per evaluator that ran, and
/// <c>DatabaseEvaluationSummary.csv</c> with one row per database and every evaluator side by side.
/// </summary>
public static class DatabaseEvaluationFiles
{
    public const string SummaryFileName = "DatabaseEvaluationSummary.csv";
    public const string EvaluatorFilePrefix = "DatabaseEvaluation_";

    public static string GetEvaluatorFileName(string evaluatorName) => $"{EvaluatorFilePrefix}{evaluatorName}.csv";

    /// <returns>Paths of the files written; none when no evaluator ran.</returns>
    public static List<string> WriteAll(DatabaseEvaluationReport report, string outputFolder)
    {
        ArgumentNullException.ThrowIfNull(report);
        var written = new List<string>();
        if (!report.HasResults)
            return written;

        Directory.CreateDirectory(outputFolder);
        foreach (var (evaluatorName, results) in report.ResultsByEvaluator)
        {
            string path = Path.Combine(outputFolder, GetEvaluatorFileName(evaluatorName));
            WriteEvaluatorFile(results, path);
            written.Add(path);
        }

        string summaryPath = Path.Combine(outputFolder, SummaryFileName);
        WriteSummaryFile(report, summaryPath);
        written.Add(summaryPath);
        return written;
    }

    public static void WriteEvaluatorFile(IReadOnlyList<DatabaseEvaluationResult> results, string path)
    {
        var detailColumns = results
            .Where(r => r.Details != null)
            .SelectMany(r => r.Details!.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",",
            new[] { "DatabaseName", "TwinOf", "IsTwin", "Score", "PValue", "QValue", "IsCalled" }
                .Concat(detailColumns).Select(Escape)));

        foreach (var result in OrderForOutput(results))
        {
            var cells = new List<string>
            {
                Escape(result.DatabaseName),
                Escape(result.TwinOf),
                FormatBool(result.IsTwin),
                FormatDouble(result.Score),
                FormatDouble(result.PValue),
                FormatDouble(result.QValue),
                FormatBool(result.IsCalled),
            };
            foreach (string column in detailColumns)
            {
                cells.Add(result.Details != null && result.Details.TryGetValue(column, out double value)
                    ? FormatDouble(value)
                    : string.Empty);
            }
            builder.AppendLine(string.Join(",", cells));
        }

        File.WriteAllText(path, builder.ToString());
    }

    public static void WriteSummaryFile(DatabaseEvaluationReport report, string path)
    {
        var evaluatorNames = report.ResultsByEvaluator.Keys.ToList();
        var byDatabase = report.ResultsByEvaluator
            .SelectMany(kvp => kvp.Value.Select(r => (Evaluator: kvp.Key, Result: r)))
            .GroupBy(x => x.Result.DatabaseName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Evaluator, x => x.Result), StringComparer.Ordinal);

        var header = new List<string> { "DatabaseName", "TwinOf", "IsTwin" };
        foreach (string name in evaluatorNames)
        {
            header.Add($"{name}_Score");
            header.Add($"{name}_QValue");
            header.Add($"{name}_IsCalled");
        }
        header.Add("CalledByCount");

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", header.Select(Escape)));

        var rows = byDatabase
            .Select(kvp => (Name: kvp.Key, Results: kvp.Value, Any: kvp.Value.Values.First()))
            .OrderBy(r => r.Any.IsTwin)
            .ThenBy(r => r.Name, StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var cells = new List<string> { Escape(row.Name), Escape(row.Any.TwinOf), FormatBool(row.Any.IsTwin) };
            int calledBy = 0;
            foreach (string name in evaluatorNames)
            {
                if (row.Results.TryGetValue(name, out var result))
                {
                    cells.Add(FormatDouble(result.Score));
                    cells.Add(FormatDouble(result.QValue));
                    cells.Add(FormatBool(result.IsCalled));
                    if (result.IsCalled)
                        calledBy++;
                }
                else
                {
                    cells.Add(string.Empty);
                    cells.Add(string.Empty);
                    cells.Add(string.Empty);
                }
            }
            cells.Add(calledBy.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(string.Join(",", cells));
        }

        File.WriteAllText(path, builder.ToString());
    }

    /// <summary>Targets before twins, then by name, so files diff cleanly between runs.</summary>
    private static IEnumerable<DatabaseEvaluationResult> OrderForOutput(IEnumerable<DatabaseEvaluationResult> results) =>
        results.OrderBy(r => r.IsTwin).ThenBy(r => r.DatabaseName, StringComparer.Ordinal);

    private static string FormatDouble(double value) =>
        double.IsNaN(value) ? "NaN" : value.ToString("R", CultureInfo.InvariantCulture);

    private static string FormatBool(bool value) => value ? "TRUE" : "FALSE";

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
