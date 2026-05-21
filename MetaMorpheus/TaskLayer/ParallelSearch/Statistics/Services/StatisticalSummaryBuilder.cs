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
            int testsRun = groupedResults.Count(p => p.IsDefined);
            int testsPassed = groupedResults.Count(r => r.IsSignificant(_alpha));
            int validFamilyCount = groupedResults.Where(r => r.IsDefined && r.EvidenceFamily.HasValue)
                .Select(r => r.EvidenceFamily!.Value)
                .Distinct()
                .Count();
            int passedFamilyCount = groupedResults.Where(r => r.IsSignificant(_alpha) && r.EvidenceFamily.HasValue)
                .Select(r => r.EvidenceFamily!.Value)
                .Distinct()
                .Count();

            analysisResult.StatisticalTestsRun = testsRun;
            analysisResult.StatisticalTestsPassed = testsPassed;
            analysisResult.TestPassedRatio = testsRun > 0 ? testsPassed / (double)testsRun : 0.0;
            analysisResult.ValidTestCount = testsRun;
            analysisResult.PassedTestCount = testsPassed;
            analysisResult.ValidFamilyCount = validFamilyCount;
            analysisResult.PassedFamilyCount = passedFamilyCount;

            foreach (var family in Enum.GetValues(typeof(StatisticalEvidenceFamily)).Cast<StatisticalEvidenceFamily>())
            {
                var familyResults = groupedResults.Where(r => r.EvidenceFamily == family).ToList();
                int validTests = familyResults.Count(r => r.IsDefined);
                int passedTests = familyResults.Count(r => r.IsSignificant(_alpha));
                double bestPValue = GetBestFiniteValue(familyResults.Select(r => r.PValue));
                double bestQValue = GetBestFiniteValue(familyResults.Select(r => r.QValue));

                TransientDatabaseMetricsFamilySummaryMapper.SetFamilyBestSummary(
                    analysisResult,
                    family,
                    validTests,
                    passedTests,
                    bestPValue,
                    bestQValue);
            }

            analysisResult.PopulateResultsFromProperties();
        }
    }

    private static double GetBestFiniteValue(IEnumerable<double> values)
    {
        var finiteValues = values.Where(p => !double.IsNaN(p) && !double.IsInfinity(p)).ToList();
        return finiteValues.Count == 0 ? double.NaN : finiteValues.Min();
    }
}
