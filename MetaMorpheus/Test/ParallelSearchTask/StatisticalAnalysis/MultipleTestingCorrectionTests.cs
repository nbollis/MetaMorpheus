using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TaskLayer.ParallelSearch.Statistics;

namespace Test.ParallelSearchTask.StatisticalAnalysis;

/// <summary>
/// Tests for Benjamini-Hochberg FDR correction
/// Verifies: q-values >= p-values, monotonicity, known examples
/// </summary>
[TestFixture]
public class MultipleTestingCorrectionTests
{
    private const double Tolerance = 1e-6;

    [Test]
    public void BenjaminiHochberg_EmptyInput_ReturnsEmptyDictionary()
    {
        var pValues = new Dictionary<string, double>();
        var qValues = MultipleTestingCorrection.BenjaminiHochberg(pValues);

        Assert.That(qValues, Is.Empty);
    }

    [Test]
    public void BenjaminiHochberg_NullInput_ReturnsEmptyDictionary()
    {
        var qValues = MultipleTestingCorrection.BenjaminiHochberg(null);

        Assert.That(qValues, Is.Empty);
    }

    [Test]
    public void BenjaminiHochberg_SingleValue_ReturnsIdentical()
    {
        var pValues = new Dictionary<string, double>
        {
            { "Test1", 0.05 }
        };

        var qValues = MultipleTestingCorrection.BenjaminiHochberg(pValues);

        Assert.That(qValues.Count, Is.EqualTo(1));
        Assert.That(qValues["Test1"], Is.EqualTo(0.05).Within(Tolerance));
    }

    [Test]
    public void BenjaminiHochberg_QValuesGreaterThanOrEqualToPValues()
    {
        var pValues = new Dictionary<string, double>
        {
            { "A", 0.001 },
            { "B", 0.01 },
            { "C", 0.05 },
            { "D", 0.1 },
            { "E", 0.5 }
        };

        var qValues = MultipleTestingCorrection.BenjaminiHochberg(pValues);

        foreach (var kvp in pValues)
        {
            Assert.That(qValues[kvp.Key], Is.GreaterThanOrEqualTo(kvp.Value),
                $"Q-value for {kvp.Key} should be >= p-value");
        }
    }

    [Test]
    public void BenjaminiHochberg_QValuesAreMonotonic()
    {
        var pValues = new Dictionary<string, double>
        {
            { "A", 0.001 },
            { "B", 0.008 },
            { "C", 0.04 },
            { "D", 0.09 },
            { "E", 0.2 }
        };

        var qValues = MultipleTestingCorrection.BenjaminiHochberg(pValues);

        // Sort by p-value
        var sorted = pValues.OrderBy(kvp => kvp.Value).ToList();

        // Check monotonicity of q-values
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            double currentQ = qValues[sorted[i].Key];
            double nextQ = qValues[sorted[i + 1].Key];

            Assert.That(nextQ, Is.GreaterThanOrEqualTo(currentQ),
                $"Q-values should be monotonically non-decreasing when sorted by p-value");
        }
    }

    [Test]
    public void BenjaminiHochberg_AllQValuesCappedAtOne()
    {
        var pValues = new Dictionary<string, double>
        {
            { "A", 0.9 },
            { "B", 0.95 },
            { "C", 0.99 }
        };

        var qValues = MultipleTestingCorrection.BenjaminiHochberg(pValues);

        foreach (var kvp in qValues)
        {
            Assert.That(kvp.Value, Is.LessThanOrEqualTo(1.0),
                $"Q-value for {kvp.Key} should not exceed 1.0");
        }
    }

    [Test]
    public void BenjaminiHochberg_KnownExample()
    {
        // Known example from Benjamini & Hochberg (1995)
        // P-values: 0.01, 0.04, 0.03, 0.005, 0.3
        // Expected q-values at n=5: 0.025, 0.1, 0.075, 0.025, 0.3
        
        var pValues = new Dictionary<string, double>
        {
            { "Test1", 0.005 },
            { "Test2", 0.01 },
            { "Test3", 0.03 },
            { "Test4", 0.04 },
            { "Test5", 0.3 }
        };

        var qValues = MultipleTestingCorrection.BenjaminiHochberg(pValues);

        // Verify specific q-values
        Assert.That(qValues["Test1"], Is.EqualTo(0.025).Within(Tolerance));
        Assert.That(qValues["Test2"], Is.EqualTo(0.025).Within(Tolerance)); // Enforced monotonicity
        Assert.That(qValues["Test3"], Is.EqualTo(0.05).Within(Tolerance));
        Assert.That(qValues["Test4"], Is.EqualTo(0.05).Within(Tolerance)); // Enforced monotonicity
        Assert.That(qValues["Test5"], Is.EqualTo(0.3).Within(Tolerance));
    }

    [Test]
    public void BenjaminiHochberg_AllPValuesZero_ReturnsZeroQValues()
    {
        var pValues = new Dictionary<string, double>
        {
            { "A", 0.0 },
            { "B", 0.0 },
            { "C", 0.0 }
        };

        var qValues = MultipleTestingCorrection.BenjaminiHochberg(pValues);

        foreach (var kvp in qValues)
        {
            Assert.That(kvp.Value, Is.EqualTo(0.0).Within(Tolerance));
        }
    }

    [Test]
    public void BenjaminiHochberg_AllPValuesOne_ReturnsOneQValues()
    {
        var pValues = new Dictionary<string, double>
        {
            { "A", 1.0 },
            { "B", 1.0 },
            { "C", 1.0 }
        };

        var qValues = MultipleTestingCorrection.BenjaminiHochberg(pValues);

        foreach (var kvp in qValues)
        {
            Assert.That(kvp.Value, Is.EqualTo(1.0).Within(Tolerance));
        }
    }

    [Test]
    public void BenjaminiHochberg_LargeNumberOfTests()
    {
        // Test with 1000 p-values
        var pValues = new Dictionary<string, double>();
        for (int i = 0; i < 1000; i++)
        {
            pValues[$"Test{i}"] = i / 1000.0;
        }

        var qValues = MultipleTestingCorrection.BenjaminiHochberg(pValues);

        Assert.That(qValues.Count, Is.EqualTo(1000));
        
        // Verify monotonicity
        var sortedP = pValues.OrderBy(kvp => kvp.Value).ToList();
        for (int i = 0; i < sortedP.Count - 1; i++)
        {
            Assert.That(qValues[sortedP[i + 1].Key], 
                Is.GreaterThanOrEqualTo(qValues[sortedP[i].Key]));
        }
    }

    [Test]
    public void ApplyBenjaminiHochberg_NullResults_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => MultipleTestingCorrection.ApplyBenjaminiHochberg(null));
    }

    [Test]
    public void ApplyBenjaminiHochberg_EmptyResults_DoesNotThrow()
    {
        var results = new List<StatisticalResult>();
        Assert.DoesNotThrow(() => MultipleTestingCorrection.ApplyBenjaminiHochberg(results));
    }

    [Test]
    public void ApplyBenjaminiHochberg_SetsQValuesCorrectly()
    {
        var results = new List<StatisticalResult>
        {
            new() { DatabaseName = "DB1", TestName = "Test1", MetricName = "PSM", PValue = 0.01 },
            new() { DatabaseName = "DB2", TestName = "Test1", MetricName = "PSM", PValue = 0.04 },
            new() { DatabaseName = "DB3", TestName = "Test1", MetricName = "PSM", PValue = 0.3 }
        };

        MultipleTestingCorrection.ApplyBenjaminiHochberg(results);

        foreach (var result in results)
        {
            Assert.That(double.IsNaN(result.QValue), Is.False, 
                $"Q-value should be set for {result.DatabaseName}");
            Assert.That(result.QValue, Is.GreaterThanOrEqualTo(result.PValue));
        }
    }

    [Test]
    public void ApplyBenjaminiHochberg_GroupsByTestAndMetric()
    {
        // Results from different tests should be corrected separately
        var results = new List<StatisticalResult>
        {
            // Test 1, Metric PSM
            new() { DatabaseName = "DB1", TestName = "Test1", MetricName = "PSM", PValue = 0.01 },
            new() { DatabaseName = "DB2", TestName = "Test1", MetricName = "PSM", PValue = 0.5 },
            
            // Test 2, Metric PSM (should be corrected separately)
            new() { DatabaseName = "DB1", TestName = "Test2", MetricName = "PSM", PValue = 0.01 },
            new() { DatabaseName = "DB2", TestName = "Test2", MetricName = "PSM", PValue = 0.5 },
            
            // Test 1, Metric Peptide (should be corrected separately)
            new() { DatabaseName = "DB1", TestName = "Test1", MetricName = "Peptide", PValue = 0.01 },
            new() { DatabaseName = "DB2", TestName = "Test1", MetricName = "Peptide", PValue = 0.5 }
        };

        MultipleTestingCorrection.ApplyBenjaminiHochberg(results);

        // Each group should have corrected q-values
        var test1PsmResults = results.Where(r => r.TestName == "Test1" && r.MetricName == "PSM").ToList();
        Assert.That(test1PsmResults[0].QValue, Is.EqualTo(0.02).Within(Tolerance)); // 0.01 * 2 / 1
        Assert.That(test1PsmResults[1].QValue, Is.EqualTo(1.0).Within(Tolerance)); // 0.5 * 2 / 2 = 1.0, capped
    }

    [Test]
    public void BenjaminiHochberg_VerySmallPValues_NumericalStability()
    {
        // Test with very small p-values
        var pValues = new Dictionary<string, double>
        {
            { "A", 1e-10 },
            { "B", 1e-8 },
            { "C", 1e-6 },
            { "D", 1e-4 },
            { "E", 0.01 }
        };

        var qValues = MultipleTestingCorrection.BenjaminiHochberg(pValues);

        // Should not produce NaN or Infinity
        foreach (var kvp in qValues)
        {
            Assert.That(double.IsNaN(kvp.Value), Is.False);
            Assert.That(double.IsInfinity(kvp.Value), Is.False);
            Assert.That(kvp.Value, Is.GreaterThanOrEqualTo(0.0));
            Assert.That(kvp.Value, Is.LessThanOrEqualTo(1.0));
        }
    }

    [Test]
    public void BenjaminiHochberg_MixedSignificance_CorrectClassification()
    {
        var pValues = new Dictionary<string, double>
        {
            { "Significant1", 0.001 },
            { "Significant2", 0.01 },
            { "Marginal", 0.045 },
            { "NotSig1", 0.1 },
            { "NotSig2", 0.5 }
        };

        var qValues = MultipleTestingCorrection.BenjaminiHochberg(pValues);

        // At alpha = 0.05, check which are significant
        int significantCount = qValues.Count(kvp => kvp.Value < 0.05);

        // Depending on the BH procedure, we expect at least the strong signals to be significant
        Assert.That(significantCount, Is.GreaterThan(0));
        Assert.That(qValues["Significant1"], Is.LessThan(qValues["NotSig1"]));
    }

    [Test]
    public void BenjaminiHochberg_Performance_CompletesQuickly()
    {
        // Test with 10,000 p-values to ensure reasonable performance
        var pValues = new Dictionary<string, double>();
        var random = new Random(42);
        
        for (int i = 0; i < 10000; i++)
        {
            pValues[$"Test{i}"] = random.NextDouble();
        }

        var startTime = DateTime.Now;
        var qValues = MultipleTestingCorrection.BenjaminiHochberg(pValues);
        var elapsedTime = DateTime.Now - startTime;

        Assert.That(qValues.Count, Is.EqualTo(10000));
        Assert.That(elapsedTime.TotalSeconds, Is.LessThan(1.0), 
            "BH correction should complete in less than 1 second for 10,000 tests");
    }
}
