#nullable enable
using EngineLayer;
using System.Collections.Generic;
using System.Linq;

namespace EngineLayer.SpectrumMatch;

public class TopScoringOnlySearchLog(double toleranceForScoreDifferentiation = SpectralMatch.ToleranceForScoreDifferentiation)
    : SearchLog(toleranceForScoreDifferentiation)
{
    private readonly SortedSet<ISearchAttempt> _allAttempts = new(Comparer);

    public override bool Add(ISearchAttempt attempt) => _allAttempts.Add(attempt);
    public override bool Remove(ISearchAttempt attempt) => _allAttempts.Remove(attempt);
    public override IEnumerable<ISearchAttempt> GetAttempts() => _allAttempts.AsEnumerable();

    public override void Clear()
    {
        _allAttempts.Clear();
    }

    public override SearchLog CloneWithAttempts(IEnumerable<ISearchAttempt> attempts)
    {
        var toReturn = new TopScoringOnlySearchLog(ToleranceForScoreDifferentiation);
        toReturn.AddRange(attempts);
        return toReturn;
    }
}