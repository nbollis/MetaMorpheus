#nullable enable
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using Proteomics;

namespace EngineLayer.SpectrumMatch;
public interface ISearchLog
{
    bool Add(ISearchAttempt attempt);
    bool Remove(ISearchAttempt attempt);
    IEnumerable<ISearchAttempt> GetAttempts();
    void Clear();
}

public abstract class SearchLog(double tolerance) : ISearchLog
{
    protected readonly double ToleranceForScoreDifferentiation = tolerance;

    public abstract bool Add(ISearchAttempt attempt);
    public abstract bool Remove(ISearchAttempt attempt);
    public abstract IEnumerable<ISearchAttempt> GetAttempts();
    public abstract void Clear();
    public abstract SearchLog CloneWithAttempts(IEnumerable<ISearchAttempt> attempts);

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
            if (searchAttempt is SpectralMatchHypothesis h && !parsimoniousProteins.Contains(h.SpecificBioPolymer.Parent))
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

    public virtual IEnumerable<ISearchAttempt> GetAttemptsByType(bool isDecoy)
    {
        return GetAttempts().Where(p => p.IsDecoy == isDecoy);
    }

    public ScoreInformation GetScoreInformation(bool isDecoy)
    {
        var allScores = GetAttemptsByType(isDecoy)
            .Select(p => p.Score)
            .ToList();

        return new ScoreInformation()
        {
            IsDecoy = isDecoy,
            NumberScored = allScores.Count,
            AverageScore = allScores.Count > 0 ? allScores.Average(p => p) : 0,
            StdScore = allScores.Count > 0 ? allScores.StandardDeviation() : 0,
            AllScores = allScores.ToArray()
        };
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
                bpNotchComparison = -1 * SpectralMatch.BioPolymerNotchFragmentIonComparer.Compare(smX, smY);
            else if (x is SpectralMatchHypothesis)
                return 1;
            else if (y is SpectralMatchHypothesis)
                return -1;

            if (bpNotchComparison != 0)
                return bpNotchComparison;

            // lower notch is better
            int notchComparison = y.Notch.CompareTo(x.Notch);
            if (notchComparison != 0) return notchComparison;

            int fullSequenceComparison = string.CompareOrdinal(y.FullSequence, x.FullSequence);
            if (fullSequenceComparison != 0) return fullSequenceComparison;

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


public class ScoreInformation
{
    public bool IsDecoy { get; set; }
    public double[] AllScores { get; set; } = [];
    public int NumberScored { get; set; }
    public double AverageScore { get; set; }
    public double StdScore { get; set; }
}