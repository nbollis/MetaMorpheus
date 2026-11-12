#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Builds per-test and per-family summary rows from statistical test results,
/// and backfills per-database metric fields (legacy and family-aware) onto
/// TransientDatabaseMetrics after test execution.
/// </summary>
public sealed class StatisticalSummaryBuilder
{
    private static readonly StatisticalEvidenceFamily[] AllFamilies =
        Enum.GetValues<StatisticalEvidenceFamily>();

    private readonly double _alpha;

    public StatisticalSummaryBuilder(double alpha)
    {
        _alpha = alpha;
    }

    public Dictionary<string, TestSummary> BuildPerTestSummaries(List<StatisticalTestResult> statisticalResults)
    {
        return statisticalResults
            .GroupBy(p => p.Key)
            .ToDictionary(
                g => g.Key,
                g => new TestSummary
                {
                    TestName = g.First().TestName,
                    MetricName = g.First().MetricName,
                    EvidenceFamily = g.First().EvidenceFamily,
                    ValidDatabases = g.Count(p => p.IsDefined),
                    UndefinedDatabases = g.Count(p => !p.IsDefined),
                    SignificantByP = g.Count(p => p.IsDefined && p.PValue <= _alpha),
                    SignificantByQ = g.Count(p => p.IsDefined && p.QValue <= _alpha)
                });
    }

    public Dictionary<string, TestSummary> BuildFamilySummaries(List<StatisticalTestResult> statisticalResults)
    {
        return statisticalResults
            .Where(p => p.EvidenceFamily.HasValue)
            .GroupBy(p => p.EvidenceFamily!.Value)
            .ToDictionary(
                g => $"FamilySummary_{g.Key}",
                g =>
                {
                    var familyResults = g.ToList();
                    return new TestSummary
                    {
                        TestName = "FamilySummary",
                        MetricName = g.Key.ToString(),
                        EvidenceFamily = g.Key,
                        IsFamilySummary = true,
                        ValidDatabases = familyResults.Where(p => p.IsDefined)
                            .Select(p => p.DatabaseName)
                            .Distinct()
                            .Count(),
                        UndefinedDatabases = familyResults.GroupBy(p => p.DatabaseName)
                            .Count(grouping => grouping.All(r => !r.IsDefined)),
                        SignificantByP = familyResults.Where(p => p.IsDefined && p.PValue <= _alpha)
                            .Select(p => p.DatabaseName)
                            .Distinct()
                            .Count(),
                        SignificantByQ = familyResults.Where(p => p.IsDefined && p.QValue <= _alpha)
                            .Select(p => p.DatabaseName)
                            .Distinct()
                            .Count()
                    };
                });
    }

    public void UpdatePerDatabaseMetrics(
        Dictionary<string, TransientDatabaseMetrics> analysisResults,
        List<StatisticalTestResult> statisticalResults)
    {
        foreach (var dbGrouping in statisticalResults.GroupBy(p => p.DatabaseName))
        {
            if (!analysisResults.TryGetValue(dbGrouping.Key, out var analysisResult))
            {
                continue;
            }

            var groupedResults = dbGrouping.ToList();
            int testsRun = 0;
            int testsPassed = 0;
            var seenFamilies = new HashSet<StatisticalEvidenceFamily>();
            var seenPassedFamilies = new HashSet<StatisticalEvidenceFamily>();

            foreach (var r in groupedResults)
            {
                if (r.IsDefined)
                {
                    testsRun++;
                    if (r.EvidenceFamily.HasValue)
                        seenFamilies.Add(r.EvidenceFamily!.Value);
                }

                if (r.IsSignificant(_alpha))
                {
                    testsPassed++;
                    if (r.EvidenceFamily.HasValue)
                        seenPassedFamilies.Add(r.EvidenceFamily!.Value);
                }
            }

            analysisResult.StatisticalTestsRun = testsRun;
            analysisResult.StatisticalTestsPassed = testsPassed;
            analysisResult.TestPassedRatio = testsRun > 0 ? testsPassed / (double)testsRun : 0.0;
            analysisResult.ValidTestCount = testsRun;
            analysisResult.PassedTestCount = testsPassed;
            analysisResult.ValidFamilyCount = seenFamilies.Count;
            analysisResult.PassedFamilyCount = seenPassedFamilies.Count;

            foreach (var family in AllFamilies)
            {
                int validTests = 0;
                int passedTests = 0;
                double bestP = double.NaN;
                double bestQ = double.NaN;

                foreach (var r in groupedResults)
                {
                    if (r.EvidenceFamily != family)
                        continue;

                    if (r.IsDefined)
                    {
                        validTests++;
                        if (!double.IsNaN(r.PValue) && !double.IsInfinity(r.PValue) &&
                            (double.IsNaN(bestP) || r.PValue < bestP))
                            bestP = r.PValue;

                        if (!double.IsNaN(r.QValue) && !double.IsInfinity(r.QValue) &&
                            (double.IsNaN(bestQ) || r.QValue < bestQ))
                            bestQ = r.QValue;
                    }

                    if (r.IsSignificant(_alpha))
                        passedTests++;
                }

                TransientDatabaseMetricsFamilySummaryMapper.SetFamilyBestSummary(
                    analysisResult,
                    family,
                    validTests,
                    passedTests,
                    bestP,
                    bestQ);
            }

            analysisResult.PopulateResultsFromProperties();
        }
    }
}
