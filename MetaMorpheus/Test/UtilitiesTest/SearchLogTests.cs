using EngineLayer;
using EngineLayer.SpectrumMatch;
using NUnit.Framework;
using Omics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using Proteomics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Test.UtilitiesTest;

[TestFixture]
public class SearchLogTests
{

    private static BioPolymerNotchFragmentIonComparer comparer;
    private static Protein targetProtein;
    private static Protein decoyProtein;
    private static PeptideWithSetModifications targetPwsm;
    private static PeptideWithSetModifications decoyPwsm;


    [SetUp]
    public static void Setup()
    {
        comparer = new BioPolymerNotchFragmentIonComparer();
        targetProtein = new Protein("PEPTIDEK", "accession");
        decoyProtein = new Protein("PEPTIDEK", "decoy");
        targetPwsm = new PeptideWithSetModifications("PEPTIDEK", null, p: targetProtein);
        decoyPwsm = new PeptideWithSetModifications("PEPTIDEK", null, p: decoyProtein);
    }

    private class MockSearchAttempt : ISearchAttempt
    {
        public double Score { get; set; }
        public int Notch { get; set; }
        public bool IsDecoy { get; set; }

        public bool Equals(ISearchAttempt other)
        {
            return other != null && Math.Abs(Score - other.Score) < SpectralMatch.ToleranceForScoreDifferentiation && Notch == other.Notch && IsDecoy == other.IsDecoy;
        }
    }

    [Test]
    public void TestAddAttempt_Target()
    {
        var searchLog = new SearchLog(2, 2);
        var attempt1 = new MockSearchAttempt { Score = 10, Notch = 1, IsDecoy = false };
        var attempt2 = new MockSearchAttempt { Score = 20, Notch = 2, IsDecoy = false };

        searchLog.AddAttempt(attempt1);
        searchLog.AddAttempt(attempt2);

        var attempts = searchLog.GetAttemptsByType(false).ToList();
        Assert.That(attempts.Count, Is.EqualTo(2));
        Assert.That(attempts[0], Is.EqualTo(attempt2));
        Assert.That(attempts[1], Is.EqualTo(attempt1));
    }

    [Test]
    public void TestAddAttempt_Decoy()
    {
        var searchLog = new SearchLog(2, 2);
        var attempt1 = new MockSearchAttempt { Score = 10, Notch = 1, IsDecoy = true };
        var attempt2 = new MockSearchAttempt { Score = 20, Notch = 2, IsDecoy = true };

        searchLog.AddAttempt(attempt1);
        searchLog.AddAttempt(attempt2);

        var attempts = searchLog.GetAttemptsByType(true).ToList();
        Assert.That(attempts.Count, Is.EqualTo(2));
        Assert.That(attempts[0], Is.EqualTo(attempt2));
        Assert.That(attempts[1], Is.EqualTo(attempt1));
    }

    [Test]
    public void TestAddAttempt_ExceedMaxTargets()
    {
        var searchLog = new SearchLog(1, 2);
        var attempt1 = new MockSearchAttempt { Score = 10, Notch = 1, IsDecoy = false };
        var attempt2 = new MockSearchAttempt { Score = 20, Notch = 2, IsDecoy = false };

        searchLog.AddAttempt(attempt1);
        searchLog.AddAttempt(attempt2);

        var attempts = searchLog.GetAttemptsByType(false).ToList();
        Assert.That(attempts.Count, Is.EqualTo(1));
        Assert.That(attempts[0], Is.EqualTo(attempt2));
    }

    [Test]
    public void TestAddAttempt_ExceedMaxDecoys()
    {
        var searchLog = new SearchLog(2, 1);
        var attempt1 = new MockSearchAttempt { Score = 10, Notch = 1, IsDecoy = true };
        var attempt2 = new MockSearchAttempt { Score = 20, Notch = 2, IsDecoy = true };

        searchLog.AddAttempt(attempt1);
        searchLog.AddAttempt(attempt2);

        var attempts = searchLog.GetAttemptsByType(true).ToList();
        Assert.That(attempts.Count, Is.EqualTo(1));
        Assert.That(attempts[0], Is.EqualTo(attempt2));
    }

    [Test]
    public void TestGetAttempts()
    {
        var searchLog = new SearchLog(2, 2);
        var attempt1 = new MockSearchAttempt { Score = 10, Notch = 1, IsDecoy = false };
        var attempt2 = new MockSearchAttempt { Score = 20, Notch = 2, IsDecoy = true };

        searchLog.AddAttempt(attempt1);
        searchLog.AddAttempt(attempt2);

        var attempts = searchLog.GetAttempts().ToList();
        Assert.That(attempts.Count, Is.EqualTo(2));
        Assert.That(attempts.Contains(attempt1));
        Assert.That(attempts.Contains(attempt2));
    }

    [Test]
    public void TestGetTopScoringAttempts_Target()
    {
        var searchLog = new SearchLog(3, 3);
        var attempt1 = new MockSearchAttempt { Score = 10, Notch = 1, IsDecoy = false };
        var attempt2 = new MockSearchAttempt { Score = 20, Notch = 2, IsDecoy = false };
        var attempt3 = new MockSearchAttempt { Score = 15, Notch = 3, IsDecoy = false };

        searchLog.AddAttempt(attempt1);
        searchLog.AddAttempt(attempt2);
        searchLog.AddAttempt(attempt3);

        var topAttempts = searchLog.GetTopScoringAttempts(5).ToList();
        Assert.That(topAttempts.Count, Is.EqualTo(2));
        Assert.That(topAttempts[0], Is.EqualTo(attempt2));
        Assert.That(topAttempts[1], Is.EqualTo(attempt3));
    }

    [Test]
    public void TestGetTopScoringAttempts_Decoy()
    {
        var searchLog = new SearchLog(3, 3);
        var attempt1 = new MockSearchAttempt { Score = 10, Notch = 1, IsDecoy = true };
        var attempt2 = new MockSearchAttempt { Score = 20, Notch = 2, IsDecoy = true };
        var attempt3 = new MockSearchAttempt { Score = 15, Notch = 3, IsDecoy = true };

        searchLog.AddAttempt(attempt1);
        searchLog.AddAttempt(attempt2);
        searchLog.AddAttempt(attempt3);

        var topAttempts = searchLog.GetTopScoringAttempts(5).ToList();
        Assert.That(topAttempts.Count, Is.EqualTo(2));
        Assert.That(topAttempts[0], Is.EqualTo(attempt2));
        Assert.That(topAttempts[1], Is.EqualTo(attempt3));
    }

    [Test]
    public void TestGetTopScoringAttempts_MaxScoreDifference()
    {
        var searchLog = new SearchLog(3, 3);
        var attempt1 = new MockSearchAttempt { Score = 10, Notch = 1, IsDecoy = false };
        var attempt2 = new MockSearchAttempt { Score = 20, Notch = 2, IsDecoy = false };
        var attempt3 = new MockSearchAttempt { Score = 15, Notch = 3, IsDecoy = true };

        searchLog.AddAttempt(attempt1);
        searchLog.AddAttempt(attempt2);
        searchLog.AddAttempt(attempt3);

        var topAttempts = searchLog.GetTopScoringAttempts(10).ToList();
        Assert.That(topAttempts.Count, Is.EqualTo(3));
        Assert.That(topAttempts[0], Is.EqualTo(attempt2));
        Assert.That(topAttempts[1], Is.EqualTo(attempt3));
        Assert.That(topAttempts[2], Is.EqualTo(attempt1));
    }

    [Test]
    public void TestGetTopScoringAttemptsWithSequenceInformation_MaxScoreDifference()
    {
        var searchLog = new SearchLog(3, 3);
        var attempt1 = new SpectralMatchHypothesis(1, targetPwsm, null, 10);
        var attempt2 = new SpectralMatchHypothesis(2, decoyPwsm, null, 20);
        var attempt3 = new SpectralMatchHypothesis(3, targetPwsm, null, 15);
        var attempt4 = new MockSearchAttempt { Score = 5, Notch = 4, IsDecoy = false };

        searchLog.AddAttempt(attempt1);
        searchLog.AddAttempt(attempt2);
        searchLog.AddAttempt(attempt3);
        searchLog.AddAttempt(attempt4);

        var topAttempts = searchLog.GetTopScoringAttemptsWithSequenceInformation(10).ToList();
        Assert.That(topAttempts.Count, Is.EqualTo(3));
        Assert.That(topAttempts[0], Is.EqualTo(attempt2));
        Assert.That(topAttempts[1], Is.EqualTo(attempt3));
        Assert.That(topAttempts[2], Is.EqualTo(attempt1));
    }

    [Test]
    public void TestGetTopScoringAttemptsWithSequenceInformation_Empty()
    {
        var searchLog = new SearchLog(3, 3);
        var topAttempts = searchLog.GetTopScoringAttemptsWithSequenceInformation(10).ToList();
        Assert.That(topAttempts.Count, Is.EqualTo(0));
    }

    [Test]
    public void TestGetTopScoringAttemptsWithSequenceInformation_NoSequenceInformation()
    {
        var searchLog = new SearchLog(3, 3);
        var attempt1 = new MockSearchAttempt { Score = 10, Notch = 1, IsDecoy = false };
        var attempt2 = new MockSearchAttempt { Score = 20, Notch = 2, IsDecoy = false };

        searchLog.AddAttempt(attempt1);
        searchLog.AddAttempt(attempt2);

        var topAttempts = searchLog.GetTopScoringAttemptsWithSequenceInformation(10).ToList();
        Assert.That(topAttempts.Count, Is.EqualTo(0));
    }
}