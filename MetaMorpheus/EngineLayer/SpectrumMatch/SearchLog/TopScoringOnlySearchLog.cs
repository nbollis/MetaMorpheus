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

        UpdateScoresOnAddition(attempt.Score);

        return added;
    }

    public override bool Remove(ISearchAttempt attempt)
    {
        bool removed = _allAttempts.Remove(attempt);
        if (!removed) return false;

        // We removed the highest scoring result, so we need to update relevant reporting properties
        if (Math.Abs(attempt.Score - Score) < ToleranceForScoreDifferentiation)
        {
            // if results are left, update the score properties
            if (_allAttempts.Count > 0)
            {
                var minAttempt = _allAttempts.Min!;
                Score = minAttempt.Score;

                // update the runner-up score if the next highest score is significantly different otherwise default
                var runnerUp = _allAttempts.FirstOrDefault(p => Math.Abs(p.Score - Score) > ToleranceForScoreDifferentiation);
                RunnerUpScore = runnerUp?.Score ?? ScoreCutoff;
                NumberOfBestScoringResults = _allAttempts.Count(p => Math.Abs(p.Score - Score) < ToleranceForScoreDifferentiation);
            }
            else
            {
                Score = 0;
                RunnerUpScore = ScoreCutoff;
                NumberOfBestScoringResults = 0;
            }
        }
        // if we removed the runner-up result, we need to update the runner-up score
        else if (Math.Abs(attempt.Score - RunnerUpScore) < ToleranceForScoreDifferentiation)
        {
            var runnerUp = _allAttempts.FirstOrDefault(p => Math.Abs(p.Score - Score) > ToleranceForScoreDifferentiation);
            RunnerUpScore = runnerUp?.Score ?? ScoreCutoff;
        }

        return true;
    }

    public override void Clear()
    {
        RunnerUpScore = ScoreCutoff;
        Score = 0;
        NumberOfBestScoringResults = 0;
        _allAttempts.Clear();
    }

    public override bool AddOrReplace(IBioPolymerWithSetMods pwsm, double newScore, int notch, bool reportAllAmbiguity, List<MatchedFragmentIon> matchedFragmentIons)
    {
        bool added = false;
        // New score beat the old score, overwrite
        if (newScore - Score > ToleranceForScoreDifferentiation)
        { 
            _allAttempts.Clear();
            added = _allAttempts.Add(new SpectralMatchHypothesis(notch, pwsm, matchedFragmentIons, newScore));
            UpdateScoresOnAddition(newScore);
        }
        // The same score and ambiguity is allowed, add
        else if (newScore - Score > -ToleranceForScoreDifferentiation && reportAllAmbiguity) 
        {
            added = _allAttempts.Add(new SpectralMatchHypothesis(notch, pwsm, matchedFragmentIons, newScore));
            NumberOfBestScoringResults++;
        }
        // The new score is better than the runner-up score, update the runner-up score
        else if (newScore - RunnerUpScore > ToleranceForScoreDifferentiation && newScore >= ScoreCutoff) 
        {
            RunnerUpScore = newScore;
        }
        return added;
    }

    public override IEnumerable<ISearchAttempt> GetAttempts() => _allAttempts.AsEnumerable();

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