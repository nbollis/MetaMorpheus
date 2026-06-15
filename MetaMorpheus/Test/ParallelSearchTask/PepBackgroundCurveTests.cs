using System.Collections.Generic;
using System.Linq;
using EngineLayer;
using EngineLayer.ParallelSearch;
using NUnit.Framework;
using Test.ParallelSearchTask.Utility;

namespace Test.ParallelSearchTask;

/// <summary>
/// Tests for the background PEP -> PEP_QValue curve used to assign a calibrated PEP q-value to transient
/// database IDs (which are far too small to compute their own PEP target/decoy). The curve is snapshotted
/// from the decoy-rich base search; each transient match's model PEP is mapped onto it by lower-bound lookup.
/// </summary>
[TestFixture]
public class PepBackgroundCurveTests
{
    // A monotone curve: PEP ascending, PEP_QValue non-decreasing (the shape BuildPepQValueCurve produces).
    private static readonly double[] PepAsc = { 0.01, 0.05, 0.20, 0.60 };
    private static readonly double[] QByPep = { 0.001, 0.01, 0.05, 0.30 };

    [Test]
    public void Lookup_EmptyCurve_ReturnsSentinelTwo()
    {
        double q = TransientPepAnalysisEngine.LookupBackgroundPepQValue(0.01, System.Array.Empty<double>(), System.Array.Empty<double>());
        Assert.That(q, Is.EqualTo(2.0));
    }

    [Test]
    public void Lookup_PepBelowAll_ReturnsLowestQValue()
    {
        // Most-confident transient PEP, below every background PEP -> the smallest (best) background q-value.
        double q = TransientPepAnalysisEngine.LookupBackgroundPepQValue(0.005, PepAsc, QByPep);
        Assert.That(q, Is.EqualTo(0.001));
    }

    [Test]
    public void Lookup_PepAboveAll_ReturnsHighestQValue()
    {
        // Worse than every background PEP -> the largest (worst) background q-value (clamped to last).
        double q = TransientPepAnalysisEngine.LookupBackgroundPepQValue(0.95, PepAsc, QByPep);
        Assert.That(q, Is.EqualTo(0.30));
    }

    [Test]
    public void Lookup_ExactPep_ReturnsThatQValue()
    {
        Assert.That(TransientPepAnalysisEngine.LookupBackgroundPepQValue(0.01, PepAsc, QByPep), Is.EqualTo(0.001));
        Assert.That(TransientPepAnalysisEngine.LookupBackgroundPepQValue(0.20, PepAsc, QByPep), Is.EqualTo(0.05));
        Assert.That(TransientPepAnalysisEngine.LookupBackgroundPepQValue(0.60, PepAsc, QByPep), Is.EqualTo(0.30));
    }

    [Test]
    public void Lookup_PepBetweenPoints_ReturnsLowerBoundQValue()
    {
        // 0.03 falls between background PEPs 0.01 and 0.05; lower-bound is 0.05 -> its q-value 0.01.
        Assert.That(TransientPepAnalysisEngine.LookupBackgroundPepQValue(0.03, PepAsc, QByPep), Is.EqualTo(0.01));
        // 0.50 falls between 0.20 and 0.60; lower-bound is 0.60 -> 0.30.
        Assert.That(TransientPepAnalysisEngine.LookupBackgroundPepQValue(0.50, PepAsc, QByPep), Is.EqualTo(0.30));
    }

    [Test]
    public void Lookup_SingleElementCurve_AlwaysReturnsThatQValue()
    {
        var pepAsc = new[] { 0.10 };
        var qByPep = new[] { 0.02 };
        Assert.That(TransientPepAnalysisEngine.LookupBackgroundPepQValue(0.001, pepAsc, qByPep), Is.EqualTo(0.02));
        Assert.That(TransientPepAnalysisEngine.LookupBackgroundPepQValue(0.99, pepAsc, qByPep), Is.EqualTo(0.02));
    }

    [Test]
    public void Lookup_IsMonotoneNonDecreasingInPep()
    {
        // Sweeping PEP upward never yields a smaller q-value than a lower PEP did.
        double prev = -1;
        for (double pep = 0.0; pep <= 1.0; pep += 0.02)
        {
            double q = TransientPepAnalysisEngine.LookupBackgroundPepQValue(pep, PepAsc, QByPep);
            Assert.That(q, Is.GreaterThanOrEqualTo(prev));
            prev = q;
        }
    }

    [Test]
    public void BuildCurve_EmptyInput_ReturnsEmptyArrays()
    {
        var (pepAsc, qByPep) = TransientPepAnalysisEngine.BuildPepQValueCurve(new List<SpectralMatch>(), peptideLevel: false);
        Assert.That(pepAsc, Is.Empty);
        Assert.That(qByPep, Is.Empty);
    }

    [Test]
    public void BuildCurve_TargetsAndDecoys_ProducesSortedMonotoneCurve()
    {
        var cp = ParallelSearchTestContextFactory.CreateCommonParameters();
        // Targets with low PEP (confident), decoys with high PEP (poor) — the realistic separation.
        var matches = new List<SpectralMatch>();
        double[] targetPeps = { 0.001, 0.01, 0.05, 0.10 };
        double[] decoyPeps = { 0.85, 0.90, 0.95, 0.99 };
        int scan = 1;
        foreach (var p in targetPeps)
        {
            var m = ParallelSearchTestContextFactory.CreateSpectralMatch(cp, isDecoy: false, score: 20, psmQValue: 0.001, peptideQValue: 0.001, scanNumber: scan++);
            m.PsmFdrInfo.PEP = p;
            matches.Add(m);
        }
        foreach (var p in decoyPeps)
        {
            var m = ParallelSearchTestContextFactory.CreateSpectralMatch(cp, isDecoy: true, score: 20, psmQValue: 0.5, peptideQValue: 0.5, scanNumber: scan++);
            m.PsmFdrInfo.PEP = p;
            matches.Add(m);
        }

        var (pepAsc, qByPep) = TransientPepAnalysisEngine.BuildPepQValueCurve(matches, peptideLevel: false);

        Assert.That(pepAsc.Length, Is.EqualTo(matches.Count));
        // PEP axis sorted ascending.
        for (int i = 1; i < pepAsc.Length; i++)
            Assert.That(pepAsc[i], Is.GreaterThanOrEqualTo(pepAsc[i - 1]), "PEP axis must be ascending");
        // q-value axis monotone non-decreasing.
        for (int i = 1; i < qByPep.Length; i++)
            Assert.That(qByPep[i], Is.GreaterThanOrEqualTo(qByPep[i - 1]), "PEP_QValue must be non-decreasing in PEP");
        // The most-confident (lowest-PEP) target has a better q-value than the worst decoy.
        Assert.That(qByPep.First(), Is.LessThan(qByPep.Last()));
    }
}
