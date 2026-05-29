#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Combines p-values in two stages: first within each evidence family, then
/// across families. Applies Benjamini-Hochberg correction at both stages.
/// Prevents a single dense family (e.g. CountEnrichment with many tests)
/// from dominating the overall Combined | All signal purely by test count.
/// </summary>
public sealed class HierarchicalCombinedScoringService
{
    private static readonly StatisticalEvidenceFamily[] AllFamilies =
        Enum.GetValues<StatisticalEvidenceFamily>();

    private readonly ICorrelationEstimator _withinFamilyCorrelationEstimator;
    private readonly PValueCombiningMethod _withinFamilyCombiningMethod;
    private readonly ICorrelationEstimator _acrossFamilyCorrelationEstimator;
    private readonly PValueCombiningMethod _acrossFamilyCombiningMethod;

    /// <summary>
    /// Smart defaults: within-family uses Brown's method with test-statistic correlation
    /// (tests within a family share underlying data and are correlated),
    /// across-family uses Fisher's with independence assumption (families capture
    /// different evidence axes and are more plausibly independent).
    /// </summary>
    public HierarchicalCombinedScoringService()
        : this(
            new TestStatisticCorrelationEstimator(), PValueCombiningMethod.Brown,
            new IndependenceCorrelationEstimator(), PValueCombiningMethod.Fishers)
    {
    }

    public HierarchicalCombinedScoringService(
        ICorrelationEstimator withinFamilyCorrelationEstimator,
        PValueCombiningMethod withinFamilyCombiningMethod,
        ICorrelationEstimator acrossFamilyCorrelationEstimator,
        PValueCombiningMethod acrossFamilyCombiningMethod)
    {
        _withinFamilyCorrelationEstimator = withinFamilyCorrelationEstimator ?? throw new ArgumentNullException(nameof(withinFamilyCorrelationEstimator));
        _withinFamilyCombiningMethod = withinFamilyCombiningMethod;
        _acrossFamilyCorrelationEstimator = acrossFamilyCorrelationEstimator ?? throw new ArgumentNullException(nameof(acrossFamilyCorrelationEstimator));
        _acrossFamilyCombiningMethod = acrossFamilyCombiningMethod;
    }

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
                "NoDefinedTestsInFamily",
                _withinFamilyCombiningMethod,
                _withinFamilyCorrelationEstimator);

            ApplyBenjaminiHochberg(combinedFamilyResults);
            resultsByCacheKey[CombinedResultNames.GetCacheKey(family.ToString())] = combinedFamilyResults;
            familyCombinedResults.AddRange(combinedFamilyResults);
        }

        var overallCombinedResults = BuildCombinedResultsForGroup(
            familyCombinedResults,
            CombinedResultNames.AllMetricName,
            null,
            "NoDefinedFamilyCombinedResults",
            _acrossFamilyCombiningMethod,
            _acrossFamilyCorrelationEstimator);

        ApplyBenjaminiHochberg(overallCombinedResults);
        resultsByCacheKey[CombinedResultNames.GetCacheKey(CombinedResultNames.AllMetricName)] = overallCombinedResults;

        return new HierarchicalCombinedScoringResult(resultsByCacheKey, familyCombinedResults, overallCombinedResults);
    }

    public void UpdateMetricsSummary(
        Dictionary<string, TransientDatabaseMetrics> analysisResults,
        HierarchicalCombinedScoringResult combinedScoringResult)
    {
        var overallByDb = combinedScoringResult.OverallCombinedResults
            .Where(r => r.IsDefined)
            .ToDictionary(r => r.DatabaseName);

        var familyByDbAndFam = combinedScoringResult.FamilyCombinedResults
            .Where(r => r.IsDefined && r.EvidenceFamily.HasValue)
            .ToDictionary(r => (r.DatabaseName, r.EvidenceFamily!.Value));

        foreach (var analysisResult in analysisResults.Values)
        {
            if (overallByDb.TryGetValue(analysisResult.DatabaseName, out var overallCombined))
            {
                analysisResult.CombinedPValue = overallCombined.PValue;
                analysisResult.CombinedQValue = overallCombined.QValue;
            }
            else
            {
                analysisResult.CombinedPValue = double.NaN;
                analysisResult.CombinedQValue = double.NaN;
            }

            foreach (var family in AllFamilies)
            {
                double pVal = double.NaN;
                double qVal = double.NaN;
                if (familyByDbAndFam.TryGetValue((analysisResult.DatabaseName, family), out var familyCombined))
                {
                    pVal = familyCombined.PValue;
                    qVal = familyCombined.QValue;
                }
                TransientDatabaseMetricsFamilySummaryMapper.SetFamilyCombinedSummary(
                    analysisResult, family, pVal, qVal);
            }

            analysisResult.PopulateResultsFromProperties();
        }
    }

    private static List<StatisticalTestResult> BuildCombinedResultsForGroup(
        List<StatisticalTestResult> sourceResults,
        string metricName,
        StatisticalEvidenceFamily? evidenceFamily,
        string undefinedReason,
        PValueCombiningMethod combiningMethod,
        ICorrelationEstimator correlationEstimator)
    {
        var combinedResults = new List<StatisticalTestResult>();

        foreach (var dbGrouping in sourceResults.GroupBy(p => p.DatabaseName))
        {
            var definedResults = dbGrouping
                .Where(p => p.IsDefined && !double.IsNaN(p.PValue) && !double.IsInfinity(p.PValue))
                .ToList();

            if (definedResults.Count == 0)
            {
                combinedResults.Add(new StatisticalTestResultBuilder()
                    .WithDatabaseName(dbGrouping.Key)
                    .WithTestName(CombinedResultNames.TestName)
                    .WithMetricName(metricName)
                    .WithEvidenceFamily(evidenceFamily)
                    .WithIsDefined(false)
                    .WithEligibilityReason(undefinedReason)
                    .WithPValue(double.NaN)
                    .WithQValue(double.NaN)
                    .WithEffectSize(null)
                    .Build());
                continue;
            }

            var combinedPValue = MetaAnalysis.CombinePValuesAcrossTests(definedResults, combiningMethod, correlationEstimator)[dbGrouping.Key];
            combinedResults.Add(new StatisticalTestResultBuilder()
                .WithDatabaseName(dbGrouping.Key)
                .WithTestName(CombinedResultNames.TestName)
                .WithMetricName(metricName)
                .WithEvidenceFamily(evidenceFamily)
                .WithIsDefined(true)
                .WithEligibilityReason(null)
                .WithPValue(combinedPValue)
                .WithQValue(double.NaN)
                .WithEffectSize(null)
                .Build());
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