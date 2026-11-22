#nullable enable
using CsvHelper.Configuration.Attributes;
using EngineLayer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TaskLayer.ParallelSearch.Analysis.Collectors;
using TaskLayer.ParallelSearch.Util.Converters;

namespace TaskLayer.ParallelSearch.Analysis;

/// <summary>
/// Aggregated result from all analyzers
/// Stores results as a dynamic dictionary that can be serialized to CSV
/// </summary>
public class TransientDatabaseMetrics : IEquatable<TransientDatabaseMetrics>
{
    /// <summary>
    /// Aggregated result from all analyzers
    /// Stores results as a dynamic dictionary that can be serialized to CSV
    /// </summary>
    public TransientDatabaseMetrics(string dbname) 
    {
        this.DatabaseName = dbname;
    }

    // Empty constructor for csv helper. 
    public TransientDatabaseMetrics() { }

    public string DatabaseName { get; set; }

    /// <summary>
    /// Name of the target this database is the entrapment twin of; empty for a real transient database.
    /// Optional so summaries written before twins existed still read.
    /// </summary>
    [Optional] public string TwinOf { get; set; } = string.Empty;

    [Ignore] public bool IsTwin => !string.IsNullOrEmpty(TwinOf);

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

    #region Core Metrics 
    
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
    public int DecoyProteinGroupsFromTransientDbAtQValueThreshold { get; set; }

    [Optional] public double MedianPsmsPerProteinGroup { get; set; }
    [Optional] public double MedianPeptidesPerProteinGroup { get; set; }
    [Optional] public double MedianUniquePeptidesPerProteinGroup { get; set; }

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] AllPsmsPerProteinGroup { get; set; } = Array.Empty<double>();

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] AllPeptidesPerProteinGroup { get; set; } = Array.Empty<double>();

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] AllUniquePeptidesPerProteinGroup { get; set; } = Array.Empty<double>();

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] AllSequenceCoverageFractions { get; set; } = Array.Empty<double>();

    [Optional]
    public double MedianSequenceCoverageFraction { get; set; } = double.NaN;

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] AllFragmentSequenceCoverageFractions { get; set; } = Array.Empty<double>();

    // Statistical testing summary (not persisted to CSV — only in StatisticalAnalysis_Results.csv)
    [Ignore] public int StatisticalTestsPassed { get; set; }
    [Ignore][Optional] public int StatisticalTestsRun { get; set; }
    [Ignore][Optional] public double TestPassedRatio { get; set; }
    [Ignore][Optional] public int ValidTestCount { get; set; }
    [Ignore][Optional] public int PassedTestCount { get; set; }
    [Ignore][Optional] public int ValidFamilyCount { get; set; }
    [Ignore][Optional] public int PassedFamilyCount { get; set; }
    [Ignore][Optional] public double CombinedPValue { get; set; } = double.NaN;
    [Ignore][Optional] public double CombinedQValue { get; set; } = double.NaN;

    [Ignore][Optional] public int CountEnrichmentValidTests { get; set; }
    [Ignore][Optional] public int CountEnrichmentPassedTests { get; set; }
    [Ignore][Optional] public double CountEnrichmentBestPValue { get; set; } = double.NaN;
    [Ignore][Optional] public double CountEnrichmentBestQValue { get; set; } = double.NaN;
    [Ignore][Optional] public double CountEnrichmentCombinedPValue { get; set; } = double.NaN;
    [Ignore][Optional] public double CountEnrichmentCombinedQValue { get; set; } = double.NaN;

    [Ignore][Optional] public int AmbiguityOrTargetDecoyValidTests { get; set; }
    [Ignore][Optional] public int AmbiguityOrTargetDecoyPassedTests { get; set; }
    [Ignore][Optional] public double AmbiguityOrTargetDecoyBestPValue { get; set; } = double.NaN;
    [Ignore][Optional] public double AmbiguityOrTargetDecoyBestQValue { get; set; } = double.NaN;
    [Ignore][Optional] public double AmbiguityOrTargetDecoyCombinedPValue { get; set; } = double.NaN;
    [Ignore][Optional] public double AmbiguityOrTargetDecoyCombinedQValue { get; set; } = double.NaN;

    [Ignore][Optional] public int ScoreDistributionValidTests { get; set; }
    [Ignore][Optional] public int ScoreDistributionPassedTests { get; set; }
    [Ignore][Optional] public double ScoreDistributionBestPValue { get; set; } = double.NaN;
    [Ignore][Optional] public double ScoreDistributionBestQValue { get; set; } = double.NaN;
    [Ignore][Optional] public double ScoreDistributionCombinedPValue { get; set; } = double.NaN;
    [Ignore][Optional] public double ScoreDistributionCombinedQValue { get; set; } = double.NaN;

    [Ignore][Optional] public int FragmentationValidTests { get; set; }
    [Ignore][Optional] public int FragmentationPassedTests { get; set; }
    [Ignore][Optional] public double FragmentationBestPValue { get; set; } = double.NaN;
    [Ignore][Optional] public double FragmentationBestQValue { get; set; } = double.NaN;
    [Ignore][Optional] public double FragmentationCombinedPValue { get; set; } = double.NaN;
    [Ignore][Optional] public double FragmentationCombinedQValue { get; set; } = double.NaN;

    [Ignore][Optional] public int RetentionTimeValidTests { get; set; }
    [Ignore][Optional] public int RetentionTimePassedTests { get; set; }
    [Ignore][Optional] public double RetentionTimeBestPValue { get; set; } = double.NaN;
    [Ignore][Optional] public double RetentionTimeBestQValue { get; set; } = double.NaN;
    [Ignore][Optional] public double RetentionTimeCombinedPValue { get; set; } = double.NaN;
    [Ignore][Optional] public double RetentionTimeCombinedQValue { get; set; } = double.NaN;

    [Ignore][Optional] public int ProteinGroupValidTests { get; set; }
    [Ignore][Optional] public int ProteinGroupPassedTests { get; set; }
    [Ignore][Optional] public double ProteinGroupBestPValue { get; set; } = double.NaN;
    [Ignore][Optional] public double ProteinGroupBestQValue { get; set; } = double.NaN;
    [Ignore][Optional] public double ProteinGroupCombinedPValue { get; set; } = double.NaN;
    [Ignore][Optional] public double ProteinGroupCombinedQValue { get; set; } = double.NaN;

    [Ignore][Optional] public int DeNovoValidTests { get; set; }
    [Ignore][Optional] public int DeNovoPassedTests { get; set; }
    [Ignore][Optional] public double DeNovoBestPValue { get; set; } = double.NaN;
    [Ignore][Optional] public double DeNovoBestQValue { get; set; } = double.NaN;
    [Ignore][Optional] public double DeNovoCombinedPValue { get; set; } = double.NaN;
    [Ignore][Optional] public double DeNovoCombinedQValue { get; set; } = double.NaN;

    [Ignore][Optional] public int PrecursorDeconvolutionValidTests { get; set; }
    [Ignore][Optional] public int PrecursorDeconvolutionPassedTests { get; set; }
    [Ignore][Optional] public double PrecursorDeconvolutionBestPValue { get; set; } = double.NaN;
    [Ignore][Optional] public double PrecursorDeconvolutionBestQValue { get; set; } = double.NaN;
    [Ignore][Optional] public double PrecursorDeconvolutionCombinedPValue { get; set; } = double.NaN;
    [Ignore][Optional] public double PrecursorDeconvolutionCombinedQValue { get; set; } = double.NaN;

    #endregion

    #region Organism Specificity Metrics 

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

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] PsmPrecursorMassErrors { get; set; } = Array.Empty<double>();

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] PsmBacterialTargetDeltaScores { get; set; } = Array.Empty<double>();

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

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] PeptidePrecursorMassErrors { get; set; } = Array.Empty<double>();

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] PeptideBacterialTargetDeltaScores { get; set; } = Array.Empty<double>();


    public int ProteinGroupTargets { get; set; }
    public int ProteinGroupDecoys { get; set; }
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

    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    [Optional] public double[] Psm_FragmentPPMErrors { get; set; } = Array.Empty<double>();

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

    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    [Optional] public double[] Peptide_FragmentPPMErrors { get; set; } = Array.Empty<double>();

    /// <summary>One value per confident target peptide: the median absolute fragment mass error of its PSM.</summary>
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    [Optional] public double[] Peptide_MedianAbsoluteFragmentPpmErrorsTargets { get; set; } = Array.Empty<double>();

    #endregion

    #region Retention Time

    // Observed minus calibrated predicted retention time, in minutes; NaN when absent
    [Optional] public double Psm_MeanAbsoluteRtError { get; set; } = double.NaN;
    [Optional] public double Psm_RtCorrelationCoefficient { get; set; } = double.NaN;

    [Optional] [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] Psm_AllRtErrors { get; set; } = Array.Empty<double>();

    [Optional] public double Peptide_MeanAbsoluteRtError { get; set; } = double.NaN;
    [Optional] public double Peptide_RtCorrelationCoefficient { get; set; } = double.NaN;

    [Optional] [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] Peptide_AllRtErrors { get; set; } = Array.Empty<double>();

    #endregion

    #region Deconvolution

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] PsmPrecursorDeconScores { get; set; } = Array.Empty<double>();

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] PsmPrecursorEnvelopePeakCounts { get; set; } = Array.Empty<double>();

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] PsmPrecursorFractionalIntensities { get; set; } = Array.Empty<double>();

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] PeptidePrecursorDeconScores { get; set; } = Array.Empty<double>();

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] PeptidePrecursorEnvelopePeakCounts { get; set; } = Array.Empty<double>();

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] PeptidePrecursorFractionalIntensities { get; set; } = Array.Empty<double>();

    #endregion

    #region DeNovo Mapping

    [Optional] public int TotalPredictions { get; set; }
    [Optional] public int TargetPredictions { get; set; }
    [Optional] public int DecoyPredictions { get; set; }
    [Optional] public int UniquePeptidesMapped { get; set; }
    [Optional] public int UniqueProteinsMapped { get; set; }
    [Optional] public double DeNovoMeanRtError { get; set; } = double.NaN;
    [Optional] public double MeanPredictionScore { get; set; } = double.NaN;

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] DeNovoRetentionTimeErrors { get; set; } = Array.Empty<double>();

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] PredictionScores { get; set; } = Array.Empty<double>();

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] TargetPredictionScores { get; set; } = Array.Empty<double>();

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] DecoyPredictionScores { get; set; } = Array.Empty<double>();

    #endregion

    #region De Novo Host Ambiguity

    // Hits whose peptide the host (the foreground organism's base proteins) also produces are host-ambiguous; the de novo
    // tests use the unambiguous hits, as the PSM tests use unambiguous PSMs. DeNovoHostProteinCount is -1 when the
    // mapping had no host (an older or external mapping summary), so the unambiguous columns are unknown.

    [Optional] public int DeNovoHostProteinCount { get; set; } = -1;
    [Optional] public int UnambiguousTargetPredictions { get; set; }
    [Optional] public int AmbiguousTargetPredictions { get; set; }
    [Optional] public int UnambiguousDecoyPredictions { get; set; }
    [Optional] public int UniqueUnambiguousPeptidesMapped { get; set; }
    [Optional] public int UniqueUnambiguousProteinsMapped { get; set; }

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] DeNovoUnambiguousRetentionTimeErrors { get; set; } = Array.Empty<double>();

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] UnambiguousTargetPredictionScores { get; set; } = Array.Empty<double>();

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] UnambiguousDecoyPredictionScores { get; set; } = Array.Empty<double>();

    // Per mapped peptide: the median RT error and median score of that peptide's predictions, and distinct decoy peptides.
    // Absent (empty, -1) in summaries written before the mapper reported them.

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] DeNovoPeptideRetentionTimeErrors { get; set; } = Array.Empty<double>();

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] DeNovoPeptidePredictionScores { get; set; } = Array.Empty<double>();

    [Optional] public int UniqueDecoyPeptidesMapped { get; set; } = -1;

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] DeNovoUnambiguousPeptideRetentionTimeErrors { get; set; } = Array.Empty<double>();

    [Optional]
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] DeNovoUnambiguousPeptidePredictionScores { get; set; } = Array.Empty<double>();

    [Optional] public int UniqueUnambiguousDecoyPeptidesMapped { get; set; } = -1;

    /// <summary>True when the de novo mapping had a host, so the unambiguous columns are filled.</summary>
    [Ignore] public bool DeNovoHasHost => DeNovoHostProteinCount >= 0;

    // The values the de novo tests use: the unambiguous hits when the mapping had a host, otherwise every hit.
    [Ignore] public int DeNovoTestedTargets => DeNovoHasHost ? UnambiguousTargetPredictions : TargetPredictions;
    [Ignore] public int DeNovoTestedDecoys => DeNovoHasHost ? UnambiguousDecoyPredictions : DecoyPredictions;
    [Ignore] public int DeNovoTestedPredictions => DeNovoTestedTargets + DeNovoTestedDecoys;
    [Ignore] public int DeNovoTestedUniquePeptides => DeNovoHasHost ? UniqueUnambiguousPeptidesMapped : UniquePeptidesMapped;
    [Ignore] public int DeNovoTestedUniqueProteins => DeNovoHasHost ? UniqueUnambiguousProteinsMapped : UniqueProteinsMapped;
    [Ignore] public double[] DeNovoTestedRetentionTimeErrors => DeNovoHasHost ? DeNovoUnambiguousRetentionTimeErrors : DeNovoRetentionTimeErrors;
    [Ignore] public double[] DeNovoTestedPredictionScores => DeNovoHasHost
        ? UnambiguousTargetPredictionScores.Concat(UnambiguousDecoyPredictionScores).ToArray()
        : PredictionScores;
    // per mapped peptide, as the de novo tests use them
    [Ignore] public double[] DeNovoTestedPeptideRetentionTimeErrors =>
        DeNovoHasHost ? DeNovoUnambiguousPeptideRetentionTimeErrors : DeNovoPeptideRetentionTimeErrors;
    [Ignore] public double[] DeNovoTestedPeptidePredictionScores =>
        DeNovoHasHost ? DeNovoUnambiguousPeptidePredictionScores : DeNovoPeptidePredictionScores;
    [Ignore] public int DeNovoTestedUniqueDecoyPeptides =>
        Math.Max(0, DeNovoHasHost ? UniqueUnambiguousDecoyPeptidesMapped : UniqueDecoyPeptidesMapped);
    [Ignore] public double DeNovoTestedMeanPredictionScore => DeNovoHasHost
        ? (DeNovoTestedPredictionScores is { Length: > 0 } scores ? scores.Average() : double.NaN)
        : MeanPredictionScore;

    #endregion

    /// <summary>
    /// Populates the Results dictionary from the typed properties
    /// Called after CSV deserialization
    /// </summary>
    public void PopulateResultsFromProperties()
    {
        Results.Clear();
        
        // Core metrics
        Results[BasicMetricCollector.TotalProteins] = TotalProteins;
        Results[BasicMetricCollector.TransientProteinCount] = TransientProteinCount;
        Results[BasicMetricCollector.TransientPeptideCount] = TransientPeptideCount;
        Results[BasicMetricCollector.TargetPsmsAtQValueThreshold] = TargetPsmsAtQValueThreshold;
        Results[BasicMetricCollector.TargetPsmsFromTransientDb] = TargetPsmsFromTransientDb;
        Results[BasicMetricCollector.TargetPsmsFromTransientDbAtQValueThreshold] = TargetPsmsFromTransientDbAtQValueThreshold;
        Results[BasicMetricCollector.TargetPeptidesAtQValueThreshold] = TargetPeptidesAtQValueThreshold;
        Results[BasicMetricCollector.TargetPeptidesFromTransientDb] = TargetPeptidesFromTransientDb;
        Results[BasicMetricCollector.TargetPeptidesFromTransientDbAtQValueThreshold] = TargetPeptidesFromTransientDbAtQValueThreshold;
        Results[ProteinGroupCollector.TargetProteinGroupsAtQValueThreshold] = TargetProteinGroupsAtQValueThreshold;
        Results[ProteinGroupCollector.TargetProteinGroupsFromTransientDb] = TargetProteinGroupsFromTransientDb;
        Results[ProteinGroupCollector.TargetProteinGroupsFromTransientDbAtQValueThreshold] = TargetProteinGroupsFromTransientDbAtQValueThreshold;
        Results[ProteinGroupCollector.DecoyProteinGroupsFromTransientDbAtQValueThreshold] = DecoyProteinGroupsFromTransientDbAtQValueThreshold;
        
        // Organism specificity
        Results[PsmPeptideSearchCollector.PsmTargets] = PsmTargets;
        Results[PsmPeptideSearchCollector.PsmDecoys] = PsmDecoys;
        Results[PsmPeptideSearchCollector.PsmBacterialTargets] = PsmBacterialTargets;
        Results[PsmPeptideSearchCollector.PsmBacterialDecoys] = PsmBacterialDecoys;
        Results[PsmPeptideSearchCollector.PsmBacterialAmbiguous] = PsmBacterialAmbiguous;
        Results[PsmPeptideSearchCollector.PsmBacterialUnambiguousTargets] = PsmBacterialUnambiguousTargets;
        Results[PsmPeptideSearchCollector.PsmBacterialUnambiguousDecoys] = PsmBacterialUnambiguousDecoys;
        Results[PsmPeptideSearchCollector.PsmBacterialUnambiguousTargetScores] = PsmBacterialUnambiguousTargetScores;
        Results[PsmPeptideSearchCollector.PsmBacterialUnambiguousDecoyScores] = PsmBacterialUnambiguousDecoyScores;
        Results[PsmPeptideSearchCollector.PsmBacteriaTargetlDeltaScores] = PsmBacterialTargetDeltaScores;

        Results[PsmPeptideSearchCollector.PeptideTargets] = PeptideTargets;
        Results[PsmPeptideSearchCollector.PeptideDecoys] = PeptideDecoys;
        Results[PsmPeptideSearchCollector.PeptideBacterialTargets] = PeptideBacterialTargets;
        Results[PsmPeptideSearchCollector.PeptideBacterialDecoys] = PeptideBacterialDecoys;
        Results[PsmPeptideSearchCollector.PeptideBacterialAmbiguous] = PeptideBacterialAmbiguous;
        Results[PsmPeptideSearchCollector.PeptideBacterialUnambiguousTargets] = PeptideBacterialUnambiguousTargets;
        Results[PsmPeptideSearchCollector.PeptideBacterialUnambiguousDecoys] = PeptideBacterialUnambiguousDecoys;
        Results[PsmPeptideSearchCollector.PeptideBacterialUnambiguousTargetScores] = PeptideBacterialUnambiguousTargetScores;
        Results[PsmPeptideSearchCollector.PeptideBacterialUnambiguousDecoyScores] = PeptideBacterialUnambiguousDecoyScores;
        Results[PsmPeptideSearchCollector.PeptideBacterialTargetDeltaScores] = PeptideBacterialTargetDeltaScores;
        
        Results[ProteinGroupCollector.ProteinGroupTargets] = ProteinGroupTargets;
        Results[ProteinGroupCollector.ProteinGroupDecoys] = ProteinGroupDecoys;
        Results[ProteinGroupCollector.ProteinGroupBacterialUnambiguousTargets] = ProteinGroupBacterialUnambiguousTargets;
        Results[ProteinGroupCollector.ProteinGroupBacterialUnambiguousDecoys] = ProteinGroupBacterialUnambiguousDecoys;

        Results[ProteinGroupCollector.MedianPeptidesPerProteinGroup] = MedianPeptidesPerProteinGroup;
        Results[ProteinGroupCollector.MedianUniquePeptidesPerProteinGroup] = MedianUniquePeptidesPerProteinGroup;
        Results[ProteinGroupCollector.MedianPsmsPerProteinGroup] = MedianPsmsPerProteinGroup;
        Results[ProteinGroupCollector.AllPeptidesPerProteinGroup] = AllPeptidesPerProteinGroup;
        Results[ProteinGroupCollector.AllUniquePeptidesPerProteinGroup] = AllUniquePeptidesPerProteinGroup;
        Results[ProteinGroupCollector.AllPsmsPerProteinGroup] = AllPsmsPerProteinGroup;
        Results[ProteinGroupCollector.AllSequenceCoverageFractions] = AllSequenceCoverageFractions;
        Results["MedianSequenceCoverageFraction"] = MedianSequenceCoverageFraction;
        Results[ProteinGroupCollector.AllFragmentSequenceCoverageFractions] = AllFragmentSequenceCoverageFractions;

        // Decon
        Results[PsmPeptideSearchCollector.PsmPrecusorDeconScores] = PsmPrecursorDeconScores;
        Results[PsmPeptideSearchCollector.PsmPrecursorMassErrors] = PsmPrecursorMassErrors;
        Results[PsmPeptideSearchCollector.PsmPrecursorEnvelopePeakCount] = PsmPrecursorEnvelopePeakCounts;
        Results[PsmPeptideSearchCollector.PsmPrecusorFractionalIntensity] = PsmPrecursorFractionalIntensities;
        Results[PsmPeptideSearchCollector.PeptidePrecusorDeconScores] = PeptidePrecursorDeconScores;
        Results[PsmPeptideSearchCollector.PeptidePrecursorMassErrors] = PeptidePrecursorMassErrors;
        Results[PsmPeptideSearchCollector.PeptidePrecursorEnvelopePeakCount] = PeptidePrecursorEnvelopePeakCounts;
        Results[PsmPeptideSearchCollector.PeptidePrecusorFractionalIntensity] = PeptidePrecursorFractionalIntensities;

        // Fragment Ion metrics - PSM
        Results[FragmentIonCollector.PSM_LongestIonSeriesBidirectionalTargets] = Psm_Bidirectional_MedianTargets;
        Results[FragmentIonCollector.PSM_ComplementaryIonCountTargets] = Psm_ComplementaryCount_MedianTargets;
        Results[FragmentIonCollector.PSM_SequenceCoverageFractionTargets] = Psm_SequenceCoverageFraction_MedianTargets;
        Results[FragmentIonCollector.PSM_LongestIonSeriesBidirectionalDecoys] = Psm_Bidirectional_MedianDecoys;
        Results[FragmentIonCollector.PSM_ComplementaryIonCountDecoys] = Psm_ComplementaryCount_MedianDecoys;
        Results[FragmentIonCollector.PSM_SequenceCoverageFractionDecoys] = Psm_SequenceCoverageFraction_MedianDecoys;
        Results[FragmentIonCollector.PSM_LongestIonSeriesBidirectional_AllTargets] = Psm_BidirectionalTargets;
        Results[FragmentIonCollector.PSM_ComplementaryIonCount_AllTargets] = Psm_ComplementaryCountTargets;
        Results[FragmentIonCollector.PSM_SequenceCoverageFraction_AllTargets] = Psm_SequenceCoverageFractionTargets;
        Results[FragmentIonCollector.PSM_LongestIonSeriesBidirectional_AllDecoys] = Psm_BidirectionalDecoys;
        Results[FragmentIonCollector.PSM_ComplementaryIonCount_AllDecoys] = Psm_ComplementaryCountDecoys;
        Results[FragmentIonCollector.PSM_SequenceCoverageFraction_AllDecoys] = Psm_SequenceCoverageFractionDecoys;
        Results[FragmentIonCollector.PSM_FragmentPPMErrors] = Psm_FragmentPPMErrors;

        // Fragment Ion metrics - Peptide
        Results[FragmentIonCollector.Peptide_LongestIonSeriesBidirectionalTargets] = Peptide_Bidirectional_MedianTargets;
        Results[FragmentIonCollector.Peptide_ComplementaryIonCountTargets] = Peptide_ComplementaryCount_MedianTargets;
        Results[FragmentIonCollector.Peptide_SequenceCoverageFractionTargets] = Peptide_SequenceCoverageFraction_MedianTargets;
        Results[FragmentIonCollector.Peptide_LongestIonSeriesBidirectionalDecoys] = Peptide_Bidirectional_MedianDecoys;
        Results[FragmentIonCollector.Peptide_ComplementaryIonCountDecoys] = Peptide_ComplementaryCount_MedianDecoys;
        Results[FragmentIonCollector.Peptide_SequenceCoverageFractionDecoys] = Peptide_SequenceCoverageFraction_MedianDecoys;
        Results[FragmentIonCollector.Peptide_LongestIonSeriesBidirectional_AllTargets] = Peptide_BidirectionalTargets;
        Results[FragmentIonCollector.Peptide_ComplementaryIonCount_AllTargets] = Peptide_ComplementaryCountTargets;
        Results[FragmentIonCollector.Peptide_SequenceCoverageFraction_AllTargets] = Peptide_SequenceCoverageFractionTargets;
        Results[FragmentIonCollector.Peptide_LongestIonSeriesBidirectional_AllDecoys] = Peptide_BidirectionalDecoys;
        Results[FragmentIonCollector.Peptide_ComplementaryIonCount_AllDecoys] = Peptide_ComplementaryCountDecoys;
        Results[FragmentIonCollector.Peptide_SequenceCoverageFraction_AllDecoys] = Peptide_SequenceCoverageFractionDecoys;
        Results[FragmentIonCollector.Peptide_FragmentPPMErrors] = Peptide_FragmentPPMErrors;
        Results[FragmentIonCollector.Peptide_MedianAbsoluteFragmentPpmErrorsTargets] = Peptide_MedianAbsoluteFragmentPpmErrorsTargets;

        // Retention Time metrics
        Results[RetentionTimeCollector.PsmMeanAbsoluteRtError] = Psm_MeanAbsoluteRtError;
        Results[RetentionTimeCollector.PsmRtCorrelationCoefficient] = Psm_RtCorrelationCoefficient;
        Results[RetentionTimeCollector.PsmAllRtErrors] = Psm_AllRtErrors;
        Results[RetentionTimeCollector.PeptideMeanAbsoluteRtError] = Peptide_MeanAbsoluteRtError;
        Results[RetentionTimeCollector.PeptideRtCorrelationCoefficient] = Peptide_RtCorrelationCoefficient;
        Results[RetentionTimeCollector.PeptideAllRtErrors] = Peptide_AllRtErrors;

        // DeNovo Mapping metrics
        Results[DeNovoMappingCollector.TotalPredictions] = TotalPredictions;
        Results[DeNovoMappingCollector.TargetPeptidesMapped] = TargetPredictions;
        Results[DeNovoMappingCollector.DecoyPeptidesMapped] = DecoyPredictions;
        Results[DeNovoMappingCollector.UniquePeptidesMapped] = UniquePeptidesMapped;
        Results[DeNovoMappingCollector.UniqueProteinsMapped] = UniqueProteinsMapped;
        Results[DeNovoMappingCollector.MeanRtError] = DeNovoMeanRtError;
        Results[DeNovoMappingCollector.RetentionTimeErrors] = DeNovoRetentionTimeErrors;
        Results[DeNovoMappingCollector.MeanPredictionScore] = MeanPredictionScore;
        Results[DeNovoMappingCollector.PredictionScores] = PredictionScores;
        Results[DeNovoMappingCollector.TargetPredictionScores] = TargetPredictionScores;
        Results[DeNovoMappingCollector.DecoyPredictionScores] = DecoyPredictionScores;
        Results[DeNovoMappingCollector.HostProteinCount] = DeNovoHostProteinCount;
        Results[DeNovoMappingCollector.UnambiguousTargetPredictions] = UnambiguousTargetPredictions;
        Results[DeNovoMappingCollector.AmbiguousTargetPredictions] = AmbiguousTargetPredictions;
        Results[DeNovoMappingCollector.UnambiguousDecoyPredictions] = UnambiguousDecoyPredictions;
        Results[DeNovoMappingCollector.UniqueUnambiguousPeptidesMapped] = UniqueUnambiguousPeptidesMapped;
        Results[DeNovoMappingCollector.UniqueUnambiguousProteinsMapped] = UniqueUnambiguousProteinsMapped;
        Results[DeNovoMappingCollector.UnambiguousRetentionTimeErrors] = DeNovoUnambiguousRetentionTimeErrors;
        Results[DeNovoMappingCollector.UnambiguousTargetPredictionScores] = UnambiguousTargetPredictionScores;
        Results[DeNovoMappingCollector.UnambiguousDecoyPredictionScores] = UnambiguousDecoyPredictionScores;
        Results[DeNovoMappingCollector.PeptideRetentionTimeErrors] = DeNovoPeptideRetentionTimeErrors;
        Results[DeNovoMappingCollector.PeptidePredictionScores] = DeNovoPeptidePredictionScores;
        Results[DeNovoMappingCollector.UniqueDecoyPeptidesMapped] = UniqueDecoyPeptidesMapped;
        Results[DeNovoMappingCollector.UnambiguousPeptideRetentionTimeErrors] = DeNovoUnambiguousPeptideRetentionTimeErrors;
        Results[DeNovoMappingCollector.UnambiguousPeptidePredictionScores] = DeNovoUnambiguousPeptidePredictionScores;
        Results[DeNovoMappingCollector.UniqueUnambiguousDecoyPeptidesMapped] = UniqueUnambiguousDecoyPeptidesMapped;
    }

    /// <summary>
    /// Populates typed properties from the Results dictionary
    /// Called before CSV serialization
    /// </summary>
    public void PopulatePropertiesFromResults()
    {
        // Core metrics
        TotalProteins = GetValue<int>(BasicMetricCollector.TotalProteins);
        TransientProteinCount = GetValue<int>(BasicMetricCollector.TransientProteinCount);
        TransientPeptideCount = GetValue<int>(BasicMetricCollector.TransientPeptideCount);
        TargetPsmsAtQValueThreshold = GetValue<int>(BasicMetricCollector.TargetPsmsAtQValueThreshold);
        TargetPsmsFromTransientDb = GetValue<int>(BasicMetricCollector.TargetPsmsFromTransientDb);
        TargetPsmsFromTransientDbAtQValueThreshold = GetValue<int>(BasicMetricCollector.TargetPsmsFromTransientDbAtQValueThreshold);
        TargetPeptidesAtQValueThreshold = GetValue<int>(BasicMetricCollector.TargetPeptidesAtQValueThreshold);
        TargetPeptidesFromTransientDb = GetValue<int>(BasicMetricCollector.TargetPeptidesFromTransientDb);
        TargetPeptidesFromTransientDbAtQValueThreshold = GetValue<int>(BasicMetricCollector.TargetPeptidesFromTransientDbAtQValueThreshold);
        TargetProteinGroupsAtQValueThreshold = GetValue<int>(ProteinGroupCollector.TargetProteinGroupsAtQValueThreshold);
        TargetProteinGroupsFromTransientDb = GetValue<int>(ProteinGroupCollector.TargetProteinGroupsFromTransientDb);
        TargetProteinGroupsFromTransientDbAtQValueThreshold = GetValue<int>(ProteinGroupCollector.TargetProteinGroupsFromTransientDbAtQValueThreshold);
        DecoyProteinGroupsFromTransientDbAtQValueThreshold = GetValue<int>(ProteinGroupCollector.DecoyProteinGroupsFromTransientDbAtQValueThreshold);
        
        // Organism specificity
        PsmTargets = GetValue<int>(PsmPeptideSearchCollector.PsmTargets);
        PsmDecoys = GetValue<int>(PsmPeptideSearchCollector.PsmDecoys);
        PsmBacterialTargets = GetValue<int>(PsmPeptideSearchCollector.PsmBacterialTargets);
        PsmBacterialDecoys = GetValue<int>(PsmPeptideSearchCollector.PsmBacterialDecoys);
        PsmBacterialAmbiguous = GetValue<int>(PsmPeptideSearchCollector.PsmBacterialAmbiguous);
        PsmBacterialUnambiguousTargets = GetValue<int>(PsmPeptideSearchCollector.PsmBacterialUnambiguousTargets);
        PsmBacterialUnambiguousDecoys = GetValue<int>(PsmPeptideSearchCollector.PsmBacterialUnambiguousDecoys);
        PsmBacterialUnambiguousTargetScores = GetValue<double[]>(PsmPeptideSearchCollector.PsmBacterialUnambiguousTargetScores) ?? Array.Empty<double>();
        PsmBacterialUnambiguousDecoyScores = GetValue<double[]>(PsmPeptideSearchCollector.PsmBacterialUnambiguousDecoyScores) ?? Array.Empty<double>();
        PsmBacterialTargetDeltaScores = GetValue<double[]>(PsmPeptideSearchCollector.PsmBacteriaTargetlDeltaScores) ?? Array.Empty<double>();
        
        PeptideTargets = GetValue<int>(PsmPeptideSearchCollector.PeptideTargets);
        PeptideDecoys = GetValue<int>(PsmPeptideSearchCollector.PeptideDecoys);
        PeptideBacterialTargets = GetValue<int>(PsmPeptideSearchCollector.PeptideBacterialTargets);
        PeptideBacterialDecoys = GetValue<int>(PsmPeptideSearchCollector.PeptideBacterialDecoys);
        PeptideBacterialAmbiguous = GetValue<int>(PsmPeptideSearchCollector.PeptideBacterialAmbiguous);
        PeptideBacterialUnambiguousTargets = GetValue<int>(PsmPeptideSearchCollector.PeptideBacterialUnambiguousTargets);
        PeptideBacterialUnambiguousDecoys = GetValue<int>(PsmPeptideSearchCollector.PeptideBacterialUnambiguousDecoys);
        PeptideBacterialUnambiguousTargetScores = GetValue<double[]>(PsmPeptideSearchCollector.PeptideBacterialUnambiguousTargetScores) ?? Array.Empty<double>();
        PeptideBacterialUnambiguousDecoyScores = GetValue<double[]>(PsmPeptideSearchCollector.PeptideBacterialUnambiguousDecoyScores) ?? Array.Empty<double>();
        PeptideBacterialTargetDeltaScores = GetValue<double[]>(PsmPeptideSearchCollector.PeptideBacterialTargetDeltaScores) ?? Array.Empty<double>();

        // Decon
        PsmPrecursorDeconScores = GetValue<double[]>(PsmPeptideSearchCollector.PsmPrecusorDeconScores) ?? Array.Empty<double>();
        PsmPrecursorMassErrors = GetValue<double[]>(PsmPeptideSearchCollector.PsmPrecursorMassErrors) ?? Array.Empty<double>();
        PsmPrecursorEnvelopePeakCounts = GetValue<double[]>(PsmPeptideSearchCollector.PsmPrecursorEnvelopePeakCount) ?? Array.Empty<double>();
        PsmPrecursorFractionalIntensities = GetValue<double[]>(PsmPeptideSearchCollector.PsmPrecusorFractionalIntensity) ?? Array.Empty<double>();
        PeptidePrecursorDeconScores = GetValue<double[]>(PsmPeptideSearchCollector.PeptidePrecusorDeconScores) ?? Array.Empty<double>();
        PeptidePrecursorMassErrors = GetValue<double[]>(PsmPeptideSearchCollector.PeptidePrecursorMassErrors) ?? Array.Empty<double>();
        PeptidePrecursorEnvelopePeakCounts = GetValue<double[]>(PsmPeptideSearchCollector.PeptidePrecursorEnvelopePeakCount) ?? Array.Empty<double>();
        PeptidePrecursorFractionalIntensities = GetValue<double[]>(PsmPeptideSearchCollector.PeptidePrecusorFractionalIntensity) ?? Array.Empty<double>();

        ProteinGroupTargets = GetValue<int>(ProteinGroupCollector.ProteinGroupTargets);
        ProteinGroupDecoys = GetValue<int>(ProteinGroupCollector.ProteinGroupDecoys);
        ProteinGroupBacterialUnambiguousTargets = GetValue<int>(ProteinGroupCollector.ProteinGroupBacterialUnambiguousTargets);
        ProteinGroupBacterialUnambiguousDecoys = GetValue<int>(ProteinGroupCollector.ProteinGroupBacterialUnambiguousDecoys);
        MedianPeptidesPerProteinGroup = GetValue<double>(ProteinGroupCollector.MedianPeptidesPerProteinGroup);
        MedianUniquePeptidesPerProteinGroup = GetValue<double>(ProteinGroupCollector.MedianUniquePeptidesPerProteinGroup);
        MedianPsmsPerProteinGroup = GetValue<double>(ProteinGroupCollector.MedianPsmsPerProteinGroup);
        AllPeptidesPerProteinGroup = GetValue<double[]>(ProteinGroupCollector.AllPeptidesPerProteinGroup) ?? Array.Empty<double>();
        AllUniquePeptidesPerProteinGroup = GetValue<double[]>(ProteinGroupCollector.AllUniquePeptidesPerProteinGroup) ?? Array.Empty<double>();
        AllPsmsPerProteinGroup = GetValue<double[]>(ProteinGroupCollector.AllPsmsPerProteinGroup) ?? Array.Empty<double>();
        AllSequenceCoverageFractions = GetValue<double[]>(ProteinGroupCollector.AllSequenceCoverageFractions) ?? Array.Empty<double>();
        MedianSequenceCoverageFraction = GetValue<double>("MedianSequenceCoverageFraction", double.NaN);
        AllFragmentSequenceCoverageFractions = GetValue<double[]>(ProteinGroupCollector.AllFragmentSequenceCoverageFractions) ?? Array.Empty<double>();

        // Fragment Ion metrics - PSM
        Psm_Bidirectional_MedianTargets = GetValue<double>(FragmentIonCollector.PSM_LongestIonSeriesBidirectionalTargets);
        Psm_ComplementaryCount_MedianTargets = GetValue<double>(FragmentIonCollector.PSM_ComplementaryIonCountTargets);
        Psm_SequenceCoverageFraction_MedianTargets = GetValue<double>(FragmentIonCollector.PSM_SequenceCoverageFractionTargets);
        Psm_Bidirectional_MedianDecoys = GetValue<double>(FragmentIonCollector.PSM_LongestIonSeriesBidirectionalDecoys);
        Psm_ComplementaryCount_MedianDecoys = GetValue<double>(FragmentIonCollector.PSM_ComplementaryIonCountDecoys);
        Psm_SequenceCoverageFraction_MedianDecoys = GetValue<double>(FragmentIonCollector.PSM_SequenceCoverageFractionDecoys);
        Psm_BidirectionalTargets = GetValue<double[]>(FragmentIonCollector.PSM_LongestIonSeriesBidirectional_AllTargets) ?? Array.Empty<double>();
        Psm_ComplementaryCountTargets = GetValue<double[]>(FragmentIonCollector.PSM_ComplementaryIonCount_AllTargets) ?? Array.Empty<double>();
        Psm_SequenceCoverageFractionTargets = GetValue<double[]>(FragmentIonCollector.PSM_SequenceCoverageFraction_AllTargets) ?? Array.Empty<double>();
        Psm_BidirectionalDecoys = GetValue<double[]>(FragmentIonCollector.PSM_LongestIonSeriesBidirectional_AllDecoys) ?? Array.Empty<double>();
        Psm_ComplementaryCountDecoys = GetValue<double[]>(FragmentIonCollector.PSM_ComplementaryIonCount_AllDecoys) ?? Array.Empty<double>();
        Psm_SequenceCoverageFractionDecoys = GetValue<double[]>(FragmentIonCollector.PSM_SequenceCoverageFraction_AllDecoys) ?? Array.Empty<double>();
        Psm_FragmentPPMErrors = GetValue<double[]>(FragmentIonCollector.PSM_FragmentPPMErrors) ?? Array.Empty<double>();

        // Fragment Ion metrics - Peptide
        Peptide_Bidirectional_MedianTargets = GetValue<double>(FragmentIonCollector.Peptide_LongestIonSeriesBidirectionalTargets);
        Peptide_ComplementaryCount_MedianTargets = GetValue<double>(FragmentIonCollector.Peptide_ComplementaryIonCountTargets);
        Peptide_SequenceCoverageFraction_MedianTargets = GetValue<double>(FragmentIonCollector.Peptide_SequenceCoverageFractionTargets);
        Peptide_Bidirectional_MedianDecoys = GetValue<double>(FragmentIonCollector.Peptide_LongestIonSeriesBidirectionalDecoys);
        Peptide_ComplementaryCount_MedianDecoys = GetValue<double>(FragmentIonCollector.Peptide_ComplementaryIonCountDecoys);
        Peptide_SequenceCoverageFraction_MedianDecoys = GetValue<double>(FragmentIonCollector.Peptide_SequenceCoverageFractionDecoys);
        Peptide_BidirectionalTargets = GetValue<double[]>(FragmentIonCollector.Peptide_LongestIonSeriesBidirectional_AllTargets) ?? Array.Empty<double>();
        Peptide_ComplementaryCountTargets = GetValue<double[]>(FragmentIonCollector.Peptide_ComplementaryIonCount_AllTargets) ?? Array.Empty<double>();
        Peptide_SequenceCoverageFractionTargets = GetValue<double[]>(FragmentIonCollector.Peptide_SequenceCoverageFraction_AllTargets) ?? Array.Empty<double>();
        Peptide_BidirectionalDecoys = GetValue<double[]>(FragmentIonCollector.Peptide_LongestIonSeriesBidirectional_AllDecoys) ?? Array.Empty<double>();
        Peptide_ComplementaryCountDecoys = GetValue<double[]>(FragmentIonCollector.Peptide_ComplementaryIonCount_AllDecoys) ?? Array.Empty<double>();
        Peptide_SequenceCoverageFractionDecoys = GetValue<double[]>(FragmentIonCollector.Peptide_SequenceCoverageFraction_AllDecoys) ?? Array.Empty<double>();
        Peptide_FragmentPPMErrors = GetValue<double[]>(FragmentIonCollector.Peptide_FragmentPPMErrors) ?? Array.Empty<double>();
        Peptide_MedianAbsoluteFragmentPpmErrorsTargets =
            GetValue<double[]>(FragmentIonCollector.Peptide_MedianAbsoluteFragmentPpmErrorsTargets) ?? Array.Empty<double>();

        // Retention Time metrics
        Psm_MeanAbsoluteRtError = GetValue(RetentionTimeCollector.PsmMeanAbsoluteRtError, double.NaN);
        Psm_RtCorrelationCoefficient = GetValue(RetentionTimeCollector.PsmRtCorrelationCoefficient, double.NaN);
        Psm_AllRtErrors = GetValue<double[]>(RetentionTimeCollector.PsmAllRtErrors) ?? Array.Empty<double>();
        Peptide_MeanAbsoluteRtError = GetValue(RetentionTimeCollector.PeptideMeanAbsoluteRtError, double.NaN);
        Peptide_RtCorrelationCoefficient = GetValue(RetentionTimeCollector.PeptideRtCorrelationCoefficient, double.NaN);
        Peptide_AllRtErrors = GetValue<double[]>(RetentionTimeCollector.PeptideAllRtErrors) ?? Array.Empty<double>();

        // Denovo Mapping metrics
        TotalPredictions = GetValue<int>(DeNovoMappingCollector.TotalPredictions);
        TargetPredictions = GetValue<int>(DeNovoMappingCollector.TargetPeptidesMapped);
        DecoyPredictions = GetValue<int>(DeNovoMappingCollector.DecoyPeptidesMapped);
        UniquePeptidesMapped = GetValue<int>(DeNovoMappingCollector.UniquePeptidesMapped);
        UniqueProteinsMapped = GetValue<int>(DeNovoMappingCollector.UniqueProteinsMapped);
        DeNovoMeanRtError = GetValue<double>(DeNovoMappingCollector.MeanRtError);
        DeNovoRetentionTimeErrors = GetValue<double[]>(DeNovoMappingCollector.RetentionTimeErrors) ?? Array.Empty<double>();
        MeanPredictionScore = GetValue<double>(DeNovoMappingCollector.MeanPredictionScore);
        PredictionScores = GetValue<double[]>(DeNovoMappingCollector.PredictionScores) ?? Array.Empty<double>();
        TargetPredictionScores = GetValue<double[]>(DeNovoMappingCollector.TargetPredictionScores) ?? Array.Empty<double>();
        DecoyPredictionScores = GetValue<double[]>(DeNovoMappingCollector.DecoyPredictionScores) ?? Array.Empty<double>();
        DeNovoHostProteinCount = GetValue(DeNovoMappingCollector.HostProteinCount, -1);
        UnambiguousTargetPredictions = GetValue<int>(DeNovoMappingCollector.UnambiguousTargetPredictions);
        AmbiguousTargetPredictions = GetValue<int>(DeNovoMappingCollector.AmbiguousTargetPredictions);
        UnambiguousDecoyPredictions = GetValue<int>(DeNovoMappingCollector.UnambiguousDecoyPredictions);
        UniqueUnambiguousPeptidesMapped = GetValue<int>(DeNovoMappingCollector.UniqueUnambiguousPeptidesMapped);
        UniqueUnambiguousProteinsMapped = GetValue<int>(DeNovoMappingCollector.UniqueUnambiguousProteinsMapped);
        DeNovoUnambiguousRetentionTimeErrors = GetValue<double[]>(DeNovoMappingCollector.UnambiguousRetentionTimeErrors) ?? Array.Empty<double>();
        UnambiguousTargetPredictionScores = GetValue<double[]>(DeNovoMappingCollector.UnambiguousTargetPredictionScores) ?? Array.Empty<double>();
        UnambiguousDecoyPredictionScores = GetValue<double[]>(DeNovoMappingCollector.UnambiguousDecoyPredictionScores) ?? Array.Empty<double>();
        DeNovoPeptideRetentionTimeErrors = GetValue<double[]>(DeNovoMappingCollector.PeptideRetentionTimeErrors) ?? Array.Empty<double>();
        DeNovoPeptidePredictionScores = GetValue<double[]>(DeNovoMappingCollector.PeptidePredictionScores) ?? Array.Empty<double>();
        UniqueDecoyPeptidesMapped = GetValue(DeNovoMappingCollector.UniqueDecoyPeptidesMapped, -1);
        DeNovoUnambiguousPeptideRetentionTimeErrors =
            GetValue<double[]>(DeNovoMappingCollector.UnambiguousPeptideRetentionTimeErrors) ?? Array.Empty<double>();
        DeNovoUnambiguousPeptidePredictionScores =
            GetValue<double[]>(DeNovoMappingCollector.UnambiguousPeptidePredictionScores) ?? Array.Empty<double>();
        UniqueUnambiguousDecoyPeptidesMapped = GetValue(DeNovoMappingCollector.UniqueUnambiguousDecoyPeptidesMapped, -1);
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
    public async Task WriteToTextFileAsync(string filePath, ConfidenceCutoff psmCutoff, ConfidenceCutoff peptideCutoff,
        double proteinGroupQValueThreshold, bool doParsimony)
    {
        await using StreamWriter file = new StreamWriter(filePath);
        await file.WriteLineAsync($"Database: {DatabaseName}");
        await file.WriteLineAsync($"Total proteins in combined database: {TotalProteins}");
        await file.WriteLineAsync($"Total proteins from transient database: {TransientProteinCount}");
        await file.WriteLineAsync($"Total peptides from transient database: {TransientPeptideCount}");
        await file.WriteLineAsync();
        
        await file.WriteLineAsync($"Target PSMs with {psmCutoff.Label}: {TargetPsmsAtQValueThreshold}");
        await file.WriteLineAsync($"Target PSMs from transient database: {TargetPsmsFromTransientDb}");
        await file.WriteLineAsync($"Target PSMs from transient database with {psmCutoff.Label}: {TargetPsmsFromTransientDbAtQValueThreshold}");
        await file.WriteLineAsync($"PSM Bacterial Targets: {PsmBacterialTargets}");
        await file.WriteLineAsync($"PSM Bacterial Unambiguous Targets: {PsmBacterialUnambiguousTargets}");
        await file.WriteLineAsync();
        
        await file.WriteLineAsync($"Target peptides with {peptideCutoff.Label}: {TargetPeptidesAtQValueThreshold}");
        await file.WriteLineAsync($"Target peptides from transient database: {TargetPeptidesFromTransientDb}");
        await file.WriteLineAsync($"Target peptides from transient database with {peptideCutoff.Label}: {TargetPeptidesFromTransientDbAtQValueThreshold}");
        await file.WriteLineAsync($"Peptide Bacterial Targets: {PeptideBacterialTargets}");
        await file.WriteLineAsync($"Peptide Bacterial Unambiguous Targets: {PeptideBacterialUnambiguousTargets}");

        if (doParsimony)
        {
            string proteinGroupLabel = $"q-value <= {Math.Round(proteinGroupQValueThreshold, 2)}";
            await file.WriteLineAsync();
            await file.WriteLineAsync($"Target protein groups with {proteinGroupLabel}: {TargetProteinGroupsAtQValueThreshold}");
            await file.WriteLineAsync($"Target protein groups with transient database proteins: {TargetProteinGroupsFromTransientDb}");
            await file.WriteLineAsync($"Target protein groups with transient database proteins with {proteinGroupLabel}: {TargetProteinGroupsFromTransientDbAtQValueThreshold}");
            await file.WriteLineAsync($"Decoy protein groups with transient database proteins with {proteinGroupLabel}: {DecoyProteinGroupsFromTransientDbAtQValueThreshold}");
            await file.WriteLineAsync($"Protein Group Bacterial Unambiguous Targets: {ProteinGroupBacterialUnambiguousTargets}");
        }
    }

    public bool Equals(TransientDatabaseMetrics? other)
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

    public TransientDatabaseMetrics Add(TransientDatabaseMetrics other)
    {
        if (other == null) return this;
        if (this.DatabaseName != other.DatabaseName) throw new MetaMorpheusException("Cannot add metrics from different databases", new ArgumentException("Database names do not match"));

        var metrics = new TransientDatabaseMetrics(this.DatabaseName) { TwinOf = this.TwinOf };
        foreach (var key in Results.Keys)
        {
            if (other.Results.ContainsKey(key))
            {
                var value1 = Results[key];
                var value2 = other.Results[key];
                if (value1 is int int1 && value2 is int int2)
                {
                    metrics.Results[key] = int1 + int2;
                }
                else if (value1 is double double1 && value2 is double double2)
                {
                    metrics.Results[key] = double1 + double2;
                }
                else if (value1 is double[] arr1 && value2 is double[] arr2)
                {
                    metrics.Results[key] = arr1.Concat(arr2).ToArray();
                }
                else
                {
                    throw new MetaMorpheusException("Must add new if case to support new type for addition", new ArgumentException($"Type {value1.GetType()} is not supported for addition"));
                }
            }
            else
            {
                metrics.Results[key] = Results[key];
            }
        }


        return metrics;
    }
}
