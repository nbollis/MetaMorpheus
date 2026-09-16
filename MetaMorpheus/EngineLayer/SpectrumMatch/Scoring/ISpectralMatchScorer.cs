using MassSpectrometry;
using Nett;
using Omics.Fragmentation;
using System;
using System.Collections.Generic;

namespace EngineLayer.SpectrumMatch.Scoring;

public interface ISpectralMatchScorer : IEquatable<ISpectralMatchScorer>, ICloneable
{
    public string Name { get; }
    public double CalculatePeptideScore(MsDataScan thisScan, List<MatchedFragmentIon> matchedFragmentIons);
}

[TreatAsInlineTable]
public abstract class BaseSpectralMatchScorer : ISpectralMatchScorer
{
    public abstract string Name { get; }
    public virtual bool HigherIsBetter { get; } = true;

    public abstract double CalculatePeptideScore(MsDataScan thisScan, List<MatchedFragmentIon> matchedFragmentIons);

    public virtual object Clone() => MemberwiseClone();

    public override bool Equals(object obj) => Equals(obj as ISpectralMatchScorer);

    public bool Equals(ISpectralMatchScorer other)
    {
        if (other == null) return false;
        return this.GetType() == other.GetType();
    }

    public override int GetHashCode() => this.GetType().GetHashCode();
    public override string ToString() => this.GetType().Name;
}
