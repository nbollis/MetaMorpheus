#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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

        return this;
    }

    public TestSuiteBuilder AddRetentionTimeTests()
    {
        _tests.Add(new GaussianTest<double>("PSM-MeanAbsoluteRtError",
            StatisticalEvidenceFamily.RetentionTime,
            r => r.Psm_MeanAbsoluteRtError,
            isLowerTailTest: true));
        _tests.Add(new GaussianTest<double>("Peptide-MeanAbsoluteRtError",
            StatisticalEvidenceFamily.RetentionTime,
            r => r.Peptide_MeanAbsoluteRtError,
            isLowerTailTest: true));

        _tests.Add(new KolmogorovSmirnovTest("PSM-RtErrors",
            StatisticalEvidenceFamily.RetentionTime,
            r => r.Psm_AllRtErrors,
            r => r.Psm_AllRtErrors.Length >= DistributionMinValuesThreshold,
            KSAlternative.Greater));
        _tests.Add(new KolmogorovSmirnovTest("Peptide-RtErrors",
            StatisticalEvidenceFamily.RetentionTime,
            r => r.Peptide_AllRtErrors,
            r => r.Peptide_AllRtErrors.Length >= DistributionMinValuesThreshold,
            KSAlternative.Greater));

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

        _tests.Add(new PermutationTest<double>("PSM-Complementary",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Psm_ComplementaryCount_MedianTargets,
            PermutationIterations));
        _tests.Add(new PermutationTest<double>("PSM-Bidirectional",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Psm_Bidirectional_MedianTargets,
            PermutationIterations));
        _tests.Add(new PermutationTest<double>("PSM-SequenceCoverage",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Psm_SequenceCoverageFraction_MedianTargets,
            PermutationIterations));
        _tests.Add(new PermutationTest<double>("Peptide-Complementary",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_ComplementaryCount_MedianTargets,
            PermutationIterations));
        _tests.Add(new PermutationTest<double>("Peptide-Bidirectional",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_Bidirectional_MedianTargets,
            PermutationIterations));
        _tests.Add(new PermutationTest<double>("Peptide-SequenceCoverage",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_SequenceCoverageFraction_MedianTargets,
            PermutationIterations));

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

        _tests.Add(new GaussianTest<double>("DeNovo-MeanAbsoluteRtError",
            StatisticalEvidenceFamily.DeNovo,
            r => r.RetentionTimeErrors.Select(Math.Abs).Average(),
            isLowerTailTest: true));
        _tests.Add(new KolmogorovSmirnovTest("DeNovo-RtErrors",
            StatisticalEvidenceFamily.DeNovo,
            r => r.RetentionTimeErrors,
            r => r.RetentionTimeErrors.Length >= DistributionMinValuesThreshold,
            KSAlternative.Greater));

        _tests.Add(new GaussianTest<double>("DeNovo-Score",
            StatisticalEvidenceFamily.DeNovo,
            r => r.MeanPredictionScore,
            isLowerTailTest: false));
        _tests.Add(new PermutationTest<double>("DeNovo-Score",
            StatisticalEvidenceFamily.DeNovo,
            r => r.MeanPredictionScore,
            PermutationIterations));
        _tests.Add(new KolmogorovSmirnovTest("DeNovo-Scores",
            StatisticalEvidenceFamily.DeNovo,
            r => r.PredictionScores,
            r => r.PredictionScores.Length >= DistributionMinValuesThreshold));

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
