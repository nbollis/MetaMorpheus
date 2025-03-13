using EngineLayer.SpectrumMatch;
using NUnit.Framework;
using Omics;
using Omics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using Proteomics;
using System.Collections.Generic;

namespace Test
{
    [TestFixture]
    public class TopScoringOnlySearchLogTests
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
        }

        [Test]
        public void TestClear()
        {
            var log = new TopScoringOnlySearchLog();
            var attempt = new SpectralMatchHypothesis(0, targetPwsm, emptyList, 10.0);

            log.Add(attempt);
            log.Clear();

            Assert.That(log.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestAddOrReplace()
        {
            var log = new TopScoringOnlySearchLog();
            var pwsm = targetPwsm;
            var matchedFragmentIons = emptyList;

            bool added = log.AddOrReplace(pwsm, 10.0, 0, true, matchedFragmentIons);

            Assert.That(added, Is.True);
            Assert.That(log.Count, Is.EqualTo(1));
            Assert.That(log.Score, Is.EqualTo(10.0));
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
        }
    }
}
