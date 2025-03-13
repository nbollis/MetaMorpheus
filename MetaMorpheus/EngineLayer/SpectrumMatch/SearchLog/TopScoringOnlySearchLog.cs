#nullable enable
using Omics.Fragmentation;
using Omics;
using System.Collections.Generic;
using System.Linq;

namespace EngineLayer.SpectrumMatch;

public class TopScoringOnlySearchLog(double toleranceForScoreDifferentiation = SpectralMatch.ToleranceForScoreDifferentiation, double scoreCutoff = 0)
    : SearchLog(toleranceForScoreDifferentiation, scoreCutoff)
{
    private readonly SortedSet<ISearchAttempt> _allAttempts = new(Comparer);

    public override bool Add(ISearchAttempt attempt) => _allAttempts.Add(attempt);
    public override bool Remove(ISearchAttempt attempt) => _allAttempts.Remove(attempt);
    public override IEnumerable<ISearchAttempt> GetAttempts() => _allAttempts.AsEnumerable();
    public override void AddOrReplace(IBioPolymerWithSetMods pwsm, double newScore, int notch, bool reportAllAmbiguity, List<MatchedFragmentIon> matchedFragmentIons)
    {
        if (newScore - Score > ToleranceForScoreDifferentiation)
        {
            Clear();
            Add(new SpectralMatchHypothesis(notch, pwsm, matchedFragmentIons, newScore));

            if (Score - RunnerUpScore > ToleranceForScoreDifferentiation)
            {
                RunnerUpScore = Score;
            }
            Score = newScore;
        }
        else if (newScore - Score > -ToleranceForScoreDifferentiation && reportAllAmbiguity)
        {
            Add(new SpectralMatchHypothesis(notch, pwsm, matchedFragmentIons, newScore));
        }
        else if (newScore - RunnerUpScore > ToleranceForScoreDifferentiation)
        {
            RunnerUpScore = newScore;
        }
    }

    public override void Clear() => _allAttempts.Clear();

    public override SearchLog CloneWithAttempts(IEnumerable<ISearchAttempt> attempts)
    {
        var toReturn = new TopScoringOnlySearchLog(ToleranceForScoreDifferentiation);
        toReturn.Score = Score;
        toReturn.RunnerUpScore = RunnerUpScore;
        toReturn.NumberOfBestScoringResults = NumberOfBestScoringResults;
        toReturn.AddRange(attempts);

        return toReturn;
    }
}