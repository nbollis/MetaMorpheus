using EngineLayer.SpectrumMatch;
using Omics.Fragmentation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EngineLayer.FragmentTypeDetection;

public class SimpleFragmentDetectionStrategy : FragmentDetectionStrategy
{
    public override string Name => "Simple";

    public override List<ProductType> DetermineOptimalFragmentTypes(List<SpectralMatch> allMatches, CommonParameters common)
    {        
        // Overall statistics
        var psmsAtOnePct = FilteredPsms.Filter(allMatches, common, true, true, true, true, false).FilteredPsmsList;
        var prodTypes = psmsAtOnePct.SelectMany(p => p.MatchedFragmentIons).Select(ion => ion.NeutralTheoreticalProduct.ProductType).Distinct();
        var analysisResult = new Dictionary<ProductType, FragmentTypeStatistics>();

        double totalIntensityOfAllIons = psmsAtOnePct.Sum(p => p.MatchedFragmentIons.Sum(m => m.Intensity));
        double totalIntensityOfAllMatchedSpectra = psmsAtOnePct.Sum(p => p.TotalIonCurrent);
        // Per-fragment-type statistics
        foreach (var fragmentType in prodTypes)
        {
            var stats = AnalyzeFragmentType(fragmentType, psmsAtOnePct, totalIntensityOfAllIons, totalIntensityOfAllMatchedSpectra);
            analysisResult[fragmentType] = stats;
        }

        return DetermineOptimalFragmentTypes(analysisResult);
    }

    /// <summary>
    /// Determine the optimal fragment types to use based on the analysis
    /// </summary>
    private List<ProductType> DetermineOptimalFragmentTypes(Dictionary<ProductType, FragmentTypeStatistics> fragmentStatistics)
    {
        var optimalTypes = new List<ProductType>();

        var averagePsmCount = fragmentStatistics.Values.Average(s => s.PsmsWithMatches);
        var stDevPsmCount = Math.Sqrt(fragmentStatistics.Values
            .Select(s => Math.Pow(s.PsmsWithMatches - averagePsmCount, 2))
            .Average());
        var minAllowablePsms = averagePsmCount - stDevPsmCount;
        var psmCountAutoPassThreshold = averagePsmCount + stDevPsmCount;

        foreach (var kvp in fragmentStatistics)
        {
            var fragmentType = kvp.Key;
            var stats = kvp.Value;

            // No Psms -> Reject type
            if (stats.PsmsWithMatches == 0)
                continue;

            // Ion type accounts for less than 1% of total matched ion signal
            if (stats.PercentIdentificationIntensityContribution < 1.0)
                continue;

            // Ion type accounts for less than 0.1% of total identified spectra TIC
            if (stats.PercentSpectralIntensityContribution < 0.1)
                continue;

            // Ion type has PSM count above auto-pass threshold -> Accept type
            if (stats.PsmsWithMatches >= psmCountAutoPassThreshold)
            {
                optimalTypes.Add(fragmentType);
                continue;
            }

            // Ion type has PSM count below minimum allowable threshold -> Reject type
            if (stats.PsmsWithMatches >= minAllowablePsms)
                continue;

            // Ion Type has less than 2 fragment matches on average when present -> Reject type
            if (stats.AverageMatchesWhenPresent < 2.0)
                continue;

        }
        return optimalTypes;
    }

    /// <summary>
    /// Analyze the performance of a single fragment type
    /// </summary>
    private FragmentTypeStatistics AnalyzeFragmentType(ProductType fragmentType, List<SpectralMatch> confidentPsms, double totalIntensityOfAllMatchedFragmentIons, double totalIntensityOfAllMatchedSpectra, int k = 10)
    {
        var stats = new FragmentTypeStatistics
        {
            FragmentType = fragmentType
        };

        // Extract PSMs and Ions that have matches for this fragment type
        var psmsWithFragment = confidentPsms
            .Where(p => p.MatchedFragmentIons.Any(ion => ion.NeutralTheoreticalProduct.ProductType == fragmentType))
            .ToList();
        var allMatchedIonsOfType = psmsWithFragment
            .SelectMany(p => p.MatchedFragmentIons)
            .Where(ion => ion.NeutralTheoreticalProduct.ProductType == fragmentType)
            .ToList();

        // Count how many PSMs have matches for this fragment type
        stats.PsmsWithMatches = psmsWithFragment.Count;
        stats.PercentOfPsmsWithMatches = confidentPsms.Any()
            ? (double)psmsWithFragment.Count / confidentPsms.Count * 100
            : 0;

        // Calculate average number of matches when present        
        if (psmsWithFragment.Any())
        {
            stats.AverageMatchesWhenPresent = psmsWithFragment.Average(p =>
                p.MatchedFragmentIons.Count(ion => ion.NeutralTheoreticalProduct.ProductType == fragmentType));

            double totalIonIntensity = allMatchedIonsOfType.Sum(ion => ion.Intensity);
            stats.TotalIntensityContribution = totalIonIntensity;
            stats.PercentSpectralIntensityContribution = totalIonIntensity / totalIntensityOfAllMatchedSpectra * 100;
            stats.PercentIdentificationIntensityContribution = totalIonIntensity / totalIntensityOfAllMatchedFragmentIons * 100;
            stats.DecoyHitRate = CalculateHitRatePerIonType(fragmentType, psmsWithFragment, decoyHitRate: true, maxK: k);
            stats.TargetHitRate = CalculateHitRatePerIonType(fragmentType, psmsWithFragment, decoyHitRate: false, maxK: k);
        }

        return stats;
    }

    private Dictionary<int, double> CalculateHitRatePerIonType(
        ProductType fragmentType,
        List<SpectralMatch> confidentPsms,
        bool decoyHitRate,
        int maxK)
    {
        // histograms: index i holds number of PSMs with "count == i"
        // We cap counts > maxK into bucket maxK because thresholds only go up to maxK.
        int[] targetHist = new int[maxK + 1];
        int[] decoyHist = new int[maxK + 1];

        foreach (var psm in confidentPsms)
        {
            int count = 0;
            var ions = psm.MatchedFragmentIons;

            // Count ions of this fragment type once per PSM
            for (int i = 0; i < ions.Count; i++)
            {
                if (ions[i].NeutralTheoreticalProduct.ProductType == fragmentType)
                    count++;
            }

            int bucket = count >= maxK ? maxK : count;

            if (psm.IsDecoy) decoyHist[bucket]++;
            else targetHist[bucket]++;
        }

        // Convert to suffix sums: ge[k] = number of PSMs with count >= k
        int[] targetGe = new int[maxK + 2];
        int[] decoyGe = new int[maxK + 2];

        for (int k = maxK; k >= 0; k--)
        {
            targetGe[k] = targetGe[k + 1] + targetHist[k];
            decoyGe[k] = decoyGe[k + 1] + decoyHist[k];
        }

        var hitRate = new Dictionary<int, double>(capacity: maxK);

        for (int k = 1; k <= maxK; k++)
        {
            int targetCount = targetGe[k];
            int decoyCount = decoyGe[k];

            double value = targetCount > 0 && decoyCount > 0
                ? (decoyHitRate ? (double)decoyCount / targetCount : (double)targetCount / decoyCount)
                : 0.0;

            hitRate[k] = value;
        }

        return hitRate;
    }
}