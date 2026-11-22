using Chemistry;
using EngineLayer;
using MassSpectrometry;
using MzLibUtil;
using NUnit.Framework;
using Omics.Fragmentation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Test
{
    /// <summary>
    /// Fragment matching reads each fragment's mass, charge, first-peak and total intensity from arrays the scan keeps, so a
    /// ParallelSearch can release the envelopes (Ms2ScanWithSpecificMass.ReleaseExperimentalFragments). These tests hold
    /// matching on a released scan to the envelope-reading code it replaced, bit for bit, on every path that reads
    /// envelopes: plain, complementary ions, and all charges (spectral library).
    /// </summary>
    [TestFixture]
    internal static class ReleasedFragmentsEquivalenceTest
    {
        [Test]
        public static void MatchingAReleasedScanGivesWhatTheEnvelopesGave()
        {
            var random = new Random(2718);
            List<MsDataScan> ms2Scans = new TestDataFile().GetAllScansList().Where(s => s.MsnOrder == 2).ToList();
            int ionsCompared = 0;

            for (int trial = 0; trial < 3000; trial++)
            {
                Tolerance tolerance = trial % 2 == 0
                    ? new PpmTolerance(1 + random.Next(30))
                    : new AbsoluteTolerance(0.001 + random.NextDouble() * 0.5);
                bool addCompIons = trial % 3 == 0;
                bool allCharges = trial % 5 == 0;
                var commonParameters = new CommonParameters(productMassTolerance: tolerance, dissociationType: DissociationType.HCD,
                    addCompIons: addCompIons);
                int precursorCharge = (1 + random.Next(5)) * (random.Next(8) == 0 ? -1 : 1);
                MsDataScan ms2 = ms2Scans[random.Next(ms2Scans.Count)];
                IsotopicEnvelope[] envelopes = RandomEnvelopes(random, trial % 25 == 0 ? 0 : 1 + random.Next(200));
                double precursorMz = (500 + random.NextDouble() * 3000).ToMz(Math.Abs(precursorCharge));

                var kept = new Ms2ScanWithSpecificMass(ms2, precursorMz, precursorCharge, "file", commonParameters, envelopes);
                var released = new Ms2ScanWithSpecificMass(ms2, precursorMz, precursorCharge, "file", commonParameters, envelopes);
                released.ReleaseExperimentalFragments();
                Assert.That(released.ExperimentalFragments, Is.Null);

                List<Product> products = RandomProducts(random, envelopes, kept.PrecursorMass, tolerance, 1 + random.Next(80));

                List<MatchedFragmentIon> expected = allCharges
                    ? MatchAllChargesBefore(kept, products, commonParameters)
                    : MatchBefore(kept, products, commonParameters);
                List<MatchedFragmentIon> actual = MetaMorpheusEngine.MatchFragmentIons(released, products, commonParameters, matchAllCharges: allCharges);

                AssertIdentical(expected, actual, trial);
                ionsCompared += expected.Count;
            }

            Assert.That(ionsCompared, Is.GreaterThan(5000));
        }

        [Test]
        public static void AskingForEnvelopesOfAReleasedScanFails()
        {
            var random = new Random(1);
            MsDataScan ms2 = new TestDataFile().GetAllScansList().First(s => s.MsnOrder == 2);
            var commonParameters = new CommonParameters();
            IsotopicEnvelope[] envelopes = RandomEnvelopes(random, 20);
            var scan = new Ms2ScanWithSpecificMass(ms2, 1000.0.ToMz(2), 2, "file", commonParameters, envelopes);
            var products = envelopes.Take(3).Select((e, i) => new Product(ProductType.b, FragmentationTerminus.N, e.MonoisotopicMass, i + 1, i + 1, 0)).ToList();

            Assert.That(MetaMorpheusEngine.MatchFragmentIons(scan, products, commonParameters, includeExperimentalEnvelope: true)
                .OfType<MatchedFragmentIonWithEnvelope>().All(i => i.Envelope != null), Is.True);
            scan.ReleaseExperimentalFragments();
            Assert.That(scan.FragmentCount, Is.EqualTo(envelopes.Length));
            Assert.Throws<MetaMorpheusException>(() =>
                MetaMorpheusEngine.MatchFragmentIons(scan, products, commonParameters, includeExperimentalEnvelope: true));
        }

        private static IsotopicEnvelope[] RandomEnvelopes(Random random, int count)
        {
            var envelopes = new List<IsotopicEnvelope>();
            for (int i = 0; i < count; i++)
            {
                double mass = 100 + random.NextDouble() * 3500;
                int repeats = random.Next(10) == 0 ? 2 + random.Next(3) : 1;
                for (int r = 0; r < repeats; r++)
                {
                    int charge = (1 + random.Next(8)) * (random.Next(10) == 0 ? -1 : 1);
                    double intensity = 1 + random.NextDouble() * 1e6;
                    var peaks = new List<(double mz, double intensity)> { (mass.ToMz(Math.Abs(charge)), intensity) };
                    if (random.Next(3) == 0)
                    {
                        peaks.Add(((mass + 1.00335).ToMz(Math.Abs(charge)), intensity * random.NextDouble()));
                    }
                    // the total intensity differs from the first peak's, so a mix-up between them shows
                    envelopes.Add(new IsotopicEnvelope(peaks, mass, charge, intensity * (1 + random.NextDouble()), 0));
                }
            }
            return envelopes.OrderBy(p => p.MonoisotopicMass).ToArray();
        }

        /// <summary>Products on or near envelope masses, and on or near the complements of envelope masses.</summary>
        private static List<Product> RandomProducts(Random random, IsotopicEnvelope[] envelopes, double precursorMass, Tolerance tolerance, int count)
        {
            ProductType[] types = { ProductType.b, ProductType.y, ProductType.c, ProductType.zDot, ProductType.D, ProductType.M };
            var products = new List<Product>();
            for (int i = 0; i < count; i++)
            {
                double mass;
                int kind = random.Next(12);
                if (envelopes.Length == 0 || kind < 2)
                {
                    mass = 100 + random.NextDouble() * 3500;
                }
                else
                {
                    double target = envelopes[random.Next(envelopes.Length)].MonoisotopicMass;
                    if (kind >= 10)
                    {
                        // a product whose complementary ion (HCD: precursor + proton - product) lands on the envelope
                        target = precursorMass + Constants.ProtonMass - target;
                    }
                    double halfWidth = (tolerance.GetMaximumValue(target) - tolerance.GetMinimumValue(target)) / 2;
                    mass = kind switch
                    {
                        2 or 3 or 10 => target,
                        4 or 5 or 11 => target + halfWidth * (random.NextDouble() * 1.8 - 0.9),
                        6 or 7 => target + halfWidth * (1.01 + random.NextDouble()) * (random.Next(2) == 0 ? -1 : 1),
                        8 => target + halfWidth * (random.Next(2) == 0 ? -1 : 1),
                        _ => double.NaN
                    };
                }
                products.Add(new Product(types[random.Next(types.Length)], FragmentationTerminus.N, mass, 1 + i, 1 + i, 0));
            }
            return products;
        }

        private static void AssertIdentical(List<MatchedFragmentIon> expected, List<MatchedFragmentIon> actual, int trial)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count), $"trial {trial}: ion count");
            for (int i = 0; i < expected.Count; i++)
            {
                var e = expected[i];
                var a = actual[i];
                Assert.That(a.GetType(), Is.EqualTo(e.GetType()), $"trial {trial}, ion {i}: type");
                Assert.That(a.NeutralTheoreticalProduct, Is.SameAs(e.NeutralTheoreticalProduct), $"trial {trial}, ion {i}: product");
                Assert.That(BitConverter.DoubleToInt64Bits(a.Mz), Is.EqualTo(BitConverter.DoubleToInt64Bits(e.Mz)), $"trial {trial}, ion {i}: m/z");
                Assert.That(BitConverter.DoubleToInt64Bits(a.Intensity), Is.EqualTo(BitConverter.DoubleToInt64Bits(e.Intensity)), $"trial {trial}, ion {i}: intensity");
                Assert.That(a.Charge, Is.EqualTo(e.Charge), $"trial {trial}, ion {i}: charge");
            }
        }

        private static List<double> ComplementaryShifts(DissociationType dissociationType)
        {
            var field = typeof(MetaMorpheusEngine).GetField("complementaryIonConversionDictionary", BindingFlags.NonPublic | BindingFlags.Static)!;
            return ((Dictionary<DissociationType, List<double>>)field.GetValue(null)!)[dissociationType];
        }

        // ===== MetaMorpheusEngine.MatchFragmentIons and MatchFragmentIonsOfAllCharges as of 0722f91f6 (envelopes read from
        // ExperimentalFragments), deconvoluted path, no envelope in the ions

        private static List<MatchedFragmentIon> MatchBefore(Ms2ScanWithSpecificMass scan, List<Product> theoreticalProducts, CommonParameters commonParameters)
        {
            var productMassTolerance = commonParameters.ProductMassTolerance;
            var matchedFragmentIons = new List<MatchedFragmentIon>();

            if (scan.ExperimentalFragments != null && !scan.ExperimentalFragments.Any())
            {
                return matchedFragmentIons;
            }

            for (int i = 0; i < theoreticalProducts.Count; i++)
            {
                var product = theoreticalProducts[i];
                if (double.IsNaN(product.NeutralMass))
                {
                    continue;
                }
                int closestIndex = scan.GetClosestExperimentalIsotopicEnvelopeIndex(product.NeutralMass);
                if (closestIndex < 0 || !productMassTolerance.Within(scan.GetDeconvolutedMonoisotopicMass(closestIndex), product.NeutralMass))
                {
                    continue;
                }
                var closestExperimentalMass = scan.ExperimentalFragments[closestIndex];
                if (Math.Abs(closestExperimentalMass.Charge) <= Math.Abs(scan.PrecursorCharge))
                {
                    matchedFragmentIons.Add(new MatchedFragmentIon(product, closestExperimentalMass.MonoisotopicMass.ToMz(closestExperimentalMass.Charge),
                        closestExperimentalMass.Peaks.First().intensity, closestExperimentalMass.Charge));
                }
            }
            if (commonParameters.AddCompIons)
            {
                foreach (double massShift in ComplementaryShifts(commonParameters.DissociationType))
                {
                    double protonMassShift = massShift.ToMass(1);

                    for (int i = 0; i < theoreticalProducts.Count; i++)
                    {
                        var product = theoreticalProducts[i];
                        if (double.IsNaN(product.NeutralMass) || product.ProductType == ProductType.D || product.ProductType == ProductType.M)
                        {
                            continue;
                        }

                        double compIonMass = scan.PrecursorMass + protonMassShift - product.NeutralMass;
                        int closestIndex = scan.GetClosestExperimentalIsotopicEnvelopeIndex(compIonMass);
                        if (closestIndex < 0 || !productMassTolerance.Within(scan.GetDeconvolutedMonoisotopicMass(closestIndex), compIonMass))
                        {
                            continue;
                        }
                        IsotopicEnvelope closestExperimentalMass = scan.ExperimentalFragments[closestIndex];
                        if (closestExperimentalMass.Charge <= scan.PrecursorCharge)
                        {
                            double mz = (scan.PrecursorMass + protonMassShift - closestExperimentalMass.MonoisotopicMass).ToMz(closestExperimentalMass.Charge);
                            matchedFragmentIons.Add(new MatchedFragmentIon(product, mz, closestExperimentalMass.TotalIntensity, closestExperimentalMass.Charge));
                        }
                    }
                }
            }

            return matchedFragmentIons;
        }

        private static List<MatchedFragmentIon> MatchAllChargesBefore(Ms2ScanWithSpecificMass scan, List<Product> theoreticalProducts, CommonParameters commonParameters)
        {
            var productMassTolerance = commonParameters.ProductMassTolerance;
            var matchedFragmentIons = new List<MatchedFragmentIon>();
            var ions = new List<string>();

            if (scan.ExperimentalFragments != null && !scan.ExperimentalFragments.Any())
            {
                return matchedFragmentIons;
            }

            foreach (Product product in theoreticalProducts)
            {
                if (double.IsNaN(product.NeutralMass))
                {
                    continue;
                }

                var minMass = productMassTolerance.GetMinimumValue(product.NeutralMass);
                var maxMass = productMassTolerance.GetMaximumValue(product.NeutralMass);
                var closestExperimentalMassList = scan.GetClosestExperimentalIsotopicEnvelopeList(minMass, maxMass);
                if (closestExperimentalMassList != null)
                {
                    foreach (var x in closestExperimentalMassList)
                    {
                        String ion = $"{product.ProductType.ToString()}{product.FragmentNumber}^{x.Charge}-{product.NeutralLoss}";
                        if (x != null
                            && !ions.Contains(ion)
                            && productMassTolerance.Within(x.MonoisotopicMass, product.NeutralMass)
                            && Math.Abs(x.Charge) <= Math.Abs(scan.PrecursorCharge))
                        {
                            matchedFragmentIons.Add(new MatchedFragmentIon(product, x.MonoisotopicMass.ToMz(x.Charge),
                                x.Peaks.First().intensity, x.Charge));
                            ions.Add(ion);
                        }
                    }
                }
            }

            return matchedFragmentIons;
        }
    }
}
