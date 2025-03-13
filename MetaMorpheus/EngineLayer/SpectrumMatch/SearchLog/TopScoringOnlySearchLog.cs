#nullable enable
using System;
using Omics.Fragmentation;
using Omics;
using System.Collections.Generic;
using System.Linq;

namespace EngineLayer.SpectrumMatch;

public class TopScoringOnlySearchLog(double toleranceForScoreDifferentiation = SpectralMatch.ToleranceForScoreDifferentiation, double scoreCutoff = 0)
    : SearchLog(toleranceForScoreDifferentiation, scoreCutoff)
{
    private readonly SortedSet<ISearchAttempt> _allAttempts = new(Comparer);

    public override int Count => _allAttempts.Count;
    public override bool Add(ISearchAttempt attempt) => _allAttempts.Add(attempt);
    public override bool Remove(ISearchAttempt attempt)
    {
        bool removed = _allAttempts.Remove(attempt);
        NumberOfBestScoringResults--;

        //bool removedHighest = Math.Abs(attempt.Score - Score) < ToleranceForScoreDifferentiation;

        //// We removed the highest scoring result, and there are still results left, so we need to update score properties
        //// We do not need to update RunnerUpScore as only those tied for best score are retained in this log. 
        //if (removedHighest && _allAttempts.Count > 0)
        //{
        //    Score = _allAttempts.Max?.Score ?? 0;
        //    NumberOfBestScoringResults--;
        //}


        return removed;
    }

    public override IEnumerable<ISearchAttempt> GetAttempts() => _allAttempts.AsEnumerable(); 
    public override void Clear() => _allAttempts.Clear();
    public override bool AddOrReplace(IBioPolymerWithSetMods pwsm, double newScore, int notch, bool reportAllAmbiguity, List<MatchedFragmentIon> matchedFragmentIons)
    {
        bool added = false;
        if (newScore - Score > ToleranceForScoreDifferentiation)
        {
            Clear();
            added = Add(new SpectralMatchHypothesis(notch, pwsm, matchedFragmentIons, newScore));

            if (Score - RunnerUpScore > ToleranceForScoreDifferentiation)
            {
                RunnerUpScore = Score;
            }
            Score = newScore;
            NumberOfBestScoringResults = 1;
        }
        else if (newScore - Score > -ToleranceForScoreDifferentiation && reportAllAmbiguity)
        {
            added = Add(new SpectralMatchHypothesis(notch, pwsm, matchedFragmentIons, newScore));
            NumberOfBestScoringResults++;
        }
        else if (newScore - RunnerUpScore > ToleranceForScoreDifferentiation)
        {
            RunnerUpScore = newScore;
        }
        return added;
    }

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