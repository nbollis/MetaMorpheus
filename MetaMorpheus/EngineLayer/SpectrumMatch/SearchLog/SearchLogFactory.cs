using System.Collections.Generic;
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
    internal static Dictionary<SearchLogType, ISearchLogFactory> AllSupportedLogs = new()
    {
        {SearchLogType.TopScoringOnly, new TopScoringOnlyLogFactory()},
        {SearchLogType.KeepAllDecoyScores, new KeepNScoresLogFactory(0, uint.MaxValue)},
        {SearchLogType.Keep7DecoyScores, new KeepNScoresLogFactory(0, 7)},
        {SearchLogType.KeepAllTargetAndDecoyScores, new KeepNScoresLogFactory(uint.MaxValue, uint.MaxValue)},
    };

    internal static ISearchLogFactory GetSearchLogFactory(SearchLogType searchLogType)
    {
        return AllSupportedLogs[searchLogType];
    }

    internal static SearchLog GetSearchLog(SearchLogType searchLogType, double scoreCutoff = 0)
    {
        return GetSearchLogFactory(searchLogType).GetSearchLog(scoreCutoff);
    }

    // This region defines the concrete factories that create search logs
    #region Concrete Factories

    /// <summary>
    /// Creates standard search log, keeps top scoring results, discards once any result outscores it. 
    /// </summary>
    private class TopScoringOnlyLogFactory : ISearchLogFactory
    {
        public SearchLog GetSearchLog(double scoreCutoff) => new TopScoringOnlySearchLog(scoreCutoff: scoreCutoff);
    }

    private class KeepNScoresLogFactory(uint targetsToKeep, uint decoysToKeep) : ISearchLogFactory
    {
        public SearchLog GetSearchLog(double scoreCutoff) => new KeepNScoresSearchLog(scoreCutoff: scoreCutoff, maxDecoysToKeep: decoysToKeep, maxTargetsToKeep: targetsToKeep);
    }

    #endregion
}

internal interface ISearchLogFactory
{
    SearchLog GetSearchLog(double scoreCutoff = 0);
}