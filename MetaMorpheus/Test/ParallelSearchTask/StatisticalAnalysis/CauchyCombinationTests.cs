using System.Collections.Generic;
using NUnit.Framework;
using TaskLayer.ParallelSearch.Statistics;

namespace Test.ParallelSearchTask.StatisticalAnalysis;

[TestFixture]
public class CauchyCombinationTests
{
    [TestCase(0.3)]
    [TestCase(0.01)]
    [TestCase(1e-40)]
    public void OneOrManyEqualPValues_CombineToThatPValue(double p)
    {
        Assert.That(MetaAnalysis.CauchyMethod([p]), Is.EqualTo(p).Within(p * 1e-9));
        Assert.That(MetaAnalysis.CauchyMethod([p, p, p, p]), Is.EqualTo(p).Within(p * 1e-9));
    }

    [Test]
    public void ATinyPValue_DominatesAsInTheTailApproximation()
    {
        // tail: combined p ≈ (sum of weights over p_i) inverted = 3 * 1e-20 when one of three tests is tiny
        double combined = MetaAnalysis.CauchyMethod([1e-20, 0.5, 0.9]);

        Assert.That(combined, Is.EqualTo(3e-20).Within(3e-20 * 1e-6));
    }

    [Test]
    public void PValuesOfOneAndEmptyInput_StayInRange()
    {
        Assert.That(MetaAnalysis.CauchyMethod([]), Is.EqualTo(1.0));
        Assert.That(MetaAnalysis.CauchyMethod([1.0, 1.0]), Is.InRange(0.5, 1.0));
        Assert.That(MetaAnalysis.CauchyMethod([0.0]), Is.EqualTo(1e-300));
    }

    [Test]
    public void IsUsedThroughTheCombiningMethodSwitch()
    {
        var results = new List<StatisticalTestResult>
        {
            new() { DatabaseName = "Db1", TestName = "T1", MetricName = "M1", PValue = 0.01 },
            new() { DatabaseName = "Db1", TestName = "T2", MetricName = "M2", PValue = 0.03 },
        };

        var combined = MetaAnalysis.CombinePValuesAcrossTests(results, PValueCombiningMethod.Cauchy);

        Assert.That(combined["Db1"], Is.EqualTo(MetaAnalysis.CauchyMethod([0.01, 0.03])));
        Assert.That(combined["Db1"], Is.InRange(0.01, 0.03), "a Cauchy combination lies between the inputs' extremes");
    }

    [Test]
    public void BrownAndKostMcDermott_NoLongerShareOneCovarianceFormula()
    {
        var correlated = new double[,] { { 1, 0.6 }, { 0.6, 1 } };
        double brown = MetaAnalysis.BrownMethod([0.01, 0.02], correlated);
        double kost = MetaAnalysis.KostMcDermottMethod([0.01, 0.02], correlated);

        Assert.That(brown, Is.Not.EqualTo(kost));
    }
}
