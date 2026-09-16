using MassSpectrometry;
using Nett;
using Omics.Fragmentation;
using System;
using System.Collections.Generic;

namespace EngineLayer.SpectrumMatch.Scoring;
//public interface IScoreFunction
//{
//    public static Dictionary<string, ScoreFunction> ScoringFunctions { get; } = new()
//    {
//        {"Morpheus", new MorpheusScore() },
//        {"Xcorr", new XcorrScore() },
//        {"SpectralLibrary", new SpectralLibraryScore() }
//    };

//    public static abstract IScoreFunction Instance { get; protected set; }
//    public double CalculatePeptideScore(MsDataScan thisScan, List<MatchedFragmentIon> matchedFragmentIons);
//}

public interface ISpectralMatchScorer : IEquatable<ISpectralMatchScorer>
{
    public string Name { get; }
    public double CalculatePeptideScore(MsDataScan thisScan, List<MatchedFragmentIon> matchedFragmentIons);
}

[TreatAsInlineTable]
public abstract class BaseSpectralMatchScorer : ISpectralMatchScorer
{
    // Used by toml parsing to create the default instance of the scorer. 
    public virtual string Name => ToString();

    public abstract double CalculatePeptideScore(MsDataScan thisScan, List<MatchedFragmentIon> matchedFragmentIons);

    public override bool Equals(object obj) => Equals(obj as ISpectralMatchScorer);

    public bool Equals(ISpectralMatchScorer other)
    {
        if (other == null) return false;
        return this.GetType() == other.GetType();
    }

    public override int GetHashCode() => this.GetType().GetHashCode();
    public override string ToString() => this.GetType().Name;
}
