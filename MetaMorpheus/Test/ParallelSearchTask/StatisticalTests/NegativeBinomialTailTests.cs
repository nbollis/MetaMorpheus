using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Distributions;
using NUnit.Framework;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Statistics;

namespace Test.ParallelSearchTask.StatisticalTests;

[TestFixture]
public class NegativeBinomialTailTests
{
    [TestCase(1, 1.5)]
    [TestCase(3, 1.5)]
    [TestCase(10, 2.0)]
    public void PoissonTails_MatchMathNet(int k, double lambda)
    {
        Assert.That(NegativeBinomialTest<double>.PoissonUpperTail(k, lambda),
            Is.EqualTo(1 - Poisson.CDF(lambda, k - 1)).Within(1e-12));
        Assert.That(NegativeBinomialTest<double>.PoissonLowerTail(k, lambda),
            Is.EqualTo(Poisson.CDF(lambda, k)).Within(1e-12));
    }

    [TestCase(1, 2.0, 0.4)]
    [TestCase(5, 0.7, 0.2)]
    [TestCase(30, 3.0, 0.5)]
    public void NegativeBinomialTails_MatchMathNet(int k, double r, double p)
    {
        Assert.That(NegativeBinomialTest<double>.NegativeBinomialUpperTail(k, r, p),
            Is.EqualTo(1 - NegativeBinomial.CDF(r, p, k - 1)).Within(1e-12));
        Assert.That(NegativeBinomialTest<double>.NegativeBinomialLowerTail(k, r, p),
            Is.EqualTo(NegativeBinomial.CDF(r, p, k)).Within(1e-12));
    }

    [Test]
    public void UpperTail_KeepsPrecisionFarOut()
    {
        // 1 - CDF would round to 0 here
        double tail = NegativeBinomialTest<double>.PoissonUpperTail(60, 1.5);

        Assert.That(tail, Is.GreaterThan(0).And.LessThan(1e-60));
    }

    [Test]
    public void SmallCountsWithoutOverdispersion_GiveModestPValues_NotTheFloor()
    {
        // the regression-panel situation: a gated null of 1s and 2s, variance below the mean
        var rows = new List<TransientDatabaseMetrics>();
        int[] counts = [1, 1, 2, 1, 2, 1, 1, 2, 1, 2, 1, 1, 2, 1, 3];
        for (int i = 0; i < counts.Length; i++)
            rows.Add(new TransientDatabaseMetrics($"Db{i:D2}") { PsmBacterialUnambiguousTargets = counts[i] });

        var test = new NegativeBinomialTest<double>("PSM", StatisticalEvidenceFamily.CountEnrichment,
            r => r.PsmBacterialUnambiguousTargets, r => r.PsmBacterialUnambiguousTargets > 0);
        var pValues = test.RunTest(rows);

        double mean = counts.Average();
        Assert.That(pValues["Db14"], Is.EqualTo(1 - Poisson.CDF(mean, 2)).Within(1e-12), "Poisson tail for 3 hits");
        Assert.That(pValues["Db14"], Is.GreaterThan(0.05));
        Assert.That(pValues.Values.Min(), Is.GreaterThan(1e-10));
    }
}
