#nullable enable
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using Omics.Fragmentation;
using Omics;
using Proteomics;

namespace EngineLayer.SpectrumMatch;

public abstract class SearchLog(double tolerance, double scoreCutoff)
{
    /// <summary>
    /// The minimum RunnerUp score allowed (this is legacy)
    /// </summary>
    protected readonly double ScoreCutoff = scoreCutoff;

    /// <summary>
    /// Tolerance to consider two scores the same for ambiguity 
    /// </summary>
    protected readonly double ToleranceForScoreDifferentiation = tolerance;

    /// <summary>
    /// Score of the current best match
    /// </summary>
    public double Score { get; protected set; }

    /// <summary>
    /// Score of the second best match
    /// </summary>
    public double RunnerUpScore { get; protected set; } = scoreCutoff;

    /// <summary>
    /// Number of ambiguous results within score tolerance
    /// </summary>
    public int NumberOfBestScoringResults { get; protected set; }

    /// <summary>
    /// Count of all search attempts in the log
    /// </summary>
    public virtual int Count => GetAttempts().Count();

    /// <summary>
    /// Returns the best match with sequence information
    /// </summary>
    public abstract SpectralMatchHypothesis First();

    /// <summary>
    /// Adds a search attempt and updates the best and runner-up scores
    /// </summary>
    /// <remarks>CAREFUL: This does not check if the result SHOULD be added as defined by the <see cref="AddOrReplace(IBioPolymerWithSetMods, double, int, bool, List{MatchedFragmentIon})"/> method</remarks>
    /// <returns>True if addition was successful, false otherwise</returns>
    public abstract bool Add(ISearchAttempt attempt);

    /// <summary>
    /// Removes a search attempt and updates the best and runner-up scores
    /// This is use by protein parsimony to remove PeptideWithSetModifications objects that have non-parsimonious protein associations and for disambiguation
    /// </summary>
    /// <param name="attempt"></param>
    /// <returns>True if removal was successful, false otherwise</returns>
    public abstract bool Remove(ISearchAttempt attempt);

    /// <summary>
    /// Clears the search log and updates scores
    /// </summary>
    public abstract void Clear();

    /// <summary>
    /// Gets all search attempts currently stored in the log
    /// </summary>
    public abstract IEnumerable<ISearchAttempt> GetAttempts();

    /// <summary>
    /// Clones the Search Log while replacing the attempts with the provided list and updating scores
    /// </summary>
    /// <returns>New search log of the same type and parameters</returns>
    public abstract SearchLog CloneWithAttempts(IEnumerable<ISearchAttempt> attempts);


    /// <summary>
    /// Adds a search attempt if it should be added to the log and updates scoring information
    /// </summary>
    /// <returns>True if addition was successful, false otherwise</returns>
    public abstract bool AddOrReplace(IBioPolymerWithSetMods pwsm, double newScore, int notch, bool reportAllAmbiguity, List<MatchedFragmentIon> matchedFragmentIons);

    /// <summary>
    /// Adds a collection of search attempts to the log
    /// </summary>
    public void AddRange(IEnumerable<ISearchAttempt> attempts)
    {
        foreach (var searchAttempt in attempts)
            Add(searchAttempt);
    }

    /// <summary>
    /// Removes a collection of search attempts from the log
    /// </summary>
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
            RemoveDestructively(searchAttempt);
    }

    /// <summary>
    /// Returns all attempts that are within the ToleranceForScoreDifferentiation of the best score
    /// </summary>
    public virtual IEnumerable<ISearchAttempt> GetTopScoringAttempts(bool allowAmbiguity = true)
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
    /// Returns all attempts that are within the ToleranceForScoreDifferentiation of the best score and retain their sequence information
    /// </summary>
    public IEnumerable<SpectralMatchHypothesis> GetTopScoringAttemptsWithSequenceInformation(bool allowAmbiguity = true)
    {
        return GetTopScoringAttempts(allowAmbiguity)
            .Where(p => p is SpectralMatchHypothesis)
            .Cast<SpectralMatchHypothesis>()
            .OrderByDescending(p => p, BioPolymerNotchFragmentIonComparer);
    }

    /// <summary>
    /// Gets all Search attempts by type (decoy or not)
    /// TODO: Add additional features to this such as entrapment and contaminant
    /// </summary>
    /// <returns>Enumerable of the internal collection used within this search log</returns>
    public virtual IEnumerable<ISearchAttempt> GetAttemptsByType(bool isDecoy)
    {
        return GetAttempts().Where(p => p.IsDecoy == isDecoy);
    }


    /// <summary>
    /// Removes a search attempt and updates the best and runner-up scores
    /// Prevents caching of results in the search log by inherited classes
    /// </summary>
    /// <returns>True if removal was successful, false otherwise</returns>
    public virtual bool RemoveDestructively(ISearchAttempt attempt) => Remove(attempt);

    /// <summary>
    /// Used to update scores when a new search attempt is added to the log
    /// </summary>
    protected virtual void UpdateScoresOnAddition(double newScore)
    {
        if (newScore - Score > ToleranceForScoreDifferentiation)
        {
            if (Score - RunnerUpScore > ToleranceForScoreDifferentiation)
            {
                RunnerUpScore = Score;
            }
            Score = newScore;
            NumberOfBestScoringResults = 1;
        }
        else if (newScore - Score > -ToleranceForScoreDifferentiation)
        {
            NumberOfBestScoringResults++;
        }
        else if (newScore - RunnerUpScore > ToleranceForScoreDifferentiation)
        {
            RunnerUpScore = newScore;
        }
    }

    /// <summary>
    /// Comparer for comparing SpectralMatchHypothesis. Ranks worst to best
    /// </summary>
    protected static readonly BioPolymerNotchFragmentIonComparer BioPolymerNotchFragmentIonComparer = new();

    /// <summary>
    /// Comparer for search attempts. Ranks best to worst
    /// </summary>
    protected static readonly IComparer<ISearchAttempt> Comparer = new SearchAttemptComparer();

    /// <summary>
    /// Sorts Spectral Matches by best to worst by score, then by the BioPolymerNotchFragmentIonComparer. 
    /// New Attempts will only be added to the SortedSet if they are unique by this comparer. 
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
                bpNotchComparison = -1 * BioPolymerNotchFragmentIonComparer.Compare(smX, smY);
            else if (x is SpectralMatchHypothesis)
                return 1;
            else if (y is SpectralMatchHypothesis)
                return -1;

            if (bpNotchComparison != 0)
                return bpNotchComparison;

            // lower notch is better
            int notchComparison = y.Notch.CompareTo(x.Notch);
            if (notchComparison != 0) return notchComparison;

            // TODO: See if this is needed to keep unique values. 
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
}


public class ScoreInformation
{
    public bool IsDecoy { get; set; }
    public double[] AllScores { get; set; } = [];
    public int NumberScored { get; set; }
    public double AverageScore { get; set; }
    public double StdScore { get; set; }
}