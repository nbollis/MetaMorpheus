using System;
namespace EngineLayer.SpectrumMatch;

/// <summary>
/// Defines the behavior of a search log that keeps track of search attempts
/// </summary>
public enum SearchLogType
{
    TopScoringOnly,
    KeepAllDecoyScores,
    Keep7DecoyScores,
    KeepAllTargetAndDecoyScores,
}

/// <summary>
/// Generates search logs based on the type of search log requested
/// </summary>
internal static class SearchLogFactory
{
    internal static SearchLog GetSearchLog(SearchLogType searchLogType, double scoreCutoff = 0)
    {
        return searchLogType switch
        {
            SearchLogType.TopScoringOnly => new TopScoringOnlySearchLog(scoreCutoff),
            SearchLogType.KeepAllDecoyScores => new KeepNScoresSearchLog(scoreCutoff, 0, ushort.MaxValue),
            SearchLogType.Keep7DecoyScores => new KeepNScoresSearchLog(scoreCutoff, 0, 7),
            SearchLogType.KeepAllTargetAndDecoyScores => new KeepNScoresSearchLog(scoreCutoff, ushort.MaxValue, ushort.MaxValue),
            _ => throw new ArgumentOutOfRangeException(nameof(searchLogType), searchLogType, null)
        };
    }
}