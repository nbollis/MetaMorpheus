using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TaskLayer.ParallelSearchTask.Analysis;
using TaskLayer.ParallelSearchTask.Statistics;
using Test.ParallelSearchTask.Utility;

namespace Test.ParallelSearchTask.StatisticalTests;

/// <summary>
/// Tests for Permutation test (decoy-based null distribution)
/// Verifies: reproducibility, p-value properties, minimum p-value enforcement
/// Note: PermutationTest uses a fixed seed by default for reproducibility
/// </summary>
[TestFixture]
public class PermutationTestTests
{
    private const double Tolerance = 0.05; // More lenient for stochastic tests
    private const int TestIterations = 1000;

    [Test]
    public void ForPsm_ReturnsCorrectMetricName()
    {
        var test = PermutationTest<int>.ForPsm(iterations: 100);
        
        Assert.That(test.TestName, Is.EqualTo("Permutation"));
        Assert.That(test.MetricName, Is.EqualTo("PSM"));
    }

    [Test]
    public void ForPeptide_ReturnsCorrectMetricName()
    {
        var test = PermutationTest<int>.ForPeptide(iterations: 100);
        
        Assert.That(test.TestName, Is.EqualTo("Permutation"));
        Assert.That(test.MetricName, Is.EqualTo("Peptide"));
    }

    [Test]
    public void ForProteinGroup_ReturnsCorrectMetricName()
    {
        var test = PermutationTest<int>.ForProteinGroup(iterations: 100);
        
        Assert.That(test.TestName, Is.EqualTo("Permutation"));
        Assert.That(test.MetricName, Is.EqualTo("ProteinGroup"));
    }

    [Test]
    public void CanRun_WithSufficientData_ReturnsTrue()
    {
        var results = new List<AggregatedAnalysisResult>
        {
            TestDataFactory.CreateRealisticResult("DB1", 
                targetPsms: 50, decoyPsms: 10,
                targetPeptides: 25, decoyPeptides: 5),
            TestDataFactory.CreateRealisticResult("DB2", 
                targetPsms: 30, decoyPsms: 8,
                targetPeptides: 15, decoyPeptides: 4)
        };
        var test = PermutationTest<int>.ForPsm(iterations: 100);

        bool canRun = test.CanRun(results);

        Assert.That(canRun, Is.True);
    }

    [Test]
    public void CanRun_WithNoDecoys_ReturnsFalse()
    {
        var results = new List<AggregatedAnalysisResult>
        {
            new() 
            { 
                DatabaseName = "DB1",
                PsmBacterialUnambiguousTargets = 50,
                PsmBacterialUnambiguousDecoys = 0,
                TransientProteinCount = 100
            },
            new() 
            { 
                DatabaseName = "DB2",
                PsmBacterialUnambiguousTargets = 30,
                PsmBacterialUnambiguousDecoys = 0,
                TransientProteinCount = 100
            }
        };

        var test = PermutationTest<int>.ForPsm(iterations: 100);
        bool canRun = test.CanRun(results);

        Assert.That(canRun, Is.False, "Should not run without decoy hits");
    }

    [Test]
    public void CanRun_WithNullInput_ReturnsFalse()
    {
        var test = PermutationTest<int>.ForPsm(iterations: 100);
        bool canRun = test.CanRun(null);

        Assert.That(canRun, Is.False);
    }

    [Test]
    public void CanRun_WithSingleDatabase_ReturnsFalse()
    {
        var results = new List<AggregatedAnalysisResult>
        {
            TestDataFactory.CreateRealisticResult("DB1", 50, 10, 25, 5)
        };

        var test = PermutationTest<int>.ForPsm(iterations: 100);
        bool canRun = test.CanRun(results);

        Assert.That(canRun, Is.False, "Need at least 2 databases");
    }

    [Test]
    public void ComputePValues_Reproducible_WithDefaultSeed()
    {
        var results = TestDataFactory.CreateHighSignalScenario(count: 20);
        
        // Create two test instances - both use default seed of 42
        var test1 = PermutationTest<int>.ForPsm(iterations: 500);
        var test2 = PermutationTest<int>.ForPsm(iterations: 500);

        var pValues1 = test1.ComputePValues(results);
        var pValues2 = test2.ComputePValues(results);

        // Should be identical with default seed
        foreach (var kvp in pValues1)
        {
            Assert.That(pValues2[kvp.Key], Is.EqualTo(kvp.Value).Within(1e-10),
                $"P-values should be identical for {kvp.Key} with default seed");
        }
    }

    [Test]
    public void ComputePValues_ValidProbabilities()
    {
        var results = TestDataFactory.CreateHighSignalScenario(count: 30);
        var test = PermutationTest<int>.ForPsm(iterations: TestIterations);

        var pValues = test.ComputePValues(results);

        foreach (var kvp in pValues)
        {
            Assert.That(kvp.Value, Is.GreaterThanOrEqualTo(0.0), 
                $"P-value for {kvp.Key} should be >= 0");
            Assert.That(kvp.Value, Is.LessThanOrEqualTo(1.0), 
                $"P-value for {kvp.Key} should be <= 1");
            Assert.That(double.IsNaN(kvp.Value), Is.False,
                $"P-value for {kvp.Key} should not be NaN");
        }
    }

    [Test]
    public void ComputePValues_MinimumPValueEnforced()
    {
        // Create data where one organism has many more targets than any decoy permutation
        var results = new List<AggregatedAnalysisResult>
        {
            new() 
            { 
                DatabaseName = "HighSignal",
                PsmBacterialUnambiguousTargets = 500, // Very high
                PsmBacterialUnambiguousDecoys = 2,
                TransientProteinCount = 100
            },
            new() 
            { 
                DatabaseName = "LowSignal",
                PsmBacterialUnambiguousTargets = 5,
                PsmBacterialUnambiguousDecoys = 2,
                TransientProteinCount = 100
            }
        };

        int iterations = 100;
        var test = PermutationTest<int>.ForPsm(iterations: iterations);
        var pValues = test.ComputePValues(results);

        // Minimum p-value should be 1/(n+1)
        double minPValue = 1.0 / (iterations + 1);
        Assert.That(pValues["HighSignal"], Is.GreaterThanOrEqualTo(minPValue),
            "P-value should not be less than 1/(iterations+1)");
    }

    [Test]
    public void ComputePValues_HigherTargets_LowerPValues()
    {
        var results = new List<AggregatedAnalysisResult>
        {
            new() 
            { 
                DatabaseName = "Low",
                PsmBacterialUnambiguousTargets = 5,
                PsmBacterialUnambiguousDecoys = 3,
                TransientProteinCount = 100
            },
            new() 
            { 
                DatabaseName = "Medium",
                PsmBacterialUnambiguousTargets = 20,
                PsmBacterialUnambiguousDecoys = 3,
                TransientProteinCount = 100
            },
            new() 
            { 
                DatabaseName = "High",
                PsmBacterialUnambiguousTargets = 50,
                PsmBacterialUnambiguousDecoys = 3,
                TransientProteinCount = 100
            }
        };

        var test = PermutationTest<int>.ForPsm(iterations: TestIterations);
        var pValues = test.ComputePValues(results);

        // Higher targets should generally have lower p-values
        Assert.That(pValues["High"], Is.LessThan(pValues["Medium"]));
        Assert.That(pValues["Medium"], Is.LessThan(pValues["Low"]));
    }

    [Test]
    public void ComputePValues_ZeroTargets_HighPValue()
    {
        var results = new List<AggregatedAnalysisResult>
        {
            new() 
            { 
                DatabaseName = "Zero",
                PsmBacterialUnambiguousTargets = 0,
                PsmBacterialUnambiguousDecoys = 5,
                TransientProteinCount = 100
            },
            new() 
            { 
                DatabaseName = "NonZero",
                PsmBacterialUnambiguousTargets = 20,
                PsmBacterialUnambiguousDecoys = 5,
                TransientProteinCount = 100
            }
        };

        var test = PermutationTest<int>.ForPsm(iterations: TestIterations);
        var pValues = test.ComputePValues(results);

        // Zero targets should have high p-value
        Assert.That(pValues["Zero"], Is.GreaterThan(0.5),
            "Zero targets should not be significant");
    }

    [Test]
    public void ComputePValues_WeightsByDatabaseSize()
    {
        // Two databases with same target counts but different sizes
        // Larger database should be less surprising (higher p-value)
        var results = new List<AggregatedAnalysisResult>
        {
            new() 
            { 
                DatabaseName = "Small",
                PsmBacterialUnambiguousTargets = 20,
                PsmBacterialUnambiguousDecoys = 5,
                TransientProteinCount = 50 // Small database
            },
            new() 
            { 
                DatabaseName = "Large",
                PsmBacterialUnambiguousTargets = 20,
                PsmBacterialUnambiguousDecoys = 5,
                TransientProteinCount = 200 // Large database
            }
        };

        var test = PermutationTest<int>.ForPsm(iterations: TestIterations);
        var pValues = test.ComputePValues(results);

        // Small database with same counts should be more significant
        // (getting same hits from smaller DB is more surprising)
        Assert.That(pValues["Small"], Is.LessThan(pValues["Large"]),
            "Same counts from smaller database should be more significant");
    }

    [Test]
    public void ComputePValues_DetectsHighSignalOrganisms()
    {
        var results = TestDataFactory.CreateHighSignalScenario(count: 50);
        var test = PermutationTest<int>.ForPsm(iterations: TestIterations);

        var pValues = test.ComputePValues(results);

        // Check that high signal organisms are detected
        var highSignalPValues = pValues
            .Where(kvp => kvp.Key.StartsWith("HighSignal"))
            .Select(kvp => kvp.Value)
            .ToList();

        int significantCount = highSignalPValues.Count(p => p < 0.05);
        Assert.That(significantCount, Is.GreaterThan(0),
            "Should detect at least some high-signal organisms");

        // Most high-signal should be significant
        double significantProportion = (double)significantCount / highSignalPValues.Count;
        Assert.That(significantProportion, Is.GreaterThan(0.5),
            "Majority of high-signal organisms should be detected");
    }

    [Test]
    public void ComputePValues_NullScenario_FewSignificant()
    {
        var results = TestDataFactory.CreateNullScenario(count: 50);
        var test = PermutationTest<int>.ForPsm(iterations: TestIterations);

        var pValues = test.ComputePValues(results);

        // In null scenario, about 5% should be significant by chance
        int significantCount = pValues.Values.Count(p => p < 0.05);
        double significantProportion = (double)significantCount / pValues.Count;

        Assert.That(significantProportion, Is.LessThan(0.15),
            "Null scenario should have few false positives");
    }

    [Test]
    public void ComputePValues_DifferentMetrics_IndependentResults()
    {
        var results = new List<AggregatedAnalysisResult>
        {
            new() 
            { 
                DatabaseName = "DB1",
                PsmBacterialUnambiguousTargets = 50,
                PsmBacterialUnambiguousDecoys = 10,
                PeptideBacterialUnambiguousTargets = 10,
                PeptideBacterialUnambiguousDecoys = 10,
                TransientProteinCount = 100
            },
            new() 
            { 
                DatabaseName = "DB2",
                PsmBacterialUnambiguousTargets = 10,
                PsmBacterialUnambiguousDecoys = 10,
                PeptideBacterialUnambiguousTargets = 50,
                PeptideBacterialUnambiguousDecoys = 10,
                TransientProteinCount = 100
            }
        };

        var testPsm = PermutationTest<int>.ForPsm(iterations: TestIterations);
        var testPeptide = PermutationTest<int>.ForPeptide(iterations: TestIterations);

        var pValuesPsm = testPsm.ComputePValues(results);
        var pValuesPeptide = testPeptide.ComputePValues(results);

        // DB1 should be more significant for PSMs, DB2 for peptides
        Assert.That(pValuesPsm["DB1"], Is.LessThan(pValuesPsm["DB2"]),
            "DB1 should be more significant for PSMs");
        Assert.That(pValuesPeptide["DB2"], Is.LessThan(pValuesPeptide["DB1"]),
            "DB2 should be more significant for Peptides");
    }

    [Test]
    public void ComputePValues_Performance_ReasonableTime()
    {
        var results = TestDataFactory.CreateHighSignalScenario(count: 100);
        var test = PermutationTest<int>.ForPsm(iterations: 1000);

        var startTime = DateTime.Now;
        var pValues = test.ComputePValues(results);
        var elapsed = DateTime.Now - startTime;

        Assert.That(pValues.Count, Is.EqualTo(100));
        Assert.That(elapsed.TotalSeconds, Is.LessThan(2.0),
            "1000 iterations on 100 databases should complete in < 2 seconds");
    }

    [Test]
    public void ComputePValues_HandlesLargeDecoyCount()
    {
        var results = new List<AggregatedAnalysisResult>
        {
            new() 
            { 
                DatabaseName = "DB1",
                PsmBacterialUnambiguousTargets = 100,
                PsmBacterialUnambiguousDecoys = 500, // Many decoys
                TransientProteinCount = 100
            },
            new() 
            { 
                DatabaseName = "DB2",
                PsmBacterialUnambiguousTargets = 50,
                PsmBacterialUnambiguousDecoys = 500,
                TransientProteinCount = 100
            }
        };

        var test = PermutationTest<int>.ForPsm(iterations: 500);
        
        Assert.DoesNotThrow(() =>
        {
            var pValues = test.ComputePValues(results);
            Assert.That(pValues.Count, Is.EqualTo(2));
        });
    }

    [Test]
    public void ComputePValues_ReproducibleAcrossRuns()
    {
        var results = TestDataFactory.CreateHighSignalScenario(count: 30);
        
        // Run 3 times - should be identical due to fixed default seed
        var test1 = PermutationTest<int>.ForPsm(iterations: 500);
        var test2 = PermutationTest<int>.ForPsm(iterations: 500);
        var test3 = PermutationTest<int>.ForPsm(iterations: 500);

        var pValues1 = test1.ComputePValues(results);
        var pValues2 = test2.ComputePValues(results);
        var pValues3 = test3.ComputePValues(results);

        // All should be identical
        foreach (var db in pValues1.Keys)
        {
            Assert.That(pValues2[db], Is.EqualTo(pValues1[db]).Within(1e-10));
            Assert.That(pValues3[db], Is.EqualTo(pValues1[db]).Within(1e-10));
        }
    }

    [Test]
    public void Description_IsInformative()
    {
        var test = PermutationTest<int>.ForPsm(iterations: 100);
        
        Assert.That(test.Description, Is.Not.Empty);
        Assert.That(test.Description.ToLower(), Does.Contain("permutation"));
        Assert.That(test.Description.ToLower(), Does.Contain("decoy"));
    }
}
