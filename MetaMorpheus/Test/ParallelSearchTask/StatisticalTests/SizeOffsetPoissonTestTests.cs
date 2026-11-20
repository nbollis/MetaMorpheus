using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Distributions;
using NUnit.Framework;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Statistics;

namespace Test.ParallelSearchTask.StatisticalTests;

[TestFixture]
public class SizeOffsetPoissonTestTests
{
    private static SizeOffsetPoissonTest PeptideTest() =>
        new("Peptide-All", StatisticalEvidenceFamily.CountEnrichment,
            r => r.PeptideBacterialUnambiguousTargets, r => r.TransientPeptideCount);

    private static TransientDatabaseMetrics Db(string name, int peptides, int size) =>
        new(name) { PeptideBacterialUnambiguousTargets = peptides, TransientPeptideCount = size };

    /// <summary>A null panel at about 9 random peptides per million, plus the databases a test names.</summary>
    private static List<TransientDatabaseMetrics> NullPanel(params TransientDatabaseMetrics[] extra)
    {
        var rows = new List<TransientDatabaseMetrics>();
        for (int i = 0; i < 200; i++)
        {
            int size = 2_000 + 500 * i; // 2,000 to 101,500 peptides
            rows.Add(Db($"Null{i:D3}", i % 40 == 0 ? 1 : 0, size));
        }
        rows.AddRange(extra);
        return rows;
    }

    [Test]
    public void PValue_IsThePoissonUpperTail_AtThePooledRateTimesSize()
    {
        var rows = NullPanel(Db("Moderate", 2, 50_000), Db("Virus", 30, 6_651));
        var test = PeptideTest();

        var pValues = test.ComputePValues(rows);

        double totalCount = rows.Sum(r => r.PeptideBacterialUnambiguousTargets);
        double totalSize = rows.Sum(r => r.TransientPeptideCount);
        double rate = (totalCount + SizeOffsetPoissonTest.RatePseudocount) / totalSize;
        Assert.That(test.PooledRate(rows), Is.EqualTo(rate).Within(1e-15));
        Assert.That(pValues["Moderate"], Is.EqualTo(1 - Poisson.CDF(rate * 50_000, 1)).Within(1e-12));
        Assert.That(pValues["Virus"], Is.LessThan(1e-30), "30 peptides where well under 1 is expected");
    }

    [Test]
    public void OneHitInATinyDatabase_IsModest_NotExtreme()
    {
        // the Gaussian ratio test gave 1 hit of 300 peptides (or 1 of 2 proteins) p ≈ 1e-16 or less
        var rows = NullPanel(Db("Tiny", 1, 300), Db("Virus", 30, 6_651));

        var pValues = PeptideTest().ComputePValues(rows);

        Assert.That(pValues["Tiny"], Is.GreaterThan(1e-3));
        Assert.That(pValues["Virus"], Is.LessThan(pValues["Tiny"] * 1e-20));
    }

    [Test]
    public void EveryDatabaseIsDefined_AndOneWithoutHitsScoresOne()
    {
        var rows = NullPanel();
        var test = PeptideTest();

        var pValues = test.RunTest(rows);

        Assert.That(rows.All(test.IsDefinedFor), Is.True, "zero-hit databases count in the multiple-testing correction");
        Assert.That(pValues.Values.Any(double.IsNaN), Is.False);
        Assert.That(pValues["Null001"], Is.EqualTo(1.0));
    }

    [Test]
    public void NoHitsAnywhere_KeepsTheRateAboveZero()
    {
        // with λ = 0 a single hit would score p = 0
        var rows = Enumerable.Range(0, 50).Select(i => Db($"Db{i:D2}", 0, 5_000)).ToList();
        var test = PeptideTest();

        Assert.That(test.PooledRate(rows), Is.GreaterThan(0));
        Assert.That(test.ComputePValues(rows).Values, Is.All.EqualTo(1.0));
    }

    [Test]
    public void ADatabaseWithoutPeptides_IsUndefined()
    {
        var rows = NullPanel(Db("Empty", 0, 0));
        var test = PeptideTest();

        Assert.That(test.IsDefinedFor(rows[^1]), Is.False);
        Assert.That(test.GetUndefinedReason(rows[^1]), Is.EqualTo("SizeNotPositive"));
        Assert.That(test.RunTest(rows)["Empty"], Is.NaN);
    }

    [Test]
    public void EffectSize_IsObservedOverExpected()
    {
        var rows = NullPanel(Db("Virus", 30, 6_651));
        var test = PeptideTest();

        double? effect = test.GetEffectSize(rows[^1], rows);

        Assert.That(effect, Is.EqualTo(30 / (test.PooledRate(rows) * 6_651)).Within(1e-9));
    }

    [Test]
    public void Suite_TestsDistinctCountsBySize_AndHasNoRateTestOnPsmOrPredictionCounts()
    {
        var suite = new TestSuiteBuilder()
            .AddCountEnrichmentTests()
            .AddProteinGroupTests()
            .AddDeNovoTests()
            .Build()
            .ToList();

        var offsetMetrics = suite.Where(t => t is SizeOffsetPoissonTest).Select(t => t.MetricName);
        Assert.That(offsetMetrics, Is.EquivalentTo(new[]
            { "Peptide-All", "Peptide-Confident", "ProteinGroup", "DeNovo-MappedPeptides", "DeNovo-MappedProteins" }));

        string[] clustered = ["PSM-All", "PSM-Confident", "DeNovo-Predictions", "DeNovo-Targets"];
        var clusteredRateTests = suite.Where(t => clustered.Contains(t.MetricName) && t.TestName is "Gaussian" or "Permutation" or "SizeOffsetPoisson");
        Assert.That(clusteredRateTests, Is.Empty);

        string[] ratioMetrics = [.. clustered, "Peptide-All", "Peptide-Confident", "ProteinGroup", "DeNovo-MappedPeptides", "DeNovo-MappedProteins"];
        Assert.That(suite.Where(t => t.TestName == "Gaussian" && ratioMetrics.Contains(t.MetricName)), Is.Empty,
            "no Gaussian test on a count/size ratio");
    }
}
