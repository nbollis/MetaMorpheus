#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskLayer.ParallelSearchTask.Analysis.Statistics.Tests;

/// <summary>
/// Permutation test using DECOY counts to model random noise
/// Tests if observed TARGET counts exceed what would be expected by random chance
/// </summary>
public class PermutationTest : StatisticalTestBase
{
    private readonly string _metricName;
    private readonly Func<AggregatedAnalysisResult, int> _targetExtractor;
    private readonly Func<AggregatedAnalysisResult, int> _decoyExtractor;
    private readonly int _iterations;
    private readonly Random _random;

    public override string TestName => "Permutation";
    public override string MetricName => _metricName;
    public override string Description =>
        $"Tests if {_metricName} target counts exceed decoy-based null distribution via permutation";

    public PermutationTest(
        string metricName,
        Func<AggregatedAnalysisResult, int> targetExtractor,
        Func<AggregatedAnalysisResult, int> decoyExtractor,
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
    public static PermutationTest ForPsm(int iterations = 1000) =>
        new("PSM",
            r => r.PsmBacterialUnambiguousTargets,
            r => r.PsmBacterialUnambiguousDecoys,
            iterations);

    public static PermutationTest ForPeptide(int iterations = 1000) =>
        new("Peptide",
            r => r.PeptideBacterialUnambiguousTargets,
            r => r.PeptideBacterialUnambiguousDecoys,
            iterations);

    public static PermutationTest ForProteinGroup(int iterations = 1000) =>
        new("ProteinGroup",
            r => r.ProteinGroupBacterialUnambiguousTargets,
            r => r.ProteinGroupBacterialUnambiguousDecoys,
            iterations);

    protected override int GetObservedCount(AggregatedAnalysisResult result)
    {
        return _targetExtractor(result);
    }

    public override bool CanRun(List<AggregatedAnalysisResult> allResults)
    {
        if (allResults == null || allResults.Count < 2)
            return false;

        // Need at least some decoy hits to build null distribution
        int totalDecoys = allResults.Sum(r => _decoyExtractor(r));
        return totalDecoys > 0;
    }

    public override Dictionary<string, double> ComputePValues(List<AggregatedAnalysisResult> allResults)
    {
        // Extract target and decoy counts
        var targetCounts = allResults.Select(r => _targetExtractor(r)).ToArray();
        var decoyCounts = allResults.Select(r => _decoyExtractor(r)).ToArray();
        var dbSizes = allResults.Select(r => r.TransientProteinCount).ToArray();

        int nOrganisms = allResults.Count;
        int totalDecoys = decoyCounts.Sum();
        int totalTargets = targetCounts.Sum();

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
                pValues[allResults[i].DatabaseName] = targetCounts[i] > 0 ? 0.001 : 1.0;
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
            int observed = targetCounts[i];
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
