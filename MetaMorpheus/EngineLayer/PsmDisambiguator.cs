using EngineLayer.SpectrumMatch;
using Omics.Fragmentation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineLayer;

public enum DisambiguationStrategy
{
    UniqueIonFilter,
    UniqueIonFilter_Complex
}

public static class PsmDisambiguator
{
    private static MatchedIonComparer MatchedIonComparer = new MatchedIonComparer();

    public static IEnumerable<SpectralMatch> Disambiguate(this List<SpectralMatch> allMatches, DisambiguationStrategy strategy, CommonParameters commonParams)
    {
        // Run the method which manipulates the search logs
        switch (strategy)
        {
            case DisambiguationStrategy.UniqueIonFilter:
                DisambiguateUniqueIonFilter(allMatches, commonParams.UniqueIonsRequired);
                break;
            case DisambiguationStrategy.UniqueIonFilter_Complex:
                DisambiguateUniqueIonFilterComplex(allMatches);
                break;
            default:
                throw new NotImplementedException();
        }

        // Remove matches that have no search attempts left
        for (var index = 0; index < allMatches.Count; index++)
        {
            var match = allMatches[index];
            if (match.SearchLog.NumberOfBestScoringResults == 0)
                allMatches[index] = null;
        }

        // TODO: Create a method that does not return, just sets the values to null
        return allMatches.Where(p => p != null);
    }

    /// <summary>
    /// Ensures all matches made from the same spectrum have at least N unique ions
    /// </summary>
    private static void DisambiguateUniqueIonFilter(List<SpectralMatch> allMatches, int uniqueIonsRequired)
    {
        var comparer = new MatchedIonComparer();
        var chimeraGroups = allMatches
            .Where(p => p != null)
            .GroupBy(p => p.ChimeraIdString);
        
        foreach (var chimeraGroup in chimeraGroups)
        {
            // Aggregate all data from the chimera group
            Dictionary<SpectralMatch, Dictionary<SpectralMatchHypothesis, HashSet<MatchedFragmentIon>>> allData =
                chimeraGroup.ToDictionary(p => p, p => p.SearchLog.GetTopScoringAttemptsWithSequenceInformation()
                    .ToDictionary(m => m, m => m.MatchedIons.ToHashSet(comparer)));

            // Get matched ions that are shared for all PSMs in the chimera group
            HashSet<MatchedFragmentIon> sharedByAllMatches = allData.Values
                .SelectMany(p => p.Values)
                .Aggregate((a, b) => a.Intersect(b).ToHashSet(comparer));

            // Remove those that are shared by all from the hashsets
            foreach (var psmData in allData)
            {
                var psm = psmData.Key;
                foreach (var match in psmData.Value)
                {
                    match.Value.ExceptWith(sharedByAllMatches);

                    // if the hashset is less than the required number of unique ions, remove the match from the search log
                    if (match.Value.Count < uniqueIonsRequired)
                        psm.SearchLog.Remove(match.Key);
                }
            }
        }
    }


    private static void DisambiguateUniqueIonFilterComplex(List<SpectralMatch> allMatches)
    {
    }
}








class MatchedIonComparer : IComparer<MatchedFragmentIon>, IEqualityComparer<MatchedFragmentIon>
{
    public int Compare(MatchedFragmentIon x, MatchedFragmentIon y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (y is null) return 1;
        if (x is null) return -1;

        var mzComparison = x.Mz.CompareTo(y.Mz);
        if (mzComparison != 0) return mzComparison;

        var chargeComparison = x.Charge.CompareTo(y.Charge);
        if (chargeComparison != 0) return chargeComparison;

        var typeComparison = x.NeutralTheoreticalProduct.ProductType.CompareTo(y.NeutralTheoreticalProduct.ProductType);
        if (typeComparison != 0) return typeComparison;

        var newtMassComparison = x.NeutralTheoreticalProduct.NeutralMass.CompareTo(y.NeutralTheoreticalProduct.NeutralMass);
        return newtMassComparison != 0 ? newtMassComparison : 0;
    }

    public bool Equals(MatchedFragmentIon x, MatchedFragmentIon y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null) return false;
        if (y is null) return false;
        if (x.GetType() != y.GetType()) return false;
        return x.NeutralTheoreticalProduct.Equals(y.NeutralTheoreticalProduct) && x.Mz.Equals(y.Mz) && x.Charge == y.Charge;
    }

    public int GetHashCode(MatchedFragmentIon obj)
    {
        return HashCode.Combine(obj.NeutralTheoreticalProduct, obj.Mz, obj.Charge);
    }
}