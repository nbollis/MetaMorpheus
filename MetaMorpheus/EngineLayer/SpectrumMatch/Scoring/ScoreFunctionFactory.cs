using System;
using System.Collections.Generic;

namespace EngineLayer.SpectrumMatch.Scoring;

public static class ScoreFunctionFactory
{
    private static readonly Dictionary<string, Func<ISpectralMatchScorer>> Factories = new()
    {
        ["MetaMorpheus"] = () => new MorpheusScorer(),
        ["Morpheus"] = () => new MorpheusScorer(),
        ["Xcorr"] = () => new XcorrScorer(),
        ["SpectralLibrary"] = () => new SpectralLibraryScorer()
    };

    public static ISpectralMatchScorer Create(string name)
    {
        if (!Factories.TryGetValue(name, out var factory))
            throw new MetaMorpheusException($"Unknown score function: {name}");

        return factory();
    }
}
