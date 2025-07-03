using EngineLayer;
using EngineLayer.PrecursorSearchModes;
using MzLibUtil;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Test
{
    [TestFixture]
    public static class SearchModesTest
    {
        [Test]
        public static void TestSearchModeTest()
        {
            MassDiffAcceptor sm = new TestSearchMode("My custom");
            Assert.That(sm.Accepts(2, 2) >= 0);
            Assert.That(sm.Accepts(0.5, 4) >= 0);
            Assert.That(sm.Accepts(0.5, 0.5) >= 0, Is.False);
            Assert.That(sm.Accepts(1, 1) >= 0);
            Assert.That(sm.GetAllowedPrecursorMassIntervalsFromTheoreticalMass(0.5).First().Minimum, Is.EqualTo(2));
            Assert.That(sm.GetAllowedPrecursorMassIntervalsFromTheoreticalMass(2).First().Minimum, Is.EqualTo(0.5));
        }

        [Test]
        public static void TestDotSearchMode()
        {
            var dsm1 = new DotMassDiffAcceptor("test1", new double[] { 0, 1 }, new AbsoluteTolerance(0.1));

            Assert.That(dsm1.Accepts(1000, 1000) >= 0);
            Assert.That(dsm1.Accepts(1000, 1000 + 0.1 / 2) >= 0);
            Assert.That(dsm1.Accepts(1000, 1000 + 0.1 * 2) >= 0, Is.False);
            Assert.That(dsm1.Accepts(1000 + 0.1 / 2, 1000) >= 0);
            Assert.That(dsm1.Accepts(1000 + 0.1 * 2, 1000) >= 0, Is.False);

            Assert.That(dsm1.Accepts(1000 + 1, 1000 + 0.1 / 2) >= 0);
            Assert.That(dsm1.Accepts(1000 + 1, 1000 + 0.1 * 2) >= 0, Is.False);
            Assert.That(dsm1.Accepts(1000 + 1 + 0.1 / 2, 1000) >= 0);
            Assert.That(dsm1.Accepts(1000 + 1 + 0.1 * 2, 1000) >= 0, Is.False);

            var theList = dsm1.GetAllowedPrecursorMassIntervalsFromTheoreticalMass(100).ToList();

            Assert.That(theList[0].Minimum, Is.EqualTo(99.9));
            Assert.That(theList[0].Maximum, Is.EqualTo(100.1));
            Assert.That(theList[1].Minimum, Is.EqualTo(100.9));
            Assert.That(theList[1].Maximum, Is.EqualTo(101.1));

            var dsm2 = new DotMassDiffAcceptor("test2", new double[] { 0, 1 }, new PpmTolerance(5));

            Assert.That(dsm2.Accepts(1000, 1000) >= 0);

            Assert.That(dsm2.Accepts(1000 * (1 + 5.0 / 1e6 / 1.0000001), 1000) >= 0); // FIRST VARIES WITHIN 5 PPM OF SECOND
            Assert.That(dsm2.Accepts(1000 * (1 - 5.0 / 1e6 / 1.0000001), 1000) >= 0); // FIRST VARIES WITHIN 5 PPM OF SECOND

            Assert.That(dsm2.Accepts(1000, 1000 * (1 - 5.0 / 1e6 / 1.0000001)) >= 0, Is.False); // VERY CAREFUL

            Assert.That(dsm2.Accepts(1000 * (1 + 5.0 / 1e6 * 1.0000001), 1000) >= 0, Is.False); // FIRST VARIES WITHIN 5 PPM OF SECOND
            Assert.That(dsm2.Accepts(1000 * (1 - 5.0 / 1e6 * 1.0000001), 1000) >= 0, Is.False); // FIRST VARIES WITHIN 5 PPM OF SECOND

            Assert.That(dsm2.Accepts(1000, 1000 * (1 + 5.0 / 1e6 * 1.0000001)) >= 0); // VERY CAREFUL

            var theList2 = dsm2.GetAllowedPrecursorMassIntervalsFromTheoreticalMass(1000).ToList();

            Assert.That(theList2[0].Contains(1000));

            Assert.That(1000 * (1 + 5.0 / 1e6 / 1.0000001) < theList2[0].Maximum);
            Assert.That(1000 * (1 - 5.0 / 1e6 / 1.0000001) > theList2[0].Minimum);
            Assert.That(1000 * (1 + 5.0 / 1e6 * 1.0000001) > theList2[0].Maximum);
            Assert.That(1000 * (1 - 5.0 / 1e6 * 1.0000001) < theList2[0].Minimum);

            Assert.That(theList2[1].Contains(1001));
        }

        [Test]
        public static void TestAdductMassDiffAcceptor_AbsoluteTolerance()
        {
            // Define adducts: Na (1 max), K (2 max)
            var adducts = new List<Adduct>
                {
                    new Adduct("Na", 1, 21.981943),
                    new Adduct("K", 2, 37.955882)
                };
            var tol = new AbsoluteTolerance(0.01);
            var acceptor = new AdductMassDiffAcceptor(adducts, tol);

            // There should be (1+1)*(2+1) = 6 combinations (Na: 0,1; K: 0,1,2)
            Assert.That(acceptor.NumNotches, Is.EqualTo(6));
            Assert.That(acceptor.NotchDescriptions[0], Is.EqualTo("Unadducted"));
            Assert.That(acceptor.NotchDescriptions, Does.Contain("Na1"));
            Assert.That(acceptor.NotchDescriptions, Does.Contain("K1"));
            Assert.That(acceptor.NotchDescriptions, Does.Contain("K2"));
            Assert.That(acceptor.NotchDescriptions, Does.Contain("Na1K1"));
            Assert.That(acceptor.NotchDescriptions, Does.Contain("Na1K2"));

            // Accepts: unadducted
            Assert.That(acceptor.Accepts(1000, 1000), Is.EqualTo(0));
            // Accepts: 1Na
            Assert.That(acceptor.Accepts(1000 + 21.981943, 1000), Is.GreaterThanOrEqualTo(0));
            // Accepts: 2K
            Assert.That(acceptor.Accepts(1000 + 2 * 37.955882, 1000), Is.GreaterThanOrEqualTo(0));
            // Not accepted: not within tolerance
            Assert.That(acceptor.Accepts(1000 + 21.981943 + 0.02, 1000), Is.EqualTo(-1));

            // Allowed intervals for theoretical mass
            var intervals = acceptor.GetAllowedPrecursorMassIntervalsFromTheoreticalMass(1000).ToList();
            Assert.That(intervals.Count, Is.EqualTo(6));
            Assert.That(intervals[0].Minimum, Is.EqualTo(1000 - 0.01).Within(1e-8));
            Assert.That(intervals[0].Maximum, Is.EqualTo(1000 + 0.01).Within(1e-8));
            Assert.That(intervals[1].Minimum, Is.EqualTo(1000 + 21.981943 - 0.01).Within(1e-8));
            Assert.That(intervals[1].Maximum, Is.EqualTo(1000 + 21.981943 + 0.01).Within(1e-8));

            // Allowed intervals for observed mass
            var observedIntervals = acceptor.GetAllowedPrecursorMassIntervalsFromObservedMass(1100).ToList();
            Assert.That(observedIntervals.Count, Is.EqualTo(6));
            // Unadducted: observedMass - 0
            Assert.That(observedIntervals[0].Minimum, Is.EqualTo(1100 - 0.01).Within(1e-8));
            Assert.That(observedIntervals[0].Maximum, Is.EqualTo(1100 + 0.01).Within(1e-8));
            // Na1: observedMass - 21.981943
            Assert.That(observedIntervals[1].Minimum, Is.EqualTo(1100 - 21.981943 - 0.01).Within(1e-8));
            Assert.That(observedIntervals[1].Maximum, Is.EqualTo(1100 - 21.981943 + 0.01).Within(1e-8));
            // K2: observedMass - 2*37.955882
            Assert.That(observedIntervals[5].Minimum, Is.EqualTo(1100 - (2 * 37.955882 + 21.981943) - 0.01).Within(1e-8));
            Assert.That(observedIntervals[5].Maximum, Is.EqualTo(1100 - (2 * 37.955882 + 21.981943) + 0.01).Within(1e-8));
        }

        [Test]
        public static void TestAdductMassDiffAcceptor_PpmTolerance()
        {
            var adducts = new List<Adduct>
            {
                new Adduct("Na", 1, 21.981943)
            };
            var tol = new PpmTolerance(10);
            var acceptor = new AdductMassDiffAcceptor(adducts, tol);

            // Accepts: unadducted, within 10 ppm
            Assert.That(acceptor.Accepts(1000 * (1 + 10.0 / 1e6 / 1.0000001), 1000), Is.GreaterThanOrEqualTo(0));
            // Accepts: 1Na, within 10 ppm
            Assert.That(acceptor.Accepts((1000 + 21.981943) * (1 + 10.0 / 1e6 / 1.0000001), 1000), Is.GreaterThanOrEqualTo(1));
            // Not accepted: outside 10 ppm
            Assert.That(acceptor.Accepts(1000 * (1 + 10.0 / 1e6 * 1.0000001), 1000), Is.EqualTo(-1));

            // Allowed intervals for theoretical mass
            var intervals = acceptor.GetAllowedPrecursorMassIntervalsFromTheoreticalMass(1000).ToList();
            Assert.That(intervals.Count, Is.EqualTo(2));
            Assert.That(intervals[0].Contains(1000), Is.True);
            Assert.That(intervals[1].Contains(1000 + 21.981943), Is.True);

            // Allowed intervals for observed mass
            var observedIntervalsPpm = acceptor.GetAllowedPrecursorMassIntervalsFromObservedMass(1100).ToList();
            Assert.That(observedIntervalsPpm.Count, Is.EqualTo(2));
            // Unadducted: observedMass - 0
            Assert.That(observedIntervalsPpm[0].Contains(1100), Is.True);
            // Na1: observedMass - 21.981943
            Assert.That(observedIntervalsPpm[1].Contains(1100 - 21.981943), Is.True);
        }

        [Test]
        public static void Adduct_ToStringFromString()
        {
            var adducts = new List<Adduct>
            {
                new Adduct("Na", 1, 21.981943),
                new Adduct("K", 2, 37.955882)
            };

            foreach (var adduct in adducts)
            {
                var stringRepresentation = adduct.ToString();
                var parsedAdduct = Adduct.FromString(stringRepresentation);

                Assert.That(parsedAdduct.Name, Is.EqualTo(adduct.Name));
                Assert.That(parsedAdduct.MaxFrequency, Is.EqualTo(adduct.MaxFrequency));
                Assert.That(parsedAdduct.MonoisotopicMass, Is.EqualTo(adduct.MonoisotopicMass).Within(1e-8));
            }
        }

        // Accept if scanPrecursorMass*peptideMass>=1.
        private class TestSearchMode : MassDiffAcceptor
        {
            public TestSearchMode(string fileNameAddition) : base(fileNameAddition)
            {
            }

            public override int Accepts(double scanPrecursorMass, double peptideMass)
            {
                return scanPrecursorMass * peptideMass >= 1 ? 1 : -1;
            }

            public override IEnumerable<AllowedIntervalWithNotch> GetAllowedPrecursorMassIntervalsFromTheoreticalMass(double peptideMonoisotopicMass)
            {
                yield return new AllowedIntervalWithNotch(1 / peptideMonoisotopicMass, double.MaxValue, 1);
            }

            public override IEnumerable<AllowedIntervalWithNotch> GetAllowedPrecursorMassIntervalsFromObservedMass(double peptideMonoisotopicMass)
            {
                yield return new AllowedIntervalWithNotch(double.MinValue, 1 / peptideMonoisotopicMass, 1);
            }

            public override string ToProseString()
            {
                throw new NotImplementedException();
            }
        }
    }
}