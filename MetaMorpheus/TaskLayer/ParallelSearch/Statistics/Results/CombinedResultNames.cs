#nullable enable
using System;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Centralizes string conventions for synthetic combined result rows.
/// Provides helpers for naming, selection keys, and cache keys so that
/// runtime combined rows (TestName="Combined") and CSV-loaded combined rows
/// (TestName="Combined_All", TestName="Combined_CountEnrichment") resolve
/// consistently throughout the framework.
/// </summary>
public static class CombinedResultNames
{
    public const string TestName = "Combined";
    public const string AllMetricName = "All";

    public static bool IsCombinedTestName(string testName)
    {
        return testName == TestName || testName.StartsWith($"{TestName}_", StringComparison.Ordinal);
    }

    public static string GetSelectionKey(string testName, string metricName)
    {
        if (!IsCombinedTestName(testName))
        {
            return testName;
        }

        if (testName == TestName)
        {
            return $"{TestName}_{metricName}";
        }

        return testName;
    }

    public static string GetCacheKey(string metricName)
    {
        return $"{TestName}_{metricName}";
    }
}
