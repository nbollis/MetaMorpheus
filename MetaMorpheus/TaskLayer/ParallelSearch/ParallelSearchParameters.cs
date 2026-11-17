#nullable enable
using System.Collections.Generic;
using EngineLayer.DatabaseLoading;

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
    public string ForegroundOrganismName { get; set; } = "Homo sapiens";

    /// <summary>
    /// When true, a shuffled entrapment twin is generated for every transient database and searched as a
    /// transient database of its own. The twins are a matched null for the per-database evaluations and enable
    /// the target-vs-twin ones. This doubles the number of transient searches.
    /// </summary>
    public bool UseTwinEntrapment { get; set; } = false;

    /// <summary>
    /// Casanovo mzTab files of de novo predictions for the spectra being searched, used instead of a ready-made
    /// <see cref="DeNovoMappingDataFilePath"/>. When any are given, mzLib's DeNovoMapper maps the predictions onto every
    /// transient database (and twin) after the searches and writes MappingSummary.tsv to the task's output folder,
    /// which is then read like a given one.
    /// </summary>
    public List<string> DeNovoMzTabPaths { get; set; } = new();

    /// <summary>
    /// The spectra files Casanovo ran on, read only for retention times an mzTab lacks. An mzTab's spectra_ref
    /// "index=N" counts MS2 spectra in the file Casanovo read, so give that file here (for example the MGF) when
    /// it differs from the searched one. Empty uses the searched spectra files.
    /// </summary>
    public List<string> DeNovoSpectraFilePaths { get; set; } = new();

    /// <summary>True when a mapping summary or mzTab files are given, so the de novo columns are filled and its tests run.</summary>
    [Nett.TomlIgnore] public bool UseDeNovoMapping => HasDeNovoMappingDataFile || DeNovoMzTabPaths is { Count: > 0 };

    /// <summary>True when a mapping summary is given; a blank path, as a hand-edited task file may have, means none.</summary>
    [Nett.TomlIgnore] public bool HasDeNovoMappingDataFile => !string.IsNullOrWhiteSpace(DeNovoMappingDataFilePath);

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
