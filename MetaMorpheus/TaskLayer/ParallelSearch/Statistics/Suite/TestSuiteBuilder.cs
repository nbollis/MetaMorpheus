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

    // Every test here is on peptide-level (or protein-group-level) values of matches that are the database's own: one
    // value per peptide, never per PSM or per fragment ion. Repeat PSMs of one peptide and the fragments of one spectrum
    // are not independent, and treating them as such made one random peptide with 30 PSMs a KS p of about 1e-17.
    // With the Cauchy combination one small p-value carries through to the database's overall p, so each test has to be
    // valid on its own.
    //
    // Errors are tested as absolute values, one-sided (smaller is better). A signed error or a two-sided test rewarded
    // errors that are shifted or broad, which is what false hits look like.
    //
    // Locations are tested with SampleMeanTest, whose standard error shrinks with n, and not with a Gaussian across
    // per-database medians, which treated a database's single value like a median of fifty.

    private static double[] Absolute(double[] values) => values.Select(Math.Abs).ToArray();

    /// <summary>
    /// Count tests, on confident matches that are not shared with the baseline (the "Bacterial Unambiguous" counts already
    /// apply the q-value cutoff). Size is handled by <see cref="SizeOffsetPoissonTest"/> on distinct-peptide counts, with
    /// every database defined. The raw-count negative binomial test, which is conservative, stays. There is no test on PSM
    /// counts (one peptide can give many PSMs) and no permutation test: its count version is the size-offset test drawn at
    /// random, and its size unit was proteins.
    /// </summary>
    public TestSuiteBuilder AddCountEnrichmentTests()
    {
        _tests.Add(new SizeOffsetPoissonTest("Peptide-All",
            StatisticalEvidenceFamily.CountEnrichment,
            r => r.PeptideBacterialUnambiguousTargets,
            r => r.TransientPeptideCount));

        _tests.Add(new NegativeBinomialTest<int>("Peptide",
            StatisticalEvidenceFamily.CountEnrichment,
            r => r.PeptideBacterialUnambiguousTargets,
            r => r.PeptideBacterialUnambiguousTargets > 0));

        return this;
    }

    public TestSuiteBuilder AddAmbiguityOrTargetDecoyTests()
    {
        _tests.Add(new FisherExactTest("Peptide",
            StatisticalEvidenceFamily.AmbiguityOrTargetDecoy,
            r => r.PeptideBacterialUnambiguousTargets,
            r => r.PeptideBacterialAmbiguous));

        // targets against decoys among the database's own peptides; shared matches are baseline identifications
        _tests.Add(new FisherExactTest("Peptide-TD",
            StatisticalEvidenceFamily.AmbiguityOrTargetDecoy,
            r => r.PeptideBacterialUnambiguousTargets,
            r => r.PeptideBacterialUnambiguousDecoys));

        _tests.Add(new KolmogorovSmirnovTest("Peptide-DeltaScores",
            StatisticalEvidenceFamily.AmbiguityOrTargetDecoy,
            r => r.PeptideBacterialTargetDeltaScores,
            r => r.PeptideBacterialTargetDeltaScores.Length >= DistributionMinValuesThreshold));
        _tests.Add(new SampleMeanTest("Peptide-DeltaScore",
            StatisticalEvidenceFamily.AmbiguityOrTargetDecoy,
            r => r.PeptideBacterialTargetDeltaScores,
            higherIsBetter: true, DistributionMinValuesThreshold));

        return this;
    }

    /// <summary>
    /// Target peptide scores against the pooled scores. (The decoy-score test, which called lower decoy scores evidence,
    /// is gone: there is no reason a present organism's decoys score lower.)
    /// </summary>
    public TestSuiteBuilder AddScoreDistributionTests()
    {
        _tests.Add(new KolmogorovSmirnovTest("PeptideScoreDistribution",
            StatisticalEvidenceFamily.ScoreDistribution,
            r => r.PeptideBacterialUnambiguousTargetScores,
            r => r.PeptideBacterialUnambiguousTargetScores.Length >= DistributionMinValuesThreshold));

        return this;
    }

    /// <summary>
    /// Retention-time agreement: absolute error (observed minus predicted), smaller is better. The signed mean and median
    /// in the lower tail rewarded peptides eluting early, not agreement.
    /// </summary>
    public TestSuiteBuilder AddRetentionTimeTests()
    {
        _tests.Add(new KolmogorovSmirnovTest("Peptide-AbsoluteRtErrors",
            StatisticalEvidenceFamily.RetentionTime,
            r => Absolute(r.Peptide_AllRtErrors),
            r => r.Peptide_AllRtErrors.Length >= DistributionMinValuesThreshold,
            KSAlternative.Greater));
        _tests.Add(new SampleMeanTest("Peptide-AbsoluteRtError",
            StatisticalEvidenceFamily.RetentionTime,
            r => Absolute(r.Peptide_AllRtErrors),
            higherIsBetter: false, DistributionMinValuesThreshold));

        return this;
    }

    public TestSuiteBuilder AddFragmentationTests()
    {
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

        _tests.Add(new SampleMeanTest("Peptide-Complementary",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_ComplementaryCountTargets,
            higherIsBetter: true, DistributionMinValuesThreshold));
        _tests.Add(new SampleMeanTest("Peptide-Bidirectional",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_BidirectionalTargets,
            higherIsBetter: true, DistributionMinValuesThreshold));
        _tests.Add(new SampleMeanTest("Peptide-SequenceCoverage",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_SequenceCoverageFractionTargets,
            higherIsBetter: true, DistributionMinValuesThreshold));

        // fragment mass errors: one value per target peptide (its median |ppm|), since the ions of one spectrum are
        // correlated; smaller is better
        _tests.Add(new KolmogorovSmirnovTest("Peptide-AbsoluteFragmentPpmErrors",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_MedianAbsoluteFragmentPpmErrorsTargets,
            r => r.Peptide_MedianAbsoluteFragmentPpmErrorsTargets.Length >= DistributionMinValuesThreshold,
            KSAlternative.Greater));
        _tests.Add(new SampleMeanTest("Peptide-AbsoluteFragmentPpmError",
            StatisticalEvidenceFamily.Fragmentation,
            r => r.Peptide_MedianAbsoluteFragmentPpmErrorsTargets,
            higherIsBetter: false, DistributionMinValuesThreshold));

        return this;
    }

    public TestSuiteBuilder AddProteinGroupTests()
    {
        // size is the peptide count, not the protein count: a null peptide hit lands in one protein either way, so a
        // 2-protein polyprotein virus is not "1 of 2 proteins" evidence
        _tests.Add(new SizeOffsetPoissonTest("ProteinGroup",
            StatisticalEvidenceFamily.ProteinGroup,
            r => r.ProteinGroupBacterialUnambiguousTargets,
            r => r.TransientPeptideCount));

        _tests.Add(new NegativeBinomialTest<int>("ProteinGroup",
            StatisticalEvidenceFamily.ProteinGroup,
            r => r.ProteinGroupBacterialUnambiguousTargets,
            r => r.ProteinGroupBacterialUnambiguousTargets > 0));

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

        _tests.Add(new SampleMeanTest("PeptidesPerProteinGroup",
            StatisticalEvidenceFamily.ProteinGroup,
            r => r.AllPeptidesPerProteinGroup,
            higherIsBetter: true, DistributionMinValuesThreshold));
        _tests.Add(new SampleMeanTest("UniquePeptidesPerProteinGroup",
            StatisticalEvidenceFamily.ProteinGroup,
            r => r.AllUniquePeptidesPerProteinGroup,
            higherIsBetter: true, DistributionMinValuesThreshold));
        _tests.Add(new SampleMeanTest("PsmsPerProteinGroup",
            StatisticalEvidenceFamily.ProteinGroup,
            r => r.AllPsmsPerProteinGroup,
            higherIsBetter: true, DistributionMinValuesThreshold));
        _tests.Add(new SampleMeanTest("SequenceCoverageFraction",
            StatisticalEvidenceFamily.ProteinGroup,
            r => r.AllSequenceCoverageFractions,
            higherIsBetter: true, DistributionMinValuesThreshold));

        return this;
    }

    /// <summary>
    /// Precursor evidence. A higher deconvolution score is better (mzLib keeps the highest-scoring envelope); the median
    /// test used to take the lower tail, the opposite of the KS test on the same scores. Mass error is absolute, smaller
    /// is better.
    /// </summary>
    public TestSuiteBuilder AddPrecursorDeconvolutionTests()
    {
        _tests.Add(new KolmogorovSmirnovTest("Peptide-PrecursorDeconScores",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PeptidePrecursorDeconScores,
            r => r.PeptidePrecursorDeconScores.Length >= DistributionMinValuesThreshold, KSAlternative.Less));
        _tests.Add(new KolmogorovSmirnovTest("Peptide-AbsolutePrecursorMassErrors",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => Absolute(r.PeptidePrecursorMassErrors),
            r => r.PeptidePrecursorMassErrors.Length >= DistributionMinValuesThreshold, KSAlternative.Greater));
        _tests.Add(new KolmogorovSmirnovTest("Peptide-PrecursorEnvelopePeakCounts",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PeptidePrecursorEnvelopePeakCounts.ToArray(),
            r => r.PeptidePrecursorEnvelopePeakCounts.Length >= DistributionMinValuesThreshold, KSAlternative.Less));
        _tests.Add(new KolmogorovSmirnovTest("Peptide-PrecursorFractionalIntensities",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PeptidePrecursorFractionalIntensities,
            r => r.PeptidePrecursorFractionalIntensities.Length >= DistributionMinValuesThreshold, KSAlternative.Less));

        _tests.Add(new SampleMeanTest("Peptide-PrecursorDeconScore",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PeptidePrecursorDeconScores,
            higherIsBetter: true, DistributionMinValuesThreshold));
        _tests.Add(new SampleMeanTest("Peptide-AbsolutePrecursorMassError",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => Absolute(r.PeptidePrecursorMassErrors),
            higherIsBetter: false, DistributionMinValuesThreshold));
        _tests.Add(new SampleMeanTest("Peptide-PrecursorEnvelopePeakCount",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PeptidePrecursorEnvelopePeakCounts.ToArray(),
            higherIsBetter: true, DistributionMinValuesThreshold));
        _tests.Add(new SampleMeanTest("Peptide-PrecursorFractionalIntensity",
            StatisticalEvidenceFamily.PrecursorDeconvolution,
            r => r.PeptidePrecursorFractionalIntensities,
            higherIsBetter: true, DistributionMinValuesThreshold));

        return this;
    }

    /// <summary>
    /// De novo tests. Prediction and target counts have no rate or permutation test: one short peptide can be
    /// predicted hundreds of times, so they are as clustered as PSM counts (see <see cref="AddCountEnrichmentTests"/>).
    /// Mapped peptides and proteins are distinct counts and get <see cref="SizeOffsetPoissonTest"/>. The RT and score
    /// arrays are per prediction, so repeat predictions of one peptide still count separately here; that remains to fix.
    /// </summary>
    public TestSuiteBuilder AddDeNovoTests()
    {
        _tests.Add(new NegativeBinomialTest<int>("DeNovo-Predictions",
            StatisticalEvidenceFamily.DeNovo,
            r => r.DeNovoTestedPredictions,
            r => r.DeNovoTestedPredictions > 0));
        _tests.Add(new NegativeBinomialTest<int>("DeNovo-Targets",
            StatisticalEvidenceFamily.DeNovo,
            r => r.DeNovoTestedTargets,
            r => r.DeNovoTestedTargets > 0));

        _tests.Add(new NegativeBinomialTest<int>("DeNovo-MappedPeptides",
            StatisticalEvidenceFamily.DeNovo,
            r => r.DeNovoTestedUniquePeptides,
            r => r.DeNovoTestedPredictions > 0));
        _tests.Add(new SizeOffsetPoissonTest("DeNovo-MappedPeptides",
            StatisticalEvidenceFamily.DeNovo,
            r => r.DeNovoTestedUniquePeptides,
            r => r.TransientPeptideCount));

        _tests.Add(new NegativeBinomialTest<int>("DeNovo-MappedProteins",
            StatisticalEvidenceFamily.DeNovo,
            r => r.DeNovoTestedUniqueProteins,
            r => r.DeNovoTestedPredictions > 0));
        _tests.Add(new SizeOffsetPoissonTest("DeNovo-MappedProteins",
            StatisticalEvidenceFamily.DeNovo,
            r => r.DeNovoTestedUniqueProteins,
            r => r.TransientPeptideCount));

        // Retention time: absolute error, smaller is better
        _tests.Add(new KolmogorovSmirnovTest("DeNovo-AbsoluteRtErrors",
            StatisticalEvidenceFamily.DeNovo,
            r => Absolute(r.DeNovoTestedRetentionTimeErrors),
            r => r.DeNovoTestedRetentionTimeErrors.Length >= DistributionMinValuesThreshold,
            KSAlternative.Greater));
        _tests.Add(new SampleMeanTest("DeNovo-AbsoluteRtError",
            StatisticalEvidenceFamily.DeNovo,
            r => Absolute(r.DeNovoTestedRetentionTimeErrors),
            higherIsBetter: false, DistributionMinValuesThreshold));

        // Scoring
        _tests.Add(new KolmogorovSmirnovTest("DeNovo-Scores",
            StatisticalEvidenceFamily.DeNovo,
            r => r.DeNovoTestedPredictionScores,
            r => r.DeNovoTestedPredictionScores.Length >= DistributionMinValuesThreshold));
        _tests.Add(new SampleMeanTest("DeNovo-PredictionScore",
            StatisticalEvidenceFamily.DeNovo,
            r => r.DeNovoTestedPredictionScores,
            higherIsBetter: true, DistributionMinValuesThreshold));
        _tests.Add(new KolmogorovSmirnovTest("DeNovo-NormalizedScores",
            StatisticalEvidenceFamily.DeNovo,
            r => DeNovoMappingCollector.NormalizeScores(r.DeNovoTestedPredictionScores).ToArray(),
            r => r.DeNovoTestedPredictionScores.Length >= DistributionMinValuesThreshold));
        _tests.Add(new SampleMeanTest("DeNovo-NormalizedPredictionScore",
            StatisticalEvidenceFamily.DeNovo,
            r => DeNovoMappingCollector.NormalizeScores(r.DeNovoTestedPredictionScores).ToArray(),
            higherIsBetter: true, DistributionMinValuesThreshold));

        // Target Decoy
        _tests.Add(new FisherExactTest("DeNovo-TargetDecoy",
            StatisticalEvidenceFamily.DeNovo,
            r => r.DeNovoTestedTargets,
            r => r.DeNovoTestedDecoys,
            r => r.DeNovoTestedTargets > 0 || r.DeNovoTestedDecoys > 0));

        // Host ambiguity: more hits only this database explains than hits the host explains too, as the PSM test does
        _tests.Add(new FisherExactTest("DeNovo-HostAmbiguity",
            StatisticalEvidenceFamily.DeNovo,
            r => r.UnambiguousTargetPredictions,
            r => r.AmbiguousTargetPredictions,
            r => r.DeNovoHasHost && r.TargetPredictions > 0));

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
