using EngineLayer.FdrAnalysis;
using EngineLayer.SpectrumMatch;
using Omics.Fragmentation;
using System.Collections.Generic;
using System.Linq;

namespace EngineLayer.FragmentTypeDetection;

/// <summary>
/// Results from the FragmentTypeAnalysisEngine
/// </summary>
public class FragmentTypeAnalysisEngineResults(MetaMorpheusEngine engine)
    : MetaMorpheusEngineResults(engine)
{

}


/// <summary>
/// Engine that analyzes fragment type performance from search results
/// </summary>
public class FragmentTypeAnalysisEngine : MetaMorpheusEngine
{
    private readonly List<SpectralMatch> _allPsms;
    private readonly MassDiffAcceptor _massDiffAcceptor;
    private readonly List<ProductType> _allFragmentTypes;
    private readonly IFragmentDetectionStrategy _fragmentDetectionStrategy;

    public FragmentTypeAnalysisEngine(
        IFragmentDetectionStrategy fragmentDetectionStrategy,
        List<SpectralMatch> allPsms,
        List<ProductType> allFragmentTypes,
        CommonParameters commonParameters,
        MassDiffAcceptor massDiffAcceptor,
        List<(string FileName, CommonParameters Parameters)> fileSpecificParameters,
        List<string> nestedIds)
        : base(commonParameters, fileSpecificParameters, nestedIds)
    {
        _allPsms = allPsms;
        _allFragmentTypes = allFragmentTypes;
        _massDiffAcceptor = massDiffAcceptor;
    }

    protected override MetaMorpheusEngineResults RunSpecific()
    {
        var analysisResult = new FragmentTypeAnalysisEngineResults(this);



        return analysisResult;
    }

    public int ReevaluateSpectralMatchConfidence(List<SpectralMatch> allMatches, List<ProductType> optimalFragmentTypes, CommonParameters combinedParams)
    {
        var original = FilteredPsms.Filter(allMatches, combinedParams, includeAmbiguous: true).TargetPsmsAboveThreshold;

        foreach (var match in allMatches)
            ReScoreSpectralMatch(match, optimalFragmentTypes);

        _ = new FdrAnalysisEngine(allMatches, _massDiffAcceptor.NumNotches, combinedParams, FileSpecificParameters, NestedIds, "PSM", false).Run();

        var newCount = FilteredPsms.Filter(allMatches, combinedParams, includeAmbiguous: true).TargetPsmsAboveThreshold;

        return newCount - original;
    }

    public static void ReScoreSpectralMatch(SpectralMatch match, List<ProductType> optimalFragmentTypes)
    {
        // Remove matched ions that are not of the optimal types
        var ionsToExclude = match.MatchedFragmentIons
            .Where(ion => !optimalFragmentTypes.Contains(ion.NeutralTheoreticalProduct.ProductType))
            .ToList();

        foreach (var ion in ionsToExclude)
            match.MatchedFragmentIons.Remove(ion);

        // Recalculate the score based on the remaining matched ions
        match.WorkingScore = MetaMorpheusEngine.CalculatePeptideScore(match.Ms2Scan, match.MatchedFragmentIons);

        // Add back the excluded ions to the match's matched ions list (but they won't contribute to the score)
        match.MatchedFragmentIons.AddRange(ionsToExclude);
    }
}