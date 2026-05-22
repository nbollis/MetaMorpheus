#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskLayer.ParallelSearch.Statistics.Calibration;

public sealed class CalibrationService
{
    public CalibrationResult Calibrate(List<StatisticalTestResult> allResults, double alpha = 0.05)
    {
        if (allResults == null || allResults.Count == 0)
        {
            return new CalibrationResult
            {
                TotalDatabases = 0,
                Alpha = alpha
            };
        }

        var nonCombined = allResults.Where(r => !r.IsCombinedResult).ToList();
        var combinedResults = allResults.Where(r => r.IsCombinedResult).ToList();

        var allDbNames = nonCombined
            .GroupBy(r => r.DatabaseName)
            .Select(g => g.Key)
            .ToHashSet();

        int totalDatabases = allDbNames.Count;

        var perTestPValues = new Dictionary<string, NullDistributionProfile>();
        var perTestEffectSizes = new Dictionary<string, NullDistributionProfile>();
        var perTestStatistics = new Dictionary<string, NullDistributionProfile>();

        foreach (var testGroup in nonCombined.GroupBy(r => r.Key))
        {
            string key = testGroup.Key;

            var definedResults = testGroup.Where(r => r.IsDefined).ToList();

            var pValues = definedResults.Select(r => r.PValue).Where(p => !double.IsNaN(p));
            if (pValues.Any())
                perTestPValues[key] = new NullDistributionProfile($"pValue_{key}", pValues);

            var effectSizes = definedResults
                .Select(r => r.EffectSize)
                .Where(e => e.HasValue && !double.IsNaN(e.Value) && !double.IsInfinity(e.Value))
                .Select(e => e!.Value);
            if (effectSizes.Any())
                perTestEffectSizes[key] = new NullDistributionProfile($"effectSize_{key}", effectSizes);

            var testStats = definedResults
                .Select(r => r.TestStatistic)
                .Where(s => s.HasValue && !double.IsNaN(s.Value) && !double.IsInfinity(s.Value))
                .Select(s => s!.Value);
            if (testStats.Any())
                perTestStatistics[key] = new NullDistributionProfile($"testStatistic_{key}", testStats);
        }

        var testPassCountsPerDb = allDbNames
            .Select(dbName =>
            {
                var dbResults = nonCombined.Where(r => r.DatabaseName == dbName);
                return dbResults.Count(r => r.IsDefined && r.IsSignificant(alpha));
            })
            .ToList();

        var overallTestPassProfile = testPassCountsPerDb.Count > 0
            ? new NullDistributionProfile("TestsPassedPerDatabase", testPassCountsPerDb.Select(c => (double)c))
            : null;

        var familyPassCountsPerDb = allDbNames
            .Select(dbName =>
            {
                var dbResults = nonCombined.Where(r => r.DatabaseName == dbName && r.EvidenceFamily.HasValue);
                return dbResults
                    .Where(r => r.IsDefined && r.IsSignificant(alpha))
                    .Select(r => r.EvidenceFamily!.Value)
                    .Distinct()
                    .Count();
            })
            .ToList();

        var overallFamilyPassProfile = familyPassCountsPerDb.Count > 0
            ? new NullDistributionProfile("FamiliesPassedPerDatabase", familyPassCountsPerDb.Select(c => (double)c))
            : null;

        var perFamilyProfiles = new Dictionary<StatisticalEvidenceFamily, NullDistributionProfile>();
        foreach (var family in Enum.GetValues(typeof(StatisticalEvidenceFamily)).Cast<StatisticalEvidenceFamily>())
        {
            var familyPassCounts = allDbNames
                .Select(dbName =>
                {
                    var dbResults = nonCombined.Where(r =>
                        r.DatabaseName == dbName &&
                        r.EvidenceFamily == family);
                    return dbResults.Count(r => r.IsDefined && r.IsSignificant(alpha));
                })
                .ToList();

            if (familyPassCounts.Count > 0)
            {
                perFamilyProfiles[family] = new NullDistributionProfile(
                    $"Family_{family}_PassCount",
                    familyPassCounts.Select(c => (double)c));
            }
        }

        NullDistributionProfile? combinedPProfile = null;
        NullDistributionProfile? combinedQProfile = null;
        if (combinedResults.Count > 0)
        {
            var allCombinedResults = combinedResults
                .Where(r => r.MetricName == CombinedResultNames.AllMetricName)
                .ToList();

            var combinedPValues = allCombinedResults
                .Where(r => r.IsDefined && !double.IsNaN(r.PValue))
                .Select(r => r.PValue);

            if (combinedPValues.Any())
                combinedPProfile = new NullDistributionProfile("CombinedPValue", combinedPValues);

            var combinedQValues = allCombinedResults
                .Where(r => r.IsDefined && !double.IsNaN(r.QValue))
                .Select(r => r.QValue);

            if (combinedQValues.Any())
                combinedQProfile = new NullDistributionProfile("CombinedQValue", combinedQValues);
        }

        NullDistributionProfile? perFamilyPassProfile = null;
        var distinctFamilyPassCounts = allDbNames
            .Select(dbName =>
            {
                var dbResults = nonCombined.Where(r =>
                    r.DatabaseName == dbName && r.EvidenceFamily.HasValue);
                return dbResults
                    .Where(r => r.IsDefined && r.IsSignificant(alpha))
                    .Select(r => r.EvidenceFamily!.Value)
                    .Distinct()
                    .Count();
            })
            .ToList();

        if (distinctFamilyPassCounts.Count > 0)
        {
            perFamilyPassProfile = new NullDistributionProfile(
                "DistinctFamiliesPassedPerDatabase",
                distinctFamilyPassCounts.Select(c => (double)c));
        }

        return new CalibrationResult
        {
            TotalDatabases = totalDatabases,
            NullBulkDatabaseCount = totalDatabases,
            Alpha = alpha,
            OverallTestPassCountProfile = overallTestPassProfile,
            OverallFamilyPassCountProfile = overallFamilyPassProfile,
            CombinedPValueProfile = combinedPProfile,
            CombinedQValueProfile = combinedQProfile,
            PerTestPValueProfiles = perTestPValues,
            PerTestEffectSizeProfiles = perTestEffectSizes,
            PerTestStatisticProfiles = perTestStatistics,
            PerFamilyTestPassCountProfiles = perFamilyProfiles,
            PerFamilyPassCountProfile = perFamilyPassProfile,
        };
    }
}
