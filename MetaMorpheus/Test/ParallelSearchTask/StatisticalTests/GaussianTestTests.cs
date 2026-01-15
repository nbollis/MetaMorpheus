using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Statistics;
using Test.ParallelSearchTask.Utility;

namespace Test.ParallelSearchTask.StatisticalTests;

/// <summary>
/// Tests for Gaussian distribution test
/// Verifies: correctness of p-values, higher signal = lower p-value, edge cases
/// </summary>
[TestFixture]
public class GaussianTestTests
{
    private const double Tolerance = 0.01; // More lenient for statistical tests

    [Test]
    public void ForPsm_ReturnsCorrectMetricName()
    {
        var test = GaussianTest<int>.ForPsm();
        
        Assert.That(test.TestName, Is.EqualTo("Gaussian"));
        Assert.That(test.MetricName, Is.EqualTo("PSM"));
    }

    [Test]
    public void ForPeptide_ReturnsCorrectMetricName()
    {
        var test = GaussianTest<int>.ForPeptide();
        
        Assert.That(test.TestName, Is.EqualTo("Gaussian"));
        Assert.That(test.MetricName, Is.EqualTo("Peptide"));
    }

    [Test]
    public void ForProteinGroup_ReturnsCorrectMetricName()
    {
        var test = GaussianTest<int>.ForProteinGroup();
        
        Assert.That(test.TestName, Is.EqualTo("Gaussian"));
        Assert.That(test.MetricName, Is.EqualTo("ProteinGroup"));
    }

    [Test]
    public void CanRun_WithSufficientData_ReturnsTrue()
    {
        var results = TestDataFactory.CreateGaussianDistribution(count: 50, mean: 20, stdDev: 5);
        var test = GaussianTest<int>.ForPsm();

        bool canRun = test.CanRun(results);

        Assert.That(canRun, Is.True);
    }

    [Test]
    public void CanRun_WithInsufficientData_ReturnsFalse()
    {
        var results = TestDataFactory.EdgeCases.SingleObservation();
        var test = GaussianTest<int>.ForPsm();

        bool canRun = test.CanRun(results);

        Assert.That(canRun, Is.False);
    }

    [Test]
    public void CanRun_WithNullInput_ReturnsFalse()
    {
        var test = GaussianTest<int>.ForPsm();
        bool canRun = test.CanRun(null);

        Assert.That(canRun, Is.False);
    }

    [Test]
    public void ComputePValues_GaussianDistribution_ReturnsValidPValues()
    {
        var results = TestDataFactory.CreateGaussianDistribution(count: 100, mean: 50, stdDev: 10);
        var test = GaussianTest<int>.ForPsm();

        var pValues = test.ComputePValues(results);

        Assert.That(pValues.Count, Is.EqualTo(100));
        
        // All p-values should be valid probabilities
        foreach (var pValue in pValues.Values)
        {
            Assert.That(pValue, Is.GreaterThanOrEqualTo(0.0));
            Assert.That(pValue, Is.LessThanOrEqualTo(1.0));
            Assert.That(double.IsNaN(pValue), Is.False);
        }
    }

    [Test]
    public void ComputePValues_HigherCounts_LowerPValues()
    {
        // Create data where some organisms have clearly higher counts
        var results = new List<AggregatedAnalysisResult>
        {
            TestDataFactory.CreateBasicResult("Low1", psmCount: 10),
            TestDataFactory.CreateBasicResult("Low2", psmCount: 12),
            TestDataFactory.CreateBasicResult("Low3", psmCount: 15),
            TestDataFactory.CreateBasicResult("High1", psmCount: 100),
            TestDataFactory.CreateBasicResult("High2", psmCount: 110)
        };

        var test = GaussianTest<int>.ForPsm();
        var pValues = test.ComputePValues(results);

        // High count organisms should have lower (more significant) p-values
        Assert.That(pValues["High1"], Is.LessThan(pValues["Low1"]));
        Assert.That(pValues["High2"], Is.LessThan(pValues["Low2"]));
    }

    [Test]
    public void ComputePValues_AtMean_ApproximatelyPointFive()
    {
        // Values at the mean should have p-value around 0.5
        var results = TestDataFactory.CreateGaussianDistribution(count: 50, mean: 100, stdDev: 10);
        
        // Add a result exactly at the mean
        results.Add(TestDataFactory.CreateBasicResult("AtMean", psmCount: 100));

        var test = GaussianTest<int>.ForPsm();
        var pValues = test.ComputePValues(results);

        // P-value for mean should be approximately 0.5 (P(X >= mean) = 0.5 for normal)
        Assert.That(pValues["AtMean"], Is.EqualTo(0.5).Within(0.1));
    }

    [Test]
    public void ComputePValues_OutlierHighCount_VeryLowPValue()
    {
        var results = TestDataFactory.EdgeCases.SingleOutlier(count: 50);
        var test = GaussianTest<int>.ForPsm();

        var pValues = test.ComputePValues(results);

        // The outlier should have very low p-value
        var outlierPValue = pValues["Outlier"];
        Assert.That(outlierPValue, Is.LessThan(0.001), 
            "Outlier should have very significant p-value");
    }

    [Test]
    public void ComputePValues_AllSameValue_AllSamePValue()
    {
        var results = TestDataFactory.EdgeCases.AllSameValue(count: 10, value: 50);
        var test = GaussianTest<int>.ForPsm();

        var pValues = test.ComputePValues(results);

        // All should have identical p-values (around 0.5)
        var firstPValue = pValues.Values.First();
        foreach (var pValue in pValues.Values)
        {
            Assert.That(pValue, Is.EqualTo(firstPValue).Within(Tolerance));
        }
    }

    [Test]
    public void ComputePValues_AllZeros_HandlesGracefully()
    {
        var results = TestDataFactory.EdgeCases.AllZeros(count: 10);
        var test = GaussianTest<int>.ForPsm();

        // Should not throw
        Assert.DoesNotThrow(() =>
        {
            var pValues = test.ComputePValues(results);
            
            // Should return valid p-values (even if all same)
            Assert.That(pValues.Count, Is.EqualTo(10));
        });
    }

    [Test]
    public void ComputePValues_Monotonicity_HigherCountsLowerPValues()
    {
        // Create ordered results
        var results = new List<AggregatedAnalysisResult>();
        for (int i = 0; i < 10; i++)
        {
            results.Add(TestDataFactory.CreateBasicResult($"DB{i}", psmCount: 10 + i * 5));
        }

        var test = GaussianTest<int>.ForPsm();
        var pValues = test.ComputePValues(results);

        // Check monotonicity: higher counts should have lower p-values
        for (int i = 0; i < 9; i++)
        {
            Assert.That(pValues[$"DB{i + 1}"], Is.LessThanOrEqualTo(pValues[$"DB{i}"]),
                $"P-value should decrease with increasing count");
        }
    }

    [Test]
    public void ComputePValues_ForPeptideMetric_UsesCorrectCounts()
    {
        var results = new List<AggregatedAnalysisResult>
        {
            new() 
            { 
                DatabaseName = "DB1",
                TargetPsmsFromTransientDbAtQValueThreshold = 100,
                TargetPeptidesFromTransientDbAtQValueThreshold = 50, // This should be used
                TargetProteinGroupsFromTransientDbAtQValueThreshold = 20,
                TransientProteinCount = 100,
                TransientPeptideCount = 250
            },
            new() 
            { 
                DatabaseName = "DB2",
                TargetPsmsFromTransientDbAtQValueThreshold = 50,
                TargetPeptidesFromTransientDbAtQValueThreshold = 100, // This should be used
                TargetProteinGroupsFromTransientDbAtQValueThreshold = 10,
                TransientProteinCount = 100,
                TransientPeptideCount = 250
            }
        };

        var testPsm = GaussianTest<int>.ForPsm();
        var testPeptide = GaussianTest<int>.ForPeptide();

        var pValuesPsm = testPsm.ComputePValues(results);
        var pValuesPeptide = testPeptide.ComputePValues(results);

        // DB1 has more PSMs, DB2 has more peptides
        // So their relative significance should flip between the two tests
        bool psmRelation = pValuesPsm["DB1"] < pValuesPsm["DB2"];
        bool peptideRelation = pValuesPeptide["DB1"] > pValuesPeptide["DB2"];

        Assert.That(psmRelation, Is.Not.EqualTo(peptideRelation), 
            "Different metrics should produce different relative rankings");
    }

    [Test]
    public void ComputePValues_LargeNumbers_NumericalStability()
    {
        var results = TestDataFactory.EdgeCases.LargeNumbers(count: 20);
        var test = GaussianTest<int>.ForPsm();

        var pValues = test.ComputePValues(results);

        foreach (var pValue in pValues.Values)
        {
            Assert.That(double.IsNaN(pValue), Is.False, "Should not produce NaN");
            Assert.That(double.IsInfinity(pValue), Is.False, "Should not produce Infinity");
            Assert.That(pValue, Is.InRange(0.0, 1.0), "P-value should be valid probability");
        }
    }

    [Test]
    public void ComputePValues_HighSignalScenario_DetectsSignificantOrganisms()
    {
        var results = TestDataFactory.CreateHighSignalScenario(count: 50);
        var test = GaussianTest<int>.ForPsm();

        var pValues = test.ComputePValues(results);

        // High signal organisms should have p < 0.05
        var highSignalPValues = pValues
            .Where(kvp => kvp.Key.StartsWith("HighSignal"))
            .Select(kvp => kvp.Value)
            .ToList();

        // At least some high signal organisms should be detected
        int significantCount = highSignalPValues.Count(p => p < 0.05);
        Assert.That(significantCount, Is.GreaterThan(0), 
            "Should detect at least some high-signal organisms");
    }

    [Test]
    public void ComputePValues_NullScenario_MostNotSignificant()
    {
        var results = TestDataFactory.CreateNullScenario(count: 50);
        var test = GaussianTest<int>.ForPsm();

        var pValues = test.ComputePValues(results);

        // In null scenario, most should not be significant
        int significantCount = pValues.Values.Count(p => p < 0.05);
        double significantProportion = (double)significantCount / pValues.Count;

        // Should be close to 5% (expected false positive rate)
        Assert.That(significantProportion, Is.LessThan(0.15), 
            "Null scenario should have few significant results");
    }

    [Test]
    public void ComputePValues_ReproducibleWithSameData()
    {
        var results = TestDataFactory.CreateGaussianDistribution(count: 50);
        var test = GaussianTest<int>.ForPsm();

        var pValues1 = test.ComputePValues(results);
        var pValues2 = test.ComputePValues(results);

        // Should be deterministic
        foreach (var kvp in pValues1)
        {
            Assert.That(pValues2[kvp.Key], Is.EqualTo(kvp.Value).Within(1e-10));
        }
    }

    [Test]
    public void Description_IsInformative()
    {
        var test = GaussianTest<int>.ForPsm();
        
        Assert.That(test.Description, Is.Not.Empty);
        Assert.That(test.Description.ToLower(), Does.Contain("gaussian"));
        Assert.That(test.Description.ToLower(), Does.Contain("psm"));
    }

    [Test]
    public void ComputePValues_MixedScale_HandlesCorrectly()
    {
        var results = TestDataFactory.EdgeCases.MixedScale(count: 20);
        var test = GaussianTest<int>.ForPsm();

        var pValues = test.ComputePValues(results);

        // Large values should have very low p-values
        var largePValues = pValues
            .Where(kvp => kvp.Key.StartsWith("Large"))
            .Select(kvp => kvp.Value);

        foreach (var pValue in largePValues)
        {
            Assert.That(pValue, Is.LessThan(0.001), 
                "Large values should be highly significant");
        }
    }

    [Test]
    public void ComputePValues_Performance_CompletesQuickly()
    {
        var results = TestDataFactory.CreateGaussianDistribution(count: 10000);
        var test = GaussianTest<int>.ForPsm();

        var startTime = DateTime.Now;
        var pValues = test.ComputePValues(results);
        var elapsed = DateTime.Now - startTime;

        Assert.That(pValues.Count, Is.EqualTo(10000));
        Assert.That(elapsed.TotalSeconds, Is.LessThan(0.5), 
            "Should compute 10,000 p-values in < 0.5 seconds");
    }
}
