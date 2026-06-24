using EngineLayer.FdrAnalysis;
using NUnit.Framework;
using PST = TaskLayer.ParallelSearch.ParallelSearchTask;

namespace Test.ParallelSearchTask;

/// <summary>
/// Tests the row-level output confidence gate. When a PEP model is active a match is confident if EITHER its
/// PEP_QValue is strictly below 5% OR its (inclusive) score-based QValue clears 5% — the per-database files
/// exist so the user can investigate any reasonable hit, so an edge-case match that only one metric flags is
/// still written. When PEP is inactive, only the score-based QValue is used.
/// </summary>
[TestFixture]
public class ConfidenceFilterTests
{
    [Test]
    public void NullInfo_IsNeverConfident()
    {
        Assert.That(PST.IsConfidentMatch(null, pepActive: true, pepQThreshold: 0.05, qThreshold: 0.05), Is.False);
        Assert.That(PST.IsConfidentMatch(null, pepActive: false, pepQThreshold: 0.05, qThreshold: 0.05), Is.False);
    }

    [Test]
    public void PepActive_ConfidentWhenPepQValueStrictlyBelowThreshold()
    {
        // Poor score QValue, but PEP clears -> confident on the PEP axis.
        Assert.That(PST.IsConfidentMatch(new FdrInfo { PEP_QValue = 0.049, QValue = 0.9 }, true, 0.05, 0.05), Is.True);
        // PEP at the boundary is NOT confident on the PEP axis (strict <), and the score QValue is poor.
        Assert.That(PST.IsConfidentMatch(new FdrInfo { PEP_QValue = 0.05, QValue = 0.9 }, true, 0.05, 0.05), Is.False);
        // Both metrics fail -> not confident.
        Assert.That(PST.IsConfidentMatch(new FdrInfo { PEP_QValue = 0.06, QValue = 0.9 }, true, 0.05, 0.05), Is.False);
    }

    [Test]
    public void PepActive_ConfidentByEitherMetric()
    {
        // Poor PEP_QValue but excellent score-based QValue -> confident when PEP is active (either metric).
        Assert.That(PST.IsConfidentMatch(new FdrInfo { PEP_QValue = 0.5, QValue = 0.001 }, true, 0.05, 0.05), Is.True);
        // Score QValue at the inclusive boundary also clears.
        Assert.That(PST.IsConfidentMatch(new FdrInfo { PEP_QValue = 0.5, QValue = 0.05 }, true, 0.05, 0.05), Is.True);
    }

    [Test]
    public void PepInactive_FallsBackToQValue_InclusiveThreshold()
    {
        Assert.That(PST.IsConfidentMatch(new FdrInfo { QValue = 0.05, PEP_QValue = 2.0 }, false, 0.05, 0.05), Is.True);  // <= inclusive
        Assert.That(PST.IsConfidentMatch(new FdrInfo { QValue = 0.051, PEP_QValue = 0.0 }, false, 0.05, 0.05), Is.False);
    }
}
