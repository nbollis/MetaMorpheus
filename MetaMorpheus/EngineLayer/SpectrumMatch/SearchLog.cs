#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Proteomics;

namespace EngineLayer.SpectrumMatch;

public class SearchLog
{
    private readonly uint _maxTargetsToKeep;
    private readonly uint _maxDecoysToKeep;
    private readonly double _toleranceForScoreDifferentiation;
    private readonly SortedSet<ISearchAttempt> _targetAttempts;
    private readonly SortedSet<ISearchAttempt> _decoyAttempts;

    public static IComparer<ISearchAttempt> Comparer { get; } = new SearchAttemptComparer();

    // TODO: Add in Score, RunnerUpScore, BestMatchingCount
    // TODO: Add in a bool param to set a limit to what gets added, e.g. ambiguous results, best results only, or all results. 

    public SearchLog(double tolerance = SpectralMatch.ToleranceForScoreDifferentiation, uint maxTargetsToKeep = uint.MaxValue, uint maxDecoysToKeep = uint.MaxValue)
    {
        _targetAttempts = new SortedSet<ISearchAttempt>(Comparer);
        _decoyAttempts = new SortedSet<ISearchAttempt>(Comparer);
        _toleranceForScoreDifferentiation = tolerance;
        _maxTargetsToKeep = maxTargetsToKeep;
        _maxDecoysToKeep = maxDecoysToKeep;
    }

    public bool Add(ISearchAttempt attempt)
    {
        bool added;
        if (attempt.IsDecoy)
        {
            added = _decoyAttempts.Add(attempt);
            if (_decoyAttempts.Count > _maxDecoysToKeep)
            {
                _decoyAttempts.Remove(_decoyAttempts.Max!);
            }
        }
        else
        {
            added = _targetAttempts.Add(attempt);
            if (_targetAttempts.Count > _maxTargetsToKeep)
            {
                _targetAttempts.Remove(_targetAttempts.Max!);
            }
        }

        return added;
    }

    public bool TryRemoveThisAmbiguousPeptide(SpectralMatchHypothesis matchHypothesis)
    {
        ISearchAttempt? toRemove = matchHypothesis.IsDecoy
            ? _decoyAttempts.FirstOrDefault(p => p is SpectralMatchHypothesis h && h.Equals(matchHypothesis))
            : _targetAttempts.FirstOrDefault(p => p is SpectralMatchHypothesis h && h.Equals(matchHypothesis));

        if (toRemove is null)
            return false;

        if (matchHypothesis.IsDecoy)
        {
            _decoyAttempts.Remove(toRemove);
        }
        else
        {
            _targetAttempts.Remove(toRemove);
        }

        return true;
    }

    /// <summary>
    /// This method is used by protein parsimony to remove PeptideWithSetModifications objects that have non-parsimonious protein associations
    /// </summary>
    public void TrimProteinMatches(HashSet<Protein> parsimoniousProteins)
    {
        _targetAttempts.RemoveWhere(p => p is SpectralMatchHypothesis h && !parsimoniousProteins.Contains(h.WithSetMods.Parent));
        _decoyAttempts.RemoveWhere(p => p is SpectralMatchHypothesis h && !parsimoniousProteins.Contains(h.WithSetMods.Parent));
    }

    public void Clear()
    {
        _targetAttempts.Clear();
        _decoyAttempts.Clear();
    }

    /// <summary>
    /// Returns all attempts that are within the maxScoreDifferenceAllowed of the best score
    /// </summary>
    public IEnumerable<ISearchAttempt> GetTopScoringAttempts(bool allowAmbiguity = true)
    {
        List<ISearchAttempt> allAttempts = _targetAttempts.Concat(_decoyAttempts).OrderBy(a => a, Comparer).ToList();
        if (allAttempts.Count == 0)
        {
            yield break;
        }

        if (!allowAmbiguity)
        {
            yield return allAttempts.First();
            yield break;
        }

        double bestScore = allAttempts[0].Score;
        foreach (ISearchAttempt attempt in allAttempts)
        {
            if (bestScore - attempt.Score <= _toleranceForScoreDifferentiation)
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
    public IEnumerable<SpectralMatchHypothesis> GetTopScoringAttemptsWithSequenceInformation(bool allowAmbiguity = true)
    {
        return GetTopScoringAttempts(allowAmbiguity)
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
        public int Compare(ISearchAttempt? x, ISearchAttempt? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (y is null) return -1;
            if (x is null) return 1;
            int scoreComparison = y.Score.CompareTo(x.Score);
            if (scoreComparison != 0) return scoreComparison;

            // if both contain sequences and ion information, with standard comparer
            if (x is SpectralMatchHypothesis smX && y is SpectralMatchHypothesis smY)
                return -1 * SpectralMatch.BioPolymerNotchFragmentIonComparer.Compare(smX, smY);

            // having sequence information is better
            if (x is SpectralMatchHypothesis)
                return 1;
            if (y is SpectralMatchHypothesis)
                return -1;

            return Math.Abs(x.Notch).CompareTo(Math.Abs(y.Notch));
        }
    }
}