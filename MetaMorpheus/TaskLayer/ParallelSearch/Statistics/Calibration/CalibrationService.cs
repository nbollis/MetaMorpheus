#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskLayer.ParallelSearch.Statistics.Calibration;

public sealed class CalibrationService
{
    private static readonly StatisticalEvidenceFamily[] AllFamilies =
        Enum.GetValues<StatisticalEvidenceFamily>();

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

            AddProfileIfData($"pValue_{key}", definedResults.Select(r => r.PValue), perTestPValues, key);

            AddProfileIfData($"effectSize_{key}", definedResults, r => r.EffectSize, perTestEffectSizes, key);

            AddProfileIfData($"testStatistic_{key}", definedResults, r => r.TestStatistic, perTestStatistics, key);
        }

        // Pre-index by database name: one pass over nonCombined, then O(1) per-DB lookups
        var byDb = nonCombined.ToLookup(r => r.DatabaseName);

        // Single pass over databases to compute all pass-count profiles
        var testPassCountsPerDb = new List<int>(allDbNames.Count);
        var familyPassCountsPerDb = new List<int>(allDbNames.Count);
        var perFamilyCounts = new Dictionary<StatisticalEvidenceFamily, List<int>>();
        foreach (var family in AllFamilies)
            perFamilyCounts[family] = new List<int>(allDbNames.Count);

        foreach (var dbName in allDbNames)
        {
            var dbResults = byDb[dbName];

            int testPasses = dbResults.Count(r => r.IsDefined && r.IsSignificant(alpha));
            testPassCountsPerDb.Add(testPasses);

            int distinctFamiliesPassed = 0;
            foreach (var family in AllFamilies)
            {
                int familyPasses = dbResults.Count(r =>
                    r.EvidenceFamily == family && r.IsDefined && r.IsSignificant(alpha));
                perFamilyCounts[family].Add(familyPasses);
                if (familyPasses > 0)
                    distinctFamiliesPassed++;
            }

            familyPassCountsPerDb.Add(distinctFamiliesPassed);
        }

        // NullDistributionProfile constructor already filters NaN/Infinity internally,
        // so we pass raw int sequences directly.
        var overallTestPassProfile = testPassCountsPerDb.Count > 0
            ? new NullDistributionProfile("TestsPassedPerDatabase", testPassCountsPerDb.Select(c => (double)c))
            : null;

        // Reuse familyPassCountsPerDb for both profiles (was duplicated)
        var overallFamilyPassProfile = familyPassCountsPerDb.Count > 0
            ? new NullDistributionProfile("FamiliesPassedPerDatabase", familyPassCountsPerDb.Select(c => (double)c))
            : null;

        var perFamilyProfiles = new Dictionary<StatisticalEvidenceFamily, NullDistributionProfile>();
        foreach (var family in AllFamilies)
        {
            var counts = perFamilyCounts[family];
            if (counts.Count > 0)
            {
                perFamilyProfiles[family] = new NullDistributionProfile(
                    $"Family_{family}_PassCount",
                    counts.Select(c => (double)c));
            }
        }

        NullDistributionProfile? combinedPProfile = null;
        NullDistributionProfile? combinedQProfile = null;
        if (combinedResults.Count > 0)
        {
            var allCombinedResults = combinedResults
                .Where(r => r.MetricName == CombinedResultNames.AllMetricName)
                .ToList();

            AddProfileIfData("CombinedPValue", allCombinedResults.Select(r => r.PValue), out combinedPProfile);
            AddProfileIfData("CombinedQValue", allCombinedResults.Select(r => r.QValue), out combinedQProfile);
        }

        NullDistributionProfile? perFamilyPassProfile = null;
        if (familyPassCountsPerDb.Count > 0)
        {
            perFamilyPassProfile = new NullDistributionProfile(
                "DistinctFamiliesPassedPerDatabase",
                familyPassCountsPerDb.Select(c => (double)c));
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

    private static void AddProfileIfData(string label, IEnumerable<double> values, out NullDistributionProfile? profile)
    {
        var filtered = values.Where(v => !double.IsNaN(v)).ToList();
        profile = filtered.Count > 0 ? new NullDistributionProfile(label, filtered) : null;
    }

    private static void AddProfileIfData(string label, IEnumerable<double> values, Dictionary<string, NullDistributionProfile> target, string key)
    {
        AddProfileIfData(label, values, out var profile);
        if (profile != null)
            target[key] = profile;
    }

    private static void AddProfileIfData(
        string label,
        IEnumerable<StatisticalTestResult> results,
        Func<StatisticalTestResult, double?> selector,
        Dictionary<string, NullDistributionProfile> target,
        string key)
    {
        var values = results
            .Select(selector)
            .Where(v => v.HasValue && !double.IsNaN(v.Value) && !double.IsInfinity(v.Value))
            .Select(v => v!.Value)
            .ToList();
        if (values.Count > 0)
            target[key] = new NullDistributionProfile(label, values);
    }
}
