#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Statistics;

public sealed class HierarchicalCombinedScoringService
{
    public HierarchicalCombinedScoringResult BuildCombinedResults(List<StatisticalTestResult> statisticalResults)
    {
        var resultsByCacheKey = new Dictionary<string, List<StatisticalTestResult>>();
        var familyCombinedResults = new List<StatisticalTestResult>();

        foreach (var familyGrouping in statisticalResults.Where(p => p.EvidenceFamily.HasValue)
                     .GroupBy(p => p.EvidenceFamily!.Value)
                     .OrderBy(p => p.Key))
        {
            var family = familyGrouping.Key;
            var combinedFamilyResults = BuildCombinedResultsForGroup(
                familyGrouping.ToList(),
                family.ToString(),
                family,
                "NoDefinedTestsInFamily");

            ApplyBenjaminiHochberg(combinedFamilyResults);
            resultsByCacheKey[CombinedResultNames.GetCacheKey(family.ToString())] = combinedFamilyResults;
            familyCombinedResults.AddRange(combinedFamilyResults);
        }

        var overallCombinedResults = BuildCombinedResultsForGroup(
            familyCombinedResults,
            CombinedResultNames.AllMetricName,
            null,
            "NoDefinedFamilyCombinedResults");

        ApplyBenjaminiHochberg(overallCombinedResults);
        resultsByCacheKey[CombinedResultNames.GetCacheKey(CombinedResultNames.AllMetricName)] = overallCombinedResults;

        return new HierarchicalCombinedScoringResult(resultsByCacheKey, familyCombinedResults, overallCombinedResults);
    }

    public void UpdateMetricsSummary(
        Dictionary<string, TransientDatabaseMetrics> analysisResults,
        HierarchicalCombinedScoringResult combinedScoringResult)
    {
        foreach (var analysisResult in analysisResults.Values)
        {
            var overallCombined = combinedScoringResult.OverallCombinedResults
                .FirstOrDefault(p => p.DatabaseName == analysisResult.DatabaseName);
            analysisResult.CombinedPValue = overallCombined?.PValue ?? double.NaN;
            analysisResult.CombinedQValue = overallCombined?.QValue ?? double.NaN;

            foreach (var family in Enum.GetValues(typeof(StatisticalEvidenceFamily)).Cast<StatisticalEvidenceFamily>())
            {
                var familyCombined = combinedScoringResult.FamilyCombinedResults
                    .FirstOrDefault(p => p.DatabaseName == analysisResult.DatabaseName && p.EvidenceFamily == family);
                TransientDatabaseMetricsFamilySummaryMapper.SetFamilyCombinedSummary(
                    analysisResult,
                    family,
                    familyCombined?.PValue ?? double.NaN,
                    familyCombined?.QValue ?? double.NaN);
            }

            analysisResult.PopulateResultsFromProperties();
        }
    }

    private static List<StatisticalTestResult> BuildCombinedResultsForGroup(
        List<StatisticalTestResult> sourceResults,
        string metricName,
        StatisticalEvidenceFamily? evidenceFamily,
        string undefinedReason)
    {
        var combinedResults = new List<StatisticalTestResult>();

        foreach (var dbGrouping in sourceResults.GroupBy(p => p.DatabaseName))
        {
            var definedResults = dbGrouping
                .Where(p => p.IsDefined && !double.IsNaN(p.PValue) && !double.IsInfinity(p.PValue))
                .ToList();

            if (definedResults.Count == 0)
            {
                combinedResults.Add(new StatisticalTestResult
                {
                    DatabaseName = dbGrouping.Key,
                    TestName = CombinedResultNames.TestName,
                    MetricName = metricName,
                    EvidenceFamily = evidenceFamily,
                    IsDefined = false,
                    EligibilityReason = undefinedReason,
                    PValue = double.NaN,
                    QValue = double.NaN,
                    EffectSize = null
                });
                continue;
            }

            var combinedPValue = MetaAnalysis.CombinePValuesAcrossTests(definedResults)[dbGrouping.Key];
            combinedResults.Add(new StatisticalTestResult
            {
                DatabaseName = dbGrouping.Key,
                TestName = CombinedResultNames.TestName,
                MetricName = metricName,
                EvidenceFamily = evidenceFamily,
                IsDefined = true,
                EligibilityReason = null,
                PValue = combinedPValue,
                QValue = double.NaN,
                EffectSize = null
            });
        }

        return combinedResults;
    }

    private static void ApplyBenjaminiHochberg(List<StatisticalTestResult> combinedResults)
    {
        var pValues = combinedResults
            .Where(p => p.IsDefined && !double.IsNaN(p.PValue) && !double.IsInfinity(p.PValue))
            .ToDictionary(r => r.DatabaseName, r => r.PValue);

        var qValues = MultipleTestingCorrection.BenjaminiHochberg(pValues);
        foreach (var result in combinedResults)
        {
            if (qValues.TryGetValue(result.DatabaseName, out var qValue))
            {
                result.QValue = qValue;
            }
        }
    }
}
