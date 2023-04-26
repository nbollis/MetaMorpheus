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
            foreach (var overlappedFeature in featuresToCompare.Where(p => featureToCheck.RetentionTimeRange.IsOverlapping(p.RetentionTimeRange)))
            {
                
            }

            return null;
        }
    }

    public class Ms1Feature : MsFeature
    {
        #region Private

        private DoubleRange rtRange;

        #endregion

        public int SampleId { get; }
        public int Id { get; }
        public double Mass { get; }
        public double Intensity { get; }
        public double RetentionTime_Begin { get; }
        public double RetentionTime_End { get; }
        public double RetentionTime_Apex { get; }
        public double IntensityApex { get; }
        public int ChargeState_Minimum { get; }
        public int ChargeState_Maximum { get; }
        public int FractionId_Minimum { get; }
        public int FractionId_Maximum { get; }

        public DoubleRange RetentionTimeRange => rtRange ??= new DoubleRange(RetentionTime_Begin, RetentionTime_End);

        public Ms1Feature(string featureFileLine)
        {
            var splits = featureFileLine.Split('\t');
            SampleId = int.Parse(splits[0]);
            Id = int.Parse(splits[1]);
            Mass = double.Parse(splits[2]);
            Intensity = double.Parse(splits[3]);
            RetentionTime_Begin = double.Parse(splits[4]);
            RetentionTime_End = double.Parse(splits[5]);
            RetentionTime_Apex = double.Parse(splits[6]);
            IntensityApex = double.Parse(splits[7]);
            ChargeState_Minimum = int.Parse(splits[8]);
            ChargeState_Maximum = int.Parse(splits[9]);
            FractionId_Minimum = int.Parse(splits[10]);
            FractionId_Maximum = int.Parse(splits[11]);
        }
    }

    public class Ms2Feature : MsFeature
    {
        public int Spectra_Id { get; }
        public int Fraciton_Id { get; }
        public string FileName { get; }
        public int Scans { get; }
        public int Ms1_Id { get; }
        public int Ms1_Scans { get; }
        public double PrecursorMass { get; }
        public double PrecursorIntensity { get; }
        public int FractionFeature_Id { get; }
        public double FractionFeature_Intensity { get; }
        public double FractionFeature_Score { get; }
        public double FractionFeature_Apex { get; }
        public int SampleFeature_Id { get; }
        public double SampleFeature_Intensity { get; }

        public Ms2Feature(string featureFileLine)
        {
            var splits = featureFileLine.Split('\t');
            Spectra_Id = int.Parse(splits[0]);
            Fraciton_Id = int.Parse(splits[1]);
            FileName = splits[2];
            Scans = int.Parse(splits[3]);
            Ms1_Id = int.Parse(splits[4]);
            Ms1_Scans = int.Parse(splits[5]);
            PrecursorMass = double.Parse(splits[6]);
            PrecursorIntensity = double.Parse(splits[7]);
            FractionFeature_Id = int.Parse(splits[8]);
            FractionFeature_Intensity = double.Parse(splits[9]);
            FractionFeature_Score = double.Parse(splits[10]);
            FractionFeature_Apex = double.Parse(splits[11]);
            SampleFeature_Id = int.Parse(splits[12]);
            SampleFeature_Intensity = double.Parse(splits[13]);
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
            Assert.That(first.RetentionTime_Begin, Is.EqualTo(2368.83));
            Assert.That(first.RetentionTime_End, Is.EqualTo(2394.78));
            Assert.That(first.RetentionTime_Apex, Is.EqualTo(2387.37));
            Assert.That(first.IntensityApex, Is.EqualTo(961066983.89));
            Assert.That(first.ChargeState_Minimum, Is.EqualTo(7));
            Assert.That(first.ChargeState_Maximum, Is.EqualTo(18));
            Assert.That(first.FractionId_Minimum, Is.EqualTo(0));
            Assert.That(first.FractionId_Maximum, Is.EqualTo(0));
            Assert.That(first.RetentionTimeRange.Width, Is.EqualTo(first.RetentionTime_End - first.RetentionTime_Begin));

            Assert.That(last.SampleId, Is.EqualTo(0));
            Assert.That(last.Id, Is.EqualTo(20572));
            Assert.That(last.Mass, Is.EqualTo(1968.81135).Within(0.00001));
            Assert.That(last.Intensity, Is.EqualTo(3661.2));
            Assert.That(last.RetentionTime_Begin, Is.EqualTo(3142.07));
            Assert.That(last.RetentionTime_End, Is.EqualTo(3142.07));
            Assert.That(last.RetentionTime_Apex, Is.EqualTo(3142.07));
            Assert.That(last.IntensityApex, Is.EqualTo(3661.2));
            Assert.That(last.ChargeState_Minimum, Is.EqualTo(1));
            Assert.That(last.ChargeState_Maximum, Is.EqualTo(1));
            Assert.That(last.FractionId_Minimum, Is.EqualTo(0));
            Assert.That(last.FractionId_Maximum, Is.EqualTo(0));
            Assert.That(last.RetentionTimeRange.Width, Is.EqualTo(last.RetentionTime_End - last.RetentionTime_Begin));
        }

        [Test]
        public static void TestMs2FeatureFirstAndLastAreCorrect()
        {

        }

        [Test]
        public static void TestGetOverlap()
        {
            var ms1Features = MsFeature.GetMs1FeaturesFromFile(Ms1FeatureFilePath).ToList();


            var temp = MsFeature.GetOverlappedByPercentage(ms1Features.Take(10).ToList(), ms1Features.First(), 80);
        }
    }
}
