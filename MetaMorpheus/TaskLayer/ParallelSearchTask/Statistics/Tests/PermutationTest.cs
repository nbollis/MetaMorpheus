#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TaskLayer.ParallelSearchTask.Analysis;

namespace TaskLayer.ParallelSearchTask.Statistics;

/// <summary>
/// Permutation test using database size-weighted random redistribution
/// Tests if observed counts exceed what would be expected by random chance
/// given the relative sizes of the transient databases
/// 
/// NULL HYPOTHESIS: Observations are distributed randomly across organisms 
/// proportional to their database sizes (number of proteins or peptides)
/// 
/// ALGORITHM:
/// 1. Observe actual counts for each organism
/// 2. For each permutation iteration:
///    - Redistribute ALL observations randomly across organisms (weighted by DB size)
///    - Record null distribution
/// 3. P-value = proportion of permutations where null >= observed
/// </summary>
public class PermutationTest<TNumeric> : StatisticalTestBase where TNumeric : INumber<TNumeric>
{
    private readonly string _metricName;
    private readonly Func<AggregatedAnalysisResult, TNumeric> _dataPointExtractor;
    private readonly int _iterations;
    private readonly Random _random;

    public override string TestName => "Permutation";
    public override string MetricName => _metricName;
    public override string Description =>
        $"Tests if {_metricName} counts exceed null distribution via size-weighted permutation";

    public PermutationTest(
        string metricName,
        Func<AggregatedAnalysisResult, TNumeric> targetExtractor,
        int iterations = 1000,
        int? seed = null)
    {
        _metricName = metricName;
        _dataPointExtractor = targetExtractor;
        _iterations = iterations;
        _random = seed.HasValue ? new Random(seed.Value) : new Random(42);
    }

    public override double GetTestValue(AggregatedAnalysisResult result) => ToDouble(_dataPointExtractor(result));

    #region Predfined Tests

    // Convenience constructors for common metrics
    public static PermutationTest<double> ForPsm(int iterations = 1000) =>
        new("PSM",
            r => r.PsmBacterialUnambiguousTargets / (double)r.TransientPeptideCount,
            iterations);

    public static PermutationTest<double> ForPeptide(int iterations = 1000) =>
        new("Peptide",
            r => r.PeptideBacterialUnambiguousTargets / (double)r.TransientPeptideCount,
            iterations);

    public static PermutationTest<double> ForProteinGroup(int iterations = 1000) =>
        new("ProteinGroup",
            r => r.ProteinGroupBacterialUnambiguousTargets / (double)r.TransientProteinCount,
            iterations);

    public static PermutationTest<double> ForPsmComplementary(int iterations = 1000) =>
        new("PSM-Complementary", 
            r => r.Psm_ComplementaryCount_MedianTargets,
            iterations);

    public static PermutationTest<double> ForPsmBidirectional(int iterations = 1000) =>
        new("PSM-Bidirectional", 
            r => r.Psm_Bidirectional_MedianTargets,
            iterations);

    public static PermutationTest<double> ForPsmSequenceCoverage(int iterations = 1000) =>
        new("PSM-SequenceCoverage", 
            r => r.Psm_SequenceCoverageFraction_MedianTargets,
            iterations);

    public static PermutationTest<double> ForPeptideComplementary(int iterations = 1000) =>
        new("Peptide-Complementary", 
            r => r.Peptide_ComplementaryCount_MedianTargets,
            iterations);

    public static PermutationTest<double> ForPeptideBidirectional(int iterations = 1000) =>
        new("Peptide-Bidirectional", 
            r => r.Peptide_Bidirectional_MedianTargets,
            iterations);

    public static PermutationTest<double> ForPeptideSequenceCoverage(int iterations = 1000) =>
        new("Peptide-SequenceCoverage", 
            r => r.Peptide_SequenceCoverageFraction_MedianTargets,
            iterations);

    #endregion

    public override bool CanRun(List<AggregatedAnalysisResult> allResults)
    {
        if (allResults == null || allResults.Count < 2)
            return false;

        // Need at least some observations to test
        double totalObservations = allResults.Sum(r => ToDouble(_dataPointExtractor(r)));
        return totalObservations > 0;
    }

    public override Dictionary<string, double> ComputePValues(List<AggregatedAnalysisResult> allResults)
    {
        // Extract observed counts for each organism
        var observedCounts = allResults.Select(r => ToDouble(_dataPointExtractor(r))).ToArray();
        var dbSizes = allResults.Select(r => r.TransientProteinCount).ToArray();

        int nOrganisms = allResults.Count;
        double totalObservations = observedCounts.Sum();

        Console.WriteLine($"{MetricName} Permutation Test:");
        Console.WriteLine($"  Organisms: {nOrganisms}");
        Console.WriteLine($"  Total observations: {totalObservations}");
        Console.WriteLine($"  Iterations: {_iterations}");

        // Handle edge case: no observations
        if (totalObservations == 0)
        {
            Console.WriteLine("  WARNING: No observations! All p-values set to 1.0");
            var emptyPValues = new Dictionary<string, double>();
            for (int i = 0; i < nOrganisms; i++)
            {
                emptyPValues[allResults[i].DatabaseName] = 1.0;
            }
            return emptyPValues;
        }

        // Calculate sampling probabilities proportional to database size
        double totalSize = dbSizes.Sum();
        if (totalSize == 0)
        {
            Console.WriteLine("  ERROR: Total database size is zero!");
            // Fallback to uniform distribution
            double[] uniformProbs = Enumerable.Repeat(1.0 / nOrganisms, nOrganisms).ToArray();
            return ComputePValuesWithProbabilities(allResults, observedCounts, uniformProbs, totalObservations);
        }

        double[] sizeProbs = dbSizes.Select(s => s / totalSize).ToArray();

        Console.WriteLine($"  Database size range: {dbSizes.Min()} - {dbSizes.Max()} proteins");
        Console.WriteLine($"  Probability range: {sizeProbs.Min():F4} - {sizeProbs.Max():F4}");

        return ComputePValuesWithProbabilities(allResults, observedCounts, sizeProbs, totalObservations);
    }

    /// <summary>
    /// Core permutation test implementation
    /// </summary>
    private Dictionary<string, double> ComputePValuesWithProbabilities(
        List<AggregatedAnalysisResult> allResults,
        double[] observedCounts,
        double[] sizeProbs,
        double totalObservations)
    {
        int nOrganisms = allResults.Count;

        // For continuous values (medians), we need to sample the actual observations
        bool isContinuous = observedCounts.Any(c => c != Math.Floor(c));

        if (isContinuous)
        {
            return ComputePValuesForContinuous(allResults, observedCounts, sizeProbs);
        }
        else
        {
            return ComputePValuesForCounts(allResults, observedCounts, sizeProbs, (int)Math.Round(totalObservations));
        }
    }

    /// <summary>
    /// Permutation test for count data (PSMs, peptides, protein groups)
    /// Redistributes discrete observations across organisms using efficient multinomial sampling
    /// </summary>
    private Dictionary<string, double> ComputePValuesForCounts(
        List<AggregatedAnalysisResult> allResults,
        double[] observedCounts,
        double[] sizeProbs,
        int totalObservations)
    {
        int nOrganisms = allResults.Count;

        // Pre-compute cumulative probabilities for binary search (much faster for large nOrganisms)
        double[] cumulativeProbs = new double[nOrganisms];
        cumulativeProbs[0] = sizeProbs[0];
        for (int i = 1; i < nOrganisms; i++)
        {
            cumulativeProbs[i] = cumulativeProbs[i - 1] + sizeProbs[i];
        }

        // OPTIMIZATION: Use multinomial sampling instead of individual observation redistribution
        // This is MUCH faster: O(iterations * nOrganisms) instead of O(iterations * totalObservations * nOrganisms)
        
        // Track count of iterations where null >= observed for each organism
        int[] countExceedsOrEquals = new int[nOrganisms];

        for (int iter = 0; iter < _iterations; iter++)
        {
            // Generate a single multinomial sample: distribute totalObservations across nOrganisms
            int[] nullCounts = SampleMultinomial(totalObservations, cumulativeProbs);

            // Check which organisms have null >= observed
            for (int i = 0; i < nOrganisms; i++)
            {
                if (nullCounts[i] >= observedCounts[i])
                    countExceedsOrEquals[i]++;
            }
        }

        // Compute p-values
        var pValueDict = new Dictionary<string, double>();

        for (int i = 0; i < nOrganisms; i++)
        {
            // P-value with continuity correction: minimum p-value is 1/(n+1)
            double pValue = Math.Max((double)countExceedsOrEquals[i] / _iterations, 1.0 / (_iterations + 1));

            // Clamp p-value to valid range
            pValue = Math.Max(1e-300, Math.Min(1.0, pValue));

            pValueDict[allResults[i].DatabaseName] = pValue;

            // Debug output for extreme cases
            if (pValue < 0.001 || observedCounts[i] > totalObservations * sizeProbs[i] * 2)
            {
                double expectedMean = totalObservations * sizeProbs[i];
                double expectedStd = Math.Sqrt(totalObservations * sizeProbs[i] * (1 - sizeProbs[i]));
                Console.WriteLine($"    {allResults[i].DatabaseName}: obs={observedCounts[i]:F1}, " +
                                $"expected μ={expectedMean:F1}±{expectedStd:F1}, p={pValue:E3}");
            }
        }

        return pValueDict;
    }

    /// <summary>
    /// Permutation test for continuous data (medians, means)
    /// Uses bootstrap resampling of organism assignments
    /// </summary>
    private Dictionary<string, double> ComputePValuesForContinuous(
        List<AggregatedAnalysisResult> allResults,
        double[] observedValues,
        double[] sizeProbs)
    {
        int nOrganisms = allResults.Count;

        // Pre-compute cumulative probabilities for binary search
        double[] cumulativeProbs = new double[nOrganisms];
        cumulativeProbs[0] = sizeProbs[0];
        for (int i = 1; i < nOrganisms; i++)
        {
            cumulativeProbs[i] = cumulativeProbs[i - 1] + sizeProbs[i];
        }

        // For continuous metrics, we compare against expected value under null
        // Expected value under null = weighted average across all organisms
        double globalMean = 0;
        double totalWeight = 0;
        for (int i = 0; i < nOrganisms; i++)
        {
            if (!double.IsNaN(observedValues[i]) && !double.IsInfinity(observedValues[i]))
            {
                globalMean += observedValues[i] * sizeProbs[i];
                totalWeight += sizeProbs[i];
            }
        }
        globalMean /= totalWeight;

        Console.WriteLine($"  Global mean (null expectation): {globalMean:F3}");

        // Track count of iterations where null >= observed for each organism
        int[] countExceedsOrEquals = new int[nOrganisms];

        for (int iter = 0; iter < _iterations; iter++)
        {
            // For each organism, sample a value from the global pool weighted by database size
            for (int i = 0; i < nOrganisms; i++)
            {
                // Draw a random organism according to size weights using binary search
                int sampledOrg = SampleFromDistributionFast(cumulativeProbs);
                double nullValue = observedValues[sampledOrg];
                
                if (nullValue >= observedValues[i])
                    countExceedsOrEquals[i]++;
            }
        }

        // Compute p-values
        var pValueDict = new Dictionary<string, double>();

        for (int i = 0; i < nOrganisms; i++)
        {
            // P-value with continuity correction
            double pValue = Math.Max((double)countExceedsOrEquals[i] / _iterations, 1.0 / (_iterations + 1));
            pValue = Math.Max(1e-300, Math.Min(1.0, pValue));

            pValueDict[allResults[i].DatabaseName] = pValue;
        }

        return pValueDict;
    }

    /// <summary>
    /// Efficiently sample from multinomial distribution
    /// Returns counts for each category after totalObservations draws
    /// Much faster than drawing individual samples: O(n + k) instead of O(n*k)
    /// where n = totalObservations, k = number of categories
    /// </summary>
    private int[] SampleMultinomial(int totalObservations, double[] cumulativeProbs)
    {
        int nCategories = cumulativeProbs.Length;
        int[] counts = new int[nCategories];

        for (int i = 0; i < totalObservations; i++)
        {
            int category = SampleFromDistributionFast(cumulativeProbs);
            counts[category]++;
        }

        return counts;
    }

    /// <summary>
    /// Fast sampling using binary search on cumulative probabilities
    /// O(log n) instead of O(n) for linear search
    /// Critical optimization for large numbers of organisms
    /// </summary>
    private int SampleFromDistributionFast(double[] cumulativeProbs)
    {
        double u = _random.NextDouble();
        
        // Binary search in cumulative probability array
        int left = 0;
        int right = cumulativeProbs.Length - 1;
        
        while (left < right)
        {
            int mid = (left + right) / 2;
            if (u <= cumulativeProbs[mid])
                right = mid;
            else
                left = mid + 1;
        }
        
        return left;
    }
}
