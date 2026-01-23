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
            r => r.PsmBacterialUnambiguousTargets / (double)r.TransientPeptideCount),
        new GaussianTest<double>("Peptide-All",
            r => r.PeptideBacterialUnambiguousTargets / (double)r.TransientPeptideCount),

        new NegativeBinomialTest<int>("PSM-All", 
            r => r.PsmBacterialUnambiguousTargets),
        new NegativeBinomialTest<int>("Peptide", 
            r => r.PeptideBacterialUnambiguousTargets),

        new PermutationTest<double>("PSM-All",
            r => r.PsmBacterialUnambiguousTargets / (double)r.TransientPeptideCount,
            IterationsForPermutationTests),
        new PermutationTest<double>("Peptide-All",
            r => r.PeptideBacterialUnambiguousTargets / (double)r.TransientPeptideCount,
            IterationsForPermutationTests),

        // 1% FDR Enrichment Tests
        new GaussianTest<double>("PSM-Confident",
            r => r.TargetPsmsFromTransientDbAtQValueThreshold / (double)r.TransientPeptideCount),
        new GaussianTest<double>("Peptide-Confident",
            r => r.TargetPeptidesFromTransientDbAtQValueThreshold / (double)r.TransientPeptideCount),

        new NegativeBinomialTest<int>("PSM-Confident",
            r => r.TargetPsmsFromTransientDbAtQValueThreshold),
        new NegativeBinomialTest<int>("Peptide-Confident",
            r => r.TargetPeptidesFromTransientDbAtQValueThreshold),

        new PermutationTest<double>("PSM-Confident",
            r => r.TargetPsmsFromTransientDbAtQValueThreshold / (double)r.TransientPeptideCount,
            IterationsForPermutationTests),
        new PermutationTest<double>("Peptide-Confident",
            r => r.TargetPeptidesFromTransientDbAtQValueThreshold / (double)r.TransientPeptideCount,
            IterationsForPermutationTests),

        // Enrichment tests based on unambiguous vs ambiguous evidence
        new FisherExactTest("PSM",
            r => r.PsmBacterialUnambiguousTargets,
            r => r.PsmBacterialAmbiguous),
        new FisherExactTest("Peptide",
            r => r.PeptideBacterialUnambiguousTargets,
            r => r.PeptideBacterialAmbiguous),


        // Enrichment tests based upon target vs decoy matches. 
        new FisherExactTest("PSM-TD",
            r => r.PsmBacterialTargets,
            r => r.PsmBacterialDecoys),
        new FisherExactTest("Peptide-TD",
            r => r.PeptideBacterialTargets,
            r => r.PeptideBacterialDecoys),
    ];

    public static List<IStatisticalTest> ScoreDistributionTest { get; } =
    [
        new KolmogorovSmirnovTest("PSMScoreDistribution",
            r => r.PsmBacterialUnambiguousTargetScores,
            r => r.PsmBacterialUnambiguousTargetScores.Length < DistributionMinValuesThreshold),
        new KolmogorovSmirnovTest("PeptideScoreDistribution",
            r => r.PeptideBacterialUnambiguousTargetScores,
            r => r.PeptideBacterialUnambiguousTargetScores.Length < DistributionMinValuesThreshold),
    ];

    public static List<IStatisticalTest> ProteinGroupTests { get; } =
    [
        new GaussianTest<double>("ProteinGroup",
            r => r.TargetProteinGroupsFromTransientDbAtQValueThreshold / (double)r.TransientProteinCount),

        new NegativeBinomialTest<int>("ProteinGroup", 
            r => r.ProteinGroupBacterialUnambiguousTargets),

        new PermutationTest<double>("ProteinGroup",
            r => r.ProteinGroupBacterialUnambiguousTargets / (double)r.TransientProteinCount,
            IterationsForPermutationTests)
    ];

    public static List<IStatisticalTest> RetentionTimeTests { get; } =
    [
        new GaussianTest<double>("PSM-MeanAbsoluteRtError",
            r => r.Psm_MeanAbsoluteRtError,
            r => r.TargetPsmsFromTransientDbAtQValueThreshold < MinValuesThreshold,
            isLowerTailTest: true),
        new GaussianTest<double>("Peptide-MeanAbsoluteRtError",
            r => r.Peptide_MeanAbsoluteRtError,
            r => r.TargetPeptidesFromTransientDbAtQValueThreshold < MinValuesThreshold,
            isLowerTailTest: true),

        new KolmogorovSmirnovTest("PSM-RtErrors",
            r => r.Psm_AllRtErrors,
            r => r.Psm_AllRtErrors.Length < DistributionMinValuesThreshold,
            KSAlternative.Greater),
        new KolmogorovSmirnovTest("Peptide-RtErrors",
            r => r.Peptide_AllRtErrors,
            r => r.Peptide_AllRtErrors.Length < DistributionMinValuesThreshold,
            KSAlternative.Greater),
    ];

    public static List<IStatisticalTest> FragmentationTests { get; } =
    [
        // Fragmentation Tests - Distribution Based     
        new KolmogorovSmirnovTest("PSM-Complementary",
            r => r.Psm_ComplementaryCountTargets,
            r => r.Psm_ComplementaryCountTargets.Length < DistributionMinValuesThreshold),
        new KolmogorovSmirnovTest("PSM-Bidirectional",
            r => r.Psm_BidirectionalTargets,
            r => r.Psm_BidirectionalTargets.Length < DistributionMinValuesThreshold),
        new KolmogorovSmirnovTest("PSM-SequenceCoverage",
            r => r.Psm_SequenceCoverageFractionTargets,
            r => r.Psm_SequenceCoverageFractionTargets.Length < DistributionMinValuesThreshold),
        new KolmogorovSmirnovTest("Peptide-Complementary",
            r => r.Peptide_ComplementaryCountTargets,
            r => r.Peptide_ComplementaryCountTargets.Length < DistributionMinValuesThreshold),
        new KolmogorovSmirnovTest("Peptide-Bidirectional",
            r => r.Peptide_BidirectionalTargets,
            r => r.Peptide_BidirectionalTargets.Length < DistributionMinValuesThreshold),
        new KolmogorovSmirnovTest("Peptide-SequenceCoverage",
            r => r.Peptide_SequenceCoverageFractionTargets,
            r => r.Peptide_SequenceCoverageFractionTargets.Length < DistributionMinValuesThreshold),

        // Fragmentation Tests - Median Based          
        new GaussianTest<double>("PSM-Complementary", 
            r => r.Psm_ComplementaryCount_MedianTargets,
            shouldSkip: r => r.TargetPsmsFromTransientDbAtQValueThreshold < MinValuesThreshold),
        new GaussianTest<double>("PSM-Bidirectional", 
            r => r.Psm_Bidirectional_MedianTargets,
            shouldSkip: r => r.TargetPsmsFromTransientDbAtQValueThreshold < MinValuesThreshold),
        new GaussianTest<double>("PSM-SequenceCoverage", 
            r => r.Psm_SequenceCoverageFraction_MedianTargets,
            shouldSkip: r => r.TargetPsmsFromTransientDbAtQValueThreshold < MinValuesThreshold),
        new GaussianTest<double>("Peptide-Complementary", 
            r => r.Peptide_ComplementaryCount_MedianTargets,
            shouldSkip: r => r.TargetPeptidesFromTransientDbAtQValueThreshold < MinValuesThreshold),
        new GaussianTest<double>("Peptide-Bidirectional", 
            r => r.Peptide_Bidirectional_MedianTargets,
            shouldSkip: r => r.TargetPeptidesFromTransientDbAtQValueThreshold < MinValuesThreshold),
        new GaussianTest<double>("Peptide-SequenceCoverage", 
            r => r.Peptide_SequenceCoverageFraction_MedianTargets,
            shouldSkip: r => r.TargetPeptidesFromTransientDbAtQValueThreshold < MinValuesThreshold),

        new PermutationTest<double>("PSM-Complementary",
            r => r.Psm_ComplementaryCount_MedianTargets,
            IterationsForPermutationTests,
            shouldSkip: r => r.TargetPsmsFromTransientDbAtQValueThreshold < MinValuesThreshold),
        new PermutationTest<double>("PSM-Bidirectional",
            r => r.Psm_Bidirectional_MedianTargets,
            IterationsForPermutationTests,
            shouldSkip: r => r.TargetPsmsFromTransientDbAtQValueThreshold < MinValuesThreshold),
        new PermutationTest<double>("PSM-SequenceCoverage",
            r => r.Psm_SequenceCoverageFraction_MedianTargets,
            IterationsForPermutationTests,
            shouldSkip: r => r.TargetPsmsFromTransientDbAtQValueThreshold < MinValuesThreshold),
        new PermutationTest<double>("Peptide-Complementary",
            r => r.Peptide_ComplementaryCount_MedianTargets,
            IterationsForPermutationTests,
            shouldSkip: r => r.TargetPeptidesFromTransientDbAtQValueThreshold < MinValuesThreshold),
        new PermutationTest<double>("Peptide-Bidirectional",
            r => r.Peptide_Bidirectional_MedianTargets,
            IterationsForPermutationTests,
            shouldSkip: r => r.TargetPeptidesFromTransientDbAtQValueThreshold < MinValuesThreshold),
        new PermutationTest<double>("Peptide-SequenceCoverage",
            r => r.Peptide_SequenceCoverageFraction_MedianTargets,
            IterationsForPermutationTests,
            shouldSkip: r => r.TargetPeptidesFromTransientDbAtQValueThreshold < MinValuesThreshold)
    ];

    public static List<IStatisticalTest> DeNovoTests { get; } = 
    [
        // Prediction Counts
        new NegativeBinomialTest<int>("DeNovo-Predictions",
            r => r.TotalPredictions),
        new GaussianTest<double>("DeNovo-Predictions",
            r => r.TotalPredictions / (double)r.TransientPeptideCount),
        new PermutationTest<double>("DeNovo-Predictions",
            r => r.TotalPredictions / (double)r.TransientPeptideCount,
            IterationsForPermutationTests),

        new NegativeBinomialTest<int>("DeNovo-Targets",
            r => r.TargetPredictions),
        new GaussianTest<double>("DeNovo-Targets",
            r => r.TargetPredictions / (double)r.TransientPeptideCount),
        new PermutationTest<double>("DeNovo-Targets",
            r => r.TargetPredictions / (double)r.TransientPeptideCount,
            IterationsForPermutationTests),

        // Mapping Counts 
        new NegativeBinomialTest<int>("DeNovo-MappedPeptides",
            r => r.UniquePeptidesMapped),
        new GaussianTest<double>("DeNovo-MappedPeptides",
            r => r.UniquePeptidesMapped / (double)r.TransientPeptideCount),
        new PermutationTest<double>("DeNovo-MappedPeptides",
            r => r.UniquePeptidesMapped / (double)r.TransientPeptideCount,
            IterationsForPermutationTests),

        new NegativeBinomialTest<int>("DeNovo-MappedProteins",
            r => r.UniqueProteinsMapped),
        new GaussianTest<double>("DeNovo-MappedProteins",
            r => r.UniqueProteinsMapped / (double)r.TransientProteinCount),
        new PermutationTest<double>("DeNovo-MappedProteins",
            r => r.UniqueProteinsMapped / (double)r.TransientProteinCount,
            IterationsForPermutationTests),

        // Prediction Retention Time Accuracy
        new GaussianTest<double>("DeNovo-MeanAbsoluteRtError",
            r => r.MeanRtError,
            r => r.TargetPredictions < MinValuesThreshold,
            isLowerTailTest: true),
        new KolmogorovSmirnovTest("DeNovo-RtErrors",
            r => r.RetentionTimeErrors,
            r => r.RetentionTimeErrors.Length < DistributionMinValuesThreshold,
            KSAlternative.Greater),

        // Score Tests
        new GaussianTest<double>("DeNovo-Score",
            r => r.MeanPredictionScore,
            r => r.TargetPredictions < MinValuesThreshold,
            isLowerTailTest: false),
        new PermutationTest<double>("DeNovo-Score",
            r => r.MeanPredictionScore,
            IterationsForPermutationTests,
            shouldSkip: r => r.TargetPredictions < MinValuesThreshold),
        new KolmogorovSmirnovTest("DeNovo-Scores",
            r => r.PredictionScores,
            r => r.PredictionScores.Length < DistributionMinValuesThreshold),
    ];

}
