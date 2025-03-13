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
    public override bool Add(ISearchAttempt attempt)
    {
        bool added = _allAttempts.Add(attempt);

        if (!added) return added;

        UpdateScores(attempt.Score);

        return added;
    }

    public override bool Remove(ISearchAttempt attempt)
    {
        bool removed = _allAttempts.Remove(attempt);
        bool removedHighest = Math.Abs(attempt.Score - Score) < ToleranceForScoreDifferentiation;

        if (!removed) return removed;
        if (!removedHighest) return removed;

        // We removed the highest scoring result, so we need to update relevant reporting properties
        NumberOfBestScoringResults--;
        if (_allAttempts.Count > 0) // if results are left, update the score properties
        {
            Score = _allAttempts.Max!.Score;

            // update the runner-up score if the next highest score is significantly different otherwise keep the current runner-up score
            var runnerUp = _allAttempts.FirstOrDefault(p => Math.Abs(p.Score - Score) > ToleranceForScoreDifferentiation);
            RunnerUpScore = runnerUp?.Score ?? RunnerUpScore;
        }
        else
        {
            Score = 0;
        }

        return removed;
    }

    public override IEnumerable<ISearchAttempt> GetAttempts() => _allAttempts.AsEnumerable(); 
    public override void Clear()
    {
        Score = 0;
        RunnerUpScore = scoreCutoff;
        NumberOfBestScoringResults = 0;
        _allAttempts.Clear();
    }

    public override bool AddOrReplace(IBioPolymerWithSetMods pwsm, double newScore, int notch, bool reportAllAmbiguity, List<MatchedFragmentIon> matchedFragmentIons)
    {
        bool added = false;
        // New score beat the old score, overwrite
        if (newScore - Score > ToleranceForScoreDifferentiation) 
        {
            Clear();
            added = _allAttempts.Add(new SpectralMatchHypothesis(notch, pwsm, matchedFragmentIons, newScore));
            UpdateScores(newScore);
        }
        // The same score and ambiguity is allowed, add
        else if (newScore - Score > -ToleranceForScoreDifferentiation && reportAllAmbiguity) 
        {
            added = _allAttempts.Add(new SpectralMatchHypothesis(notch, pwsm, matchedFragmentIons, newScore));
            NumberOfBestScoringResults++;
        }
        // The new score is better than the runner-up score, update the runner-up score
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