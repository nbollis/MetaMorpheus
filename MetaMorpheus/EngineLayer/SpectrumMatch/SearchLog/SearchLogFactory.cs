using System.Collections.Generic;
namespace EngineLayer.SpectrumMatch;

/// <summary>
/// Defines the behavior of a search log that keeps track of search attempts
/// </summary>
public enum SearchLogType
{
    TopScoringOnly,
    KeepNScores,
}

/// <summary>
/// Generates search logs based on the type of search log requested
/// </summary>
internal static class SearchLogFactory
{
    internal static Dictionary<SearchLogType, ISearchLogFactory> AllSupportedLogs = new()
    {
        {SearchLogType.TopScoringOnly, new StandardSearchLogFactory()},
    };

    internal static ISearchLogFactory GetSearchLogFactory(SearchLogType searchLogType)
    {
        return AllSupportedLogs[searchLogType];
    }

    internal static SearchLog GetSearchLog(SearchLogType searchLogType)
    {
        return GetSearchLogFactory(searchLogType).GetSearchLog();
    }


    // This region defines the concrete factories that create search logs
    #region Concrete Factories

    /// <summary>
    /// Creates standard search log, keeps top scoring results, discards once any result outscores it. 
    /// </summary>
    private class StandardSearchLogFactory : ISearchLogFactory
    {
        public SearchLog GetSearchLog() => new TopScoringOnlySearchLog();
    }

    #endregion
}

internal interface ISearchLogFactory
{
    SearchLog GetSearchLog();
}