using System;
using System.Collections.Generic;
using System.Linq;
using Omics.Fragmentation;

namespace EngineLayer.FragmentTypeDetection;

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

        var analysisResult = new FragmentTypeAnalysisResult();
        
        // Overall statistics
        var psmsAtOnePct = _allPsms.Where(p => p.FdrInfo.QValue <= 0.01 && !p.IsDecoy).ToList();
        analysisResult.TotalPsms = _allPsms.Count;
        analysisResult.PsmsAt1PercentFdr = psmsAtOnePct.Count;
        analysisResult.AverageScore = psmsAtOnePct.Any() ? psmsAtOnePct.Average(p => p.Score) : 0;
        
        // Per-fragment-type statistics
        foreach (var fragmentType in _allFragmentTypes)
        {
            var stats = AnalyzeFragmentType(fragmentType, psmsAtOnePct);
            analysisResult.FragmentTypeStatistics[fragmentType] = stats;
        }
        
        return new FragmentTypeAnalysisEngineResults(this, analysisResult);
    }

    /// <summary>
    /// Analyze the performance of a single fragment type
    /// </summary>
    private FragmentTypeStatistics AnalyzeFragmentType(ProductType fragmentType, List<SpectralMatch> psmsAtOnePct)
    {
        var stats = new FragmentTypeStatistics
        {
            FragmentType = fragmentType
        };
        
        // Count how many PSMs have matches for this fragment type
        int psmsWithThisFragment = psmsAtOnePct.Count(p => 
            p.MatchedFragmentIons.Any(ion => ion.NeutralTheoreticalProduct.ProductType == fragmentType));
        
        stats.PsmsWithMatches = psmsWithThisFragment;
        stats.PercentOfPsmsWithMatches = psmsAtOnePct.Any() 
            ? (double)psmsWithThisFragment / psmsAtOnePct.Count * 100 
            : 0;
        
        // Calculate average number of matches when present
        var psmsWithFragment = psmsAtOnePct
            .Where(p => p.MatchedFragmentIons.Any(ion => ion.NeutralTheoreticalProduct.ProductType == fragmentType))
            .ToList();
        
        if (psmsWithFragment.Any())
        {
            stats.AverageMatchesWhenPresent = psmsWithFragment.Average(p => 
                p.MatchedFragmentIons.Count(ion => ion.NeutralTheoreticalProduct.ProductType == fragmentType));
        }
        
        // TODO: Add more sophisticated statistics:
        // - Average score contribution
        // - Correlation with high-confidence PSMs
        // - Intensity-weighted statistics
        
        return stats;
    }
}

/// <summary>
/// Results from the FragmentTypeAnalysisEngine
/// </summary>
public class FragmentTypeAnalysisEngineResults : MetaMorpheusEngineResults
{
    public FragmentTypeAnalysisResult AnalysisResult { get; }

    public FragmentTypeAnalysisEngineResults(
        MetaMorpheusEngine engine, 
        FragmentTypeAnalysisResult analysisResult) 
        : base(engine)
    {
        AnalysisResult = analysisResult;
    }
}

/// <summary>
/// Class to hold the analysis results for fragment types
/// </summary>
public class FragmentTypeAnalysisResult
{
    public int TotalPsms { get; set; }
    public int PsmsAt1PercentFdr { get; set; }
    public double AverageScore { get; set; }
    public Dictionary<ProductType, FragmentTypeStatistics> FragmentTypeStatistics { get; set; }
    
    public FragmentTypeAnalysisResult()
    {
        FragmentTypeStatistics = new Dictionary<ProductType, FragmentTypeStatistics>();
    }
}

/// <summary>
/// Statistics for an individual fragment type
/// </summary>
public class FragmentTypeStatistics
{
    public ProductType FragmentType { get; set; }
    public int PsmsWithMatches { get; set; }
    public double PercentOfPsmsWithMatches { get; set; }
    public double AverageMatchesWhenPresent { get; set; }
    
    // TODO: Add more statistics as needed:
    // - Average score contribution
    // - Correlation with high-confidence PSMs
    // - Intensity-weighted metrics
}
