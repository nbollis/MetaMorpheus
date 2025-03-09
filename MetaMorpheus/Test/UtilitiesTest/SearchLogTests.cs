using EngineLayer;
using EngineLayer.SpectrumMatch;
using NUnit.Framework;
using Omics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using Proteomics;
using System;
using System.Collections.Generic;
using System.Linq;

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
        decoyProtein = new Protein("PEPTIDEK", "decoy", isDecoy: true);
        targetPwsm = new PeptideWithSetModifications("PEPTIDEK", null, p: targetProtein);
        decoyPwsm = new PeptideWithSetModifications("PEPTIDEK", null, p: decoyProtein);
    }

    [Test]
    public void TestAddAttempt_Target()
    {
        var searchLog = new SearchLog(5, 2, 2);
        var attempt1 = new MinimalSearchAttempt { Score = 10, IsDecoy = false };
        var attempt2 = new MinimalSearchAttempt { Score = 20, IsDecoy = false };

        searchLog.Add(attempt1);
        searchLog.Add(attempt2);

        var attempts = searchLog.GetAttemptsByType(false).ToList();
        Assert.That(attempts.Count, Is.EqualTo(2));
        Assert.That(attempts[0], Is.EqualTo(attempt2));
        Assert.That(attempts[1], Is.EqualTo(attempt1));
    }

    [Test]
    public void TestAddAttempt_Decoy()
    {
        var searchLog = new SearchLog(5, 2, 2);
        var attempt1 = new MinimalSearchAttempt { Score = 10, IsDecoy = true };
        var attempt2 = new MinimalSearchAttempt { Score = 20, IsDecoy = true };

        searchLog.Add(attempt1);
        searchLog.Add(attempt2);

        var attempts = searchLog.GetAttemptsByType(true).ToList();
        Assert.That(attempts.Count, Is.EqualTo(2));
        Assert.That(attempts[0], Is.EqualTo(attempt2));
        Assert.That(attempts[1], Is.EqualTo(attempt1));
    }

    [Test]
    public void TestAddAttempt_ExceedMaxTargets()
    {
        var searchLog = new SearchLog(5, 1, 2);
        var attempt1 = new MinimalSearchAttempt { Score = 10, IsDecoy = false };
        var attempt2 = new MinimalSearchAttempt { Score = 20, IsDecoy = false };

        searchLog.Add(attempt1);
        searchLog.Add(attempt2);

        var attempts = searchLog.GetAttemptsByType(false).ToList();
        Assert.That(attempts.Count, Is.EqualTo(1));
        Assert.That(attempts[0], Is.EqualTo(attempt2));
    }

    [Test]
    public void TestAddAttempt_ExceedMaxDecoys()
    {
        var searchLog = new SearchLog(5, 2, 1);
        var attempt1 = new MinimalSearchAttempt { Score = 10, IsDecoy = true };
        var attempt2 = new MinimalSearchAttempt { Score = 20, IsDecoy = true };

        searchLog.Add(attempt1);
        searchLog.Add(attempt2);

        var attempts = searchLog.GetAttemptsByType(true).ToList();
        Assert.That(attempts.Count, Is.EqualTo(1));
        Assert.That(attempts[0], Is.EqualTo(attempt2));
    }

    [Test]
    public void TestGetAttempts()
    {
        var searchLog = new SearchLog(5, 2, 2);
        var attempt1 = new MinimalSearchAttempt { Score = 10, IsDecoy = false };
        var attempt2 = new MinimalSearchAttempt { Score = 20, IsDecoy = true };

        searchLog.Add(attempt1);
        searchLog.Add(attempt2);

        var attempts = searchLog.GetAttempts().ToList();
        Assert.That(attempts.Count, Is.EqualTo(2));
        Assert.That(attempts.Contains(attempt1));
        Assert.That(attempts.Contains(attempt2));
    }

    [Test]
    public void TestGetTopScoringAttempts_Target()
    {
        var searchLog = new SearchLog(5, 3, 3);
        var attempt1 = new MinimalSearchAttempt { Score = 10, IsDecoy = false };
        var attempt2 = new MinimalSearchAttempt { Score = 20, IsDecoy = false };
        var attempt3 = new MinimalSearchAttempt { Score = 15, IsDecoy = false };

        searchLog.Add(attempt1);
        searchLog.Add(attempt2);
        searchLog.Add(attempt3);

        var topAttempts = searchLog.GetTopScoringAttempts().ToList();
        Assert.That(topAttempts.Count, Is.EqualTo(2));
        Assert.That(topAttempts[0], Is.EqualTo(attempt2));
        Assert.That(topAttempts[1], Is.EqualTo(attempt3));
    }

    [Test]
    public void TestGetTopScoringAttempts_Decoy()
    {
        var searchLog = new SearchLog(5, 3, 3);
        var attempt1 = new MinimalSearchAttempt { Score = 10, IsDecoy = true };
        var attempt2 = new MinimalSearchAttempt { Score = 20, IsDecoy = true };
        var attempt3 = new MinimalSearchAttempt { Score = 15, IsDecoy = true };

        searchLog.Add(attempt1);
        searchLog.Add(attempt2);
        searchLog.Add(attempt3);

        var topAttempts = searchLog.GetTopScoringAttempts().ToList();
        Assert.That(topAttempts.Count, Is.EqualTo(2));
        Assert.That(topAttempts[0], Is.EqualTo(attempt2));
        Assert.That(topAttempts[1], Is.EqualTo(attempt3));
    }

    [Test]
    public void TestGetTopScoringAttempts_MaxScoreDifference()
    {
        var searchLog = new SearchLog(10, 3, 3);
        var attempt1 = new MinimalSearchAttempt { Score = 10, IsDecoy = false };
        var attempt2 = new MinimalSearchAttempt { Score = 20, IsDecoy = false };
        var attempt3 = new MinimalSearchAttempt { Score = 15, IsDecoy = true };

        searchLog.Add(attempt1);
        searchLog.Add(attempt2);
        searchLog.Add(attempt3);

        var topAttempts = searchLog.GetTopScoringAttempts().ToList();
        Assert.That(topAttempts.Count, Is.EqualTo(3));
        Assert.That(topAttempts[0], Is.EqualTo(attempt2));
        Assert.That(topAttempts[1], Is.EqualTo(attempt3));
        Assert.That(topAttempts[2], Is.EqualTo(attempt1));
    }

    [Test]
    public void TestGetTopScoringAttemptsWithSequenceInformation_MaxScoreDifference()
    {
        var searchLog = new SearchLog(10, 3, 3);
        var attempt1 = new SpectralMatchHypothesis(1, targetPwsm, null, 10);
        var attempt2 = new SpectralMatchHypothesis(2, decoyPwsm, null, 20);
        var attempt3 = new SpectralMatchHypothesis(3, targetPwsm, null, 15);
        var attempt4 = new MinimalSearchAttempt { Score = 5, IsDecoy = false };

        searchLog.Add(attempt1);
        searchLog.Add(attempt2);
        searchLog.Add(attempt3);
        searchLog.Add(attempt4);

        var topAttempts = searchLog.GetTopScoringAttemptsWithSequenceInformation().ToList();
        Assert.That(topAttempts.Count, Is.EqualTo(3));
        Assert.That(topAttempts[0], Is.EqualTo(attempt2));
        Assert.That(topAttempts[1], Is.EqualTo(attempt3));
        Assert.That(topAttempts[2], Is.EqualTo(attempt1));
    }

    [Test]
    public void TestGetTopScoringAttemptsWithSequenceInformation_Empty()
    {
        var searchLog = new SearchLog(10, 3, 3);
        var topAttempts = searchLog.GetTopScoringAttemptsWithSequenceInformation().ToList();
        Assert.That(topAttempts.Count, Is.EqualTo(0));
    }

    [Test]
    public void TestGetTopScoringAttemptsWithSequenceInformation_NoSequenceInformation()
    {
        var searchLog = new SearchLog(10, 3, 3);
        var attempt1 = new MinimalSearchAttempt { Score = 10, IsDecoy = false };
        var attempt2 = new MinimalSearchAttempt { Score = 20, IsDecoy = false };

        searchLog.Add(attempt1);
        searchLog.Add(attempt2);

        var topAttempts = searchLog.GetTopScoringAttemptsWithSequenceInformation().ToList();
        Assert.That(topAttempts.Count, Is.EqualTo(0));
    }

    [Test]
    public void TestTryRemoveThisAmbiguousPeptide_Target()
    {
        var searchLog = new SearchLog(5, 2, 2);
        var attempt1 = new SpectralMatchHypothesis(1, targetPwsm, null, 10);
        var attempt2 = new SpectralMatchHypothesis(2, targetPwsm, null, 20);

        searchLog.Add(attempt1);
        searchLog.Add(attempt2);

        bool removed = searchLog.TryRemoveThisAmbiguousPeptide(attempt1);
        var attempts = searchLog.GetAttemptsByType(false).ToList();

        Assert.That(removed, Is.True);
        Assert.That(attempts.Count, Is.EqualTo(1));
        Assert.That(attempts[0], Is.EqualTo(attempt2));
    }

    [Test]
    public void TestTryRemoveThisAmbiguousPeptide_Decoy()
    {
        var searchLog = new SearchLog(5, 2, 2);
        var attempt1 = new SpectralMatchHypothesis(1, decoyPwsm, null, 10);
        var attempt2 = new SpectralMatchHypothesis(2, decoyPwsm, null, 20);

        searchLog.Add(attempt1);
        searchLog.Add(attempt2);

        bool removed = searchLog.TryRemoveThisAmbiguousPeptide(attempt1);
        var attempts = searchLog.GetAttemptsByType(true).ToList();

        Assert.That(removed, Is.True);
        Assert.That(attempts.Count, Is.EqualTo(1));
        Assert.That(attempts[0], Is.EqualTo(attempt2));
    }

    [Test]
    public void TestTryRemoveThisAmbiguousPeptide_NotFound()
    {
        var searchLog = new SearchLog(5, 2, 2);
        var attempt1 = new SpectralMatchHypothesis(1, targetPwsm, null, 10);
        var attempt2 = new SpectralMatchHypothesis(2, targetPwsm, null, 20);
        var attempt3 = new SpectralMatchHypothesis(3, targetPwsm, null, 30);

        searchLog.Add(attempt1);
        searchLog.Add(attempt2);

        bool removed = searchLog.TryRemoveThisAmbiguousPeptide(attempt3);
        var attempts = searchLog.GetAttemptsByType(false).ToList();

        Assert.That(removed, Is.False);
        Assert.That(attempts.Count, Is.EqualTo(2));
        Assert.That(attempts.Contains(attempt1));
        Assert.That(attempts.Contains(attempt2));
    }

    [Test]
    public void TestTrimProteinMatches_Target()
    {
        var searchLog = new SearchLog(5, 2, 2);
        var attempt1 = new SpectralMatchHypothesis(1, targetPwsm, null, 10);
        var attempt2 = new SpectralMatchHypothesis(2, targetPwsm, null, 20);
        var attempt3 = new SpectralMatchHypothesis(3, decoyPwsm, null, 15);

        searchLog.Add(attempt1);
        searchLog.Add(attempt2);
        searchLog.Add(attempt3);

        var parsimoniousProteins = new HashSet<Protein> { targetProtein };
        searchLog.TrimProteinMatches(parsimoniousProteins);

        var attempts = searchLog.GetAttempts().ToList();
        Assert.That(attempts.Count, Is.EqualTo(2));
        Assert.That(attempts.Contains(attempt1));
        Assert.That(attempts.Contains(attempt2));
    }

    [Test]
    public void TestTrimProteinMatches_Decoy()
    {
        var searchLog = new SearchLog(5, 2, 2);
        var attempt1 = new SpectralMatchHypothesis(1, decoyPwsm, null, 10);
        var attempt2 = new SpectralMatchHypothesis(2, decoyPwsm, null, 20);
        var attempt3 = new SpectralMatchHypothesis(3, targetPwsm, null, 15);

        searchLog.Add(attempt1);
        searchLog.Add(attempt2);
        searchLog.Add(attempt3);

        var parsimoniousProteins = new HashSet<Protein> { decoyProtein };
        searchLog.TrimProteinMatches(parsimoniousProteins);

        var attempts = searchLog.GetAttempts().ToList();
        Assert.That(attempts.Count, Is.EqualTo(2));
        Assert.That(attempts.Contains(attempt1));
        Assert.That(attempts.Contains(attempt2));
    }

    [Test]
    public void TestTrimProteinMatches_Mixed()
    {
        var searchLog = new SearchLog(5, 2, 2);
        var attempt1 = new SpectralMatchHypothesis(1, targetPwsm, null, 10);
        var attempt2 = new SpectralMatchHypothesis(2, decoyPwsm, null, 20);
        var attempt3 = new SpectralMatchHypothesis(3, targetPwsm, null, 15);
        var attempt4 = new SpectralMatchHypothesis(4, decoyPwsm, null, 25);

        searchLog.Add(attempt1);
        searchLog.Add(attempt2);
        searchLog.Add(attempt3);
        searchLog.Add(attempt4);

        var parsimoniousProteins = new HashSet<Protein> { targetProtein, decoyProtein };
        searchLog.TrimProteinMatches(parsimoniousProteins);

        var attempts = searchLog.GetAttempts().ToList();
        Assert.That(attempts.Count, Is.EqualTo(4));
        Assert.That(attempts.Contains(attempt1));
        Assert.That(attempts.Contains(attempt2));
        Assert.That(attempts.Contains(attempt3));
        Assert.That(attempts.Contains(attempt4));
    }

    [Test]
    public void TestTrimProteinMatches_Empty()
    {
        var searchLog = new SearchLog(5, 2, 2);
        var attempt1 = new SpectralMatchHypothesis(1, targetPwsm, null, 10);
        var attempt2 = new SpectralMatchHypothesis(2, decoyPwsm, null, 20);

        searchLog.Add(attempt1);
        searchLog.Add(attempt2);

        var parsimoniousProteins = new HashSet<Protein>();
        searchLog.TrimProteinMatches(parsimoniousProteins);

        var attempts = searchLog.GetAttempts().ToList();
        Assert.That(attempts.Count, Is.EqualTo(0));
    }
}