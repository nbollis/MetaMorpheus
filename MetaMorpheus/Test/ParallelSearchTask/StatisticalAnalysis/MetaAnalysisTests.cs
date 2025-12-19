using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TaskLayer.ParallelSearchTask.Statistics;

namespace Test.ParallelSearchTask.StatisticalAnalysis;

/// <summary>
/// Tests for Fisher's method for combining p-values
/// Verifies: correct chi-squared calculation, combining effect, edge cases
/// </summary>
[TestFixture]
public class MetaAnalysisTests
{
    private const double Tolerance = 1e-6;

    [Test]
    public void FisherMethod_EmptyInput_ReturnsOne()
    {
        var pValues = new List<double>();
        double combined = MetaAnalysis.FisherMethod(pValues);

        Assert.That(combined, Is.EqualTo(1.0).Within(Tolerance));
    }

    [Test]
    public void FisherMethod_SinglePValue_ReturnsApproximatelySameValue()
    {
        // Fisher's method with k=1 should give a related but transformed value
        var pValues = new List<double> { 0.05 };
        double combined = MetaAnalysis.FisherMethod(pValues);

        // Should be between 0 and 1
        Assert.That(combined, Is.GreaterThanOrEqualTo(0.0));
        Assert.That(combined, Is.LessThanOrEqualTo(1.0));
        
        // Combined p-value should be in the same ballpark
        Assert.That(combined, Is.InRange(0.01, 0.2));
    }

    [Test]
    public void FisherMethod_TwoSignificantPValues_StrongerThanEither()
    {
        // Two marginal p-values should combine to be more significant
        var pValues = new List<double> { 0.08, 0.09 };
        double combined = MetaAnalysis.FisherMethod(pValues);

        Assert.That(combined, Is.LessThan(0.08), 
            "Combined p-value should be stronger than individual p-values");
    }

    [Test]
    public void FisherMethod_KnownExample()
    {
        // Known example: three p-values: 0.05, 0.1, 0.2
        // Test statistic = -2 * (ln(0.05) + ln(0.1) + ln(0.2)) 
        //                = -2 * (-2.9957 - 2.3026 - 1.6094)
        //                = -2 * (-6.9077) = 13.8154
        // Chi-squared with df=6: P(X > 13.8154) ? 0.0319
        
        var pValues = new List<double> { 0.05, 0.1, 0.2 };
        double combined = MetaAnalysis.FisherMethod(pValues);

        Assert.That(combined, Is.EqualTo(0.0319).Within(0.001), 
            "Fisher's method should match known result");
    }

    [Test]
    public void FisherMethod_AllNonSignificant_RemainsNonSignificant()
    {
        var pValues = new List<double> { 0.5, 0.6, 0.7, 0.8 };
        double combined = MetaAnalysis.FisherMethod(pValues);

        Assert.That(combined, Is.GreaterThan(0.05), 
            "Combining non-significant p-values should remain non-significant");
    }

    [Test]
    public void FisherMethod_ManyTests_CombiningEffect()
    {
        // Ten marginal tests (p=0.07 each) should combine to be significant
        var pValues = Enumerable.Repeat(0.07, 10).ToList();
        double combined = MetaAnalysis.FisherMethod(pValues);

        Assert.That(combined, Is.LessThan(0.05), 
            "Many marginal p-values should combine to be significant");
    }

    [Test]
    public void FisherMethod_HandlesNaN()
    {
        // NaN values should be filtered out
        var pValues = new List<double> { 0.05, double.NaN, 0.1 };
        double combined = MetaAnalysis.FisherMethod(pValues);

        // Should compute using only valid values
        Assert.That(double.IsNaN(combined), Is.False);
        Assert.That(combined, Is.GreaterThanOrEqualTo(0.0));
        Assert.That(combined, Is.LessThanOrEqualTo(1.0));
    }

    [Test]
    public void FisherMethod_HandlesInfinity()
    {
        // Infinity values should be filtered out
        var pValues = new List<double> { 0.05, double.PositiveInfinity, 0.1 };
        double combined = MetaAnalysis.FisherMethod(pValues);

        Assert.That(double.IsInfinity(combined), Is.False);
        Assert.That(combined, Is.GreaterThanOrEqualTo(0.0));
        Assert.That(combined, Is.LessThanOrEqualTo(1.0));
    }

    [Test]
    public void FisherMethod_VerySmallPValues_NumericalStability()
    {
        // Test with very small p-values
        var pValues = new List<double> { 1e-10, 1e-8, 1e-6 };
        double combined = MetaAnalysis.FisherMethod(pValues);

        // Should produce a very small p-value without underflow
        Assert.That(combined, Is.GreaterThanOrEqualTo(0.0));
        Assert.That(combined, Is.LessThan(1e-6));
        Assert.That(double.IsNaN(combined), Is.False);
    }

    [Test]
    public void FisherMethod_AllZeros_HandledGracefully()
    {
        // P-values of 0 are problematic for log, should be clamped
        var pValues = new List<double> { 0.0, 0.0, 0.0 };
        double combined = MetaAnalysis.FisherMethod(pValues);

        // Should not be NaN or Infinity
        Assert.That(double.IsNaN(combined), Is.False);
        Assert.That(double.IsInfinity(combined), Is.False);
        
        // Should be very small
        Assert.That(combined, Is.LessThan(1e-10));
    }

    [Test]
    public void FisherMethod_AllOnes_ReturnsOne()
    {
        var pValues = new List<double> { 1.0, 1.0, 1.0 };
        double combined = MetaAnalysis.FisherMethod(pValues);

        Assert.That(combined, Is.EqualTo(1.0).Within(Tolerance));
    }

    [Test]
    public void FisherMethod_MonotonicityWithIncreasingTests()
    {
        // Adding more significant tests should decrease combined p-value
        var pValues1 = new List<double> { 0.05 };
        var pValues2 = new List<double> { 0.05, 0.05 };
        var pValues3 = new List<double> { 0.05, 0.05, 0.05 };

        double combined1 = MetaAnalysis.FisherMethod(pValues1);
        double combined2 = MetaAnalysis.FisherMethod(pValues2);
        double combined3 = MetaAnalysis.FisherMethod(pValues3);

        Assert.That(combined2, Is.LessThan(combined1));
        Assert.That(combined3, Is.LessThan(combined2));
    }

    [Test]
    public void CombinePValuesAcrossTests_EmptyResults_ReturnsEmptyDictionary()
    {
        var results = new List<StatisticalResult>();
        var combined = MetaAnalysis.CombinePValuesAcrossTests(results);

        Assert.That(combined, Is.Empty);
    }

    [Test]
    public void CombinePValuesAcrossTests_SingleDatabase_SingleTest()
    {
        var results = new List<StatisticalResult>
        {
            new() { DatabaseName = "DB1", TestName = "Test1", MetricName = "PSM", PValue = 0.05 }
        };

        var combined = MetaAnalysis.CombinePValuesAcrossTests(results);

        Assert.That(combined.Count, Is.EqualTo(1));
        Assert.That(combined.ContainsKey("DB1"), Is.True);
        
        // Single test should give similar p-value (transformed by chi-squared)
        Assert.That(combined["DB1"], Is.InRange(0.01, 0.2));
    }

    [Test]
    public void CombinePValuesAcrossTests_MultipleDatabases_GroupedCorrectly()
    {
        var results = new List<StatisticalResult>
        {
            // DB1: two tests
            new() { DatabaseName = "DB1", TestName = "Test1", MetricName = "PSM", PValue = 0.05 },
            new() { DatabaseName = "DB1", TestName = "Test2", MetricName = "PSM", PValue = 0.08 },
            
            // DB2: two tests
            new() { DatabaseName = "DB2", TestName = "Test1", MetricName = "PSM", PValue = 0.5 },
            new() { DatabaseName = "DB2", TestName = "Test2", MetricName = "PSM", PValue = 0.6 }
        };

        var combined = MetaAnalysis.CombinePValuesAcrossTests(results);

        Assert.That(combined.Count, Is.EqualTo(2));
        Assert.That(combined.ContainsKey("DB1"), Is.True);
        Assert.That(combined.ContainsKey("DB2"), Is.True);
        
        // DB1 should have stronger combined signal
        Assert.That(combined["DB1"], Is.LessThan(combined["DB2"]));
        
        // Both should be valid probabilities
        Assert.That(combined["DB1"], Is.InRange(0.0, 1.0));
        Assert.That(combined["DB2"], Is.InRange(0.0, 1.0));
    }

    [Test]
    public void CombinePValuesAcrossTests_CombinesAcrossMetrics()
    {
        var results = new List<StatisticalResult>
        {
            // DB1: same test, different metrics
            new() { DatabaseName = "DB1", TestName = "Gaussian", MetricName = "PSM", PValue = 0.05 },
            new() { DatabaseName = "DB1", TestName = "Gaussian", MetricName = "Peptide", PValue = 0.06 },
            new() { DatabaseName = "DB1", TestName = "Gaussian", MetricName = "Protein", PValue = 0.07 }
        };

        var combined = MetaAnalysis.CombinePValuesAcrossTests(results);

        Assert.That(combined.Count, Is.EqualTo(1));
        
        // Combined should be stronger than any individual
        Assert.That(combined["DB1"], Is.LessThan(0.05));
    }

    [Test]
    public void CombinePValuesAcrossTests_ManyTests_Performance()
    {
        // Test with many databases and tests
        var results = new List<StatisticalResult>();
        
        for (int db = 0; db < 100; db++)
        {
            for (int test = 0; test < 10; test++)
            {
                results.Add(new StatisticalResult
                {
                    DatabaseName = $"DB{db}",
                    TestName = $"Test{test}",
                    MetricName = "PSM",
                    PValue = 0.05 + (db * 0.001) + (test * 0.01)
                });
            }
        }

        var startTime = DateTime.Now;
        var combined = MetaAnalysis.CombinePValuesAcrossTests(results);
        var elapsed = DateTime.Now - startTime;

        Assert.That(combined.Count, Is.EqualTo(100));
        Assert.That(elapsed.TotalSeconds, Is.LessThan(0.5), 
            "Combining p-values should be fast");
    }

    [Test]
    public void FisherMethod_SymmetryProperty()
    {
        // Order shouldn't matter
        var pValues1 = new List<double> { 0.01, 0.05, 0.1 };
        var pValues2 = new List<double> { 0.1, 0.05, 0.01 };
        var pValues3 = new List<double> { 0.05, 0.01, 0.1 };

        double combined1 = MetaAnalysis.FisherMethod(pValues1);
        double combined2 = MetaAnalysis.FisherMethod(pValues2);
        double combined3 = MetaAnalysis.FisherMethod(pValues3);

        Assert.That(combined1, Is.EqualTo(combined2).Within(Tolerance));
        Assert.That(combined1, Is.EqualTo(combined3).Within(Tolerance));
    }

    [Test]
    public void FisherMethod_OneMarginalOneStrongTest_StrongerThanMarginalAlone()
    {
        var pValues = new List<double> { 0.08, 0.001 };
        double combined = MetaAnalysis.FisherMethod(pValues);

        // Should be much stronger than the marginal test alone
        Assert.That(combined, Is.LessThan(0.001));
    }

    [Test]
    public void FisherMethod_ContradictoryEvidence_Intermediate()
    {
        // One very significant, one very non-significant
        var pValues = new List<double> { 0.001, 0.9 };
        double combined = MetaAnalysis.FisherMethod(pValues);

        // Should be somewhere in between
        Assert.That(combined, Is.GreaterThan(0.001));
        Assert.That(combined, Is.LessThan(0.9));
    }

    [Test]
    public void CombinePValuesAcrossTests_HandlesNaNInResults()
    {
        var results = new List<StatisticalResult>
        {
            new() { DatabaseName = "DB1", TestName = "Test1", MetricName = "PSM", PValue = 0.05 },
            new() { DatabaseName = "DB1", TestName = "Test2", MetricName = "PSM", PValue = double.NaN },
            new() { DatabaseName = "DB1", TestName = "Test3", MetricName = "PSM", PValue = 0.1 }
        };

        var combined = MetaAnalysis.CombinePValuesAcrossTests(results);

        Assert.That(combined.ContainsKey("DB1"), Is.True);
        Assert.That(double.IsNaN(combined["DB1"]), Is.False);
    }

    [Test]
    public void FisherMethod_LargeNumberOfTests_NumericalStability()
    {
        // Test with 100 tests
        var pValues = Enumerable.Repeat(0.05, 100).ToList();
        double combined = MetaAnalysis.FisherMethod(pValues);

        // Should be extremely small but not underflow to 0
        Assert.That(combined, Is.GreaterThan(0.0));
        Assert.That(combined, Is.LessThan(1e-50));
        Assert.That(double.IsNaN(combined), Is.False);
    }
}
