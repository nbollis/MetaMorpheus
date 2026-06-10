using EngineLayer.FdrAnalysis;
using NUnit.Framework;
using PST = TaskLayer.ParallelSearch.ParallelSearchTask;

namespace Test.ParallelSearchTask;

/// <summary>
/// Tests the row-level output confidence gate. When a PEP model is active a match is confident iff its
/// PEP_QValue is strictly below 5%; otherwise it falls back to the (inclusive) score-based QValue. The two
/// axes are independent — a great QValue does not make a poor-PEP match confident, and vice versa.
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
    public void PepActive_UsesPepQValue_StrictlyBelowThreshold()
    {
        Assert.That(PST.IsConfidentMatch(new FdrInfo { PEP_QValue = 0.049, QValue = 0.9 }, true, 0.05, 0.05), Is.True);
        Assert.That(PST.IsConfidentMatch(new FdrInfo { PEP_QValue = 0.05, QValue = 0.0 }, true, 0.05, 0.05), Is.False); // strict <
        Assert.That(PST.IsConfidentMatch(new FdrInfo { PEP_QValue = 0.06, QValue = 0.0 }, true, 0.05, 0.05), Is.False);
    }

    [Test]
    public void PepActive_IgnoresScoreQValue()
    {
        // Excellent score-based QValue but poor PEP_QValue -> NOT confident when PEP is active.
        Assert.That(PST.IsConfidentMatch(new FdrInfo { PEP_QValue = 0.5, QValue = 0.001 }, true, 0.05, 0.05), Is.False);
    }

    [Test]
    public void PepInactive_FallsBackToQValue_InclusiveThreshold()
    {
        Assert.That(PST.IsConfidentMatch(new FdrInfo { QValue = 0.05, PEP_QValue = 2.0 }, false, 0.05, 0.05), Is.True);  // <= inclusive
        Assert.That(PST.IsConfidentMatch(new FdrInfo { QValue = 0.051, PEP_QValue = 0.0 }, false, 0.05, 0.05), Is.False);
    }
}
