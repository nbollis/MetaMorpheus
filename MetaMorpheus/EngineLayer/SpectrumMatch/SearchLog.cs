using System;
using System.Collections.Generic;
using System.Linq;

namespace EngineLayer.SpectrumMatch;

public class SearchLog
{
    private readonly int _maxTargetsToKeep;
    private readonly int _maxDecoysToKeep;
    private readonly SortedSet<ISearchAttempt> _targetAttempts;
    private readonly SortedSet<ISearchAttempt> _decoyAttempts;

    public static IComparer<ISearchAttempt> Comparer { get; } = new SearchAttemptComparer();

    public SearchLog(int maxTargetsToKeep = int.MaxValue, int maxDecoysToKeep = int.MaxValue)
    {
        _targetAttempts = new SortedSet<ISearchAttempt>(Comparer);
        _decoyAttempts = new SortedSet<ISearchAttempt>(Comparer);
        _maxTargetsToKeep = maxTargetsToKeep;
        _maxDecoysToKeep = maxDecoysToKeep;
    }

    public void AddAttempt(ISearchAttempt attempt)

    {
        if (attempt.IsDecoy)
        {
            _decoyAttempts.Add(attempt);
            if (_decoyAttempts.Count > _maxDecoysToKeep)
            {
                _decoyAttempts.Remove(_decoyAttempts.Max);
            }
        }
        else
        {
            _targetAttempts.Add(attempt);
            if (_targetAttempts.Count > _maxTargetsToKeep)
            {
                _targetAttempts.Remove(_targetAttempts.Max);
            }
        }
    }

    /// <summary>
    /// Returns all attempts that are within the maxScoreDifferenceAllowed of the best score
    /// </summary>
    public IEnumerable<ISearchAttempt> GetTopScoringAttempts(double maxScoreDifferenceAllowed)
    {
        List<ISearchAttempt> allAttempts = _targetAttempts.Concat(_decoyAttempts).OrderByDescending(a => a.Score).ToList();
        if (allAttempts.Count == 0)
        {
            yield break;
        }

        double bestScore = allAttempts[0].Score;
        foreach (ISearchAttempt attempt in allAttempts)
        {
            if (bestScore - attempt.Score <= maxScoreDifferenceAllowed)
            {
                yield return attempt;
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// Returns all attempts that are within the maxScoreDifferenceAllowed of the best score and retain their sequence information
    /// </summary>
    public IEnumerable<SpectralMatchHypothesis> GetTopScoringAttemptsWithSequenceInformation(double maxScoreDifferenceAllowed)
    {
        return GetTopScoringAttempts(maxScoreDifferenceAllowed)
            .Where(p => p is SpectralMatchHypothesis)
            .Cast<SpectralMatchHypothesis>()
            .OrderByDescending(p => p, SpectralMatch.BioPolymerNotchFragmentIonComparer);
    }

    public IEnumerable<ISearchAttempt> GetAttempts()
    {
        return _targetAttempts.Concat(_decoyAttempts);
    }

    public IEnumerable<ISearchAttempt> GetAttemptsByType(bool isDecoy)
    {
        return isDecoy ? _decoyAttempts : _targetAttempts;
    }

    /// <summary>
    /// Sorts Spectral Matches by best to worst by score, then by notch
    /// Where the best score is the highest score and the lowest notch by absolute value (i.e. -2 is worse than -1)
    /// Does not care about target vs decoy as the class is used for both
    /// </summary>
    private class SearchAttemptComparer : IComparer<ISearchAttempt>
    {
        public int Compare(ISearchAttempt x, ISearchAttempt y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (y is null) return -1;
            if (x is null) return 1;
            int scoreComparison = y.Score.CompareTo(x.Score);
            if (scoreComparison != 0) return scoreComparison;
            return Math.Abs(x.Notch).CompareTo(Math.Abs(y.Notch));
        }
    }
}