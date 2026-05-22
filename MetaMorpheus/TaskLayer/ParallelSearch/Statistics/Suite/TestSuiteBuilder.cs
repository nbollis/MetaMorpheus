#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using TaskLayer.ParallelSearch.Analysis.Collectors;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Builds a composed list of IStatisticalTest instances for a parallel search run.
/// Each Add method registers tests for one evidence family. AddFamily dispatches
/// to the correct family method using the StatisticalEvidenceFamily enum.
/// </summary>
public sealed class TestSuiteBuilder : IEnumerable<IStatisticalTest>
{
    private readonly List<IStatisticalTest> _tests = new();

    public int DistributionMinValuesThreshold { get; set; } = 2;
    public int PermutationIterations { get; set; } = 1000;

    public TestSuiteBuilder AddCountEnrichmentTests()
    {
        _tests.Add(new GaussianTest<double>("PSM-All",
            StatisticalEvidenceFamily.CountEnrichment,
            r => r.PsmBacterialUnambiguousTargets / (double)r.TransientPeptideCount));
        _tests.Add(new GaussianTest<double>("Peptide-All",
            StatisticalEvidenceFamily.CountEnrichment,
            r => r.PeptideBacterialUnambiguousTargets / (double)r.TransientPeptideCount));

        _tests.Add(new NegativeBinomialTest<int>("PSM-All",
            StatisticalEvidenceFamily.CountEnrichment,
            r => r.PsmBacterialUnambiguousTargets));
        _tests.Add(new NegativeBinomialTest<int>("Peptide",
            StatisticalEvidenceFamily.CountEnrichment,
            r => r.PeptideBacterialUnambiguousTargets));

        _tests.Add(new PermutationTest<double>("PSM-All",
            StatisticalEvidenceFamily.CountEnrichment,
            r => (double)r.PsmBacterialUnambiguousTargets,
            PermutationIterations));
        _tests.Add(new PermutationTest<double>("Peptide-All",
            StatisticalEvidenceFamily.CountEnrichment,
            r => (double)r.PeptideBacterialUnambiguousTargets,
            PermutationIterations));


        _tests.Add(new GaussianTest<double>("PSM-Confident",
            StatisticalEvidenceFamily.CountEnrichment,
            r => r.TargetPsmsFromTransientDbAtQValueThreshold / (double)r.TransientPeptideCount));
        _tests.Add(new GaussianTest<double>("Peptide-Confident",
            StatisticalEvidenceFamily.CountEnrichment,
            r => r.TargetPeptidesFromTransientDbAtQValueThreshold / (double)r.TransientPeptideCount));

        _tests.Add(new NegativeBinomialTest<int>("PSM-Confident",
            StatisticalEvidenceFamily.CountEnrichment,
            r => r.TargetPsmsFromTransientDbAtQValueThreshold));
        _tests.Add(new NegativeBinomialTest<int>("Peptide-Confident",
            StatisticalEvidenceFamily.CountEnrichment,
            r => r.TargetPeptidesFromTransientDbAtQValueThreshold));

        _tests.Add(new PermutationTest<double>("PSM-Confident",
            StatisticalEvidenceFamily.CountEnrichment,
            r => (double)r.TargetPsmsFromTransientDbAtQValueThreshold,
            PermutationIterations));
        _tests.Add(new PermutationTest<double>("Peptide-Confident",
            StatisticalEvidenceFamily.CountEnrichment,
            r => (double)r.TargetPeptidesFromTransientDbAtQValueThreshold,
            PermutationIterations));

        return this;
    }

    public TestSuiteBuilder AddAmbiguityOrTargetDecoyTests()
    {
        _tests.Add(new FisherExactTest("PSM",
            StatisticalEvidenceFamily.AmbiguityOrTargetDecoy,
            r => r.PsmBacterialUnambiguousTargets,
            r => r.PsmBacterialAmbiguous));
        _tests.Add(new FisherExactTest("Peptide",
            StatisticalEvidenceFamily.AmbiguityOrTargetDecoy,
            r => r.PeptideBacterialUnambiguousTargets,
            r => r.PeptideBacterialAmbiguous));

        _tests.Add(new FisherExactTest("PSM-TD",
            StatisticalEvidenceFamily.AmbiguityOrTargetDecoy,
            r => r.PsmBacterialTargets,
            r => r.PsmBacterialDecoys));
        _tests.Add(new FisherExactTest("Peptide-TD",
            StatisticalEvidenceFamily.AmbiguityOrTargetDecoy,
            r => r.PeptideBacterialTargets,
            r => r.PeptideBacterialDecoys));

        _tests.Add(new KolmogorovSmirnovTest("PSM-DeltaScores",
            StatisticalEvidenceFamily.AmbiguityOrTargetDecoy,
            r => r.PsmBacterialTargetDeltaScores,
            r => r.PsmBacterialTargetDeltaScores.Length >= DistributionMinValuesThreshold));
        _tests.Add(new KolmogorovSmirnovTest("Peptide-DeltaScores",
            StatisticalEvidenceFamily.AmbiguityOrTargetDecoy,
            r => r.PeptideBacterialTargetDeltaScores,
            r => r.PeptideBacterialTargetDeltaScores.Length >= DistributionMinValuesThreshold));
        _tests.Add(new GaussianTest<double>("PSM-MedianDeltaScore",
            StatisticalEvidenceFamily.AmbiguityOrTargetDecoy,
            r => r.PsmBacterialTargetDeltaScores.Length > 0 ? r.PsmBacterialTargetDeltaScores.Median() : double.NaN));
        _tests.Add(new GaussianTest<double>("Peptide-MedianDeltaScore",
            StatisticalEvidenceFamily.AmbiguityOrTargetDecoy,
            r => r.PeptideBacterialTargetDeltaScores.Length > 0 ? r.PeptideBacterialTargetDeltaScores.Median() : double.NaN));

        return this;
    }

    public TestSuiteBuilder AddScoreDistributionTests()
    {
        _tests.Add(new KolmogorovSmirnovTest("PSMScoreDistribution",
            StatisticalEvidenceFamily.ScoreDistribution,
            r => r.PsmBacterialUnambiguousTargetScores,
            r => r.PsmBacterialUnambiguousTargetScores.Length >= DistributionMinValuesThreshold));
        _tests.Add(new KolmogorovSmirnovTest("PeptideScoreDistribution",
            StatisticalEvidenceFamily.ScoreDistribution,
            r => r.PeptideBacterialUnambiguousTargetScores,
            r => r.PeptideBacterialUnambiguousTargetScores.Length >= DistributionMinValuesThreshold));

        _tests.Add(new KolmogorovSmirnovTest("PSMDecoyScoreDistribution",
            StatisticalEvidenceFamily.ScoreDistribution,
            r => r.PsmBacterialUnambiguousDecoyScores,
            r => r.PsmBacterialUnambiguousDecoyScores.Length >= DistributionMinValuesThreshold,
            KSAlternative.Greater));
        _tests.Add(new KolmogorovSmirnovTest("PeptideDecoyScoreDistribution",
            StatisticalEvidenceFamily.ScoreDistribution,
            r => r.PeptideBacterialUnambiguousDecoyScores,
            r => r.PeptideBacterialUnambiguousDecoyScores.Length >= DistributionMinValuesThreshold,
            KSAlternative.Greater));

        return this;
    }

    public TestSuiteBuilder AddRetentionTimeTests()
    {
        _tests.Add(new GaussianTest<double>("PSM-MeanAbsoluteRtError",
            StatisticalEvidenceFamily.RetentionTime,
            r => r.Psm_MeanAbsoluteRtError,
            isLowerTailTest: true));
        _tests.Add(new GaussianTest<double>("PSM-MeanRtError",
            StatisticalEvidenceFamily.RetentionTime,
            r => r.Psm_AllRtErrors.Length > 0 ? r.Psm_AllRtErrors.Average() : double.NaN,
            isLowerTailTest: true));
        _tests.Add(new GaussianTest<double>("PSM-MedianRtError",
            StatisticalEvidenceFamily.RetentionTime,
            r => r.Psm_AllRtErrors.Length > 0 ? r.Psm_AllRtErrors.Median() : double.NaN,
            isLowerTailTest: true));
        _tests.Add(new GaussianTest<double>("PSM-StdDevRtError",
            StatisticalEvidenceFamily.RetentionTime,
            r => r.Psm_AllRtErrors.Length > 0 ? r.Psm_AllRtErrors.StandardDeviation() : double.NaN,
            isLowerTailTest: true));
        _tests.Add(new GaussianTest<double>("PSM-RootMeanSquareRtError",
            StatisticalEvidenceFamily.RetentionTime,
            r => r.Psm_AllRtErrors.Length > 0 ? r.Psm_AllRtErrors.RootMeanSquare() : double.NaN,
            isLowerTailTest: true));
        _tests.Add(new KolmogorovSmirnovTest("PSM-RtErrors",
            StatisticalEvidenceFamily.RetentionTime ,
            r => r.Psm_AllRtErrors,
            r => r.Psm_AllRtErrors.Length >= DistributionMinValuesThreshold,
            KSAlternative.TwoSided));



        _tests.Add(new GaussianTest<double>("Peptide-MeanAbsoluteRtError",
            StatisticalEvidenceFamily.RetentionTime,
            r => r.Peptide_MeanAbsoluteRtError,
            isLowerTailTest: true));
        _tests.Add(new GaussianTest<double>("Peptide-MeanRtError",
            StatisticalEvidenceFamily.RetentionTime,
            r => r.Peptide_AllRtErrors.Length > 0 ? r.Peptide_AllRtErrors.Average() : double.NaN,
            isLowerTailTest: true));
        _tests.Add(new GaussianTest<double>("Peptide-MedianRtError",
            StatisticalEvidenceFamily.RetentionTime,
            r => r.Peptide_AllRtErrors.Length > 0 ? r.Peptide_AllRtErrors.Median() : double.NaN,
            isLowerTailTest: true));
        _tests.Add(new GaussianTest<double>("Peptide-StdDevRtError",
            StatisticalEvidenceFamily.RetentionTime,
            r => r.Peptide_AllRtErrors.Length > 0 ? r.Peptide_AllRtErrors.StandardDeviation() : double.NaN,
            isLowerTailTest: true));
        _tests.Add(new GaussianTest<double>("Peptide-RootMeanSquareRtError",
            StatisticalEvidenceFamily.RetentionTime,
            r => r.Peptide_AllRtErrors.Length > 0 ? r.Peptide_AllRtErrors.RootMeanSquare() : double.NaN,
            isLowerTailTest: true));
        _tests.Add(new KolmogorovSmirnovTest("Peptide-RtErrors",
            StatisticalEvidenceFamily.RetentionTime,
            r => r.Peptide_AllRtErrors,
            r => r.Peptide_AllRtErrors.Length >= DistributionMinValuesThreshold,
            KSAlternative.TwoSided));

        return this;
    }

    public TestSuiteBuilder AddFragmentationTests()
    {
        _tests.Add(new KolmogorovSmirnovTest("PSM-Complementary",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Psm_ComplementaryCountTargets,
            r => r.Psm_ComplementaryCountTargets.Length >= DistributionMinValuesThreshold));
        _tests.Add(new KolmogorovSmirnovTest("PSM-Bidirectional",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Psm_BidirectionalTargets,
            r => r.Psm_BidirectionalTargets.Length >= DistributionMinValuesThreshold));
        _tests.Add(new KolmogorovSmirnovTest("PSM-SequenceCoverage",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Psm_SequenceCoverageFractionTargets,
            r => r.Psm_SequenceCoverageFractionTargets.Length >= DistributionMinValuesThreshold));
        _tests.Add(new KolmogorovSmirnovTest("Peptide-Complementary",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_ComplementaryCountTargets,
            r => r.Peptide_ComplementaryCountTargets.Length >= DistributionMinValuesThreshold));
        _tests.Add(new KolmogorovSmirnovTest("Peptide-Bidirectional",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_BidirectionalTargets,
            r => r.Peptide_BidirectionalTargets.Length >= DistributionMinValuesThreshold));
        _tests.Add(new KolmogorovSmirnovTest("Peptide-SequenceCoverage",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_SequenceCoverageFractionTargets,
            r => r.Peptide_SequenceCoverageFractionTargets.Length >= DistributionMinValuesThreshold));

        _tests.Add(new GaussianTest<double>("PSM-Complementary",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Psm_ComplementaryCount_MedianTargets));
        _tests.Add(new GaussianTest<double>("PSM-Bidirectional",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Psm_Bidirectional_MedianTargets));
        _tests.Add(new GaussianTest<double>("PSM-SequenceCoverage",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Psm_SequenceCoverageFraction_MedianTargets));
        _tests.Add(new GaussianTest<double>("Peptide-Complementary",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_ComplementaryCount_MedianTargets));
        _tests.Add(new GaussianTest<double>("Peptide-Bidirectional",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_Bidirectional_MedianTargets));
        _tests.Add(new GaussianTest<double>("Peptide-SequenceCoverage",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_SequenceCoverageFraction_MedianTargets));

        _tests.Add(new KolmogorovSmirnovTest("PSM-PPMErrors",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Psm_FragmentPPMErrors,
            r => r.Psm_FragmentPPMErrors.Length >= DistributionMinValuesThreshold, KSAlternative.TwoSided));
        _tests.Add(new KolmogorovSmirnovTest("Peptide-PPMErrors",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_FragmentPPMErrors,
            r => r.Peptide_FragmentPPMErrors.Length >= DistributionMinValuesThreshold, KSAlternative.TwoSided));

        _tests.Add(new GaussianTest<double>("PSM-MeanPPMError",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Psm_FragmentPPMErrors.Length > 0 ? r.Psm_FragmentPPMErrors.Average(Math.Abs) : double.NaN,
            isLowerTailTest: true));
        _tests.Add(new GaussianTest<double>("Peptide-MeanPPMError",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_FragmentPPMErrors.Length > 0 ? r.Peptide_FragmentPPMErrors.Average(Math.Abs) : double.NaN,
            isLowerTailTest: true));

        return this;
    }

    public TestSuiteBuilder AddProteinGroupTests()
    {
        _tests.Add(new GaussianTest<double>("ProteinGroup",
            StatisticalEvidenceFamily.ProteinGroup,
            r => r.TargetProteinGroupsFromTransientDbAtQValueThreshold / (double)r.TransientProteinCount));

        _tests.Add(new NegativeBinomialTest<int>("ProteinGroup",
            StatisticalEvidenceFamily.ProteinGroup,
            r => r.ProteinGroupBacterialUnambiguousTargets));

        _tests.Add(new PermutationTest<double>("ProteinGroup",
            StatisticalEvidenceFamily.ProteinGroup,
            r => (double)r.ProteinGroupBacterialUnambiguousTargets,
            PermutationIterations));

        _tests.Add(new GaussianTest<double>("MedianPeptidesPerProteinGroup",
            StatisticalEvidenceFamily.ProteinGroup,
            r => r.MedianPeptidesPerProteinGroup));
        _tests.Add(new GaussianTest<double>("MedianUniquePeptidesPerProteinGroup",
            StatisticalEvidenceFamily.ProteinGroup,
            r => r.MedianUniquePeptidesPerProteinGroup));
        _tests.Add(new GaussianTest<double>("MedianPsmsPerProteinGroup",
            StatisticalEvidenceFamily.ProteinGroup,
            r => r.MedianPsmsPerProteinGroup));

        _tests.Add(new KolmogorovSmirnovTest("AllPeptidesPerProteinGroup",
            StatisticalEvidenceFamily.ProteinGroup,
            r => r.AllPeptidesPerProteinGroup,
            r => r.AllPeptidesPerProteinGroup.Length >= DistributionMinValuesThreshold));
        _tests.Add(new KolmogorovSmirnovTest("AllUniquePeptidesPerProteinGroup",
            StatisticalEvidenceFamily.ProteinGroup,
            r => r.AllUniquePeptidesPerProteinGroup,
            r => r.AllUniquePeptidesPerProteinGroup.Length >= DistributionMinValuesThreshold));
        _tests.Add(new KolmogorovSmirnovTest("AllPsmsPerProteinGroup",
            StatisticalEvidenceFamily.ProteinGroup,
            r => r.AllPsmsPerProteinGroup,
            r => r.AllPsmsPerProteinGroup.Length >= DistributionMinValuesThreshold));

        _tests.Add(new KolmogorovSmirnovTest("SequenceCoverage",
            StatisticalEvidenceFamily.ProteinGroup,
            r => r.AllSequenceCoverageFractions,
            r => r.AllSequenceCoverageFractions.Length >= DistributionMinValuesThreshold));
        _tests.Add(new GaussianTest<double>("MedianSequenceCoverageFraction",
            StatisticalEvidenceFamily.ProteinGroup,
            r => r.MedianSequenceCoverageFraction));

        return this;
    }

    public TestSuiteBuilder AddPrecursorDeconvolutionTests()
    {
        _tests.Add(new KolmogorovSmirnovTest("PSM-PrecursorDeconScores",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PsmPrecursorDeconScores,
            r => r.PsmPrecursorDeconScores.Length >= DistributionMinValuesThreshold, KSAlternative.Less));
        _tests.Add(new KolmogorovSmirnovTest("PSM-PrecursorMassErrors",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PsmPrecursorMassErrors,
            r => r.PsmPrecursorMassErrors.Length >= DistributionMinValuesThreshold, KSAlternative.TwoSided));
        _tests.Add(new KolmogorovSmirnovTest("PSM-PrecursorEnvelopePeakCounts",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PsmPrecursorEnvelopePeakCounts.Select(v => (double)v).ToArray(),
            r => r.PsmPrecursorEnvelopePeakCounts.Length >= DistributionMinValuesThreshold, KSAlternative.Less));
        _tests.Add(new KolmogorovSmirnovTest("PSM-PrecursorFractionalIntensities",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PsmPrecursorFractionalIntensities,
            r => r.PsmPrecursorFractionalIntensities.Length >= DistributionMinValuesThreshold, KSAlternative.Less));
        _tests.Add(new KolmogorovSmirnovTest("Peptide-PrecursorDeconScores",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PeptidePrecursorDeconScores,
            r => r.PeptidePrecursorDeconScores.Length >= DistributionMinValuesThreshold, KSAlternative.Less));
        _tests.Add(new KolmogorovSmirnovTest("Peptide-PrecursorMassErrors",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PeptidePrecursorMassErrors,
            r => r.PeptidePrecursorMassErrors.Length >= DistributionMinValuesThreshold, KSAlternative.TwoSided));
        _tests.Add(new KolmogorovSmirnovTest("Peptide-PrecursorEnvelopePeakCounts",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PeptidePrecursorEnvelopePeakCounts.Select(v => (double)v).ToArray(),
            r => r.PeptidePrecursorEnvelopePeakCounts.Length >= DistributionMinValuesThreshold, KSAlternative.Less));
        _tests.Add(new KolmogorovSmirnovTest("Peptide-PrecursorFractionalIntensities",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PeptidePrecursorFractionalIntensities,
            r => r.PeptidePrecursorFractionalIntensities.Length >= DistributionMinValuesThreshold, KSAlternative.Less));

        _tests.Add(new GaussianTest<double>("PSM-MedianPrecursorDeconScore",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PsmPrecursorDeconScores.Length > 0 ? r.PsmPrecursorDeconScores.Median() : double.NaN,
            isLowerTailTest: true));
        _tests.Add(new GaussianTest<double>("PSM-MedianPrecursorMassError",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PsmPrecursorMassErrors.Length > 0 ? r.PsmPrecursorMassErrors.Average(Math.Abs) : double.NaN,
            isLowerTailTest: true));
        _tests.Add(new GaussianTest<double>("PSM-MedianPrecursorEnvelopePeakCount",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PsmPrecursorEnvelopePeakCounts.Length > 0 ? r.PsmPrecursorEnvelopePeakCounts.Select(v => (double)v).Median() : double.NaN));
        _tests.Add(new NegativeBinomialTest<double>("PSM-MedianPrecursorEnvelopePeakCount",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PsmPrecursorEnvelopePeakCounts.Length > 0 ? r.PsmPrecursorEnvelopePeakCounts.Select(v => (double)v).Median() : double.NaN));
        _tests.Add(new GaussianTest<double>("PSM-MedianPrecursorFractionalIntensity",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PsmPrecursorFractionalIntensities.Length > 0 ? r.PsmPrecursorFractionalIntensities.Median() : double.NaN));
        _tests.Add(new GaussianTest<double>("Peptide-MedianPrecursorDeconScore",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PeptidePrecursorDeconScores.Length > 0 ? r.PeptidePrecursorDeconScores.Median() : double.NaN,
            isLowerTailTest: true));
        _tests.Add(new GaussianTest<double>("Peptide-MedianPrecursorMassError",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PeptidePrecursorMassErrors.Length > 0 ? r.PeptidePrecursorMassErrors.Average(Math.Abs) : double.NaN,
            isLowerTailTest: true));
        _tests.Add(new GaussianTest<double>("Peptide-MedianPrecursorEnvelopePeakCount",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PeptidePrecursorEnvelopePeakCounts.Length > 0 ? r.PeptidePrecursorEnvelopePeakCounts.Select(v => (double)v).Median() : double.NaN));
        _tests.Add(new NegativeBinomialTest<double>("Peptide-MedianPrecursorEnvelopePeakCount",
           StatisticalEvidenceFamily.PrecursorDeconvolution,
           r => r.PeptidePrecursorEnvelopePeakCounts.Length > 0 ? r.PeptidePrecursorEnvelopePeakCounts.Select(v => (double)v).Median() : double.NaN));
        _tests.Add(new GaussianTest<double>("Peptide-MedianPrecursorFractionalIntensity",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PeptidePrecursorFractionalIntensities.Length > 0 ? r.PeptidePrecursorFractionalIntensities.Median() : double.NaN));

        return this;
    }

    public TestSuiteBuilder AddDeNovoTests()
    {
        _tests.Add(new NegativeBinomialTest<int>("DeNovo-Predictions",
            StatisticalEvidenceFamily.DeNovo,
            r => r.TotalPredictions));
        _tests.Add(new GaussianTest<double>("DeNovo-Predictions",
            StatisticalEvidenceFamily.DeNovo,
            r => r.TotalPredictions / (double)r.TransientPeptideCount));
        _tests.Add(new PermutationTest<double>("DeNovo-Predictions",
            StatisticalEvidenceFamily.DeNovo,
            r => (double)r.TotalPredictions,
            PermutationIterations,
            r => r.TotalPredictions > 0));

        _tests.Add(new NegativeBinomialTest<int>("DeNovo-Targets",
            StatisticalEvidenceFamily.DeNovo,
            r => r.TargetPredictions));
        _tests.Add(new GaussianTest<double>("DeNovo-Targets",
            StatisticalEvidenceFamily.DeNovo,
            r => r.TargetPredictions / (double)r.TransientPeptideCount));
        _tests.Add(new PermutationTest<double>("DeNovo-Targets",
            StatisticalEvidenceFamily.DeNovo,
            r => (double)r.TargetPredictions,
            PermutationIterations,
            r => r.TotalPredictions > 0));

        _tests.Add(new NegativeBinomialTest<int>("DeNovo-MappedPeptides",
            StatisticalEvidenceFamily.DeNovo,
            r => r.UniquePeptidesMapped));
        _tests.Add(new GaussianTest<double>("DeNovo-MappedPeptides",
            StatisticalEvidenceFamily.DeNovo,
            r => r.UniquePeptidesMapped / (double)r.TransientPeptideCount));
        _tests.Add(new PermutationTest<double>("DeNovo-MappedPeptides",
            StatisticalEvidenceFamily.DeNovo,
            r => (double)r.UniquePeptidesMapped,
            PermutationIterations,
            r => r.TotalPredictions > 0));

        _tests.Add(new NegativeBinomialTest<int>("DeNovo-MappedProteins",
            StatisticalEvidenceFamily.DeNovo,
            r => r.UniqueProteinsMapped));
        _tests.Add(new GaussianTest<double>("DeNovo-MappedProteins",
            StatisticalEvidenceFamily.DeNovo,
            r => r.UniqueProteinsMapped / (double)r.TransientProteinCount));
        _tests.Add(new PermutationTest<double>("DeNovo-MappedProteins",
            StatisticalEvidenceFamily.DeNovo,
            r => (double)r.UniqueProteinsMapped,
            PermutationIterations,
            r => r.TotalPredictions > 0));

        // Retention Time
        _tests.Add(new GaussianTest<double>("DeNovo-MeanAbsoluteRtError",
            StatisticalEvidenceFamily.DeNovo,
            r => r.DeNovoRetentionTimeErrors.Length > 0 ? r.DeNovoRetentionTimeErrors.Average(Math.Abs) : double.NaN,
            r => r.DeNovoRetentionTimeErrors.Length > 0,
            isLowerTailTest: true));
        _tests.Add(new GaussianTest<double>("DeNovo-MeanRtError",
            StatisticalEvidenceFamily.DeNovo,
            r => r.DeNovoRetentionTimeErrors.Length > 0 ? r.DeNovoRetentionTimeErrors.Average() : double.NaN,
            isLowerTailTest: true));
        _tests.Add(new GaussianTest<double>("DeNovo-MedianRtError",
            StatisticalEvidenceFamily.DeNovo,
            r => r.DeNovoRetentionTimeErrors.Length > 0 ? r.DeNovoRetentionTimeErrors.Median() : double.NaN,
            isLowerTailTest: true));
        _tests.Add(new GaussianTest<double>("DeNovo-StdDevRtError",
            StatisticalEvidenceFamily.DeNovo,
            r => r.DeNovoRetentionTimeErrors.Length > 0 ? r.DeNovoRetentionTimeErrors.StandardDeviation() : double.NaN,
            isLowerTailTest: true));
        _tests.Add(new GaussianTest<double>("DeNovo-RootMeanSquareRtError",
            StatisticalEvidenceFamily.DeNovo,
            r => r.DeNovoRetentionTimeErrors.Length > 0 ? r.DeNovoRetentionTimeErrors.RootMeanSquare() : double.NaN,
            isLowerTailTest: true));
        _tests.Add(new KolmogorovSmirnovTest("DeNovo-RtErrors",
            StatisticalEvidenceFamily.DeNovo,
            r => r.DeNovoRetentionTimeErrors,
            r => r.DeNovoRetentionTimeErrors.Length >= DistributionMinValuesThreshold,
            KSAlternative.Greater));

        // Scoring
        _tests.Add(new GaussianTest<double>("DeNovo-Score",
            StatisticalEvidenceFamily.DeNovo,
            r => r.MeanPredictionScore));
        _tests.Add(new PermutationTest<double>("DeNovo-Score",
            StatisticalEvidenceFamily.DeNovo,
            r => r.MeanPredictionScore,
            PermutationIterations));
        _tests.Add(new KolmogorovSmirnovTest("DeNovo-Scores",
            StatisticalEvidenceFamily.DeNovo,
            r => r.PredictionScores,
            r => r.PredictionScores.Length >= DistributionMinValuesThreshold));
        _tests.Add(new GaussianTest<double>("DeNovo-MedianPredictionScore",
            StatisticalEvidenceFamily.DeNovo,
            r => r.PredictionScores.Length > 0 ? r.PredictionScores.Median() : double.NaN));
        _tests.Add(new GaussianTest<double>("DeNovo-StdDevPredictionScore",
            StatisticalEvidenceFamily.DeNovo,
            r => r.PredictionScores.Length > 0 ? r.PredictionScores.StandardDeviation() : double.NaN,
            isLowerTailTest: true));

        // Normalized Scoring
        _tests.Add(new GaussianTest<double>("DeNovo-NormalizedScore",
            StatisticalEvidenceFamily.DeNovo,
            r => r.MeanPredictionScore <0 ? r.MeanPredictionScore + 1 : r.MeanPredictionScore));
        _tests.Add(new PermutationTest<double>("DeNovo-NormalizedScore",
            StatisticalEvidenceFamily.DeNovo,
            r => r.MeanPredictionScore < 0 ? r.MeanPredictionScore + 1 : r.MeanPredictionScore,
            PermutationIterations));
        _tests.Add(new KolmogorovSmirnovTest("DeNovo-NormalizedScores",
            StatisticalEvidenceFamily.DeNovo,
            r => DeNovoMappingCollector.NormalizeScores(r.PredictionScores).ToArray(),
            r => r.PredictionScores.Length >= DistributionMinValuesThreshold));
        _tests.Add(new GaussianTest<double>("DeNovo-MedianNormalizedPredictionScore",
            StatisticalEvidenceFamily.DeNovo,
            r => r.PredictionScores.Length > 0 ? DeNovoMappingCollector.NormalizeScores(r.PredictionScores).Median() : double.NaN));
        _tests.Add(new GaussianTest<double>("DeNovo-StdDevNormalizedPredictionScore",
            StatisticalEvidenceFamily.DeNovo,
            r => r.PredictionScores.Length > 0 ? DeNovoMappingCollector.NormalizeScores(r.PredictionScores).StandardDeviation() : double.NaN,
            isLowerTailTest: true));

        // Target Decoy
        _tests.Add(new FisherExactTest("DeNovo-TargetDecoy",
            StatisticalEvidenceFamily.DeNovo,
            r => r.TargetPredictions,
            r => r.DecoyPredictions,
            r => r.TargetPredictions > 0 || r.DecoyPredictions > 0));

        _tests.Add(new NegativeBinomialTest<int>("DeNovo-DecoyPredictions",
            StatisticalEvidenceFamily.DeNovo,
            r => r.DecoyPredictions, 
            r => r.DecoyPredictions > 0,
            isLowerTailTest: true));

        return this;
    }

    /// <summary>
    /// Dispatches to the correct family-specific Add method based on the
    /// StatisticalEvidenceFamily enum value.
    /// </summary>
    public TestSuiteBuilder AddFamily(StatisticalEvidenceFamily family)
    {
        return family switch
        {
            StatisticalEvidenceFamily.CountEnrichment => AddCountEnrichmentTests(),
            StatisticalEvidenceFamily.AmbiguityOrTargetDecoy => AddAmbiguityOrTargetDecoyTests(),
            StatisticalEvidenceFamily.ScoreDistribution => AddScoreDistributionTests(),
            StatisticalEvidenceFamily.Fragmentation => AddFragmentationTests(),
            StatisticalEvidenceFamily.RetentionTime => AddRetentionTimeTests(),
            StatisticalEvidenceFamily.ProteinGroup => AddProteinGroupTests(),
            StatisticalEvidenceFamily.DeNovo => AddDeNovoTests(),
            StatisticalEvidenceFamily.PrecursorDeconvolution => AddPrecursorDeconvolutionTests(),
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, null)
        };
    }

    public TestSuiteBuilder AddTests(IEnumerable<IStatisticalTest> tests)
    {
        _tests.AddRange(tests);
        return this;
    }

    public IReadOnlyList<IStatisticalTest> Build() => _tests.AsReadOnly();

    public IEnumerator<IStatisticalTest> GetEnumerator() => _tests.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _tests.GetEnumerator();
}
