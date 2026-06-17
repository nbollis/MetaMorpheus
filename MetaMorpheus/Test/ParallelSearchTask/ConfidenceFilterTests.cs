using EngineLayer.FdrAnalysis;
using NUnit.Framework;
using PST = TaskLayer.ParallelSearch.ParallelSearchTask;

namespace Test.ParallelSearchTask;

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
        Assert.That(PST.IsConfidentMatch(new FdrInfo { PEP_QValue = 0.049, QValue = 0.9 }, true, 0.05, 0.05), Is.True);
        Assert.That(PST.IsConfidentMatch(new FdrInfo { PEP_QValue = 0.05, QValue = 0.9 }, true, 0.05, 0.05), Is.False);
        Assert.That(PST.IsConfidentMatch(new FdrInfo { PEP_QValue = 0.06, QValue = 0.9 }, true, 0.05, 0.05), Is.False);
    }

    [Test]
    public void PepActive_ConfidentByEitherMetric()
    {
        Assert.That(PST.IsConfidentMatch(new FdrInfo { PEP_QValue = 0.5, QValue = 0.001 }, true, 0.05, 0.05), Is.True);
        Assert.That(PST.IsConfidentMatch(new FdrInfo { PEP_QValue = 0.5, QValue = 0.05 }, true, 0.05, 0.05), Is.True);
    }

    [Test]
    public void PepInactive_FallsBackToQValue_InclusiveThreshold()
    {
        Assert.That(PST.IsConfidentMatch(new FdrInfo { QValue = 0.05, PEP_QValue = 2.0 }, false, 0.05, 0.05), Is.True);
        Assert.That(PST.IsConfidentMatch(new FdrInfo { QValue = 0.051, PEP_QValue = 0.0 }, false, 0.05, 0.05), Is.False);
    }
}
