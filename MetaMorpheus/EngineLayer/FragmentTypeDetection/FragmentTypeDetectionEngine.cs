using System;
using System.Collections.Generic;
using System.Linq;
using EngineLayer.ClassicSearch;
using EngineLayer.FdrAnalysis;
using MassSpectrometry;
using MzLibUtil;
using Omics;
using Omics.Fragmentation;
using Omics.Modifications;

namespace EngineLayer.FragmentTypeDetection;

/// <summary>
/// Engine that performs a comprehensive search with specified fragment types
/// and returns FDR-analyzed results
/// </summary>
public class FragmentTypeDetectionEngine : MetaMorpheusEngine
{
    private readonly Ms2ScanWithSpecificMass[] _ms2Scans;
    private readonly List<Modification> _variableModifications;
    private readonly List<Modification> _fixedModifications;
    private readonly List<IBioPolymer> _bioPolymerList;
    private readonly MassDiffAcceptor _searchMode;
    private readonly List<ProductType> _fragmentTypes;
    private readonly string _fileNameWithoutExtension;

    public FragmentTypeDetectionEngine(
        Ms2ScanWithSpecificMass[] ms2Scans,
        List<Modification> variableModifications,
        List<Modification> fixedModifications,
        List<IBioPolymer> bioPolymerList,
        MassDiffAcceptor searchMode,
        List<ProductType> fragmentTypes,
        string fileNameWithoutExtension,
        CommonParameters commonParameters,
        List<(string FileName, CommonParameters Parameters)> fileSpecificParameters,
        List<string> nestedIds)
        : base(commonParameters, fileSpecificParameters, nestedIds)
    {
        _ms2Scans = ms2Scans;
        _variableModifications = variableModifications;
        _fixedModifications = fixedModifications;
        _bioPolymerList = bioPolymerList;
        _searchMode = searchMode;
        _fragmentTypes = fragmentTypes;
        _fileNameWithoutExtension = fileNameWithoutExtension;
    }

    protected override MetaMorpheusEngineResults RunSpecific()
    {
        // Set up custom fragmentation with specified fragment types
        SetupCustomFragmentation(_fragmentTypes);

        Status($"Searching with fragment types: {string.Join(", ", _fragmentTypes.Take(5))}...");

        // Run search
        SpectralMatch[] fileSpecificPsms = new SpectralMatch[_ms2Scans.Length];

        new ClassicSearchEngine(
            fileSpecificPsms,
            _ms2Scans,
            _variableModifications,
            _fixedModifications,
            null, null, null,
            _bioPolymerList,
            _searchMode,
            CommonParameters,
            FileSpecificParameters,
            null,
            NestedIds,
            false).Run();

        // Collect non-null PSMs
        var psms = fileSpecificPsms.Where(p => p != null).ToList();

        // Run FDR analysis
        Status("Running FDR analysis...");
        new FdrAnalysisEngine(
            psms,
            _searchMode.NumNotches,
            CommonParameters,
            FileSpecificParameters,
            NestedIds,
            doPEP: false).Run();

        return new FragmentTypeDetectionEngineResults(this, psms);
    }

    /// <summary>
    /// Set up custom fragmentation in the common parameters with the specified fragment types
    /// </summary>
    private void SetupCustomFragmentation(List<ProductType> fragmentTypes)
    {
        // Set the custom fragment types in the appropriate DissociationType dictionary
        CommonParameters.DigestionParams.ProductsFromDissociationType()[DissociationType.Custom] = fragmentTypes;

        // Update the common parameters to use custom dissociation type
        CommonParameters.SetCustomProductTypes();
    }
}

/// <summary>
/// Results from the FragmentTypeDetectionEngine
/// </summary>
public class FragmentTypeDetectionEngineResults : MetaMorpheusEngineResults
{
    public List<SpectralMatch> AllPsms { get; }

    public FragmentTypeDetectionEngineResults(
        MetaMorpheusEngine engine,
        List<SpectralMatch> allPsms)
        : base(engine)
    {
        AllPsms = allPsms;
    }
}
