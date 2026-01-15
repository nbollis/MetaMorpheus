#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CsvHelper.Configuration.Attributes;
using TaskLayer.ParallelSearch.Analysis.Analyzers;
using TaskLayer.ParallelSearch.Util;

namespace TaskLayer.ParallelSearch.Analysis;

/// <summary>
/// Aggregated result from all analyzers
/// Stores results as a dynamic dictionary that can be serialized to CSV
/// </summary>
public class AggregatedAnalysisResult : ITransientDbResults, IEquatable<AggregatedAnalysisResult>
{
    /// <summary>
    /// Aggregated result from all analyzers
    /// Stores results as a dynamic dictionary that can be serialized to CSV
    /// </summary>
    public AggregatedAnalysisResult(string dbname) 
    {
        this.DatabaseName = dbname;
    }

    // Empty constructor for csv helper. 
    public AggregatedAnalysisResult() { }

    public string DatabaseName { get; set; }
    
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

    #region Fragment Ions

    public double Psm_Bidirectional_MedianTargets { get; set; }
    public double Psm_ComplementaryCount_MedianTargets { get; set; }
    public double Psm_SequenceCoverageFraction_MedianTargets { get; set; }
    public double Psm_Bidirectional_MedianDecoys { get; set; }
    public double Psm_ComplementaryCount_MedianDecoys { get; set; }
    public double Psm_SequenceCoverageFraction_MedianDecoys { get; set; }

    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] Psm_BidirectionalTargets { get; set; } = Array.Empty<double>();
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] Psm_ComplementaryCountTargets { get; set; } = Array.Empty<double>();
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] Psm_SequenceCoverageFractionTargets { get; set; } = Array.Empty<double>();
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] Psm_BidirectionalDecoys { get; set; } = Array.Empty<double>();
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] Psm_ComplementaryCountDecoys { get; set; } = Array.Empty<double>();
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] Psm_SequenceCoverageFractionDecoys { get; set; } = Array.Empty<double>();

    public double Peptide_Bidirectional_MedianTargets { get; set; }
    public double Peptide_ComplementaryCount_MedianTargets { get; set; }
    public double Peptide_SequenceCoverageFraction_MedianTargets { get; set; }
    public double Peptide_Bidirectional_MedianDecoys { get; set; }
    public double Peptide_ComplementaryCount_MedianDecoys { get; set; }
    public double Peptide_SequenceCoverageFraction_MedianDecoys { get; set; }

    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] Peptide_BidirectionalTargets { get; set; } = Array.Empty<double>();
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] Peptide_ComplementaryCountTargets { get; set; } = Array.Empty<double>();
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] Peptide_SequenceCoverageFractionTargets { get; set; } = Array.Empty<double>();
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] Peptide_BidirectionalDecoys { get; set; } = Array.Empty<double>();
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] Peptide_ComplementaryCountDecoys { get; set; } = Array.Empty<double>();
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] Peptide_SequenceCoverageFractionDecoys { get; set; } = Array.Empty<double>();

    #endregion

    #region Retention Time

    public double Psm_MeanAbsoluteRtError { get; set; }
    public double Psm_RtCorrelationCoefficient { get; set; }
    
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] Psm_AllRtErrors { get; set; } = Array.Empty<double>();

    public double Peptide_MeanAbsoluteRtError { get; set; }
    public double Peptide_RtCorrelationCoefficient { get; set; }
    
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] Peptide_AllRtErrors { get; set; } = Array.Empty<double>();

    #endregion


    /// <summary>
    /// Populates the Results dictionary from the typed properties
    /// Called after CSV deserialization
    /// </summary>
    public void PopulateResultsFromProperties()
    {
        Results.Clear();
        
        // Core metrics
        Results[ResultCountAnalyzer.TotalProteins] = TotalProteins;
        Results[ResultCountAnalyzer.TransientProteinCount] = TransientProteinCount;
        Results[ResultCountAnalyzer.TransientPeptideCount] = TransientPeptideCount;
        Results[ResultCountAnalyzer.TargetPsmsAtQValueThreshold] = TargetPsmsAtQValueThreshold;
        Results[ResultCountAnalyzer.TargetPsmsFromTransientDb] = TargetPsmsFromTransientDb;
        Results[ResultCountAnalyzer.TargetPsmsFromTransientDbAtQValueThreshold] = TargetPsmsFromTransientDbAtQValueThreshold;
        Results[ResultCountAnalyzer.TargetPeptidesAtQValueThreshold] = TargetPeptidesAtQValueThreshold;
        Results[ResultCountAnalyzer.TargetPeptidesFromTransientDb] = TargetPeptidesFromTransientDb;
        Results[ResultCountAnalyzer.TargetPeptidesFromTransientDbAtQValueThreshold] = TargetPeptidesFromTransientDbAtQValueThreshold;
        Results[ProteinGroupAnalyzer.TargetProteinGroupsAtQValueThreshold] = TargetProteinGroupsAtQValueThreshold;
        Results[ProteinGroupAnalyzer.TargetProteinGroupsFromTransientDb] = TargetProteinGroupsFromTransientDb;
        Results[ProteinGroupAnalyzer.TargetProteinGroupsFromTransientDbAtQValueThreshold] = TargetProteinGroupsFromTransientDbAtQValueThreshold;
        Results["StatisticalTestsPassed"] = StatisticalTestsPassed;
        
        // Organism specificity
        Results[OrganismSpecificityAnalyzer.PsmTargets] = PsmTargets;
        Results[OrganismSpecificityAnalyzer.PsmDecoys] = PsmDecoys;
        Results[OrganismSpecificityAnalyzer.PsmBacterialTargets] = PsmBacterialTargets;
        Results[OrganismSpecificityAnalyzer.PsmBacterialDecoys] = PsmBacterialDecoys;
        Results[OrganismSpecificityAnalyzer.PsmBacterialAmbiguous] = PsmBacterialAmbiguous;
        Results[OrganismSpecificityAnalyzer.PsmBacterialUnambiguousTargets] = PsmBacterialUnambiguousTargets;
        Results[OrganismSpecificityAnalyzer.PsmBacterialUnambiguousDecoys] = PsmBacterialUnambiguousDecoys;
        Results[OrganismSpecificityAnalyzer.PsmBacterialUnambiguousTargetScores] = PsmBacterialUnambiguousTargetScores;
        Results[OrganismSpecificityAnalyzer.PsmBacterialUnambiguousDecoyScores] = PsmBacterialUnambiguousDecoyScores;
        
        Results[OrganismSpecificityAnalyzer.PeptideTargets] = PeptideTargets;
        Results[OrganismSpecificityAnalyzer.PeptideDecoys] = PeptideDecoys;
        Results[OrganismSpecificityAnalyzer.PeptideBacterialTargets] = PeptideBacterialTargets;
        Results[OrganismSpecificityAnalyzer.PeptideBacterialDecoys] = PeptideBacterialDecoys;
        Results[OrganismSpecificityAnalyzer.PeptideBacterialAmbiguous] = PeptideBacterialAmbiguous;
        Results[OrganismSpecificityAnalyzer.PeptideBacterialUnambiguousTargets] = PeptideBacterialUnambiguousTargets;
        Results[OrganismSpecificityAnalyzer.PeptideBacterialUnambiguousDecoys] = PeptideBacterialUnambiguousDecoys;
        Results[OrganismSpecificityAnalyzer.PeptideBacterialUnambiguousTargetScores] = PeptideBacterialUnambiguousTargetScores;
        Results[OrganismSpecificityAnalyzer.PeptideBacterialUnambiguousDecoyScores] = PeptideBacterialUnambiguousDecoyScores;
        
        Results[ProteinGroupAnalyzer.ProteinGroupTargets] = ProteinGroupTargets;
        Results[ProteinGroupAnalyzer.ProteinGroupDecoys] = ProteinGroupDecoys;
        Results[ProteinGroupAnalyzer.ProteinGroupBacterialTargets] = ProteinGroupBacterialTargets;
        Results[ProteinGroupAnalyzer.ProteinGroupBacterialDecoys] = ProteinGroupBacterialDecoys;
        Results[ProteinGroupAnalyzer.ProteinGroupBacterialUnambiguousTargets] = ProteinGroupBacterialUnambiguousTargets;
        Results[ProteinGroupAnalyzer.ProteinGroupBacterialUnambiguousDecoys] = ProteinGroupBacterialUnambiguousDecoys;

        // Fragment Ion metrics - PSM
        Results[FragmentIonAnalyzer.PSM_LongestIonSeriesBidirectionalTargets] = Psm_Bidirectional_MedianTargets;
        Results[FragmentIonAnalyzer.PSM_ComplementaryIonCountTargets] = Psm_ComplementaryCount_MedianTargets;
        Results[FragmentIonAnalyzer.PSM_SequenceCoverageFractionTargets] = Psm_SequenceCoverageFraction_MedianTargets;
        Results[FragmentIonAnalyzer.PSM_LongestIonSeriesBidirectionalDecoys] = Psm_Bidirectional_MedianDecoys;
        Results[FragmentIonAnalyzer.PSM_ComplementaryIonCountDecoys] = Psm_ComplementaryCount_MedianDecoys;
        Results[FragmentIonAnalyzer.PSM_SequenceCoverageFractionDecoys] = Psm_SequenceCoverageFraction_MedianDecoys;
        Results[FragmentIonAnalyzer.PSM_LongestIonSeriesBidirectional_AllTargets] = Psm_BidirectionalTargets;
        Results[FragmentIonAnalyzer.PSM_ComplementaryIonCount_AllTargets] = Psm_ComplementaryCountTargets;
        Results[FragmentIonAnalyzer.PSM_SequenceCoverageFraction_AllTargets] = Psm_SequenceCoverageFractionTargets;
        Results[FragmentIonAnalyzer.PSM_LongestIonSeriesBidirectional_AllDecoys] = Psm_BidirectionalDecoys;
        Results[FragmentIonAnalyzer.PSM_ComplementaryIonCount_AllDecoys] = Psm_ComplementaryCountDecoys;
        Results[FragmentIonAnalyzer.PSM_SequenceCoverageFraction_AllDecoys] = Psm_SequenceCoverageFractionDecoys;

        // Fragment Ion metrics - Peptide
        Results[FragmentIonAnalyzer.Peptide_LongestIonSeriesBidirectionalTargets] = Peptide_Bidirectional_MedianTargets;
        Results[FragmentIonAnalyzer.Peptide_ComplementaryIonCountTargets] = Peptide_ComplementaryCount_MedianTargets;
        Results[FragmentIonAnalyzer.Peptide_SequenceCoverageFractionTargets] = Peptide_SequenceCoverageFraction_MedianTargets;
        Results[FragmentIonAnalyzer.Peptide_LongestIonSeriesBidirectionalDecoys] = Peptide_Bidirectional_MedianDecoys;
        Results[FragmentIonAnalyzer.Peptide_ComplementaryIonCountDecoys] = Peptide_ComplementaryCount_MedianDecoys;
        Results[FragmentIonAnalyzer.Peptide_SequenceCoverageFractionDecoys] = Peptide_SequenceCoverageFraction_MedianDecoys;
        Results[FragmentIonAnalyzer.Peptide_LongestIonSeriesBidirectional_AllTargets] = Peptide_BidirectionalTargets;
        Results[FragmentIonAnalyzer.Peptide_ComplementaryIonCount_AllTargets] = Peptide_ComplementaryCountTargets;
        Results[FragmentIonAnalyzer.Peptide_SequenceCoverageFraction_AllTargets] = Peptide_SequenceCoverageFractionTargets;
        Results[FragmentIonAnalyzer.Peptide_LongestIonSeriesBidirectional_AllDecoys] = Peptide_BidirectionalDecoys;
        Results[FragmentIonAnalyzer.Peptide_ComplementaryIonCount_AllDecoys] = Peptide_ComplementaryCountDecoys;
        Results[FragmentIonAnalyzer.Peptide_SequenceCoverageFraction_AllDecoys] = Peptide_SequenceCoverageFractionDecoys;

        // Retention Time metrics
        Results[RetentionTimeAnalyzer.PsmMeanAbsoluteRtError] = Psm_MeanAbsoluteRtError;
        Results[RetentionTimeAnalyzer.PsmRtCorrelationCoefficient] = Psm_RtCorrelationCoefficient;
        Results[RetentionTimeAnalyzer.PsmAllRtErrors] = Psm_AllRtErrors;
        Results[RetentionTimeAnalyzer.PeptideMeanAbsoluteRtError] = Peptide_MeanAbsoluteRtError;
        Results[RetentionTimeAnalyzer.PeptideRtCorrelationCoefficient] = Peptide_RtCorrelationCoefficient;
        Results[RetentionTimeAnalyzer.PeptideAllRtErrors] = Peptide_AllRtErrors;
    }

    /// <summary>
    /// Populates typed properties from the Results dictionary
    /// Called before CSV serialization
    /// </summary>
    public void PopulatePropertiesFromResults()
    {
        // Core metrics
        TotalProteins = GetValue<int>(ResultCountAnalyzer.TotalProteins);
        TransientProteinCount = GetValue<int>(ResultCountAnalyzer.TransientProteinCount);
        TransientPeptideCount = GetValue<int>(ResultCountAnalyzer.TransientPeptideCount);
        TargetPsmsAtQValueThreshold = GetValue<int>(ResultCountAnalyzer.TargetPsmsAtQValueThreshold);
        TargetPsmsFromTransientDb = GetValue<int>(ResultCountAnalyzer.TargetPsmsFromTransientDb);
        TargetPsmsFromTransientDbAtQValueThreshold = GetValue<int>(ResultCountAnalyzer.TargetPsmsFromTransientDbAtQValueThreshold);
        TargetPeptidesAtQValueThreshold = GetValue<int>(ResultCountAnalyzer.TargetPeptidesAtQValueThreshold);
        TargetPeptidesFromTransientDb = GetValue<int>(ResultCountAnalyzer.TargetPeptidesFromTransientDb);
        TargetPeptidesFromTransientDbAtQValueThreshold = GetValue<int>(ResultCountAnalyzer.TargetPeptidesFromTransientDbAtQValueThreshold);
        TargetProteinGroupsAtQValueThreshold = GetValue<int>(ProteinGroupAnalyzer.TargetProteinGroupsAtQValueThreshold);
        TargetProteinGroupsFromTransientDb = GetValue<int>(ProteinGroupAnalyzer.TargetProteinGroupsFromTransientDb);
        TargetProteinGroupsFromTransientDbAtQValueThreshold = GetValue<int>(ProteinGroupAnalyzer.TargetProteinGroupsFromTransientDbAtQValueThreshold);
        StatisticalTestsPassed = GetValue<int>("StatisticalTestsPassed");
        
        // Organism specificity
        PsmTargets = GetValue<int>(OrganismSpecificityAnalyzer.PsmTargets);
        PsmDecoys = GetValue<int>(OrganismSpecificityAnalyzer.PsmDecoys);
        PsmBacterialTargets = GetValue<int>(OrganismSpecificityAnalyzer.PsmBacterialTargets);
        PsmBacterialDecoys = GetValue<int>(OrganismSpecificityAnalyzer.PsmBacterialDecoys);
        PsmBacterialAmbiguous = GetValue<int>(OrganismSpecificityAnalyzer.PsmBacterialAmbiguous);
        PsmBacterialUnambiguousTargets = GetValue<int>(OrganismSpecificityAnalyzer.PsmBacterialUnambiguousTargets);
        PsmBacterialUnambiguousDecoys = GetValue<int>(OrganismSpecificityAnalyzer.PsmBacterialUnambiguousDecoys);
        PsmBacterialUnambiguousTargetScores = GetValue<double[]>(OrganismSpecificityAnalyzer.PsmBacterialUnambiguousTargetScores) ?? Array.Empty<double>();
        PsmBacterialUnambiguousDecoyScores = GetValue<double[]>(OrganismSpecificityAnalyzer.PsmBacterialUnambiguousDecoyScores) ?? Array.Empty<double>();
        
        PeptideTargets = GetValue<int>(OrganismSpecificityAnalyzer.PeptideTargets);
        PeptideDecoys = GetValue<int>(OrganismSpecificityAnalyzer.PeptideDecoys);
        PeptideBacterialTargets = GetValue<int>(OrganismSpecificityAnalyzer.PeptideBacterialTargets);
        PeptideBacterialDecoys = GetValue<int>(OrganismSpecificityAnalyzer.PeptideBacterialDecoys);
        PeptideBacterialAmbiguous = GetValue<int>(OrganismSpecificityAnalyzer.PeptideBacterialAmbiguous);
        PeptideBacterialUnambiguousTargets = GetValue<int>(OrganismSpecificityAnalyzer.PeptideBacterialUnambiguousTargets);
        PeptideBacterialUnambiguousDecoys = GetValue<int>(OrganismSpecificityAnalyzer.PeptideBacterialUnambiguousDecoys);
        PeptideBacterialUnambiguousTargetScores = GetValue<double[]>(OrganismSpecificityAnalyzer.PeptideBacterialUnambiguousTargetScores) ?? Array.Empty<double>();
        PeptideBacterialUnambiguousDecoyScores = GetValue<double[]>(OrganismSpecificityAnalyzer.PeptideBacterialUnambiguousDecoyScores) ?? Array.Empty<double>();
        
        ProteinGroupTargets = GetValue<int>(ProteinGroupAnalyzer.ProteinGroupTargets);
        ProteinGroupDecoys = GetValue<int>(ProteinGroupAnalyzer.ProteinGroupDecoys);
        ProteinGroupBacterialTargets = GetValue<int>(ProteinGroupAnalyzer.ProteinGroupBacterialTargets);
        ProteinGroupBacterialDecoys = GetValue<int>(ProteinGroupAnalyzer.ProteinGroupBacterialDecoys);
        ProteinGroupBacterialUnambiguousTargets = GetValue<int>(ProteinGroupAnalyzer.ProteinGroupBacterialUnambiguousTargets);
        ProteinGroupBacterialUnambiguousDecoys = GetValue<int>(ProteinGroupAnalyzer.ProteinGroupBacterialUnambiguousDecoys);

        // Fragment Ion metrics - PSM
        Psm_Bidirectional_MedianTargets = GetValue<double>(FragmentIonAnalyzer.PSM_LongestIonSeriesBidirectionalTargets);
        Psm_ComplementaryCount_MedianTargets = GetValue<double>(FragmentIonAnalyzer.PSM_ComplementaryIonCountTargets);
        Psm_SequenceCoverageFraction_MedianTargets = GetValue<double>(FragmentIonAnalyzer.PSM_SequenceCoverageFractionTargets);
        Psm_Bidirectional_MedianDecoys = GetValue<double>(FragmentIonAnalyzer.PSM_LongestIonSeriesBidirectionalDecoys);
        Psm_ComplementaryCount_MedianDecoys = GetValue<double>(FragmentIonAnalyzer.PSM_ComplementaryIonCountDecoys);
        Psm_SequenceCoverageFraction_MedianDecoys = GetValue<double>(FragmentIonAnalyzer.PSM_SequenceCoverageFractionDecoys);
        Psm_BidirectionalTargets = GetValue<double[]>(FragmentIonAnalyzer.PSM_LongestIonSeriesBidirectional_AllTargets) ?? Array.Empty<double>();
        Psm_ComplementaryCountTargets = GetValue<double[]>(FragmentIonAnalyzer.PSM_ComplementaryIonCount_AllTargets) ?? Array.Empty<double>();
        Psm_SequenceCoverageFractionTargets = GetValue<double[]>(FragmentIonAnalyzer.PSM_SequenceCoverageFraction_AllTargets) ?? Array.Empty<double>();
        Psm_BidirectionalDecoys = GetValue<double[]>(FragmentIonAnalyzer.PSM_LongestIonSeriesBidirectional_AllDecoys) ?? Array.Empty<double>();
        Psm_ComplementaryCountDecoys = GetValue<double[]>(FragmentIonAnalyzer.PSM_ComplementaryIonCount_AllDecoys) ?? Array.Empty<double>();
        Psm_SequenceCoverageFractionDecoys = GetValue<double[]>(FragmentIonAnalyzer.PSM_SequenceCoverageFraction_AllDecoys) ?? Array.Empty<double>();

        // Fragment Ion metrics - Peptide
        Peptide_Bidirectional_MedianTargets = GetValue<double>(FragmentIonAnalyzer.Peptide_LongestIonSeriesBidirectionalTargets);
        Peptide_ComplementaryCount_MedianTargets = GetValue<double>(FragmentIonAnalyzer.Peptide_ComplementaryIonCountTargets);
        Peptide_SequenceCoverageFraction_MedianTargets = GetValue<double>(FragmentIonAnalyzer.Peptide_SequenceCoverageFractionTargets);
        Peptide_Bidirectional_MedianDecoys = GetValue<double>(FragmentIonAnalyzer.Peptide_LongestIonSeriesBidirectionalDecoys);
        Peptide_ComplementaryCount_MedianDecoys = GetValue<double>(FragmentIonAnalyzer.Peptide_ComplementaryIonCountDecoys);
        Peptide_SequenceCoverageFraction_MedianDecoys = GetValue<double>(FragmentIonAnalyzer.Peptide_SequenceCoverageFractionDecoys);
        Peptide_BidirectionalTargets = GetValue<double[]>(FragmentIonAnalyzer.Peptide_LongestIonSeriesBidirectional_AllTargets) ?? Array.Empty<double>();
        Peptide_ComplementaryCountTargets = GetValue<double[]>(FragmentIonAnalyzer.Peptide_ComplementaryIonCount_AllTargets) ?? Array.Empty<double>();
        Peptide_SequenceCoverageFractionTargets = GetValue<double[]>(FragmentIonAnalyzer.Peptide_SequenceCoverageFraction_AllTargets) ?? Array.Empty<double>();
        Peptide_BidirectionalDecoys = GetValue<double[]>(FragmentIonAnalyzer.Peptide_LongestIonSeriesBidirectional_AllDecoys) ?? Array.Empty<double>();
        Peptide_ComplementaryCountDecoys = GetValue<double[]>(FragmentIonAnalyzer.Peptide_ComplementaryIonCount_AllDecoys) ?? Array.Empty<double>();
        Peptide_SequenceCoverageFractionDecoys = GetValue<double[]>(FragmentIonAnalyzer.Peptide_SequenceCoverageFraction_AllDecoys) ?? Array.Empty<double>();

        // Retention Time metrics
        Psm_MeanAbsoluteRtError = GetValue<double>(RetentionTimeAnalyzer.PsmMeanAbsoluteRtError);
        Psm_RtCorrelationCoefficient = GetValue<double>(RetentionTimeAnalyzer.PsmRtCorrelationCoefficient);
        Psm_AllRtErrors = GetValue<double[]>(RetentionTimeAnalyzer.PsmAllRtErrors) ?? Array.Empty<double>();
        Peptide_MeanAbsoluteRtError = GetValue<double>(RetentionTimeAnalyzer.PeptideMeanAbsoluteRtError);
        Peptide_RtCorrelationCoefficient = GetValue<double>(RetentionTimeAnalyzer.PeptideRtCorrelationCoefficient);
        Peptide_AllRtErrors = GetValue<double[]>(RetentionTimeAnalyzer.PeptideAllRtErrors) ?? Array.Empty<double>();
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

    public bool Equals(AggregatedAnalysisResult? other)
    {
        if (other is null) return false;
        if (other.DatabaseName != DatabaseName) return false;
        if (other.Results.Count != Results.Count) return false;
        return true;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(DatabaseName, Results.Count);
    }
}