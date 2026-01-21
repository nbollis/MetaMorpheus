#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TaskLayer.ParallelSearch.Analysis;
using static Nett.TomlObjectFactory;

namespace TaskLayer.ParallelSearch.Statistics;

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
public class PermutationTest<TNumeric>(
    string metricName,
    Func<TransientDatabaseMetrics, TNumeric> targetExtractor,
    int iterations = 1000,
    Func<TransientDatabaseMetrics, bool>? shouldSkip = null,
    int? seed = null)
    : StatisticalTestBase(metricName, shouldSkip: shouldSkip)
    where TNumeric : INumber<TNumeric>
{
    private readonly Random _random = seed.HasValue ? new Random(seed.Value) : new Random(42);

    public override string TestName => "Permutation";
    public override string Description =>
        $"Tests if {MetricName} counts exceed null distribution via size-weighted permutation";

    public override double GetTestValue(TransientDatabaseMetrics result) => ToDouble(targetExtractor(result));

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
            iterations,
            shouldSkip: r => r.TargetPsmsFromTransientDbAtQValueThreshold < 2);

    public static PermutationTest<double> ForPsmBidirectional(int iterations = 1000) =>
        new("PSM-Bidirectional", 
            r => r.Psm_Bidirectional_MedianTargets,
            iterations,
            shouldSkip: r => r.TargetPsmsFromTransientDbAtQValueThreshold < 2);

    public static PermutationTest<double> ForPsmSequenceCoverage(int iterations = 1000) =>
        new("PSM-SequenceCoverage", 
            r => r.Psm_SequenceCoverageFraction_MedianTargets,
            iterations,
            shouldSkip: r => r.TargetPsmsFromTransientDbAtQValueThreshold < 2);

    public static PermutationTest<double> ForPeptideComplementary(int iterations = 1000) =>
        new("Peptide-Complementary", 
            r => r.Peptide_ComplementaryCount_MedianTargets,
            iterations,
            shouldSkip: r => r.TargetPeptidesFromTransientDbAtQValueThreshold < 2);

    public static PermutationTest<double> ForPeptideBidirectional(int iterations = 1000) =>
        new("Peptide-Bidirectional", 
            r => r.Peptide_Bidirectional_MedianTargets,
            iterations,
            shouldSkip: r => r.TargetPeptidesFromTransientDbAtQValueThreshold < 2);

    public static PermutationTest<double> ForPeptideSequenceCoverage(int iterations = 1000) =>
        new("Peptide-SequenceCoverage", 
            r => r.Peptide_SequenceCoverageFraction_MedianTargets,
            iterations,
            shouldSkip: r => r.TargetPeptidesFromTransientDbAtQValueThreshold < 2);

    #endregion

    public override bool CanRun(List<TransientDatabaseMetrics> allResults)
    {
        if (allResults == null || allResults.Count < 2)
            return false;

        // Need at least some observations to test
        double totalObservations = allResults.Sum(r => ToDouble(targetExtractor(r)));
        return totalObservations > 0;
    }

    public override Dictionary<string, double> ComputePValues(List<TransientDatabaseMetrics> allResults)
    {
        var pValues = allResults.ToDictionary(p => p.DatabaseName, p => double.NaN);

        // Extract observed counts for each organism
        var observedCounts = new List<double>();
        var dbSizes = new List<int>();
        foreach (var result in allResults)
        {
            if (ShouldSkip != null && ShouldSkip(result))
                continue;

            observedCounts.Add(ToDouble(targetExtractor(result)));
            dbSizes.Add(result.TransientProteinCount);
        }

        int nOrganisms = observedCounts.Count;
        double totalObservations = observedCounts.Sum();

        Console.WriteLine($"{MetricName} Permutation Test:");
        Console.WriteLine($"  Organisms: {nOrganisms}");
        Console.WriteLine($"  Total observations: {totalObservations}");
        Console.WriteLine($"  Iterations: {iterations}");

        // Calculate sampling probabilities proportional to database size
        double totalSize = dbSizes.Sum();
        if (totalSize == 0)
        {
            Console.WriteLine("  ERROR: Total database size is zero!");
            // Fallback to uniform distribution
            double[] uniformProbs = Enumerable.Repeat(1.0 / nOrganisms, nOrganisms).ToArray();
            return ComputePValuesWithProbabilities(allResults, observedCounts, uniformProbs, totalObservations, pValues);
        }

        double[] sizeProbs = dbSizes.Select(s => s / totalSize).ToArray();

        Console.WriteLine($"  Database size range: {dbSizes.Min()} - {dbSizes.Max()} proteins");
        Console.WriteLine($"  Probability range: {sizeProbs.Min():F4} - {sizeProbs.Max():F4}");

        return ComputePValuesWithProbabilities(allResults, observedCounts, sizeProbs, totalObservations, pValues);
    }

    /// <summary>
    /// Core permutation test implementation
    /// </summary>
    private Dictionary<string, double> ComputePValuesWithProbabilities(
        List<TransientDatabaseMetrics> allResults,
        List<double> observedCounts,
        double[] sizeProbs,
        double totalObservations, Dictionary<string, double> pValues)
    {
        // For continuous values (medians), we need to sample the actual observations
        bool isContinuous = observedCounts.Any(c => c != Math.Floor(c));

        if (isContinuous)
        {
            return ComputePValuesForContinuous(allResults, observedCounts, sizeProbs, pValues);
        }
        else
        {
            return ComputePValuesForCounts(allResults, observedCounts, sizeProbs, (int)Math.Round(totalObservations), pValues);
        }
    }

    /// <summary>
    /// Permutation test for count data (PSMs, peptides, protein groups)
    /// Redistributes discrete observations across organisms using efficient multinomial sampling
    /// </summary>
    private Dictionary<string, double> ComputePValuesForCounts(
        List<TransientDatabaseMetrics> allResults,
        List<double> observedCounts,
        double[] sizeProbs,
        int totalObservations, Dictionary<string, double> pValueDict)
    {
        int nOrganisms = observedCounts.Count;

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

        for (int iter = 0; iter < iterations; iter++)
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

        int obsIndex = 0;
        for (int resultIndex = 0; resultIndex < allResults.Count; resultIndex++)
        {
            // We did not add this results observed count and should skip setting P. 
            if (ShouldSkip != null && ShouldSkip(allResults[resultIndex]))
                continue;

            // P-value with continuity correction: minimum p-value is 1/(n+1)
            double pValue = Math.Max((double)countExceedsOrEquals[obsIndex] / iterations, 1.0 / (iterations + 1));

            // Clamp p-value to valid range
            pValue = Math.Max(1e-300, Math.Min(1.0, pValue));

            pValueDict[allResults[resultIndex].DatabaseName] = pValue;

            // Debug output for extreme cases
            if (pValue < 0.001 || observedCounts[obsIndex] > totalObservations * sizeProbs[obsIndex] * 2)
            {
                double expectedMean = totalObservations * sizeProbs[obsIndex];
                double expectedStd = Math.Sqrt(totalObservations * sizeProbs[obsIndex] * (1 - sizeProbs[obsIndex]));
                Console.WriteLine($"    {allResults[resultIndex].DatabaseName}: obs={observedCounts[obsIndex]:F1}, " +
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
        List<TransientDatabaseMetrics> allResults,
        List<double> observedValues,
        double[] sizeProbs, Dictionary<string, double> pValueDict)
    {
        int nOrganisms = observedValues.Count;

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

        for (int iter = 0; iter < iterations; iter++)
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

        int obsIndex = 0;
        for (int resultIndex = 0; resultIndex < allResults.Count; resultIndex++)
        {            
            // We did not add this results observed count and should skip setting P. 
            if (ShouldSkip != null && ShouldSkip(allResults[resultIndex]))
                continue;

            // P-value with continuity correction
            double pValue = Math.Max((double)countExceedsOrEquals[obsIndex] / iterations, 1.0 / (iterations + 1));
            pValue = Math.Max(1e-300, Math.Min(1.0, pValue));

            pValueDict[allResults[resultIndex].DatabaseName] = pValue;
            obsIndex++;
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
