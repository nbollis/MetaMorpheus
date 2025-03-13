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
    protected readonly SortedSet<ISearchAttempt> TopScoringAttempts = new(Comparer);

    public override int Count => TopScoringAttempts.Count;
    public override bool Add(ISearchAttempt attempt)
    {
        bool added = TopScoringAttempts.Add(attempt);

        if (!added) return added;

        UpdateScoresOnAddition(attempt.Score);

        return added;
    }

    public override bool Remove(ISearchAttempt attempt)
    {
        bool removed = TopScoringAttempts.Remove(attempt);
        if (!removed) return false;

        // We removed the highest scoring result, so we need to update relevant reporting properties
        if (Math.Abs(attempt.Score - Score) < ToleranceForScoreDifferentiation)
        {
            // if results are left, update the score properties
            if (TopScoringAttempts.Count > 0)
            {
                var minAttempt = TopScoringAttempts.Min!;
                Score = minAttempt.Score;

                // update the runner-up score if the next highest score is significantly different otherwise default
                var runnerUp = TopScoringAttempts.FirstOrDefault(p => Math.Abs(p.Score - Score) > ToleranceForScoreDifferentiation);
                RunnerUpScore = runnerUp?.Score ?? ScoreCutoff;
                NumberOfBestScoringResults = TopScoringAttempts.Count(p => Math.Abs(p.Score - Score) < ToleranceForScoreDifferentiation);
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
            var runnerUp = TopScoringAttempts.FirstOrDefault(p => Math.Abs(p.Score - Score) > ToleranceForScoreDifferentiation);
            RunnerUpScore = runnerUp?.Score ?? ScoreCutoff;
        }

        return true;
    }

    public override void Clear()
    {
        RunnerUpScore = ScoreCutoff;
        Score = 0;
        NumberOfBestScoringResults = 0;
        TopScoringAttempts.Clear();
    }

    public override bool AddOrReplace(IBioPolymerWithSetMods pwsm, double newScore, int notch, bool reportAllAmbiguity, List<MatchedFragmentIon> matchedFragmentIons)
    {
        bool added = false;
        // New score beat the old score, overwrite
        if (newScore - Score > ToleranceForScoreDifferentiation)
        {
            var runnerUpScore = Math.Max(RunnerUpScore, ScoreCutoff);
            Clear();
            added = TopScoringAttempts.Add(new SpectralMatchHypothesis(notch, pwsm, matchedFragmentIons, newScore));
            UpdateScoresOnAddition(newScore);
            RunnerUpScore = runnerUpScore;
        }
        // The same score and ambiguity is allowed, add
        else if (newScore - Score > -ToleranceForScoreDifferentiation && reportAllAmbiguity) 
        {
            added = TopScoringAttempts.Add(new SpectralMatchHypothesis(notch, pwsm, matchedFragmentIons, newScore));
            NumberOfBestScoringResults++;
        }
        // The new score is better than the runner-up score, update the runner-up score
        else if (newScore - RunnerUpScore > ToleranceForScoreDifferentiation && newScore >= ScoreCutoff) 
        {
            RunnerUpScore = newScore;
        }
        return added;
    }

    public override IEnumerable<ISearchAttempt> GetAttempts() => TopScoringAttempts.AsEnumerable();

    public override SearchLog CloneWithAttempts(IEnumerable<ISearchAttempt> attempts)
    {
        var toReturn = new TopScoringOnlySearchLog(ToleranceForScoreDifferentiation, ScoreCutoff)
        {
            Score = Score,
            RunnerUpScore = RunnerUpScore,
            NumberOfBestScoringResults = NumberOfBestScoringResults
        };
        toReturn.AddRange(attempts);

        return toReturn;
    }
}