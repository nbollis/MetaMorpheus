using System.Collections.Generic;
using NUnit.Framework;
using TaskLayer.ParallelSearch.Statistics;
using PST = TaskLayer.ParallelSearch.ParallelSearchTask;

namespace Test.ParallelSearchTask;

/// <summary>
/// Tests the family-aware organism-detection predicate: a database is a confident detection when at least
/// N independent evidence families are significant AND the overall combined q-value clears the threshold.
/// This replaced the test-ratio gate that required passing >=50% of ALL tests — unreachable for a small
/// genome (SARS-CoV-2 passed 31/78), so nothing was ever written.
/// </summary>
[TestFixture]
public class FamilyDetectionTests
{
    private const int MinFamilies = 4;
    private const double QThreshold = 0.01;

    private static StatisticalTestResult Family(StatisticalEvidenceFamily fam, double qValue) => new()
    {
        TestName = "GaussianTest", // not a combined name -> IsCombinedResult == false
        MetricName = fam.ToString(),
        EvidenceFamily = fam,
        IsDefined = true,
        QValue = qValue,
    };

    private static StatisticalTestResult OverallCombined(double qValue, string metric = "All") => new()
    {
        TestName = "Combined", // IsCombinedResult == true
        MetricName = metric,
        IsDefined = true,
        QValue = qValue,
    };

    private static readonly StatisticalEvidenceFamily[] SevenFamilies =
    {
        StatisticalEvidenceFamily.CountEnrichment,
        StatisticalEvidenceFamily.AmbiguityOrTargetDecoy,
        StatisticalEvidenceFamily.ScoreDistribution,
        StatisticalEvidenceFamily.Fragmentation,
        StatisticalEvidenceFamily.RetentionTime,
        StatisticalEvidenceFamily.ProteinGroup,
        StatisticalEvidenceFamily.PrecursorDeconvolution,
    };

    [Test]
    public void StrongDetection_AllFamiliesSignificant_Qualifies()
    {
        var results = new List<StatisticalTestResult>();
        foreach (var f in SevenFamilies) results.Add(Family(f, 1e-300));
        results.Add(OverallCombined(4.7e-300));

        Assert.That(PST.QualifiesAsDetectedOrganism(results, MinFamilies, QThreshold), Is.True);
    }

    [Test]
    public void TooFewFamilies_DoesNotQualify()
    {
        var results = new List<StatisticalTestResult>
        {
            Family(StatisticalEvidenceFamily.CountEnrichment, 1e-50),
            Family(StatisticalEvidenceFamily.Fragmentation, 1e-50),
            Family(StatisticalEvidenceFamily.RetentionTime, 1e-50), // only 3 distinct families
            OverallCombined(1e-100),
        };
        Assert.That(PST.QualifiesAsDetectedOrganism(results, MinFamilies, QThreshold), Is.False);
    }

    [Test]
    public void CombinedQOverThreshold_DoesNotQualify()
    {
        var results = new List<StatisticalTestResult>();
        for (int i = 0; i < 5; i++) results.Add(Family(SevenFamilies[i], 1e-50));
        results.Add(OverallCombined(0.02)); // 5 families pass but combined q 0.02 > 0.01

        Assert.That(PST.QualifiesAsDetectedOrganism(results, MinFamilies, QThreshold), Is.False);
    }

    [Test]
    public void NoOverallCombinedResult_NaN_DoesNotQualify()
    {
        var results = new List<StatisticalTestResult>();
        for (int i = 0; i < 5; i++) results.Add(Family(SevenFamilies[i], 1e-50)); // families pass, but no "All" combined

        Assert.That(PST.QualifiesAsDetectedOrganism(results, MinFamilies, QThreshold), Is.False);
    }

    [Test]
    public void InsignificantFamilyResults_DoNotCountTowardFamilies()
    {
        var results = new List<StatisticalTestResult>();
        foreach (var f in SevenFamilies) results.Add(Family(f, 0.5)); // present but NOT significant
        results.Add(OverallCombined(1e-300));

        Assert.That(PST.QualifiesAsDetectedOrganism(results, MinFamilies, QThreshold), Is.False);
    }

    [Test]
    public void PerFamilyCombined_NotTreatedAsOverall()
    {
        // A combined result for a single family (MetricName != "All") must NOT supply the overall combined q.
        var results = new List<StatisticalTestResult>();
        for (int i = 0; i < 5; i++) results.Add(Family(SevenFamilies[i], 1e-50));
        results.Add(OverallCombined(1e-300, metric: "CountEnrichment")); // per-family combined, not the overall

        Assert.That(PST.QualifiesAsDetectedOrganism(results, MinFamilies, QThreshold), Is.False);
    }

    [Test]
    public void DuplicateSignificantFamily_CountsOnce()
    {
        // Same family significant twice should count as ONE family, so 3 distinct families < 4 -> no.
        var results = new List<StatisticalTestResult>
        {
            Family(StatisticalEvidenceFamily.CountEnrichment, 1e-50),
            Family(StatisticalEvidenceFamily.CountEnrichment, 1e-60), // duplicate family
            Family(StatisticalEvidenceFamily.Fragmentation, 1e-50),
            Family(StatisticalEvidenceFamily.RetentionTime, 1e-50),
            OverallCombined(1e-100),
        };
        Assert.That(PST.QualifiesAsDetectedOrganism(results, MinFamilies, QThreshold), Is.False);
    }
}
