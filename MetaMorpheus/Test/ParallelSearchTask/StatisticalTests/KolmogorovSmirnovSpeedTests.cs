using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Statistics;

namespace Test.ParallelSearchTask.StatisticalTests;

/// <summary>
/// The KS test used to merge each database's scores with the whole global array, which made it quadratic in the
/// number of databases (hours on a 30,000-database run). <see cref="KolmogorovSmirnovTest.CdfDifferences"/> gets
/// the same statistic by binary search; these tests hold it to the old merge exactly, ties included.
/// </summary>
[TestFixture]
public class KolmogorovSmirnovSpeedTests
{
    /// <summary>The merge the KS test used before, copied unchanged as the reference.</summary>
    private static (double MaxLess, double MaxGreater, double MaxDiff) OldSweep(double[] dbScores, double[] globalScores)
    {
        int nDb = dbScores.Length;
        int totalScoreCount = globalScores.Length;
        double maxDiff = 0.0, maxLess = 0.0, maxGreater = 0.0;
        int di = 0, gi = 0;

        while (di < nDb || gi < totalScoreCount)
        {
            bool advanceDb = gi >= totalScoreCount || (di < nDb && dbScores[di] <= globalScores[gi]);
            bool advanceGlobal = di >= nDb || (gi < totalScoreCount && globalScores[gi] <= dbScores[Math.Min(di, nDb - 1)]);

            if (advanceDb)
            {
                double val = dbScores[di];
                while (di < nDb && dbScores[di] == val) di++;
            }
            if (advanceGlobal)
            {
                double val = globalScores[gi];
                while (gi < totalScoreCount && globalScores[gi] == val) gi++;
            }

            double fDb = (double)di / nDb;
            double fAll = (double)gi / totalScoreCount;

            double absDiff = Math.Abs(fDb - fAll);
            if (absDiff > maxDiff) maxDiff = absDiff;
            double diffLess = fAll - fDb;
            if (diffLess > maxLess) maxLess = diffLess;
            double diffGreater = fDb - fAll;
            if (diffGreater > maxGreater) maxGreater = diffGreater;
        }

        return (maxLess, maxGreater, maxDiff);
    }

    [TestCase(1, true)]
    [TestCase(2, true)]
    [TestCase(3, false)]
    [TestCase(4, false)]
    public void CdfDifferences_MatchTheOldMergeExactly(int seed, bool coarseValues)
    {
        var random = new Random(seed);
        // coarse values give many ties within and across databases; fine ones give almost none
        double Next() => coarseValues ? random.Next(0, 12) / 2.0 : random.NextDouble() * 50;

        var databases = Enumerable.Range(0, 200)
            .Select(_ => Enumerable.Range(0, random.Next(1, 40)).Select(_ => Next()).OrderBy(v => v).ToArray())
            .ToList();
        var global = databases.SelectMany(d => d).OrderBy(v => v).ToArray();

        foreach (var db in databases)
        {
            var (oldLess, oldGreater, oldDiff) = OldSweep(db, global);
            var (newLess, newGreater) = KolmogorovSmirnovTest.CdfDifferences(db, global);

            Assert.That(newLess, Is.EqualTo(oldLess), "max(F_all - F_db)");
            Assert.That(newGreater, Is.EqualTo(oldGreater), "max(F_db - F_all)");
            Assert.That(Math.Max(newLess, newGreater), Is.EqualTo(oldDiff), "max|F_db - F_all|");
        }
    }

    [Test]
    public void AFullRunSizedTest_FinishesInSeconds()
    {
        // 30,000 databases of 20 scores: the old merge walked all 600,000 scores for every database (1.8e10 steps)
        var random = new Random(7);
        var rows = Enumerable.Range(0, 30_000)
            .Select(i => new TransientDatabaseMetrics($"Db{i:D5}")
            {
                PsmBacterialUnambiguousTargetScores = Enumerable.Range(0, 20).Select(_ => Math.Round(random.NextDouble() * 30, 1)).ToArray()
            })
            .ToList();
        var test = new KolmogorovSmirnovTest("Scores", StatisticalEvidenceFamily.ScoreDistribution,
            r => r.PsmBacterialUnambiguousTargetScores);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var pValues = test.ComputePValues(rows);
        foreach (var row in rows)
            test.GetEffectSize(row, rows);
        watch.Stop();

        Assert.That(pValues, Has.Count.EqualTo(rows.Count));
        Assert.That(watch.Elapsed.TotalSeconds, Is.LessThan(30));
    }

    [Test]
    public void EffectSize_FollowsTheListItIsGiven()
    {
        // the pooled median is cached per list of results; a different list must not reuse it
        var test = new KolmogorovSmirnovTest("Scores", StatisticalEvidenceFamily.ScoreDistribution,
            r => r.PsmBacterialUnambiguousTargetScores);
        TransientDatabaseMetrics Db(string name, params double[] scores) =>
            new(name) { PsmBacterialUnambiguousTargetScores = scores };

        var low = new List<TransientDatabaseMetrics> { Db("A", 1, 2, 3), Db("B", 2, 3, 4) };
        var high = new List<TransientDatabaseMetrics> { Db("A", 1, 2, 3), Db("B", 20, 30, 40) };

        double? first = test.GetEffectSize(low[0], low);
        double? again = test.GetEffectSize(low[0], low);
        double? other = test.GetEffectSize(high[0], high);

        Assert.That(again, Is.EqualTo(first));
        Assert.That(first, Is.EqualTo(2 - 3), "median 2 against the pooled median 3");
        Assert.That(other, Is.EqualTo(2 - 20), "median 2 against the pooled median of the second list, 20");
    }
}
