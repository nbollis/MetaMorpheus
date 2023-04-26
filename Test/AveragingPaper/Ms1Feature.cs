using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Easy.Common.Interfaces;
using MzLibUtil;
using NUnit.Framework;
using Org.BouncyCastle.Bcpg.Sig;
using static iText.StyledXmlParser.Jsoup.Select.Evaluator;
using static Plotly.NET.StyleParam.DrawingStyle;

namespace Test.AveragingPaper
{
    public abstract class MsFeature
    {
        public bool Artifact => Isotopologue && LowHarmonic && HighHarmonic;
        public bool Isotopologue { get; private set; } = false;
        public bool LowHarmonic { get; private set; } = false;
        public bool HighHarmonic { get; private set; } = false;

        public static IEnumerable<Ms1Feature> GetMs1FeaturesFromFile(string filepath)
        {
            bool firstLine = true;
            string line;
            using (var sr = new StreamReader(filepath))
            {
                line = sr.ReadLine();
                while (!sr.EndOfStream)
                {
                    if (firstLine)
                    {
                        firstLine = false;
                        continue;
                    }

                    line = sr.ReadLine();
                    yield return new Ms1Feature(line);
                }
            }
        }

        public static IEnumerable<Ms2Feature> GetMs2FeaturesFromFile(string filepath)
        {
            bool firstLine = true;
            string line;
            using (var sr = new StreamReader(filepath))
            {
                line = sr.ReadLine();
                while (!sr.EndOfStream)
                {
                    if (firstLine)
                    {
                        firstLine = false;
                        continue;
                    }

                    line = sr.ReadLine();
                    yield return new Ms2Feature(line);
                }
            }
        }

        public static void PerformArtifactDetection(List<Ms1Feature> features)
        {

            foreach (var feature in features)
            {
                // features with an 80% or greater overlap in retention time
                foreach (var overlappedFeature in GetOverlappedByPercentage(features, feature, 80))
                {
                    
                }
            }
        }

        /// <summary>
        /// Returns features with x percentage overlap in their retention time elution
        /// </summary>
        /// <param name="featuresToCompare"></param>
        /// <param name="featureToCheck"></param>
        /// <param name="percentage"></param>
        /// <returns></returns>
        internal static IEnumerable<Ms1Feature> GetOverlappedByPercentage(List<Ms1Feature> featuresToCompare, Ms1Feature featureToCheck, int percentage)
        {
            foreach (var featureToCompare in featuresToCompare)
            {
                if (featureToCompare.Id == featureToCheck.Id) continue;
                double overlap = FindOverlapping(featureToCheck.RetentionTimeBegin, featureToCheck.RetentionTimeEnd,
                    featureToCompare.RetentionTimeBegin, featureToCompare.RetentionTimeEnd);
                if (overlap / featureToCheck.RetentionTimeWidth * 100 >= percentage)
                    yield return featureToCompare;
            }
        }

        internal static double FindOverlapping(double start1, double end1, double start2, double end2)
        {
            return Math.Max(0, Math.Min(end1, end2) - Math.Max(start1, start2));
        }
    }

    public class Ms1Feature : MsFeature
    {
        #region Private

        private DoubleRange _rtRange;

        #endregion

        public int SampleId { get; }
        public int Id { get; }
        public double Mass { get; }
        public double Intensity { get; }
        public double RetentionTimeBegin { get; }
        public double RetentionTimeEnd { get; }
        public double RetentionTimeApex { get; }
        public double IntensityApex { get; }
        public int ChargeStateMinimum { get; }
        public int ChargeStateMaximum { get; }
        public int FractionIdMinimum { get; }
        public int FractionIdMaximum { get; }

        public DoubleRange RetentionTimeRange => _rtRange ??= new DoubleRange(RetentionTimeBegin, RetentionTimeEnd);
        public double RetentionTimeWidth => RetentionTimeEnd - RetentionTimeBegin;

        public Ms1Feature(string featureFileLine)
        {
            var splits = featureFileLine.Split('\t');
            SampleId = int.Parse(splits[0]);
            Id = int.Parse(splits[1]);
            Mass = double.Parse(splits[2]);
            Intensity = double.Parse(splits[3]);
            RetentionTimeBegin = double.Parse(splits[4]);
            RetentionTimeEnd = double.Parse(splits[5]);
            RetentionTimeApex = double.Parse(splits[6]);
            IntensityApex = double.Parse(splits[7]);
            ChargeStateMinimum = int.Parse(splits[8]);
            ChargeStateMaximum = int.Parse(splits[9]);
            FractionIdMinimum = int.Parse(splits[10]);
            FractionIdMaximum = int.Parse(splits[11]);
        }

        public override string ToString()
        {
            return Id.ToString();
        }
    }

    public class Ms2Feature : MsFeature
    {
        public int SpectraId { get; }
        public int FracitonId { get; }
        public string FileName { get; }
        public int Scans { get; }
        public int Ms1Id { get; }
        public int Ms1Scans { get; }
        public double PrecursorMass { get; }
        public double PrecursorIntensity { get; }
        public int FractionFeatureId { get; }
        public double FractionFeatureIntensity { get; }
        public double FractionFeatureScore { get; }
        public double FractionFeatureApex { get; }
        public int SampleFeatureId { get; }
        public double SampleFeatureIntensity { get; }

        public Ms2Feature(string featureFileLine)
        {
            var splits = featureFileLine.Split('\t');
            SpectraId = int.Parse(splits[0]);
            FracitonId = int.Parse(splits[1]);
            FileName = splits[2];
            Scans = int.Parse(splits[3]);
            Ms1Id = int.Parse(splits[4]);
            Ms1Scans = int.Parse(splits[5]);
            PrecursorMass = double.Parse(splits[6]);
            PrecursorIntensity = double.Parse(splits[7]);
            FractionFeatureId = int.Parse(splits[8]);
            FractionFeatureIntensity = double.Parse(splits[9]);
            FractionFeatureScore = double.Parse(splits[10]);
            FractionFeatureApex = double.Parse(splits[11]);
            SampleFeatureId = int.Parse(splits[12]);
            SampleFeatureIntensity = double.Parse(splits[13]);
        }
    }

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
            Assert.That(first.RetentionTimeRange.Width, Is.EqualTo(first.RetentionTimeEnd - first.RetentionTimeBegin));

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
            Assert.That(last.RetentionTimeRange.Width, Is.EqualTo(last.RetentionTimeEnd - last.RetentionTimeBegin));
        }

        [Test]
        public static void TestMs2FeatureFirstAndLastAreCorrect()
        {

        }

        [Test]
        public static void TestGetOverlap()
        {
            var ms1Features = MsFeature.GetMs1FeaturesFromFile(Ms1FeatureFilePath).ToList();

       
            var expectedResults = new Dictionary<int, int>();
            expectedResults.Add(0, 2);
            expectedResults.Add(1, 0);
            expectedResults.Add(2, 1);
            expectedResults.Add(3, 2);
            expectedResults.Add(4, 2);
            expectedResults.Add(5, 0);
            expectedResults.Add(6, 0);
            expectedResults.Add(7, 1);
            expectedResults.Add(8, 3);
            expectedResults.Add(9, 3);

            var firstTen = ms1Features.Take(10).ToList();
            for (int i = 0; i < expectedResults.Count; i++)
            {
                var overlappedFeatures = MsFeature.GetOverlappedByPercentage(firstTen, ms1Features[i], 80).ToList();
                Assert.That(overlappedFeatures.Count, Is.EqualTo(expectedResults[i]));
            }
            
        }
    }
}
