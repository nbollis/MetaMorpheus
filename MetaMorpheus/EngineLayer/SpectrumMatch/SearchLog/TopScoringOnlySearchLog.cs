#nullable enable
using Omics.Fragmentation;
using Omics;
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

    public override void AddOrReplace(SpectralMatch spectralMatch, IBioPolymerWithSetMods pwsm, double newScore, int notch, bool reportAllAmbiguity, List<MatchedFragmentIon> matchedFragmentIons, double newXcorr)
    {
        bool added = false;
        if (newScore - spectralMatch.Score > ToleranceForScoreDifferentiation)
        {
            _allAttempts.Clear();
            added = Add(new SpectralMatchHypothesis(notch, pwsm, matchedFragmentIons, newScore));

            if (spectralMatch.Score - spectralMatch.RunnerUpScore > ToleranceForScoreDifferentiation)
            {
                spectralMatch.RunnerUpScore = spectralMatch.Score;
            }
            spectralMatch.Score = newScore;
            spectralMatch.Xcorr = newXcorr;
        }
        else if (newScore - spectralMatch.Score > -ToleranceForScoreDifferentiation && reportAllAmbiguity)
        {
            added = Add(new SpectralMatchHypothesis(notch, pwsm, matchedFragmentIons, newScore));
        }
        else if (newScore - spectralMatch.RunnerUpScore > ToleranceForScoreDifferentiation)
        {
            spectralMatch.RunnerUpScore = newScore;
        }

        if (!added)
        {
            Add(new MinimalSearchAttempt { Score = newScore, IsDecoy = true, Notch = notch, FullSequence = pwsm.FullSequence });
        }
    }

    public override SearchLog CloneWithAttempts(IEnumerable<ISearchAttempt> attempts)
    {
        var toReturn = new TopScoringOnlySearchLog(ToleranceForScoreDifferentiation);
        toReturn.AddRange(attempts);
        return toReturn;
    }
}