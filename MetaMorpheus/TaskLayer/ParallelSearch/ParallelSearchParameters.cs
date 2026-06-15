#nullable enable
using System.Collections.Generic;
using EngineLayer.DatabaseLoading;
using TaskLayer.ParallelSearch.Util;

namespace TaskLayer.ParallelSearch;

public class ParallelSearchParameters : SearchParameters
{
    // Transient databases - one search per database in this list
    public List<DbForTask> TransientDatabases { get; set; } = new();
    public bool OverwriteTransientSearchOutputs { get; set; } = true;
    public int MaxSearchesInParallel { get; set; } = 4;
    public bool WriteTransientSpectralLibrary { get; set; } = false;
    public bool WriteTransientResultsOnly { get; set; } = true;
    public bool CompressTransientSearchOutputs { get; set; } = false;
    public string? DeNovoMappingDataFilePath { get; set; } = null;

    /// <summary>
    /// When true, the entries in <see cref="TransientDatabases"/> are merged-index .msl files: a single file
    /// (or a set of shards) holds many databases, each entry tagged "db|accession". Merged mode loads each
    /// file once, runs the candidate filter once, and emits one searchable database per source db-group, so
    /// parallelism is driven by the database count INSIDE the file rather than the file-list count. Replaces
    /// the former <c>MM_PARALLELSEARCH_MERGED</c> environment variable so the behavior is on the
    /// parameter/TOML/CLI surface and reproducible. Only takes effect when every transient database ends in
    /// <c>.msl</c>.
    /// </summary>
    public bool UseMergedTransientLibrary { get; set; } = false;

    #region Follow-Up Search Parameters

    /// <summary>
    /// Ratio of tests that must pass for an organism to pass writing cutoff. 
    /// </summary>
    public double TestRatioForWriting { get; set; } = 0.5;

    /// <summary>
    /// When true, uses family-aware ranking (PassedFamilyCount and CombinedQValue)
    /// instead of the legacy test-count ratio filter.
    /// </summary>
    public bool UseFamilyAwareRanking { get; set; } = false;

    public Dictionary<DatabaseToProduce, (bool Write, bool Search)> DatabasesToWriteAndSearch {get; set;} = new()
    {
        { DatabaseToProduce.AllSignificantOrganisms, (false, false) },
        { DatabaseToProduce.AllDetectedProteinsFromSignificantOrganisms, (false, false) },
        { DatabaseToProduce.AllDetectedPeptidesFromSignificantOrganisms, (false, false) }
    };

    #endregion

    public ParallelSearchParameters() : base() 
    {
        TransientDatabases = new();
        SetDefaultValues();
    }

    public ParallelSearchParameters(SearchParameters searchParams) : this() 
    { 
        CopySearchParameters(searchParams);
        SetDefaultValues();
    }

    private void SetDefaultValues()
    {
        DoParsimony = true;
        NoOneHitWonders = true;
        MassDiffAcceptorType = MassDiffAcceptorType.Exact;
        SearchType = SearchType.Classic;
        DoLabelFreeQuantification = false;
        DoMultiplexQuantification = false;
    }

    private void CopySearchParameters(SearchParameters searchParams)
    {
        // Copy all properties from the base SearchParameters class
        DisposeOfFileWhenDone = searchParams.DisposeOfFileWhenDone;
        DoParsimony = searchParams.DoParsimony;
        ModPeptidesAreDifferent = searchParams.ModPeptidesAreDifferent;
        NoOneHitWonders = searchParams.NoOneHitWonders;
        MatchBetweenRuns = searchParams.MatchBetweenRuns;
        MbrFdrThreshold = searchParams.MbrFdrThreshold;
        Normalize = searchParams.Normalize;
        QuantifyPpmTol = searchParams.QuantifyPpmTol;
        DoHistogramAnalysis = searchParams.DoHistogramAnalysis;
        SearchTarget = searchParams.SearchTarget;
        DecoyType = searchParams.DecoyType;
        MassDiffAcceptorType = searchParams.MassDiffAcceptorType;
        WritePrunedDatabase = searchParams.WritePrunedDatabase;
        KeepAllUniprotMods = searchParams.KeepAllUniprotMods;
        DoLocalizationAnalysis = searchParams.DoLocalizationAnalysis;
        DoLabelFreeQuantification = searchParams.DoLabelFreeQuantification;
        UseSharedPeptidesForLFQ = searchParams.UseSharedPeptidesForLFQ;
        DoMultiplexQuantification = searchParams.DoMultiplexQuantification;
        MultiplexModId = searchParams.MultiplexModId;
        SearchType = searchParams.SearchType;
        LocalFdrCategories = searchParams.LocalFdrCategories;
        CustomMdac = searchParams.CustomMdac;
        MaxFragmentSize = searchParams.MaxFragmentSize;
        MinAllowedInternalFragmentLength = searchParams.MinAllowedInternalFragmentLength;
        HistogramBinTolInDaltons = searchParams.HistogramBinTolInDaltons;
        ModsToWriteSelection = searchParams.ModsToWriteSelection;
        MaximumMassThatFragmentIonScoreIsDoubled = searchParams.MaximumMassThatFragmentIonScoreIsDoubled;
        WriteMzId = searchParams.WriteMzId;
        WritePepXml = searchParams.WritePepXml;
        WriteHighQValuePsms = searchParams.WriteHighQValuePsms;
        WriteDecoys = searchParams.WriteDecoys;
        WriteContaminants = searchParams.WriteContaminants;
        WriteIndividualFiles = searchParams.WriteIndividualFiles;
        WriteSpectralLibrary = searchParams.WriteSpectralLibrary;
        UpdateSpectralLibrary = searchParams.UpdateSpectralLibrary;
        CompressIndividualFiles = searchParams.CompressIndividualFiles;
        SilacLabels = searchParams.SilacLabels;
        StartTurnoverLabel = searchParams.StartTurnoverLabel; 
        EndTurnoverLabel = searchParams.EndTurnoverLabel; 
        TCAmbiguity = searchParams.TCAmbiguity;
        IncludeModMotifInMzid = searchParams.IncludeModMotifInMzid;
        WriteDigestionProductCountFile = searchParams.WriteDigestionProductCountFile;
    }
}