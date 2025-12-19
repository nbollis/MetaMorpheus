#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TaskLayer.ParallelSearchTask.Analysis;

namespace TaskLayer.ParallelSearchTask.Statistics;

/// <summary>
/// Permutation test using DECOY counts to model random noise
/// Tests if observed TARGET counts exceed what would be expected by random chance
/// </summary>
public class PermutationTest<TNumeric> : StatisticalTestBase where TNumeric : INumber<TNumeric>
{
    private readonly string _metricName;
    private readonly Func<AggregatedAnalysisResult, TNumeric> _targetExtractor;
    private readonly Func<AggregatedAnalysisResult, TNumeric> _decoyExtractor;
    private readonly int _iterations;
    private readonly Random _random;

    public override string TestName => "Permutation";
    public override string MetricName => _metricName;
    public override string Description =>
        $"Tests if {_metricName} target counts exceed decoy-based null distribution via permutation";

    public PermutationTest(
        string metricName,
        Func<AggregatedAnalysisResult, TNumeric> targetExtractor,
        Func<AggregatedAnalysisResult, TNumeric> decoyExtractor,
        int iterations = 1000,
        int? seed = null)
    {
        _metricName = metricName;
        _targetExtractor = targetExtractor;
        _decoyExtractor = decoyExtractor;
        _iterations = iterations;
        _random = seed.HasValue ? new Random(seed.Value) : new Random(42);
    }

    // Convenience constructors for common metrics
    public static PermutationTest<int> ForPsm(int iterations = 1000) =>
        new("PSM",
            r => r.PsmBacterialUnambiguousTargets,
            r => r.PsmBacterialUnambiguousDecoys,
            iterations);

    public static PermutationTest<int> ForPeptide(int iterations = 1000) =>
        new("Peptide",
            r => r.PeptideBacterialUnambiguousTargets,
            r => r.PeptideBacterialUnambiguousDecoys,
            iterations);

    public static PermutationTest<int> ForProteinGroup(int iterations = 1000) =>
        new("ProteinGroup",
            r => r.ProteinGroupBacterialUnambiguousTargets,
            r => r.ProteinGroupBacterialUnambiguousDecoys,
            iterations);

    public static PermutationTest<double> ForPsmComplementary(int iterations = 1000) =>
        new("PSM-Complementary", 
            r => r.Psm_ComplementaryCount_MedianTargets,
            r => r.Psm_ComplementaryCount_MedianDecoys,
            iterations);

    public static PermutationTest<double> ForPsmBidirectional(int iterations = 1000) =>
        new("PSM-Bidirectional", 
            r => r.Psm_Bidirectional_MedianTargets,
            r => r.Psm_Bidirectional_MedianDecoys,
            iterations);

    public static PermutationTest<double> ForPsmSequenceCoverage(int iterations = 1000) =>
        new("PSM-SequenceCoverage", 
            r => r.Psm_SequenceCoverageFraction_MedianTargets,
            r => r.Psm_SequenceCoverageFraction_MedianDecoys,
            iterations);

    public static PermutationTest<double> ForPeptideComplementary(int iterations = 1000) =>
        new("Peptide-Complementary", 
            r => r.Peptide_ComplementaryCount_MedianTargets,
            r => r.Peptide_ComplementaryCount_MedianDecoys,
            iterations);

    public static PermutationTest<double> ForPeptideBidirectional(int iterations = 1000) =>
        new("Peptide-Bidirectional", 
            r => r.Peptide_Bidirectional_MedianTargets,
            r => r.Peptide_Bidirectional_MedianDecoys,
            iterations);

    public static PermutationTest<double> ForPeptideSequenceCoverage(int iterations = 1000) =>
        new("Peptide-SequenceCoverage", 
            r => r.Peptide_SequenceCoverageFraction_MedianTargets,
            r => r.Peptide_SequenceCoverageFraction_MedianDecoys,
            iterations);

    public override bool CanRun(List<AggregatedAnalysisResult> allResults)
    {
        if (allResults == null || allResults.Count < 2)
            return false;

        // Need at least some decoy hits to build null distribution
        double totalDecoys = allResults.Sum(r => ToDouble(_decoyExtractor(r)));
        return totalDecoys > 0;
    }

    public override Dictionary<string, double> ComputePValues(List<AggregatedAnalysisResult> allResults)
    {
        // Extract target and decoy counts
        var targetCounts = allResults.Select(r => _targetExtractor(r)).ToArray();
        var decoyCounts = allResults.Select(r => _decoyExtractor(r)).ToArray();
        var dbSizes = allResults.Select(r => r.TransientProteinCount).ToArray();

        int nOrganisms = allResults.Count;
        int totalDecoys = ToInt32(Sum(decoyCounts));
        int totalTargets = ToInt32(Sum(targetCounts));

        Console.WriteLine($"{MetricName} Permutation Test:");
        Console.WriteLine($"  Organisms: {nOrganisms}");
        Console.WriteLine($"  Total TARGET hits: {totalTargets}");
        Console.WriteLine($"  Total DECOY hits: {totalDecoys}");
        Console.WriteLine($"  Iterations: {_iterations}");

        // Handle edge case: no decoys
        if (totalDecoys == 0)
        {
            Console.WriteLine("  WARNING: No decoy hits! Marking all non-zero targets as significant.");
            var pValues = new Dictionary<string, double>();
            for (int i = 0; i < nOrganisms; i++)
            {
                bool isZero = ToDouble(targetCounts[i]) == 0.0;
                pValues[allResults[i].DatabaseName] = isZero ? 1.0 : 0.001;
            }
            return pValues;
        }

        // Calculate sampling probabilities proportional to database size
        double totalSize = dbSizes.Sum();
        double[] sizeProbs = dbSizes.Select(s => s / totalSize).ToArray();

        // Build null distribution by redistributing decoy hits
        int[,] nullDist = new int[_iterations, nOrganisms];

        for (int iter = 0; iter < _iterations; iter++)
        {
            // Randomly assign each decoy hit to an organism (weighted by db size)
            for (int decoy = 0; decoy < totalDecoys; decoy++)
            {
                int assignedOrg = SampleFromDistribution(sizeProbs);
                nullDist[iter, assignedOrg]++;
            }
        }

        // Compute p-values: proportion of null >= observed
        var pValueDict = new Dictionary<string, double>();

        for (int i = 0; i < nOrganisms; i++)
        {
            int observed = ToInt32(targetCounts[i]);
            int countExceedsOrEquals = 0;

            for (int iter = 0; iter < _iterations; iter++)
            {
                if (nullDist[iter, i] >= observed)
                    countExceedsOrEquals++;
            }

            // P-value with continuity correction: minimum p-value is 1/(n+1)
            double pValue = Math.Max((double)countExceedsOrEquals / _iterations, 1.0 / (_iterations + 1));
            pValueDict[allResults[i].DatabaseName] = pValue;
        }

        return pValueDict;
    }

    /// <summary>
    /// Sample from a discrete probability distribution
    /// </summary>
    private int SampleFromDistribution(double[] probabilities)
    {
        double u = _random.NextDouble();
        double cumulative = 0.0;

        for (int i = 0; i < probabilities.Length; i++)
        {
            cumulative += probabilities[i];
            if (u <= cumulative)
                return i;
        }

        return probabilities.Length - 1; // Fallback due to floating point errors
    }
}
