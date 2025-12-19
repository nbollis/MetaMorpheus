#nullable enable
using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TaskLayer.ParallelSearchTask.Util;

namespace TaskLayer.ParallelSearchTask.Analysis;

/// <summary>
/// Aggregated result from all analyzers
/// Stores results as a dynamic dictionary that can be serialized to CSV
/// </summary>
public class AggregatedAnalysisResult : ITransientDbResults
{
    public string DatabaseName { get; set; } = string.Empty;
    
    /// <summary>
    /// Serialized representation of all analysis results for CSV storage
    /// This uses a custom converter to flatten the dictionary
    /// </summary>
    [Ignore]
    public Dictionary<string, object> Results { get; set; } = new();
    
    /// <summary>
    /// List of errors encountered during analysis
    /// </summary>
    [Ignore]
    public List<string> Errors { get; } = [];

    #region Core Metrics (Always Present)
    
    // Basic counts
    public int TotalProteins { get; set; }
    public int TransientProteinCount { get; set; }
    public int TransientPeptideCount { get; set; }
    
    // PSM metrics
    public int TargetPsmsAtQValueThreshold { get; set; }
    public int TargetPsmsFromTransientDb { get; set; }
    public int TargetPsmsFromTransientDbAtQValueThreshold { get; set; }
    
    // Peptide metrics
    public int TargetPeptidesAtQValueThreshold { get; set; }
    public int TargetPeptidesFromTransientDb { get; set; }
    public int TargetPeptidesFromTransientDbAtQValueThreshold { get; set; }
    
    // Protein group metrics (0 if parsimony not run)
    public int TargetProteinGroupsAtQValueThreshold { get; set; }
    public int TargetProteinGroupsFromTransientDb { get; set; }
    public int TargetProteinGroupsFromTransientDbAtQValueThreshold { get; set; }
    
    // Statistical testing summary
    public int StatisticalTestsPassed { get; set; }
    
    #endregion

    #region Organism Specificity Metrics (Optional)
    
    public int PsmTargets { get; set; }
    public int PsmDecoys { get; set; }
    public int PsmBacterialTargets { get; set; }
    public int PsmBacterialDecoys { get; set; }
    public int PsmBacterialAmbiguous { get; set; }
    public int PsmBacterialUnambiguousTargets { get; set; }
    public int PsmBacterialUnambiguousDecoys { get; set; }
    
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] PsmBacterialUnambiguousTargetScores { get; set; } = Array.Empty<double>();
    
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] PsmBacterialUnambiguousDecoyScores { get; set; } = Array.Empty<double>();
    
    public int PeptideTargets { get; set; }
    public int PeptideDecoys { get; set; }
    public int PeptideBacterialTargets { get; set; }
    public int PeptideBacterialDecoys { get; set; }
    public int PeptideBacterialAmbiguous { get; set; }
    public int PeptideBacterialUnambiguousTargets { get; set; }
    public int PeptideBacterialUnambiguousDecoys { get; set; }
    
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] PeptideBacterialUnambiguousTargetScores { get; set; } = Array.Empty<double>();
    
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] PeptideBacterialUnambiguousDecoyScores { get; set; } = Array.Empty<double>();
    
    public int ProteinGroupTargets { get; set; }
    public int ProteinGroupDecoys { get; set; }
    public int ProteinGroupBacterialTargets { get; set; }
    public int ProteinGroupBacterialDecoys { get; set; }
    public int ProteinGroupBacterialUnambiguousTargets { get; set; }
    public int ProteinGroupBacterialUnambiguousDecoys { get; set; }
    
    #endregion

    /// <summary>
    /// Populates the Results dictionary from the typed properties
    /// Called after CSV deserialization
    /// </summary>
    public void PopulateResultsFromProperties()
    {
        Results.Clear();
        
        // Core metrics
        Results["TotalProteins"] = TotalProteins;
        Results["TransientProteinCount"] = TransientProteinCount;
        Results["TransientPeptideCount"] = TransientPeptideCount;
        Results["TargetPsmsAtQValueThreshold"] = TargetPsmsAtQValueThreshold;
        Results["TargetPsmsFromTransientDb"] = TargetPsmsFromTransientDb;
        Results["TargetPsmsFromTransientDbAtQValueThreshold"] = TargetPsmsFromTransientDbAtQValueThreshold;
        Results["TargetPeptidesAtQValueThreshold"] = TargetPeptidesAtQValueThreshold;
        Results["TargetPeptidesFromTransientDb"] = TargetPeptidesFromTransientDb;
        Results["TargetPeptidesFromTransientDbAtQValueThreshold"] = TargetPeptidesFromTransientDbAtQValueThreshold;
        Results["TargetProteinGroupsAtQValueThreshold"] = TargetProteinGroupsAtQValueThreshold;
        Results["TargetProteinGroupsFromTransientDb"] = TargetProteinGroupsFromTransientDb;
        Results["TargetProteinGroupsFromTransientDbAtQValueThreshold"] = TargetProteinGroupsFromTransientDbAtQValueThreshold;
        Results["StatisticalTestsPassed"] = StatisticalTestsPassed;
        
        // Organism specificity
        Results["PsmTargets"] = PsmTargets;
        Results["PsmDecoys"] = PsmDecoys;
        Results["PsmBacterialTargets"] = PsmBacterialTargets;
        Results["PsmBacterialDecoys"] = PsmBacterialDecoys;
        Results["PsmBacterialAmbiguous"] = PsmBacterialAmbiguous;
        Results["PsmBacterialUnambiguousTargets"] = PsmBacterialUnambiguousTargets;
        Results["PsmBacterialUnambiguousDecoys"] = PsmBacterialUnambiguousDecoys;
        Results["PsmBacterialUnambiguousTargetScores"] = PsmBacterialUnambiguousTargetScores;
        Results["PsmBacterialUnambiguousDecoyScores"] = PsmBacterialUnambiguousDecoyScores;
        
        Results["PeptideTargets"] = PeptideTargets;
        Results["PeptideDecoys"] = PeptideDecoys;
        Results["PeptideBacterialTargets"] = PeptideBacterialTargets;
        Results["PeptideBacterialDecoys"] = PeptideBacterialDecoys;
        Results["PeptideBacterialAmbiguous"] = PeptideBacterialAmbiguous;
        Results["PeptideBacterialUnambiguousTargets"] = PeptideBacterialUnambiguousTargets;
        Results["PeptideBacterialUnambiguousDecoys"] = PeptideBacterialUnambiguousDecoys;
        Results["PeptideBacterialUnambiguousTargetScores"] = PeptideBacterialUnambiguousTargetScores;
        Results["PeptideBacterialUnambiguousDecoyScores"] = PeptideBacterialUnambiguousDecoyScores;
        
        Results["ProteinGroupTargets"] = ProteinGroupTargets;
        Results["ProteinGroupDecoys"] = ProteinGroupDecoys;
        Results["ProteinGroupBacterialTargets"] = ProteinGroupBacterialTargets;
        Results["ProteinGroupBacterialDecoys"] = ProteinGroupBacterialDecoys;
        Results["ProteinGroupBacterialUnambiguousTargets"] = ProteinGroupBacterialUnambiguousTargets;
        Results["ProteinGroupBacterialUnambiguousDecoys"] = ProteinGroupBacterialUnambiguousDecoys;
    }

    /// <summary>
    /// Populates typed properties from the Results dictionary
    /// Called before CSV serialization
    /// </summary>
    public void PopulatePropertiesFromResults()
    {
        // Core metrics
        TotalProteins = GetValue<int>("TotalProteins");
        TransientProteinCount = GetValue<int>("TransientProteinCount");
        TransientPeptideCount = GetValue<int>("TransientPeptideCount");
        TargetPsmsAtQValueThreshold = GetValue<int>("TargetPsmsAtQValueThreshold");
        TargetPsmsFromTransientDb = GetValue<int>("TargetPsmsFromTransientDb");
        TargetPsmsFromTransientDbAtQValueThreshold = GetValue<int>("TargetPsmsFromTransientDbAtQValueThreshold");
        TargetPeptidesAtQValueThreshold = GetValue<int>("TargetPeptidesAtQValueThreshold");
        TargetPeptidesFromTransientDb = GetValue<int>("TargetPeptidesFromTransientDb");
        TargetPeptidesFromTransientDbAtQValueThreshold = GetValue<int>("TargetPeptidesFromTransientDbAtQValueThreshold");
        TargetProteinGroupsAtQValueThreshold = GetValue<int>("TargetProteinGroupsAtQValueThreshold");
        TargetProteinGroupsFromTransientDb = GetValue<int>("TargetProteinGroupsFromTransientDb");
        TargetProteinGroupsFromTransientDbAtQValueThreshold = GetValue<int>("TargetProteinGroupsFromTransientDbAtQValueThreshold");
        StatisticalTestsPassed = GetValue<int>("StatisticalTestsPassed");
        
        // Organism specificity
        PsmTargets = GetValue<int>("PsmTargets");
        PsmDecoys = GetValue<int>("PsmDecoys");
        PsmBacterialTargets = GetValue<int>("PsmBacterialTargets");
        PsmBacterialDecoys = GetValue<int>("PsmBacterialDecoys");
        PsmBacterialAmbiguous = GetValue<int>("PsmBacterialAmbiguous");
        PsmBacterialUnambiguousTargets = GetValue<int>("PsmBacterialUnambiguousTargets");
        PsmBacterialUnambiguousDecoys = GetValue<int>("PsmBacterialUnambiguousDecoys");
        PsmBacterialUnambiguousTargetScores = GetValue<double[]>("PsmBacterialUnambiguousTargetScores") ?? Array.Empty<double>();
        PsmBacterialUnambiguousDecoyScores = GetValue<double[]>("PsmBacterialUnambiguousDecoyScores") ?? Array.Empty<double>();
        
        PeptideTargets = GetValue<int>("PeptideTargets");
        PeptideDecoys = GetValue<int>("PeptideDecoys");
        PeptideBacterialTargets = GetValue<int>("PeptideBacterialTargets");
        PeptideBacterialDecoys = GetValue<int>("PeptideBacterialDecoys");
        PeptideBacterialAmbiguous = GetValue<int>("PeptideBacterialAmbiguous");
        PeptideBacterialUnambiguousTargets = GetValue<int>("PeptideBacterialUnambiguousTargets");
        PeptideBacterialUnambiguousDecoys = GetValue<int>("PeptideBacterialUnambiguousDecoys");
        PeptideBacterialUnambiguousTargetScores = GetValue<double[]>("PeptideBacterialUnambiguousTargetScores") ?? Array.Empty<double>();
        PeptideBacterialUnambiguousDecoyScores = GetValue<double[]>("PeptideBacterialUnambiguousDecoyScores") ?? Array.Empty<double>();
        
        ProteinGroupTargets = GetValue<int>("ProteinGroupTargets");
        ProteinGroupDecoys = GetValue<int>("ProteinGroupDecoys");
        ProteinGroupBacterialTargets = GetValue<int>("ProteinGroupBacterialTargets");
        ProteinGroupBacterialDecoys = GetValue<int>("ProteinGroupBacterialDecoys");
        ProteinGroupBacterialUnambiguousTargets = GetValue<int>("ProteinGroupBacterialUnambiguousTargets");
        ProteinGroupBacterialUnambiguousDecoys = GetValue<int>("ProteinGroupBacterialUnambiguousDecoys");
    }

    /// <summary>
    /// Gets a typed value from the results dictionary with a default fallback
    /// </summary>
    public T? GetValue<T>(string key, T? defaultValue = default)
    {
        if (Results.TryGetValue(key, out var value))
        {
            try
            {
                if (value is T typedValue)
                    return typedValue;
                
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    /// <summary>
    /// Writes the database results to a text file
    /// Maintains backward compatibility with TransientDatabaseSearchResults format
    /// </summary>
    public async Task WriteToTextFileAsync(string filePath, double qValueThreshold, bool doParsimony)
    {
        await using StreamWriter file = new StreamWriter(filePath);
        await file.WriteLineAsync($"Database: {DatabaseName}");
        await file.WriteLineAsync($"Total proteins in combined database: {TotalProteins}");
        await file.WriteLineAsync($"Total proteins from transient database: {TransientProteinCount}");
        await file.WriteLineAsync($"Total peptides from transient database: {TransientPeptideCount}");
        await file.WriteLineAsync();
        
        await file.WriteLineAsync($"Target PSMs at {qValueThreshold * 100}% FDR: {TargetPsmsAtQValueThreshold}");
        await file.WriteLineAsync($"Target PSMs from transient database: {TargetPsmsFromTransientDb}");
        await file.WriteLineAsync($"Target PSMs from transient database at {qValueThreshold * 100}% FDR: {TargetPsmsFromTransientDbAtQValueThreshold}");
        await file.WriteLineAsync($"PSM Bacterial Targets: {PsmBacterialTargets}");
        await file.WriteLineAsync($"PSM Bacterial Unambiguous Targets: {PsmBacterialUnambiguousTargets}");
        await file.WriteLineAsync();
        
        await file.WriteLineAsync($"Target peptides at {qValueThreshold * 100}% FDR: {TargetPeptidesAtQValueThreshold}");
        await file.WriteLineAsync($"Target peptides from transient database: {TargetPeptidesFromTransientDb}");
        await file.WriteLineAsync($"Target peptides from transient database at {qValueThreshold * 100}% FDR: {TargetPeptidesFromTransientDbAtQValueThreshold}");
        await file.WriteLineAsync($"Peptide Bacterial Targets: {PeptideBacterialTargets}");
        await file.WriteLineAsync($"Peptide Bacterial Unambiguous Targets: {PeptideBacterialUnambiguousTargets}");

        if (doParsimony)
        {
            await file.WriteLineAsync();
            await file.WriteLineAsync($"Target protein groups at {qValueThreshold * 100}% FDR: {TargetProteinGroupsAtQValueThreshold}");
            await file.WriteLineAsync($"Target protein groups with transient database proteins: {TargetProteinGroupsFromTransientDb}");
            await file.WriteLineAsync($"Target protein groups with transient database proteins at {qValueThreshold * 100}% FDR: {TargetProteinGroupsFromTransientDbAtQValueThreshold}");
            await file.WriteLineAsync($"Protein Group Bacterial Targets: {ProteinGroupBacterialTargets}");
            await file.WriteLineAsync($"Protein Group Bacterial Unambiguous Targets: {ProteinGroupBacterialUnambiguousTargets}");
        }
    }
}