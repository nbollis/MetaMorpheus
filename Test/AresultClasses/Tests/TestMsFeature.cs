using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chemistry;
using NUnit.Framework;

namespace Test
{
    [TestFixture]
    public class TestMsFeatureFileReader
    {
        public static string Ms1FeatureFilePath = Path.Combine(TestContext.CurrentContext.TestDirectory,
            @"TestData\jurkat_td_rep2_fract2_ms1.feature");
        public string Ms2FeatureFilePath = Path.Combine(TestContext.CurrentContext.TestDirectory,
            @"TestData\jurkat_td_rep2_fract2_ms2.feature");

        [Test]
        public void TestMs1FeaturesLoadsCorrectlyAndCountIsCorrect()
        {
            var ms1Features = MsFeature.GetMs1FeaturesFromFile(Ms1FeatureFilePath);
            Assert.That(ms1Features.Count(), Is.EqualTo(20573));
        }

        [Test]
        public void TestMs2FeaturesLoadsCorrectlyAndCountIsCorrect()
        {
            var ms2Features = MsFeature.GetMs2FeaturesFromFile(Ms2FeatureFilePath);
            Assert.That(ms2Features.Count(), Is.EqualTo(2863));
        }

        [Test]
        public static void TestMs1FeatureFirstAndLastAreCorrect()
        {
            var ms1Features = MsFeature.GetMs1FeaturesFromFile(Ms1FeatureFilePath).ToList();
            var first = ms1Features.First();
            var last = ms1Features.Last();

            Assert.That(first.SampleId, Is.EqualTo(0));
            Assert.That(first.Id, Is.EqualTo(0));
            Assert.That(first.Mass, Is.EqualTo(10835.8419057238).Within(0.00001));
            Assert.That(first.Intensity, Is.EqualTo(11364715021.72).Within(0.00001));
            Assert.That(first.RetentionTimeBegin, Is.EqualTo(2368.83));
            Assert.That(first.RetentionTimeEnd, Is.EqualTo(2394.78));
            Assert.That(first.RetentionTimeApex, Is.EqualTo(2387.37));
            Assert.That(first.IntensityApex, Is.EqualTo(961066983.89));
            Assert.That(first.ChargeStateMinimum, Is.EqualTo(7));
            Assert.That(first.ChargeStateMaximum, Is.EqualTo(18));
            Assert.That(first.FractionIdMinimum, Is.EqualTo(0));
            Assert.That(first.FractionIdMaximum, Is.EqualTo(0));

            Assert.That(last.SampleId, Is.EqualTo(0));
            Assert.That(last.Id, Is.EqualTo(20572));
            Assert.That(last.Mass, Is.EqualTo(1968.81135).Within(0.00001));
            Assert.That(last.Intensity, Is.EqualTo(3661.2));
            Assert.That(last.RetentionTimeBegin, Is.EqualTo(3142.07));
            Assert.That(last.RetentionTimeEnd, Is.EqualTo(3142.07));
            Assert.That(last.RetentionTimeApex, Is.EqualTo(3142.07));
            Assert.That(last.IntensityApex, Is.EqualTo(3661.2));
            Assert.That(last.ChargeStateMinimum, Is.EqualTo(1));
            Assert.That(last.ChargeStateMaximum, Is.EqualTo(1));
            Assert.That(last.FractionIdMinimum, Is.EqualTo(0));
            Assert.That(last.FractionIdMaximum, Is.EqualTo(0));
        }

        [Test]
        public static void TestMs2FeatureFirstAndLastAreCorrect()
        {

        }

        [Test]
        public static void TestGetOverlap()
        {
            var ms1Features = MsFeature.GetMs1FeaturesFromFile(Ms1FeatureFilePath).ToList();
            var expectedResults = new Dictionary<int, int>
            {
                { 0, 2 },
                { 1, 0 },
                { 2, 1 },
                { 3, 2 },
                { 4, 2 },
                { 5, 0 },
                { 6, 0 },
                { 7, 1 },
                { 8, 3 },
                { 9, 3 }
            };

            var firstTen = ms1Features.Take(10).ToList();
            for (int i = 0; i < expectedResults.Count; i++)
            {
                var overlappedFeatures = MsFeature.GetRetentionTimeOverlappedFeaturesByPercentage(ms1Features[i], firstTen, 80).ToList();
                Assert.That(overlappedFeatures.Count, Is.EqualTo(expectedResults[i]));
            }
        }

        [Test]
        public static void TestIsHarmonic()
        {
            var feature = new Ms1Feature(0, 0, 10000, 1000, 10, 20, 15, 5000, 4, 15, 0, 0);

            var lowHarmonic = new Ms1Feature(0, 0, 5000, 1000, 10, 20, 15, 5000, 4, 15, 0, 0);
            var lowHarmonic2 = new Ms1Feature(0, 0, 2500, 1000, 10, 20, 15, 5000, 4, 15, 0, 0);
            var lowHarmonic3 = new Ms1Feature(0, 0, 1250, 1000, 10, 20, 15, 5000, 4, 15, 0, 0);
            var closeButNotActuallyLowHarmonic = new Ms1Feature(0, 0, 2510, 1000, 10, 20, 15, 5000, 4, 15, 0, 0);

            Assert.That(MsFeature.IsLowHarmonic(feature.Mass, lowHarmonic.Mass));
            Assert.That(MsFeature.IsLowHarmonic(feature.Mass, lowHarmonic2.Mass));
            Assert.That(MsFeature.IsLowHarmonic(feature.Mass, lowHarmonic3.Mass));
            Assert.That(!MsFeature.IsLowHarmonic(feature.Mass, closeButNotActuallyLowHarmonic.Mass));


            var highHarmonic = new Ms1Feature(0, 0, 20000, 1000, 10, 20, 15, 5000, 4, 15, 0, 0);
            var highHarmonic2 = new Ms1Feature(0, 0, 40000, 1000, 10, 20, 15, 5000, 4, 15, 0, 0);
            var highHarmonic3 = new Ms1Feature(0, 0, 80000, 1000, 10, 20, 15, 5000, 4, 15, 0, 0);
            var closeButNotActuallyHighHarmonic = new Ms1Feature(0, 0, 40040, 1000, 10, 20, 15, 5000, 4, 15, 0, 0);

            Assert.That(MsFeature.IsHighHarmonic(feature.Mass, highHarmonic.Mass));
            Assert.That(MsFeature.IsHighHarmonic(feature.Mass, highHarmonic2.Mass));
            Assert.That(MsFeature.IsHighHarmonic(feature.Mass, highHarmonic3.Mass));
            Assert.That(!MsFeature.IsHighHarmonic(feature.Mass, closeButNotActuallyHighHarmonic.Mass));
        }


        [Test]
        [TestCase(56.56, 18)] // mass = 1000
        [TestCase(201.0, 25)] // mass = 5000
        [TestCase(466.12, 41)] // mass = 20000
        public static void TestIsChargeOffByOne(double mz, int charge)
        {
            var mass = mz.ToMass(charge);

            for (int i = 1; i < charge; i++)
            {
                // calculate mz of actual charge + 1, then convert to mass with same charge as input
                var chargeAbove = charge + i;
                var mzAbove = mass.ToMz(chargeAbove);
                var massAboveArtifact = mzAbove.ToMass(charge);

                // will be considered am artifact if their m/z separation is less than 4 Th
                if (Math.Abs(mz - mzAbove) > 4)
                    Assert.That(!MsFeature.IsChargeOffByOne(mass, massAboveArtifact));
                else
                    Assert.That(MsFeature.IsChargeOffByOne(mass, massAboveArtifact));


                if (charge - i <= 0) continue;
                // calculate mz of actual charge - 1, then convert to mass with same charge as input
                var chargeBelow = charge - i;
                var mzBelow = mass.ToMz(chargeBelow);
                var massBelowArtifact = mzBelow.ToMass(charge);

                // will be considered am artifact if their m/z separation is less than 4 Th
                if (Math.Abs(mz - mzAbove) > 4)
                    Assert.That(!MsFeature.IsChargeOffByOne(mass, massBelowArtifact));
                else
                    Assert.That(MsFeature.IsChargeOffByOne(mass, massBelowArtifact));
            }
        }

        [Test]
        [TestCase(1000)]
        [TestCase(2000)]
        [TestCase(5000)]
        [TestCase(10000)]
        public static void TestIsIsotopologue(double mass)
        {
            for (int n = 1; n < 5; n++)
            {
                var downNIsotopes = mass - n * Constants.C13MinusC12;
                var upNIsotopes = mass + n * Constants.C13MinusC12;

                if (n <= 1)
                {
                    Assert.That(MsFeature.IsIsotopologue(mass, downNIsotopes));
                    Assert.That(MsFeature.IsIsotopologue(mass, upNIsotopes));
                }
                else
                {
                    Assert.That(!MsFeature.IsIsotopologue(mass, downNIsotopes));
                    Assert.That(!MsFeature.IsIsotopologue(mass, upNIsotopes));
                }
            }

            for (int n = 1; n < 5; n++)
            {
                var downNIsotopes = mass - n * Constants.C13MinusC12 - 1;
                var upNIsotopes = mass + n * Constants.C13MinusC12 + 1;

                Assert.That(!MsFeature.IsIsotopologue(mass, downNIsotopes));
                Assert.That(!MsFeature.IsIsotopologue(mass, upNIsotopes));
            }
        }

        [Test]
        public static void TestMs1FeatureReadWriteWithArtifactDetection()
        {
            // process features
            var ms1Features = MsFeature.GetMs1FeaturesFromFile(Ms1FeatureFilePath).ToList();
            Assert.That(!ms1Features.All(p => p.PerformedArtifactDetection));
            MsFeature.PerformArtifactDetection(ms1Features);
            Assert.That(ms1Features.All(p => p.PerformedArtifactDetection));

            // export processed file
            var tempName = Path.GetFileNameWithoutExtension(Ms1FeatureFilePath)?.Insert(Path.GetFileNameWithoutExtension(Ms1FeatureFilePath).IndexOf("_ms1"), "_2") + ".feature";
            var tempOutPath = Path.Combine(Path.GetDirectoryName(Ms1FeatureFilePath), tempName);
            Assert.That(!File.Exists(tempOutPath));
            MsFeature.ExportFeatures(ms1Features, tempOutPath);
            Assert.That(File.Exists(tempOutPath));

            // test each object property and line of file
            var reloadedFeatures = MsFeature.GetMs1FeaturesFromFile(tempOutPath).ToList();
            Assert.That(ms1Features.Count, Is.EqualTo(reloadedFeatures.Count()));
            Assert.That(reloadedFeatures.All(p => p.PerformedArtifactDetection));
            for (int i = 0; i < ms1Features.Count; i++)
            {
                Assert.That(ms1Features[i].DeepCompare(reloadedFeatures[i], out List<object> errors));
            }

            // compare each line of file
            var originalLines = File.ReadAllLines(Ms1FeatureFilePath);
            var newLines = File.ReadAllLines(tempOutPath);
            Assert.That(originalLines.Length, Is.EqualTo(newLines.Length));
            //for (int i = 1; i < originalLines.Length; i++)
            //{
            //    originalLines[i].Split('\t').ForEach(p => Assert.Contains(, newLines[i].Split('\t')));
            //}

            File.Delete(tempOutPath);
            Assert.That(!File.Exists(tempOutPath));
        }

        [Test]
        public static void TestMs1FeatureReadWrite()
        {
            var ms1Features = MsFeature.GetMs1FeaturesFromFile(Ms1FeatureFilePath).ToList();
            var tempName = Path.GetFileNameWithoutExtension(Ms1FeatureFilePath)?.Insert(Path.GetFileNameWithoutExtension(Ms1FeatureFilePath).IndexOf("_ms1"), "_2") + ".feature";
            var tempOutPath = Path.Combine(Path.GetDirectoryName(Ms1FeatureFilePath), tempName);

            Assert.That(!File.Exists(tempOutPath));
            MsFeature.ExportFeatures(ms1Features, tempOutPath);
            Assert.That(File.Exists(tempOutPath));

            var reloadedFeatures = MsFeature.GetMs1FeaturesFromFile(tempOutPath).ToList();
            Assert.That(ms1Features.Count, Is.EqualTo(reloadedFeatures.Count()));
            for (int i = 0; i < ms1Features.Count; i++)
            {
                Assert.That(ms1Features[i].DeepCompare(reloadedFeatures[i], out List<object> errors));
            }


            var originalLines = File.ReadAllLines(Ms1FeatureFilePath);
            var newLines = File.ReadAllLines(tempOutPath);
            Assert.That(originalLines.Length, Is.EqualTo(newLines.Length));
            for (int i = 0; i < originalLines.Length; i++)
            {
                Assert.That(originalLines[i], Is.EqualTo(newLines[i]));
            }

            File.Delete(tempOutPath);
            Assert.That(!File.Exists(tempOutPath));
        }
    }
}
