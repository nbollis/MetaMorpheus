#nullable enable
using System.Collections.Generic;
using EngineLayer.DatabaseLoading;

namespace TaskLayer.ParallelSearchTask;

public enum DatabaseToProduce
{
    AllSignificantOrganisms,
    AllDetectedProteinsFromSignificantOrganisms,
    AllDetectedPeptidesFromSignificantOrganisms
}

public static class DatabaseToProduceExtension
{
    public static string GetFileName(this DatabaseToProduce mode)
    {
        return mode switch
        {
            DatabaseToProduce.AllSignificantOrganisms => "AllSignificantOrganisms.fasta",
            DatabaseToProduce.AllDetectedProteinsFromSignificantOrganisms => "AllDetectedProteinsFromSignificantOrganisms.fasta",
            DatabaseToProduce.AllDetectedPeptidesFromSignificantOrganisms => "AllProteinsFromDetectedPeptidesFromSignificantOrganisms.fasta",
            _ => "UnknownDatabase.fasta"
        };
    }
}

public class ParallelSearchParameters : SearchParameters
{
    // Transient databases - one search per database in this list
    public List<DbForTask> TransientDatabases { get; set; } = new();
    public bool OverwriteTransientSearchOutputs { get; set; } = true;
    public int MaxSearchesInParallel { get; set; } = 4;
    public bool WriteTransientSpectralLibrary { get; set; } = false;
    public bool WriteTransientResultsOnly { get; set; } = true;
    public bool CompressTransientSearchOutputs { get; set; } = false;

    #region Follow-Up Search Parameters

    /// <summary>
    /// Ratio of tests that must pass for an organism to pass writing cutoff. 
    /// </summary>
    public double TestRatioForWriting { get; set; } = 0.5;

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

        DoParsimony = true;
        NoOneHitWonders = true;
        MassDiffAcceptorType = MassDiffAcceptorType.Exact;
        SearchType = SearchType.Classic;
        DoLabelFreeQuantification = false;
        DoMultiplexQuantification = false;
    }

    public ParallelSearchParameters(SearchParameters searchParams) : this() 
    { 
        CopySearchParameters(searchParams); 
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
        DoSpectralRecovery = searchParams.DoSpectralRecovery;
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