using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Distributions;
using NUnit.Framework;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Statistics;

namespace Test.ParallelSearchTask.StatisticalTests;

[TestFixture]
public class SampleMeanTestTests
{
    private static SampleMeanTest Test(bool higherIsBetter, int minValues = 1) =>
        new("Scores", StatisticalEvidenceFamily.ScoreDistribution, r => r.PeptideBacterialUnambiguousTargetScores,
            higherIsBetter, minValues);

    private static TransientDatabaseMetrics Db(string name, double[] values) =>
        new(name) { PeptideBacterialUnambiguousTargetScores = values };

    [Test]
    public void MixedSmallAndLargeDatabases_UnderTheNull_StayNearNominal()
    {
        // The median Gaussian this replaces gave p <= 1e-5 about 30 times as often as nominal on exactly this mix
        var random = new Random(11);
        var normal = new Normal(0, 1, random);
        var rows = Enumerable.Range(0, 5000)
            .Select(i => Db($"Db{i:D4}", normal.Samples().Take(i % 5 < 3 ? 1 : 50).ToArray()))
            .ToList();

        var pValues = Test(higherIsBetter: true).ComputePValues(rows).Values.ToArray();

        double atOnePercent = pValues.Count(p => p <= 0.01) / (double)pValues.Length;
        Assert.That(atOnePercent, Is.InRange(0.005, 0.02), "about 1% at p <= 0.01");
        Assert.That(pValues.Count(p => p <= 1e-5), Is.LessThanOrEqualTo(1), "expected 0.05 at p <= 1e-5");
    }

    [Test]
    public void TheBetterDirection_GetsTheSmallP()
    {
        var rows = Enumerable.Range(0, 100).Select(i => Db($"Null{i:D3}", [i % 10, (i + 3) % 10, (i + 7) % 10])).ToList();
        rows.Add(Db("High", [9, 9, 9, 9, 9, 9]));
        rows.Add(Db("Low", [0, 0, 0, 0, 0, 0]));

        var higher = Test(higherIsBetter: true).ComputePValues(rows);
        var lower = Test(higherIsBetter: false).ComputePValues(rows);

        Assert.That(higher["High"], Is.LessThan(1e-3));
        Assert.That(higher["Low"], Is.GreaterThan(0.99));
        Assert.That(lower["Low"], Is.LessThan(1e-3));
        Assert.That(lower["High"], Is.GreaterThan(0.99));
    }

    [Test]
    public void TooFewValues_IsUndefined()
    {
        var rows = new List<TransientDatabaseMetrics> { Db("One", [5]), Db("Two", [1, 2]), Db("Three", [3, 4, 5]) };
        var test = Test(higherIsBetter: true, minValues: 2);

        var pValues = test.RunTest(rows);

        Assert.That(test.IsDefinedFor(rows[0]), Is.False);
        Assert.That(test.GetUndefinedReason(rows[0]), Is.EqualTo("TooFewValues"));
        Assert.That(pValues["One"], Is.NaN);
        Assert.That(pValues["Three"], Is.Not.NaN);
    }
}
