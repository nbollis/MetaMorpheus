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

[TreatAsInlineTable]
public abstract class ScoreFunction : IEquatable<ScoreFunction>
{
    public string Name => ToString();
    public static Dictionary<string, ScoreFunction> ScoringFunctions { get; } = new()
        {
            {"Morpheus", new MorpheusScore() },
            {"Xcorr", new XcorrScore() },
            {"SpectralLibrary", new SpectralLibraryScore() }
        };

    public abstract double CalculatePeptideScore(MsDataScan thisScan, List<MatchedFragmentIon> matchedFragmentIons);

    public bool Equals(ScoreFunction other)
    {
        if (other == null) return false;
        return this.GetType() == other.GetType();
    }

    public override bool Equals(object obj) => Equals(obj as ScoreFunction);
    public override int GetHashCode() => this.GetType().GetHashCode();
    public override string ToString() => this.GetType().Name;
}