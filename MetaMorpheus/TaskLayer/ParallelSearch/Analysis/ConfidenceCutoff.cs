#nullable enable
using System.Collections.Generic;
using EngineLayer;
using EngineLayer.FdrAnalysis;
using EngineLayer.SpectrumMatch;

namespace TaskLayer.ParallelSearch.Analysis;

/// <summary>
/// The value and threshold a match is held to when the collectors count it as confident: the q-value notch at the
/// q-value threshold. Parallel search filters on the q-value notch only (<see cref="ParallelSearchTask"/> refuses any
/// other <see cref="CommonParameters.IdentificationFilter"/>), so this is the rule <see cref="FilteredPsms.Passes"/>
/// applies to a normal search under that filter.
/// </summary>
public sealed class ConfidenceCutoff
{
    /// <summary>The one identification filter parallel search supports.</summary>
    public const FilterType Filter = FilterType.QValueNotch;

    public double Threshold { get; }
    public bool PeptideLevel { get; }

    private ConfidenceCutoff(double threshold, bool peptideLevel)
    {
        Threshold = threshold;
        PeptideLevel = peptideLevel;
    }

    /// <summary>The cutoff for these settings. The matches are not read; the parameter is kept for the callers' shape.</summary>
    public static ConfidenceCutoff For(IEnumerable<SpectralMatch> matches, CommonParameters commonParameters, bool peptideLevel) =>
        new(FilteredPsms.Threshold(commonParameters, Filter), peptideLevel);

    /// <summary>The protein group q-value threshold a normal search writes protein groups at under the q-value notch filter.</summary>
    public static double ProteinGroupThreshold(IEnumerable<SpectralMatch> psms, CommonParameters commonParameters) =>
        FilteredPsms.Threshold(commonParameters, Filter);

    /// <summary>What the matches were held to, worded as a normal search's results.txt words it, e.g. "q-value notch &lt;= 0.01".</summary>
    public string Label => FilteredPsms.Describe(Filter, Threshold);

    public bool Passes(SpectralMatch match) => Passes(match.GetFdrInfo(PeptideLevel));

    public bool Passes(FdrInfo? fdrInfo) => fdrInfo != null && fdrInfo.QValueNotch <= Threshold;
}
