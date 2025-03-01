using System;
using System.Collections.Generic;
using Omics;
using Omics.Fragmentation;

namespace EngineLayer.Util;

/// <summary>
/// Compares possible PSMs first by score, then by the <see cref="BioPolymerNotchFragmentIonComparer"/>.
/// </summary>
public class TentativePsmComparer : Comparer<(double Score, (int notch, IBioPolymerWithSetMods pwsm, List<MatchedFragmentIon> ions))>
{
    private static readonly BioPolymerNotchFragmentIonComparer BioPolymerNotchFragmentIonComparer = new();
    public override int Compare((double Score, (int notch, IBioPolymerWithSetMods pwsm, List<MatchedFragmentIon> ions)) x,
        (double Score, (int notch, IBioPolymerWithSetMods pwsm, List<MatchedFragmentIon> ions)) y)
    {
        if (Math.Abs(x.Score - y.Score) > SpectralMatch.ToleranceForScoreDifferentiation)
            return x.Score.CompareTo(y.Score); // Higher score is better
        else return BioPolymerNotchFragmentIonComparer.Compare(x.Item2, y.Item2);
    }
}