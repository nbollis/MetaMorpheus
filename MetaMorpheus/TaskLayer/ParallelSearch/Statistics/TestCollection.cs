using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Serves as a marker class for test collection related utilities
/// </summary>
public static class TestCollection
{
    public static int MinValuesThreshold { get; set; } = 1;
    public static int DistributionMinValuesThreshold { get; set; } = 2;
    public static int IterationsForPermutationTests { get; set; } = 1000;

    public static List<IStatisticalTest> BaseTests { get; } =
    [
        // Raw Match Count Based Enrichment Tests
        new GaussianTest<double>("PSM-All",
            StatisticalEvidenceFamily.CountEnrichment,
            r => r.PsmBacterialUnambiguousTargets / (double)r.TransientPeptideCount),
        new GaussianTest<double>("Peptide-All",
            StatisticalEvidenceFamily.CountEnrichment,
            r => r.PeptideBacterialUnambiguousTargets / (double)r.TransientPeptideCount),

        new NegativeBinomialTest<int>("PSM-All", 
            StatisticalEvidenceFamily.CountEnrichment,
            r => r.PsmBacterialUnambiguousTargets),
        new NegativeBinomialTest<int>("Peptide", 
            StatisticalEvidenceFamily.CountEnrichment,
            r => r.PeptideBacterialUnambiguousTargets),

        //new PermutationTest<double>("PSM-All",
        //    r => r.PsmBacterialUnambiguousTargets / (double)r.TransientPeptideCount,
        //    IterationsForPermutationTests),
        //new PermutationTest<double>("Peptide-All",
        //    r => r.PeptideBacterialUnambiguousTargets / (double)r.TransientPeptideCount,
        //    IterationsForPermutationTests),

        // 1% FDR Enrichment Tests
        new GaussianTest<double>("PSM-Confident",
            StatisticalEvidenceFamily.CountEnrichment,
            r => r.TargetPsmsFromTransientDbAtQValueThreshold / (double)r.TransientPeptideCount),
        new GaussianTest<double>("Peptide-Confident",
            StatisticalEvidenceFamily.CountEnrichment,
            r => r.TargetPeptidesFromTransientDbAtQValueThreshold / (double)r.TransientPeptideCount),

        new NegativeBinomialTest<int>("PSM-Confident",
            StatisticalEvidenceFamily.CountEnrichment,
            r => r.TargetPsmsFromTransientDbAtQValueThreshold),
        new NegativeBinomialTest<int>("Peptide-Confident",
            StatisticalEvidenceFamily.CountEnrichment,
            r => r.TargetPeptidesFromTransientDbAtQValueThreshold),

        //new PermutationTest<double>("PSM-Confident",
        //    r => r.TargetPsmsFromTransientDbAtQValueThreshold / (double)r.TransientPeptideCount,
        //    IterationsForPermutationTests),
        //new PermutationTest<double>("Peptide-Confident",
        //    r => r.TargetPeptidesFromTransientDbAtQValueThreshold / (double)r.TransientPeptideCount,
        //    IterationsForPermutationTests),

        // Enrichment tests based on unambiguous vs ambiguous evidence
        new FisherExactTest("PSM",
            StatisticalEvidenceFamily.AmbiguityOrTargetDecoy,
            r => r.PsmBacterialUnambiguousTargets,
            r => r.PsmBacterialAmbiguous),
        new FisherExactTest("Peptide",
            StatisticalEvidenceFamily.AmbiguityOrTargetDecoy,
            r => r.PeptideBacterialUnambiguousTargets,
            r => r.PeptideBacterialAmbiguous),


        // Enrichment tests based upon target vs decoy matches. 
        new FisherExactTest("PSM-TD",
            StatisticalEvidenceFamily.AmbiguityOrTargetDecoy,
            r => r.PsmBacterialTargets,
            r => r.PsmBacterialDecoys),
        new FisherExactTest("Peptide-TD",
            StatisticalEvidenceFamily.AmbiguityOrTargetDecoy,
            r => r.PeptideBacterialTargets,
            r => r.PeptideBacterialDecoys),
    ];

    public static List<IStatisticalTest> ScoreDistributionTest { get; } =
    [
        new KolmogorovSmirnovTest("PSMScoreDistribution",
            StatisticalEvidenceFamily.ScoreDistribution,
            r => r.PsmBacterialUnambiguousTargetScores,
            r => r.PsmBacterialUnambiguousTargetScores.Length >= DistributionMinValuesThreshold),
        new KolmogorovSmirnovTest("PeptideScoreDistribution",
            StatisticalEvidenceFamily.ScoreDistribution,
            r => r.PeptideBacterialUnambiguousTargetScores,
            r => r.PeptideBacterialUnambiguousTargetScores.Length >= DistributionMinValuesThreshold),
    ];

    public static List<IStatisticalTest> ProteinGroupTests { get; } =
    [
        new GaussianTest<double>("ProteinGroup",
            StatisticalEvidenceFamily.ProteinGroup,
            r => r.TargetProteinGroupsFromTransientDbAtQValueThreshold / (double)r.TransientProteinCount),

        new NegativeBinomialTest<int>("ProteinGroup", 
            StatisticalEvidenceFamily.ProteinGroup,
            r => r.ProteinGroupBacterialUnambiguousTargets),

        //new PermutationTest<double>("ProteinGroup",
        //    r => r.ProteinGroupBacterialUnambiguousTargets / (double)r.TransientProteinCount,
        //    IterationsForPermutationTests)
    ];

    public static List<IStatisticalTest> RetentionTimeTests { get; } =
    [
        new GaussianTest<double>("PSM-MeanAbsoluteRtError",
            StatisticalEvidenceFamily.RetentionTime,
            r => r.Psm_MeanAbsoluteRtError,
            r => r.TargetPsmsFromTransientDbAtQValueThreshold >= MinValuesThreshold,
            isLowerTailTest: true),
        new GaussianTest<double>("Peptide-MeanAbsoluteRtError",
            StatisticalEvidenceFamily.RetentionTime,
            r => r.Peptide_MeanAbsoluteRtError,
            r => r.TargetPeptidesFromTransientDbAtQValueThreshold >= MinValuesThreshold,
            isLowerTailTest: true),

        new KolmogorovSmirnovTest("PSM-RtErrors",
            StatisticalEvidenceFamily.RetentionTime,
            r => r.Psm_AllRtErrors,
            r => r.Psm_AllRtErrors.Length >= DistributionMinValuesThreshold,
            KSAlternative.Greater),
        new KolmogorovSmirnovTest("Peptide-RtErrors",
            StatisticalEvidenceFamily.RetentionTime,
            r => r.Peptide_AllRtErrors,
            r => r.Peptide_AllRtErrors.Length >= DistributionMinValuesThreshold,
            KSAlternative.Greater),
    ];

    public static List<IStatisticalTest> FragmentationTests { get; } =
    [
        // Fragmentation Tests - Distribution Based     
        new KolmogorovSmirnovTest("PSM-Complementary",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Psm_ComplementaryCountTargets,
            r => r.Psm_ComplementaryCountTargets.Length >= DistributionMinValuesThreshold),
        new KolmogorovSmirnovTest("PSM-Bidirectional",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Psm_BidirectionalTargets,
            r => r.Psm_BidirectionalTargets.Length >= DistributionMinValuesThreshold),
        new KolmogorovSmirnovTest("PSM-SequenceCoverage",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Psm_SequenceCoverageFractionTargets,
            r => r.Psm_SequenceCoverageFractionTargets.Length >= DistributionMinValuesThreshold),
        new KolmogorovSmirnovTest("Peptide-Complementary",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_ComplementaryCountTargets,
            r => r.Peptide_ComplementaryCountTargets.Length >= DistributionMinValuesThreshold),
        new KolmogorovSmirnovTest("Peptide-Bidirectional",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_BidirectionalTargets,
            r => r.Peptide_BidirectionalTargets.Length >= DistributionMinValuesThreshold),
        new KolmogorovSmirnovTest("Peptide-SequenceCoverage",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_SequenceCoverageFractionTargets,
            r => r.Peptide_SequenceCoverageFractionTargets.Length >= DistributionMinValuesThreshold),

        // Fragmentation Tests - Median Based          
        new GaussianTest<double>("PSM-Complementary", 
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Psm_ComplementaryCount_MedianTargets,
            isDefinedFor: r => r.TargetPsmsFromTransientDbAtQValueThreshold >= MinValuesThreshold),
        new GaussianTest<double>("PSM-Bidirectional", 
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Psm_Bidirectional_MedianTargets,
            isDefinedFor: r => r.TargetPsmsFromTransientDbAtQValueThreshold >= MinValuesThreshold),
        new GaussianTest<double>("PSM-SequenceCoverage", 
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Psm_SequenceCoverageFraction_MedianTargets,
            isDefinedFor: r => r.TargetPsmsFromTransientDbAtQValueThreshold >= MinValuesThreshold),
        new GaussianTest<double>("Peptide-Complementary", 
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_ComplementaryCount_MedianTargets,
            isDefinedFor: r => r.TargetPeptidesFromTransientDbAtQValueThreshold >= MinValuesThreshold),
        new GaussianTest<double>("Peptide-Bidirectional", 
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_Bidirectional_MedianTargets,
            isDefinedFor: r => r.TargetPeptidesFromTransientDbAtQValueThreshold >= MinValuesThreshold),
        new GaussianTest<double>("Peptide-SequenceCoverage", 
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_SequenceCoverageFraction_MedianTargets,
            isDefinedFor: r => r.TargetPeptidesFromTransientDbAtQValueThreshold >= MinValuesThreshold),

        //new PermutationTest<double>("PSM-Complementary",
        //    r => r.Psm_ComplementaryCount_MedianTargets,
        //    IterationsForPermutationTests,
        //    isDefinedFor: r => r.TargetPsmsFromTransientDbAtQValueThreshold >= MinValuesThreshold),
        //new PermutationTest<double>("PSM-Bidirectional",
        //    r => r.Psm_Bidirectional_MedianTargets,
        //    IterationsForPermutationTests,
        //    isDefinedFor: r => r.TargetPsmsFromTransientDbAtQValueThreshold >= MinValuesThreshold),
        //new PermutationTest<double>("PSM-SequenceCoverage",
        //    r => r.Psm_SequenceCoverageFraction_MedianTargets,
        //    IterationsForPermutationTests,
        //    isDefinedFor: r => r.TargetPsmsFromTransientDbAtQValueThreshold >= MinValuesThreshold),
        //new PermutationTest<double>("Peptide-Complementary",
        //    r => r.Peptide_ComplementaryCount_MedianTargets,
        //    IterationsForPermutationTests,
        //    isDefinedFor: r => r.TargetPeptidesFromTransientDbAtQValueThreshold >= MinValuesThreshold),
        //new PermutationTest<double>("Peptide-Bidirectional",
        //    r => r.Peptide_Bidirectional_MedianTargets,
        //    IterationsForPermutationTests,
        //    isDefinedFor: r => r.TargetPeptidesFromTransientDbAtQValueThreshold >= MinValuesThreshold),
        //new PermutationTest<double>("Peptide-SequenceCoverage",
        //    r => r.Peptide_SequenceCoverageFraction_MedianTargets,
        //    IterationsForPermutationTests,
        //    isDefinedFor: r => r.TargetPeptidesFromTransientDbAtQValueThreshold >= MinValuesThreshold)
    ];

    public static List<IStatisticalTest> DeNovoTests { get; } = 
    [
        // Prediction Counts
        new NegativeBinomialTest<int>("DeNovo-Predictions",
            StatisticalEvidenceFamily.DeNovo,
            r => r.TotalPredictions,
            r => r.TotalPredictions >= MinValuesThreshold),
        new GaussianTest<double>("DeNovo-Predictions",
            StatisticalEvidenceFamily.DeNovo,
            r => r.TotalPredictions / (double)r.TransientPeptideCount,
            r => r.TotalPredictions >= MinValuesThreshold),
        //new PermutationTest<double>("DeNovo-Predictions",
        //    r => r.TotalPredictions / (double)r.TransientPeptideCount,
        //    IterationsForPermutationTests,
        //    r => r.TotalPredictions < MinValuesThreshold),

        new NegativeBinomialTest<int>("DeNovo-Targets",
            StatisticalEvidenceFamily.DeNovo,
            r => r.TargetPredictions,
            r => r.TotalPredictions >= MinValuesThreshold),
        new GaussianTest<double>("DeNovo-Targets",
            StatisticalEvidenceFamily.DeNovo,
            r => r.TargetPredictions / (double)r.TransientPeptideCount,
            r => r.TotalPredictions >= MinValuesThreshold),
        //new PermutationTest<double>("DeNovo-Targets",
        //    r => r.TargetPredictions / (double)r.TransientPeptideCount,
        //    IterationsForPermutationTests,
        //    r => r.TotalPredictions < MinValuesThreshold),

        // Mapping Counts 
        new NegativeBinomialTest<int>("DeNovo-MappedPeptides",
            StatisticalEvidenceFamily.DeNovo,
            r => r.UniquePeptidesMapped,
            r => r.TotalPredictions >= MinValuesThreshold),
        new GaussianTest<double>("DeNovo-MappedPeptides",
            StatisticalEvidenceFamily.DeNovo,
            r => r.UniquePeptidesMapped / (double)r.TransientPeptideCount,
            r => r.TotalPredictions >= MinValuesThreshold),
        //new PermutationTest<double>("DeNovo-MappedPeptides",
        //    r => r.UniquePeptidesMapped / (double)r.TransientPeptideCount,
        //    IterationsForPermutationTests,
        //    r => r.TotalPredictions < MinValuesThreshold),

        new NegativeBinomialTest<int>("DeNovo-MappedProteins",
            StatisticalEvidenceFamily.DeNovo,
            r => r.UniqueProteinsMapped, 
            r => r.TotalPredictions >= MinValuesThreshold),
        new GaussianTest<double>("DeNovo-MappedProteins",
            StatisticalEvidenceFamily.DeNovo,
            r => r.UniqueProteinsMapped / (double)r.TransientProteinCount,
            r => r.TotalPredictions >= MinValuesThreshold),
        //new PermutationTest<double>("DeNovo-MappedProteins",
        //    r => r.UniqueProteinsMapped / (double)r.TransientProteinCount, 
        //    IterationsForPermutationTests,
        //    r => r.TotalPredictions < MinValuesThreshold),

        // Prediction Retention Time Accuracy
        new GaussianTest<double>("DeNovo-MeanAbsoluteRtError",
            StatisticalEvidenceFamily.DeNovo,
            r => r.RetentionTimeErrors.Select(Math.Abs).Average(),
            r => r.TargetPredictions >= MinValuesThreshold,
            isLowerTailTest: true),
        new KolmogorovSmirnovTest("DeNovo-RtErrors",
            StatisticalEvidenceFamily.DeNovo,
            r => r.RetentionTimeErrors,
            r => r.RetentionTimeErrors.Length >= DistributionMinValuesThreshold,
            KSAlternative.Greater),

        // Score Tests
        new GaussianTest<double>("DeNovo-Score",
            StatisticalEvidenceFamily.DeNovo,
            r => r.MeanPredictionScore,
            r => r.TargetPredictions >= MinValuesThreshold,
            isLowerTailTest: false),
        //new PermutationTest<double>("DeNovo-Score",
        //    r => r.MeanPredictionScore,
        //    IterationsForPermutationTests,
        //    isDefinedFor: r => r.TargetPredictions >= MinValuesThreshold),
        new KolmogorovSmirnovTest("DeNovo-Scores",
            StatisticalEvidenceFamily.DeNovo,
            r => r.PredictionScores,
            r => r.PredictionScores.Length >= DistributionMinValuesThreshold),
    ];

}
