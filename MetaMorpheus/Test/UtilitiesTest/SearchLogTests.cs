using EngineLayer.SpectrumMatch;
using NUnit.Framework;
using Omics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using Proteomics;
using System.Collections.Generic;
using System.Linq;
using static Test.UtilitiesTest.SpectralMatchHypothesisTests;

namespace Test.UtilitiesTest;

[TestFixture]
public class SearchLogTests
{
    private static Protein targetProtein;
    private static Protein decoyProtein;
    private static PeptideWithSetModifications targetPwsm;
    private static PeptideWithSetModifications decoyPwsm;
    private static List<MatchedFragmentIon> emptyList;

    [SetUp]
    public static void Setup()
    {
        targetProtein = new Protein("PEPTIDEK", "accession");
        decoyProtein = new Protein("PEPTIDEK", "decoy", isDecoy: true);
        targetPwsm = new PeptideWithSetModifications("PEPTIDEK", null, p: targetProtein);
        decoyPwsm = new PeptideWithSetModifications("PEPTIDEK", null, p: decoyProtein);
        emptyList = new();
    }

    [Test]
    public void TestAdd()
    {
        var log = new TopScoringOnlySearchLog();
        var attempt = new SpectralMatchHypothesis(0, targetPwsm, emptyList, 10.0);

        bool added = log.Add(attempt);

        Assert.That(added, Is.True);
        Assert.That(log.Count, Is.EqualTo(1));
        Assert.That(log.Score, Is.EqualTo(10.0));
        Assert.That(log.NumberOfBestScoringResults, Is.EqualTo(1));
        Assert.That(log.RunnerUpScore, Is.EqualTo(0));
    }

    [Test]
    public void TestRemove()
    {
        var log = new TopScoringOnlySearchLog();
        var attempt = new SpectralMatchHypothesis(0, targetPwsm, emptyList, 10.0);

        log.Add(attempt);
        bool removed = log.Remove(attempt);

        Assert.That(removed, Is.True);
        Assert.That(log.Count, Is.EqualTo(0));
        Assert.That(log.Score, Is.EqualTo(0));
        Assert.That(log.NumberOfBestScoringResults, Is.EqualTo(0));
        Assert.That(log.RunnerUpScore, Is.EqualTo(0));
    }

    [Test]
    public void TestClear()
    {
        var log = new TopScoringOnlySearchLog();
        var attempt = new SpectralMatchHypothesis(0, targetPwsm, emptyList, 10.0);

        log.Add(attempt);
        log.Clear();

        Assert.That(log.Count, Is.EqualTo(0));
        Assert.That(log.Score, Is.EqualTo(0));
        Assert.That(log.NumberOfBestScoringResults, Is.EqualTo(0));
        Assert.That(log.RunnerUpScore, Is.EqualTo(0));
    }

    [Test]
    public void TestAddOrReplace()
    {
        var log = new TopScoringOnlySearchLog(0.001, 1);
        var pwsm = targetPwsm;
        var matchedFragmentIons = emptyList;

        // Test adding a new score
        bool added = log.AddOrReplace(pwsm, 10.0, 0, true, matchedFragmentIons);

        Assert.That(added, Is.True);
        Assert.That(log.Count, Is.EqualTo(1));
        Assert.That(log.Score, Is.EqualTo(10.0));
        Assert.That(log.NumberOfBestScoringResults, Is.EqualTo(1));
        Assert.That(log.RunnerUpScore, Is.EqualTo(1));

        // Test adding a score with ambiguity allowed
        added = log.AddOrReplace(decoyPwsm, 10.0, 0, true, matchedFragmentIons);
        Assert.That(added, Is.True);
        Assert.That(log.Count, Is.EqualTo(2));
        Assert.That(log.Score, Is.EqualTo(10.0));
        Assert.That(log.NumberOfBestScoringResults, Is.EqualTo(2));
        Assert.That(log.RunnerUpScore, Is.EqualTo(1));

        // Test adding a score with ambiguity not allowed
        added = log.AddOrReplace(decoyPwsm, 10.0, 0, false, matchedFragmentIons);
        Assert.That(added, Is.False);
        Assert.That(log.Count, Is.EqualTo(2));
        Assert.That(log.Score, Is.EqualTo(10.0));
        Assert.That(log.NumberOfBestScoringResults, Is.EqualTo(2));
        Assert.That(log.RunnerUpScore, Is.EqualTo(10));

        // Test adding a new higher score
        added = log.AddOrReplace(pwsm, 20.0, 0, true, matchedFragmentIons);
        Assert.That(added, Is.True);
        Assert.That(log.Count, Is.EqualTo(1));
        Assert.That(log.Score, Is.EqualTo(20.0));
        Assert.That(log.NumberOfBestScoringResults, Is.EqualTo(1));
        Assert.That(log.RunnerUpScore, Is.EqualTo(10.0));

        // Test adding a new lower score
        added = log.AddOrReplace(pwsm, 5.0, 0, true, matchedFragmentIons);
        Assert.That(added, Is.False);
        Assert.That(log.Count, Is.EqualTo(1));
        Assert.That(log.Score, Is.EqualTo(20.0));
        Assert.That(log.NumberOfBestScoringResults, Is.EqualTo(1));
        Assert.That(log.RunnerUpScore, Is.EqualTo(10.0));

        // Test adding a new score that is better than the runner-up score
        added = log.AddOrReplace(pwsm, 15.0, 0, true, matchedFragmentIons);
        Assert.That(added, Is.False);
        Assert.That(log.Count, Is.EqualTo(1));
        Assert.That(log.Score, Is.EqualTo(20.0));
        Assert.That(log.NumberOfBestScoringResults, Is.EqualTo(1));
        Assert.That(log.RunnerUpScore, Is.EqualTo(15.0));
    }

    [Test]
    public void TestAddDuplicates()
    {
        var log = new TopScoringOnlySearchLog(0.1, 0.5);
        var attempts = new List<ISearchAttempt>
        {
            new TestSearchAttempt(0, false, 1.0, "test1"),
            new TestSearchAttempt(0, false, 2.0, "test2")
        };

        log.AddRange(attempts);
        Assert.That(log.Count, Is.EqualTo(2));
        log.AddRange(attempts);
        Assert.That(log.Count, Is.EqualTo(2));
    }

    [Test]
    public void TestCloneWithAttempts()
    {
        var log = new TopScoringOnlySearchLog();
        var attempt = new SpectralMatchHypothesis(0, targetPwsm, emptyList, 10.0);

        log.Add(attempt);
        var clone = log.CloneWithAttempts(new List<ISearchAttempt> { attempt });

        Assert.That(clone.Count, Is.EqualTo(1));
        Assert.That(clone.Score, Is.EqualTo(10.0));
        Assert.That(log.NumberOfBestScoringResults, Is.EqualTo(1));
        Assert.That(log.RunnerUpScore, Is.EqualTo(0));
    }

    [Test]
    public void TestGetAttempts()
    {
        var log = new TopScoringOnlySearchLog();
        var attempt = new SpectralMatchHypothesis(0, targetPwsm, emptyList, 10.0);

        log.Add(attempt);
        var attempts = log.GetAttempts();

        Assert.That(attempts, Is.Not.Null);
        Assert.That(attempts, Has.Exactly(1).EqualTo(attempt));
    }

    [Test]
    public void TestAddRange()
    {
        var log = new TopScoringOnlySearchLog(0.1, 0.5);
        var attempts = new List<ISearchAttempt>
        {
            new TestSearchAttempt(0, false, 1.0, "test1"),
            new TestSearchAttempt(0, false, 2.0, "test2")
        };

        log.AddRange(attempts);
        Assert.That(log.Count, Is.EqualTo(2));
    }

    [Test]
    public void TestRemoveRange()
    {
        var log = new TopScoringOnlySearchLog(0.1, 0.5);
        var attempts = new List<ISearchAttempt>
        {
            new TestSearchAttempt(0, false, 1.0, "test1"),
            new TestSearchAttempt(0, false, 2.0, "test2")
        };

        log.AddRange(attempts);
        log.RemoveRange(attempts);
        Assert.That(log.Count, Is.EqualTo(0));
    }

    [Test]
    public void TestTrimProteinMatches()
    {
        // Arrange
        var protein1 = new Protein("Protein1", null);
        var protein2 = new Protein("Protein2", null);
        var protein3 = new Protein("Protein3", null);

        var parsimoniousProteins = new HashSet<Protein> { protein1, protein3 };

        var searchLog = new TopScoringOnlySearchLog(0.1, 0.1);

        var attempt1 = new SpectralMatchHypothesis(0, new PeptideWithSetModifications("", [], p: protein1), new List<MatchedFragmentIon>(), 10);
        var attempt2 = new SpectralMatchHypothesis(0, new PeptideWithSetModifications("", [], p: protein2), new List<MatchedFragmentIon>(), 20);
        var attempt3 = new SpectralMatchHypothesis(0, new PeptideWithSetModifications("", [], p: protein3), new List<MatchedFragmentIon>(), 30);

        searchLog.Add(attempt1);
        searchLog.Add(attempt2);
        searchLog.Add(attempt3);

        // Act
        searchLog.TrimProteinMatches(parsimoniousProteins);

        // Assert
        var remainingAttempts = searchLog.GetAttempts().ToList();
        Assert.That(remainingAttempts.Count, Is.EqualTo(2));
        Assert.That(remainingAttempts.Contains(attempt1), Is.True);
        Assert.That(remainingAttempts.Contains(attempt2), Is.False);
        Assert.That(remainingAttempts.Contains(attempt3), Is.True);
    }

    [Test]
    public void TestGetTopScoringAttempts()
    {
        var log = new TopScoringOnlySearchLog(0.1, 0.5);
        var attempt1 = new TestSearchAttempt(0, false, 1.0, "TEST1");
        var attempt2 = new TestSearchAttempt(0, false, 0.95, "TEST2");

        // test empty return 
        var topAttempts = log.GetTopScoringAttempts().ToList();
        Assert.That(topAttempts.Count, Is.EqualTo(0));

        // test with ambiguity
        log.Add(attempt1);
        log.Add(attempt2);

        topAttempts = log.GetTopScoringAttempts().ToList();

        Assert.That(topAttempts.Count, Is.EqualTo(2));
        Assert.That(topAttempts[0].Score, Is.EqualTo(1.0));
        Assert.That(topAttempts[1].Score, Is.EqualTo(0.95));

        // test without ambiguity
        topAttempts = log.GetTopScoringAttempts(false).ToList();
        Assert.That(topAttempts.Count, Is.EqualTo(1));
        Assert.That(topAttempts[0].Score, Is.EqualTo(1.0));
        Assert.That(topAttempts[0].FullSequence, Is.EqualTo("TEST1"));
    }

    [Test]
    public void TestGetTopScoringAttemptsWithSequenceInformation()
    {
        var log = new TopScoringOnlySearchLog(0.1, 0.5);
        var attempts = new List<ISearchAttempt>
        {
            new SpectralMatchHypothesis(0, targetPwsm, emptyList, 1.0),
            new SpectralMatchHypothesis(0, targetPwsm, emptyList, 1.05),
            new TestSearchAttempt(0, true, 0.09, "test3"),
        };

        log.AddRange(attempts);
        var topAttempts = log.GetTopScoringAttemptsWithSequenceInformation().ToList();
        Assert.That(topAttempts.Count, Is.EqualTo(2));
        Assert.That(log.Count, Is.EqualTo(3));
        Assert.That(topAttempts[0].Score, Is.EqualTo(1.05));
        Assert.That(topAttempts[1].Score, Is.EqualTo(1.0));
    }

    [Test]
    public void TestGetAttemptsByType()
    {
        var log = new TopScoringOnlySearchLog(0.1, 0.5);
        var decoyAttempt = new TestSearchAttempt(0, true, 1.0, "DECOY");
        var targetAttempt = new TestSearchAttempt(0, false, 1.0, "TARGET");

        log.Add(decoyAttempt);
        log.Add(targetAttempt);

        var decoyAttempts = log.GetAttemptsByType(true).ToArray();
        Assert.That(decoyAttempts.Length, Is.EqualTo(1));
        Assert.That(decoyAttempts.First().Score, Is.EqualTo(1.0));
        Assert.That(decoyAttempts.First().IsDecoy, Is.True);

        var targetAttempts = log.GetAttemptsByType(false).ToArray();
        Assert.That(targetAttempts.Length, Is.EqualTo(1));
        Assert.That(targetAttempts.First().Score, Is.EqualTo(1.0));
        Assert.That(targetAttempts.First().IsDecoy, Is.False);
    }

    [Test]
    public void TestUpdateScores()
    {
        var log = new TopScoringOnlySearchLog(0.1, 0.5);
        var updateScoresMethod = typeof(TopScoringOnlySearchLog).GetMethod("UpdateScores", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        updateScoresMethod!.Invoke(log, new object[] { 1.0 });
        Assert.That(log.Score, Is.EqualTo(1.0));
        Assert.That(log.RunnerUpScore, Is.EqualTo(0.5));
        Assert.That(log.NumberOfBestScoringResults, Is.EqualTo(1));

        updateScoresMethod.Invoke(log, new object[] { 0.90 });
        Assert.That(log.Score, Is.EqualTo(1.0));
        Assert.That(log.RunnerUpScore, Is.EqualTo(0.5));
        Assert.That(log.NumberOfBestScoringResults, Is.EqualTo(2));

        updateScoresMethod.Invoke(log, new object[] { 0.80 });
        Assert.That(log.Score, Is.EqualTo(1.0));
        Assert.That(log.RunnerUpScore, Is.EqualTo(0.8));
        Assert.That(log.NumberOfBestScoringResults, Is.EqualTo(2));

        updateScoresMethod.Invoke(log, new object[] { 1.0 });
        Assert.That(log.Score, Is.EqualTo(1.0));
        Assert.That(log.RunnerUpScore, Is.EqualTo(0.8));
        Assert.That(log.NumberOfBestScoringResults, Is.EqualTo(3));
    }

    [Test]
    public void TestAddAndRemoveMultipleSteps()
    {
        var log = new TopScoringOnlySearchLog();
        var attempt10 = new SpectralMatchHypothesis(0, targetPwsm, emptyList, 10.0);
        var attempt20 = new SpectralMatchHypothesis(0, targetPwsm, emptyList, 20.0);
        var attempt15 = new SpectralMatchHypothesis(0, targetPwsm, emptyList, 15.0);

        // Add first attempt
        log.Add(attempt10);
        Assert.That(log.Count, Is.EqualTo(1));
        Assert.That(log.Score, Is.EqualTo(10.0));
        Assert.That(log.RunnerUpScore, Is.EqualTo(0));
        Assert.That(log.NumberOfBestScoringResults, Is.EqualTo(1));

        // Add second attempt
        log.Add(attempt20);
        Assert.That(log.Count, Is.EqualTo(2));
        Assert.That(log.Score, Is.EqualTo(20.0));
        Assert.That(log.RunnerUpScore, Is.EqualTo(10.0));
        Assert.That(log.NumberOfBestScoringResults, Is.EqualTo(1));

        // Add third attempt
        log.Add(attempt15);
        Assert.That(log.Count, Is.EqualTo(3));
        Assert.That(log.Score, Is.EqualTo(20.0));
        Assert.That(log.RunnerUpScore, Is.EqualTo(15.0));
        Assert.That(log.NumberOfBestScoringResults, Is.EqualTo(1));

        // Remove second attempt
        log.Remove(attempt20);
        Assert.That(log.Count, Is.EqualTo(2));
        Assert.That(log.Score, Is.EqualTo(15.0));
        Assert.That(log.RunnerUpScore, Is.EqualTo(10.0));
        Assert.That(log.NumberOfBestScoringResults, Is.EqualTo(1));

        // Remove first attempt
        log.Remove(attempt10);
        Assert.That(log.Count, Is.EqualTo(1));
        Assert.That(log.Score, Is.EqualTo(15.0));
        Assert.That(log.RunnerUpScore, Is.EqualTo(0));
        Assert.That(log.NumberOfBestScoringResults, Is.EqualTo(1));

        // Remove third attempt
        log.Remove(attempt15);
        Assert.That(log.Count, Is.EqualTo(0));
        Assert.That(log.Score, Is.EqualTo(0));
        Assert.That(log.RunnerUpScore, Is.EqualTo(0));
        Assert.That(log.NumberOfBestScoringResults, Is.EqualTo(0));
    }
}
