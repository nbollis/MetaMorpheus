#nullable enable
using System;
using Omics.Fragmentation;
using Omics;
using System.Collections.Generic;
using System.Linq;

namespace EngineLayer.SpectrumMatch;

public class KeepNScoresSearchLog : TopScoringOnlySearchLog
{
    // these are all results except for the top scoring result. 
    private readonly SortedSet<ISearchAttempt> _targetAttempts;
    private readonly SortedSet<ISearchAttempt> _decoyAttempts;
    private readonly ushort _maxDecoysToKeep;
    private readonly ushort _maxTargetsToKeep;

    public override int Count => TopScoringAttempts.Count + _decoyAttempts.Count + _targetAttempts.Count;

    public KeepNScoresSearchLog(double tolerance = SpectralMatch.ToleranceForScoreDifferentiation, double scoreCutoff = 0, ushort maxTargetsToKeep = ushort.MaxValue, ushort maxDecoysToKeep = ushort.MaxValue)
        : base(tolerance, scoreCutoff)
    {
        _targetAttempts = new(Comparer);
        _decoyAttempts = new SortedSet<ISearchAttempt>(Comparer);
        _maxDecoysToKeep = maxDecoysToKeep;
        _maxTargetsToKeep = maxTargetsToKeep;
    }

    private void AddToInternalCollections(ISearchAttempt attempt)
    {
        // add it to the respective set if there is room
        SortedSet <ISearchAttempt> attempts = attempt.IsDecoy ? _decoyAttempts : _targetAttempts;
        uint maxToKeep = attempt.IsDecoy ? _maxDecoysToKeep : _maxTargetsToKeep;

        // Ensure there is room
        if (attempts.Count >= maxToKeep && Comparer.Compare(attempt, attempts.Max) <= 0)
            return;

        // Add to set and trim if necessary
        bool added = attempts.Add(attempt);
        if (added && attempts.Count > maxToKeep)
        {
            attempts.Remove(attempts.Max!);
        }
    }

    public override bool Add(ISearchAttempt attempt)
    {
        // Returns true if added to the top scoring set. 
        bool added = base.Add(attempt);

        // If it was not added to the top scoring set, add to the respective set
        if (!added && !TopScoringAttempts.Contains(attempt))
            AddToInternalCollections(attempt);

        return added;
    }

    public override bool Remove(ISearchAttempt attempt)
    {
        // This will be true only if the attempt to remove is one of the top scoring (i.e. disambiguation)
        bool removed = base.Remove(attempt);

        // if we removed a top scoring attempt, add back to the respective set
        if (removed)
            AddToInternalCollections(attempt);

        return removed;
    }

    public override bool RemoveDestructively(ISearchAttempt attempt) => base.Remove(attempt);

    public override bool AddOrReplace(IBioPolymerWithSetMods pwsm, double newScore, int notch, bool reportAllAmbiguity, List<MatchedFragmentIon> matchedFragmentIons)
    {
        // Addition will be successful if the attempt is the highest scoring
        bool added = base.AddOrReplace(pwsm, newScore, notch, reportAllAmbiguity, matchedFragmentIons);

        // if ambiguity is not allowed and new result has same score as best, do nothing and return 
        if (!reportAllAmbiguity && !added && Math.Abs(Score - newScore) < ToleranceForScoreDifferentiation)
            return added;

        // if not top scoring
        if (!added)
        {
            // add it to the respective set if there is room
            SortedSet<ISearchAttempt> attempts = pwsm.Parent.IsDecoy ? _decoyAttempts : _targetAttempts;
            uint maxToKeep = pwsm.Parent.IsDecoy ? _maxDecoysToKeep : _maxTargetsToKeep;

            // Ensure there is room and score is better than worst score
            if (attempts.Count >= maxToKeep && (attempts.Max == null || newScore < attempts.Max.Score))
                return added;

            var minimalistic = new MinimalSearchAttempt
            {
                IsDecoy = true,
                Notch = notch,
                Score = newScore,
                FullSequence = pwsm.FullSequence
            };

            // Add to set and trim if necessary
            added = attempts.Add(minimalistic);
            if (added && attempts.Count > maxToKeep)
                attempts.Remove(attempts.Max!);
        }
        return added;
    }

    public override void Clear()
    {
        // save top scoring 
        foreach (var attempt in TopScoringAttempts)
            AddToInternalCollections(attempt);

        // clear top scoring list and reset scores
        base.Clear();
    }

    public override IEnumerable<ISearchAttempt> GetAttempts()
    {
        return TopScoringAttempts.Concat(_targetAttempts).Concat(_decoyAttempts);
    }

    public override IEnumerable<ISearchAttempt> GetAttemptsByType(bool isDecoy)
    {
        var topScoringToReturn = TopScoringAttempts.Where(p => p.IsDecoy == isDecoy);
        return topScoringToReturn.Concat(isDecoy ? _decoyAttempts : _targetAttempts);
    }

    public override KeepNScoresSearchLog CloneWithAttempts(IEnumerable<ISearchAttempt> attempts)
    {
        var toReturn = new KeepNScoresSearchLog(ToleranceForScoreDifferentiation, ScoreCutoff, _maxTargetsToKeep, _maxDecoysToKeep)
        {
            Score = Score,
            RunnerUpScore = RunnerUpScore,
            NumberOfBestScoringResults = NumberOfBestScoringResults
        };

        toReturn.AddRange(attempts);
        toReturn.AddRange(_targetAttempts);
        toReturn.AddRange(_decoyAttempts);
        return toReturn;
    }
}