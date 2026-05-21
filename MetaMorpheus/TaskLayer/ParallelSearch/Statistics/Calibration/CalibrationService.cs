#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskLayer.ParallelSearch.Statistics.Calibration;

public sealed class CalibrationService
{
    private const int MaxIterations = 5;
    private const double OutlierPercentileThreshold = 0.95;

    public CalibrationResult Calibrate(List<StatisticalTestResult> allResults, double alpha = 0.05)
    {
        if (allResults == null || allResults.Count == 0)
        {
            return new CalibrationResult
            {
                TotalDatabases = 0,
                NullBulkDatabaseCount = 0,
                DatabasesRemovedAsOutliers = 0,
                IterationsUsed = 0,
                Alpha = alpha
            };
        }

        var nonCombined = allResults.Where(r => !r.IsCombinedResult).ToList();
        var combinedResults = allResults.Where(r => r.IsCombinedResult).ToList();

        int totalDatabases = nonCombined.GroupBy(r => r.DatabaseName).Count();

        var (nullBulk, removedCount, iterations) = IdentifyNullBulk(nonCombined, alpha);

        var nullBulkResults = nonCombined
            .Where(r => nullBulk.Contains(r.DatabaseName))
            .ToList();

        var nullBulkCombined = combinedResults
            .Where(r => nullBulk.Contains(r.DatabaseName))
            .ToList();

        var perTestPValues = new Dictionary<string, NullDistributionProfile>();
        var perTestEffectSizes = new Dictionary<string, NullDistributionProfile>();
        var perTestStatistics = new Dictionary<string, NullDistributionProfile>();

        foreach (var testGroup in nullBulkResults.GroupBy(r => r.Key))
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

        var testPassCountsPerDb = nullBulk
            .Select(dbName =>
            {
                var dbResults = nullBulkResults.Where(r => r.DatabaseName == dbName);
                return dbResults.Count(r => r.IsDefined && r.IsSignificant(alpha));
            })
            .ToList();

        var overallTestPassProfile = testPassCountsPerDb.Count > 0
            ? new NullDistributionProfile("TestsPassedPerDatabase", testPassCountsPerDb.Select(c => (double)c))
            : null;

        var familyPassCountsPerDb = nullBulk
            .Select(dbName =>
            {
                var dbResults = nullBulkResults.Where(r => r.DatabaseName == dbName && r.EvidenceFamily.HasValue);
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
            var familyPassCounts = nullBulk
                .Select(dbName =>
                {
                    var dbResults = nullBulkResults.Where(r =>
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
        if (nullBulkCombined.Count > 0)
        {
            var allCombinedResults = nullBulkCombined
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
        var distinctFamilyPassCounts = nullBulk
            .Select(dbName =>
            {
                var dbResults = nullBulkResults.Where(r =>
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
            NullBulkDatabaseCount = nullBulk.Count,
            DatabasesRemovedAsOutliers = removedCount,
            IterationsUsed = iterations,
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

    private (HashSet<string> nullBulk, int removedCount, int iterations) IdentifyNullBulk(
        List<StatisticalTestResult> results, double alpha)
    {
        var dbSigCounts = results
            .GroupBy(r => r.DatabaseName)
            .Select(g => (
                DbName: g.Key,
                SigCount: g.Count(r => r.IsDefined && r.IsSignificant(alpha))
            ))
            .OrderBy(p => p.SigCount)
            .ToList();

        if (dbSigCounts.Count < 3)
            return (dbSigCounts.Select(p => p.DbName).ToHashSet(), 0, 1);

        var distinctCounts = dbSigCounts.Select(p => p.SigCount).Distinct().Count();
        int keepCount = Math.Max(1, (int)Math.Ceiling(OutlierPercentileThreshold * dbSigCounts.Count));
        keepCount = Math.Min(keepCount, dbSigCounts.Count);

        int removedCount = dbSigCounts.Count - keepCount;

        if (removedCount == 0 || distinctCounts == 1)
        {
            return (dbSigCounts.Select(p => p.DbName).ToHashSet(),
                distinctCounts == 1 ? 0 : removedCount,
                distinctCounts == 1 ? 0 : 1);
        }

        var nullBulk = dbSigCounts.Take(keepCount).Select(p => p.DbName).ToHashSet();
        var removedList = dbSigCounts.Skip(keepCount).ToList();

        for (int iteration = 2; iteration <= MaxIterations; iteration++)
        {
            var nullSigCounts = nullBulk
                .Select(dbName => (double)results.Where(r => r.DatabaseName == dbName)
                    .Count(r => r.IsDefined && r.IsSignificant(alpha)))
                .ToList();

            if (nullSigCounts.Count < 3)
                break;

            double nullMean = nullSigCounts.Average();
            double nullVar = nullSigCounts.Sum(v => (v - nullMean) * (v - nullMean)) / nullSigCounts.Count;
            double nullSd = Math.Sqrt(nullVar);
            double upperBound = nullMean + 3.0 * Math.Max(nullSd, 0.5);

            var toReintegrate = removedList
                .Where(p => p.SigCount <= upperBound)
                .ToList();

            if (toReintegrate.Count == 0)
                break;

            foreach (var reintegrated in toReintegrate)
                nullBulk.Add(reintegrated.DbName);

            removedList = removedList.Except(toReintegrate).ToList();
            removedCount = removedList.Count;

            if (removedCount == 0)
                break;
        }

        return (nullBulk, removedCount, MaxIterations);
    }
}
