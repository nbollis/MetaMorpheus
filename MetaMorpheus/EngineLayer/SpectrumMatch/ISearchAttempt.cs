using System;

namespace EngineLayer.SpectrumMatch;

public interface ISearchAttempt : IEquatable<ISearchAttempt>
{
    double Score { get; }
    int Notch { get; }
    bool IsDecoy { get; }
}