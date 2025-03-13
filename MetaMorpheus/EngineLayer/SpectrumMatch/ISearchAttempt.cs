using System;
using System.Runtime.CompilerServices;

namespace EngineLayer.SpectrumMatch;

/// <summary>
/// An interface used during the search process by the SearchLog stored within the SpectralMatch. 
/// </summary>
public interface ISearchAttempt : IEquatable<ISearchAttempt>
{
    double Score { get; }
    bool IsDecoy { get; }
    int Notch { get; }
    string FullSequence { get; }
}

/// <summary>
/// Class designed to hold the minimal information to represent a failed search attempt of a decoy 
/// </summary>
public class MinimalSearchAttempt : ISearchAttempt
{
    public double Score { get; init; }
    public bool IsDecoy { get; init; }
    public int Notch { get; init; }
    public string FullSequence { get; init; }

    public MinimalSearchAttempt() { }
    public MinimalSearchAttempt(ISearchAttempt attempt)
    {
        Score = attempt.Score;
        IsDecoy = attempt.IsDecoy;
        Notch = attempt.Notch;
        FullSequence = attempt.FullSequence;
    }

    public bool Equals(ISearchAttempt other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return IsDecoy == other.IsDecoy
               && Notch == other.Notch
               && FullSequence == other.FullSequence
               && Math.Abs(Score - other.Score) < SpectralMatch.ToleranceForScoreDifferentiation;
    }
}
