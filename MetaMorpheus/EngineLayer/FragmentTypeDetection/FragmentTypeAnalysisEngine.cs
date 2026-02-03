using System.Collections.Generic;
using System.Linq;
using EngineLayer.SpectrumMatch;
using Omics.Fragmentation;

namespace EngineLayer.FragmentTypeDetection;

/// <summary>
/// Results from the FragmentTypeAnalysisEngine
/// </summary>
public class FragmentTypeAnalysisEngineResults(MetaMorpheusEngine engine)
    : MetaMorpheusEngineResults(engine)
{
    public List<SpectralMatch> AllSpectralMatches { get; set; }
    public int TotalPsms { get; set; }
    public int ConfidentPsms { get; set; }
    public double AverageScore { get; set; }
    public Dictionary<ProductType, FragmentTypeStatistics> FragmentTypeStatistics { get; set; } = new();
}


/// <summary>
/// Engine that analyzes fragment type performance from search results
/// </summary>
public class FragmentTypeAnalysisEngine : MetaMorpheusEngine
{
    private readonly List<SpectralMatch> _allPsms;
    private readonly List<ProductType> _allFragmentTypes;

    public FragmentTypeAnalysisEngine(
        List<SpectralMatch> allPsms,
        List<ProductType> allFragmentTypes,
        CommonParameters commonParameters,
        List<(string FileName, CommonParameters Parameters)> fileSpecificParameters,
        List<string> nestedIds)
        : base(commonParameters, fileSpecificParameters, nestedIds)
    {
        _allPsms = allPsms;
        _allFragmentTypes = allFragmentTypes;
    }

    protected override MetaMorpheusEngineResults RunSpecific()
    {
        Status("Analyzing individual fragment type performance...");

        var analysisResult = new FragmentTypeAnalysisEngineResults(this);
        analysisResult.AllSpectralMatches = _allPsms;

        // Overall statistics
        var psmsAtOnePct = FilteredPsms.Filter(_allPsms, CommonParameters, true, true, true, true, false).FilteredPsmsList;
        analysisResult.TotalPsms = _allPsms.Count;
        analysisResult.ConfidentPsms = psmsAtOnePct.Count;
        analysisResult.AverageScore = psmsAtOnePct.Any() ? psmsAtOnePct.Average(p => p.Score) : 0;

        double totalIntensityOfAllIons = psmsAtOnePct.Sum(p => p.MatchedFragmentIons.Sum(m => m.Intensity));
        double totalIntensityOfAllMatchedSpectra = psmsAtOnePct.Sum(p => p.TotalIonCurrent);
        // Per-fragment-type statistics
        foreach (var fragmentType in _allFragmentTypes)
        {
            var stats = AnalyzeFragmentType(fragmentType, psmsAtOnePct, totalIntensityOfAllIons, totalIntensityOfAllMatchedSpectra);
            analysisResult.FragmentTypeStatistics[fragmentType] = stats;
        }

        return analysisResult;
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