#nullable enable
using System.Collections.Generic;
using System.Linq;
using Proteomics;

namespace EngineLayer.SpectrumMatch;

public class SearchLog(double toleranceForScoreDifferentiation = SpectralMatch.ToleranceForScoreDifferentiation)
{
    private readonly SortedSet<ISearchAttempt> _allAttempts = new(Comparer);
    protected readonly double ToleranceForScoreDifferentiation = toleranceForScoreDifferentiation;
    
    public virtual bool KeepAllDecoys => false;
    public virtual bool Add(ISearchAttempt attempt) => _allAttempts.Add(attempt);
    public virtual bool Remove(ISearchAttempt attempt) => _allAttempts.Remove(attempt);
    public virtual IEnumerable<ISearchAttempt> GetAttempts() => _allAttempts.AsEnumerable();

    public virtual void Clear()
    {
        if (KeepAllDecoys)
        {
            foreach (var searchAttempt in _allAttempts.Where(p => !p.IsDecoy).ToList())
            {
                _allAttempts.Remove(searchAttempt);
            }
        }
        else
        {
            _allAttempts.Clear();
        }
    }

    public void AddRange(IEnumerable<ISearchAttempt> attempts)
    {
        foreach (var searchAttempt in attempts)
            Add(searchAttempt);
    }

    public void RemoveRange(IEnumerable<ISearchAttempt> attempts)
    {
        foreach (var searchAttempt in attempts)
            Remove(searchAttempt);
    }

    /// <summary>
    /// This method is used by protein parsimony to remove PeptideWithSetModifications objects that have non-parsimonious protein associations
    /// </summary>
    /// <param name="parsimoniousProteins">proteins to keep</param>
    public void TrimProteinMatches(HashSet<Protein> parsimoniousProteins)
    {
        List<ISearchAttempt> toRemove = new();
        foreach (var searchAttempt in GetAttempts())
        {
            if (searchAttempt is SpectralMatchHypothesis h && !parsimoniousProteins.Contains(h.WithSetMods.Parent))
                toRemove.Add(searchAttempt);
        }

        foreach (var searchAttempt in toRemove)
            Remove(searchAttempt);
    }

    /// <summary>
    /// Returns all attempts that are within the maxScoreDifferenceAllowed of the best score
    /// </summary>
    public IEnumerable<ISearchAttempt> GetTopScoringAttempts(bool allowAmbiguity = true)
    {
        List<ISearchAttempt> allAttempts = GetAttempts().OrderBy(a => a, Comparer).ToList();
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
            if (bestScore - attempt.Score <= ToleranceForScoreDifferentiation)
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

    public IEnumerable<ISearchAttempt> GetAttemptsByType(bool isDecoy)
    {
        return GetAttempts().Where(p => p.IsDecoy == isDecoy);
    }

    protected static IComparer<ISearchAttempt> Comparer { get; } = new SearchAttemptComparer();

    /// <summary>
    /// Sorts Spectral Matches by best to worst by score, then by the BioPolymerNotchFragmentIonComparer. 
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
            int bpNotchComparison = 0;
            if (x is SpectralMatchHypothesis smX && y is SpectralMatchHypothesis smY)
                bpNotchComparison = - 1 * SpectralMatch.BioPolymerNotchFragmentIonComparer.Compare(smX, smY);
            else if (x is SpectralMatchHypothesis)
                return 1;
            else if (y is SpectralMatchHypothesis)
                return -1;

            if (bpNotchComparison != 0)
                return bpNotchComparison;

            return x.IsDecoy switch
            {
                true when y.IsDecoy => 0,
                true => 1,
                false when y.IsDecoy => -1,
                _ => 0
            };
        }
    }
}



public class KeepAllDecoySearchLog(double tolerance = SpectralMatch.ToleranceForScoreDifferentiation,
    uint maxTargetsToKeep = uint.MaxValue, uint maxDecoysToKeep = uint.MaxValue)
    : SearchLog(tolerance)
{
    private readonly SortedSet<ISearchAttempt> _targetAttempts = new(Comparer);
    private readonly SortedSet<ISearchAttempt> _decoyAttempts = new(Comparer);
    public override bool KeepAllDecoys => true;

    public override bool Add(ISearchAttempt attempt)
    {
        bool added;
        if (attempt.IsDecoy)
        {
            added = _decoyAttempts.Add(attempt);
            if (_decoyAttempts.Count > maxDecoysToKeep)
            {
                _decoyAttempts.Remove(_decoyAttempts.Max!);
            }
        }
        else
        {
            added = _targetAttempts.Add(attempt);
            if (_targetAttempts.Count > maxTargetsToKeep)
            {
                _targetAttempts.Remove(_targetAttempts.Max!);
            }
        }

        return added;
    }

    public override void Clear()
    {
        _targetAttempts.Clear();
    }

    public override IEnumerable<ISearchAttempt> GetAttempts()
    {
        return _targetAttempts.Concat(_decoyAttempts);
    }
}