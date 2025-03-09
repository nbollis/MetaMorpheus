using EngineLayer;
using EngineLayer.SpectrumMatch;
using NUnit.Framework;
using System;
using System.Linq;

namespace Test
{
    [TestFixture]
    public class SearchLogTests
    {
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
    }
}
