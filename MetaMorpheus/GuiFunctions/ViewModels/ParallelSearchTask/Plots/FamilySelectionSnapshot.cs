using System;
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearch.Statistics;

namespace GuiFunctions.ViewModels.ParallelSearchTask.Plots;

public sealed class FamilySelectionSnapshot
{
    public string SelectedTestKey { get; init; } = string.Empty;
    public StatisticalEvidenceFamily? Family { get; init; }
    public string SelectedFamilyName { get; init; } = string.Empty;
    public IReadOnlyList<StatisticalTestResult> SelectedTestResults { get; init; } = Array.Empty<StatisticalTestResult>();
    public IReadOnlyList<FamilyTestGroupSnapshot> TestGroups { get; init; } = Array.Empty<FamilyTestGroupSnapshot>();
    public IReadOnlyList<FamilyDatabaseSummary> DatabaseSummaries { get; init; } = Array.Empty<FamilyDatabaseSummary>();

    public static FamilySelectionSnapshot Empty(string selectedTestKey = "") => new()
    {
        SelectedTestKey = selectedTestKey,
    };

    public static FamilySelectionSnapshot Build(List<StatisticalTestResult> allResults, string selectedTestKey)
    {
        if (string.IsNullOrEmpty(selectedTestKey) || allResults.Count == 0)
            return Empty(selectedTestKey);

        var selectedTestResults = allResults.Where(r => r.MatchesSelection(selectedTestKey)).ToList();
        if (selectedTestResults.Count == 0)
            return Empty(selectedTestKey);

        var family = selectedTestResults.Select(r => r.EvidenceFamily).FirstOrDefault(f => f.HasValue);
        if (family == null)
            return Empty(selectedTestKey);

        var familyResults = allResults.Where(r => r.EvidenceFamily == family).ToList();

        var testGroups = familyResults
            .Where(r => !r.IsCombinedResult && r.IsDefined)
            .GroupBy(r => (r.TestName, r.MetricName))
            .OrderBy(g => g.Key.TestName)
            .ThenBy(g => g.Key.MetricName)
            .Select(g =>
            {
                var results = g.ToList();
                return new FamilyTestGroupSnapshot
                {
                    TestName = g.Key.TestName,
                    MetricName = g.Key.MetricName,
                    DisplayName = $"{g.Key.TestName} | {g.Key.MetricName}",
                    Results = results,
                    RawValues = results
                        .Select(r => r.TestStatistic)
                        .Where(s => s.HasValue && !double.IsNaN(s.Value) && !double.IsInfinity(s.Value))
                        .Select(s => s!.Value)
                        .ToList(),
                    PValues = results.Select(r => r.PValue).Where(p => !double.IsNaN(p) && p > 0 && p <= 1.0).ToList(),
                    QValues = results.Select(r => r.QValue).Where(q => !double.IsNaN(q) && q > 0 && q <= 1.0).ToList(),
                };
            })
            .ToList();

        var combinedByDb = familyResults
            .Where(r => r.IsCombinedResult)
            .GroupBy(r => r.DatabaseName)
            .ToDictionary(g => g.Key, g => g.First());

        var databaseSummaries = familyResults
            .Where(r => !r.IsCombinedResult && r.IsDefined)
            .GroupBy(r => r.DatabaseName)
            .Select(g =>
            {
                var defined = g.Where(r => !double.IsNaN(r.PValue) && r.PValue > 0).ToList();
                combinedByDb.TryGetValue(g.Key, out var combined);
                return new FamilyDatabaseSummary
                {
                    DatabaseName = g.Key,
                    CombinedResult = combined,
                    DefinedResults = defined,
                    MinP = defined.Count > 0 ? defined.Min(r => r.PValue) : double.NaN,
                    MeanP = defined.Count > 0 ? defined.Average(r => r.PValue) : double.NaN,
                    MaxP = defined.Count > 0 ? defined.Max(r => r.PValue) : double.NaN,
                };
            })
            .Where(s => s.CombinedResult != null && s.DefinedResults.Count > 0)
            .OrderBy(s => s.MinP)
            .ToList();

        return new FamilySelectionSnapshot
        {
            SelectedTestKey = selectedTestKey,
            Family = family,
            SelectedFamilyName = family.Value.ToString(),
            SelectedTestResults = selectedTestResults,
            TestGroups = testGroups,
            DatabaseSummaries = databaseSummaries,
        };
    }
}

public sealed class FamilyTestGroupSnapshot
{
    public string TestName { get; init; } = string.Empty;
    public string MetricName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public IReadOnlyList<StatisticalTestResult> Results { get; init; } = Array.Empty<StatisticalTestResult>();
    public IReadOnlyList<double> RawValues { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> PValues { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> QValues { get; init; } = Array.Empty<double>();
}

public sealed class FamilyDatabaseSummary
{
    public string DatabaseName { get; init; } = string.Empty;
    public StatisticalTestResult? CombinedResult { get; init; }
    public IReadOnlyList<StatisticalTestResult> DefinedResults { get; init; } = Array.Empty<StatisticalTestResult>();
    public double MinP { get; init; } = double.NaN;
    public double MeanP { get; init; } = double.NaN;
    public double MaxP { get; init; } = double.NaN;
}
