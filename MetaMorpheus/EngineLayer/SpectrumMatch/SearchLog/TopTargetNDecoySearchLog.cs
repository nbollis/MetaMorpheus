#nullable enable
using System.Collections.Generic;
using System.Linq;
using Omics;
using Omics.Fragmentation;

namespace EngineLayer.SpectrumMatch;

public class TopTargetNDecoySearchLog(double toleranceForScoreDifferentiation = SpectralMatch.ToleranceForScoreDifferentiation, double scoreCutoff = 0, uint maxDecoyScoresToRetain = uint.MaxValue)
    : TopScoringOnlySearchLog(toleranceForScoreDifferentiation, scoreCutoff)
{
    private readonly uint _maxDecoyScoresToRetain = maxDecoyScoresToRetain;
    private readonly SortedSet<ISearchAttempt> _decoyAttempts = new(Comparer);

    public override int Count => TopScoringAttempts.Count + _decoyAttempts.Count;

    public override bool Add(ISearchAttempt attempt)
    {
        bool added = base.Add(attempt);
        if (!attempt.IsDecoy) return added;

        // if we add a decoy, add it to the decoy attempts list
        added = AddDecoyAttempt(attempt);
        return added;
    }

    public override bool Remove(ISearchAttempt attempt)
    {
        // This will be true only if the attempt to remove is one of the top scoring (i.e. disambiguation)
        bool removed = base.Remove(attempt);

        // if we remove a decoy, add it back to the decoy attempts list
        if (attempt.IsDecoy && removed)
            AddDecoyAttempt(attempt);
        return removed;
    }

    public override void Clear()
    {
        // save top scoring decoys
        var topScoringDecoys = TopScoringAttempts.Where(p => p.IsDecoy);
        foreach (var decoy in topScoringDecoys)
            AddDecoyAttempt(decoy);

        // clear top scoring list and reset scores
        base.Clear();
    }

    public override bool AddOrReplace(IBioPolymerWithSetMods pwsm, double newScore, int notch, bool reportAllAmbiguity, List<MatchedFragmentIon> matchedFragmentIons)
    {
        // Addition will be successful if the attempt is the highest scoring
        bool added = base.AddOrReplace(pwsm, newScore, notch, reportAllAmbiguity, matchedFragmentIons);

        // if not top scoring, and it is a decoy
        if (!added && pwsm.Parent.IsDecoy)
        {
            var minimalistic = new MinimalSearchAttempt
            {
                IsDecoy = true,
                Notch = notch,
                Score = newScore,
                FullSequence = pwsm.FullSequence
            };
            added = AddDecoyAttempt(minimalistic);
        }
        return added;
    }

    public override IEnumerable<ISearchAttempt> GetAttempts() => TopScoringAttempts.Concat(_decoyAttempts);

    public override SearchLog CloneWithAttempts(IEnumerable<ISearchAttempt> attempts)
    {
        var toReturn = new TopTargetNDecoySearchLog(ToleranceForScoreDifferentiation, ScoreCutoff, _maxDecoyScoresToRetain)
        {
            Score = Score,
            RunnerUpScore = RunnerUpScore,
            NumberOfBestScoringResults = NumberOfBestScoringResults
        };
        toReturn.AddRange(attempts);

        return toReturn;
    }

    private bool AddDecoyAttempt(ISearchAttempt attempt)
    {
        bool added = false;
        if (!attempt.IsDecoy)
            return added;

        // If the log is at capacity and attempt is not better than the worst decoy score, don't add it
        if (_decoyAttempts.Count >= _maxDecoyScoresToRetain && Comparer.Compare(attempt, _decoyAttempts.Max!) <= 0)
            return added;

        // Only cache the minimal information
        if (attempt is not MinimalSearchAttempt msa)
            msa = new MinimalSearchAttempt(attempt);

        return _decoyAttempts.Add(msa);
    }
}