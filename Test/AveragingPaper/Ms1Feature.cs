using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chemistry;
using Easy.Common.Extensions;
using Easy.Common.Interfaces;
using EngineLayer;
using GuiFunctions;
using MathNet.Numerics;
using MzLibUtil;
using NUnit.Framework;
using Org.BouncyCastle.Bcpg.Sig;
using static iText.StyledXmlParser.Jsoup.Select.Evaluator;
using static Plotly.NET.StyleParam.DrawingStyle;
using Constants = Chemistry.Constants;

namespace Test.AveragingPaper
{
    public abstract class MsFeature
    {
        #region Artifact Detection Properties

        public bool Artifact => Isotopologue && LowHarmonic && HighHarmonic && ChargeOffByOne;
        public bool Isotopologue { get; protected set; }
        public bool LowHarmonic { get; protected set; }
        public bool HighHarmonic { get; protected set; }
        public bool ChargeOffByOne { get; protected set; }
        public bool PerformedArtifactDetection { get; protected set; }

        // 2 -> 100 per the FLASHDeconv paper
        public static int[] cRange { get; }

        // -10 -> 10 per the FLASHDeconv paper
        public static int[] kRange { get; }

        // 10 ppm tolerance per the FLASHDeconv paper
        public static Tolerance PpmTolerance { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// constructor that initializes static values
        /// </summary>
        static MsFeature()
        {
            cRange = Enumerable.Range(2, 99).ToArray();
            kRange = Enumerable.Range(-10, 21).ToArray();
            PpmTolerance = new PpmTolerance(10);
        }

        /// <summary>
        /// Constructor that sets all artifact properties to false
        /// </summary>
        protected MsFeature()
        {
            Isotopologue = false;
            LowHarmonic = false;
            HighHarmonic = false;
            ChargeOffByOne = false;
            PerformedArtifactDetection = false;
        }

        #endregion

        #region IO

        /// <summary>
        /// Loads in all Ms1Features from a ms1.feature file
        /// Tested with TopPIC output
        /// </summary>
        /// <param name="filepath"></param>
        /// <returns></returns>
        public static IEnumerable<Ms1Feature> GetMs1FeaturesFromFile(string filepath)
        {
            bool firstLine = true;
            string line;
            using var sr = new StreamReader(filepath);
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

        /// <summary>
        /// Loads in all Ms1Features from a ms2.feature file
        /// Tested with TopPIC output
        /// </summary>
        /// <param name="filepath"></param>
        /// <returns></returns>
        public static IEnumerable<Ms2Feature> GetMs2FeaturesFromFile(string filepath)
        {
            bool firstLine = true;
            string line;
            using var sr = new StreamReader(filepath);
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

        public static void ExportFeatures(List<Ms1Feature> featuresToExport, string exportPath)
        {
            var extension = Path.GetExtension(exportPath);
            if (extension is null)
                exportPath += ".feature";
            else if (!extension.Equals(".feature"))
                throw new ArgumentException("Feature export path cannot have an extension besides .feature");

            using var sw = new StreamWriter(File.Create(exportPath));
            sw.WriteLine(featuresToExport.First().TabSeparatedHeader);
            foreach (var feature in featuresToExport)
            {
                sw.WriteLine(feature.ToTsvString());
            }
        }

        #endregion

        #region Artifact Detection Methods


        /// <summary>
        /// Check each feature against each other feature in the collection for being an artifact
        /// </summary>
        /// <remarks>
        /// Taken from FLASHDeconv paper section titled .Mass and Isotopologue Artifact Detection'
        /// https://www.sciencedirect.com/science/article/pii/S2405471220300302?via%3Dihub#sec4
        /// f -> feature
        /// F -> features
        /// F(f) -> overlappedFeatures
        /// (Delta) -> difference between C13 and C12
        /// </remarks>
        /// <param name="features"></param>
        public static void PerformArtifactDetection(List<Ms1Feature> features)
        {
            foreach (var feature in features)
            {
                // features with an 80% or greater overlap in retention time
                // filter out overlapped features that are lower in intensity
                foreach (var overlappedFeature in GetRetentionTimeOverlappedFeaturesByPercentage(feature, features, 80)
                             .Where(p => p.Intensity < feature.Intensity))
                {
                    feature.LowHarmonic = IsLowHarmonic(feature.Mass, overlappedFeature.Mass);
                    feature.HighHarmonic = IsHighHarmonic(feature.Mass, overlappedFeature.Mass);
                    feature.Isotopologue = IsIsotopologue(feature.Mass, overlappedFeature.Mass);
                    feature.ChargeOffByOne = IsChargeOffByOne(feature.Mass, overlappedFeature.Mass);
                }
                feature.PerformedArtifactDetection = true;
            }
        }

        /// <summary>
        /// Determines if featureToCompareAgainstMass is a low harmonic of featureToCheckMass within charge range cRange and +- kRange missed monoisotopics
        /// </summary>
        /// <param name="featureToCheckMass"></param>
        /// <param name="featureToCompareAgainstMass"></param>
        /// <returns></returns>
        internal static bool IsLowHarmonic(double featureToCheckMass, double featureToCompareAgainstMass)
        {
            // foreach combination of c and k, determine if there are any combinations of c(m + k(Delta)) within 10 ppm of the featureToChecks mass
            // where m is the mass of the feature to compare against
            // expanded implementation is shown below
            return (from c in cRange from k in kRange select c * (featureToCompareAgainstMass + k * Chemistry.Constants.C13MinusC12))
                .Any(value => PpmTolerance.Within(featureToCheckMass, value));

            //for (int c = 0; c < cRange.Length; c++)
            //{
            //    for (int k = 0; k < kRange.Length; k++)
            //    {
            //        // c(m + k(Delta))
            //        var value = cRange[c] * (featureToCompareAgainstMonoMass + kRange[k] * Chemistry.Constants.C13MinusC12);
            //        if (PpmTolerance.Within(featureToCheckMonoMass, value))
            //            return true;
            //    }
            //}
            //return false;
        }

        /// <summary>
        /// Determines if featureToCompareAgainstMass is a high harmonic of featureToCheckMass within charge range cRange and +- kRange missed monoisotopics
        /// </summary>
        /// <param name="featureToCheckMass"></param>
        /// <param name="featureToCompareAgainstMass"></param>
        /// <returns></returns>
        internal static bool IsHighHarmonic(double featureToCheckMass, double featureToCompareAgainstMass)
        {
            // foreach combination of c and k, determine if there are any combinations of (m + k(Delta))/c within 10 ppm of the featureToChecks mass
            // where m is the mass of the feature to compare against
            // expanded implementation is shown below
            return (from c in cRange from k in kRange select (featureToCompareAgainstMass + k * Chemistry.Constants.C13MinusC12) / c)
                .Any(value => PpmTolerance.Within(featureToCheckMass, value));

            //for (int c = 0; c < cRange.Length; c++)
            //{
            //    for (int k = 0; k < kRange.Length; k++)
            //    {
            //        // (m + k(Delta)) / c
            //        var value = (featureToCompareAgainstMonoMass + kRange[k] * Chemistry.Constants.C13MinusC12) / cRange[c];
            //        if (PpmTolerance.Within(featureToCheckMonoMass, value))
            //            return true;
            //    }
            //}
            //return false;
        }

        /// <summary>
        /// Determines if featureToCheckAgainst is an off by one charge artifact of featureToCheck
        /// </summary>
        /// <param name="featureToCheckMass"></param>
        /// <param name="featureToCheckAgainstMass"></param>
        /// <returns></returns>
        internal static bool IsChargeOffByOne(double featureToCheckMass, double featureToCheckAgainstMass)
        {
            // c* is the specific c within cRange being tested
            // charge states are selected such that m/c* - m/(c* + 1) < 4
            foreach (var c in cRange.Where(c => (featureToCheckAgainstMass / (double)c) - (featureToCheckAgainstMass / (double)(c + 1)) < 4))
            {
                foreach (var k in kRange)
                {
                    // (m + k(Delta)) * (c* + 1) / c*
                    if (PpmTolerance.Within(featureToCheckMass,
                            (featureToCheckAgainstMass + k * Constants.C13MinusC12) * (c + 1) / c))
                        return true;

                    // (m + k(Delta)) * (c* - 1) / c*
                    if (PpmTolerance.Within(featureToCheckMass,
                            (featureToCheckAgainstMass + k * Constants.C13MinusC12) * (c - 1) / c))
                        return true;
                }
            }
            return false;
        }

        internal static bool IsIsotopologue(double featureToCheckMass, double featureToCheckAgainstMass)
        {
            // m +- (Delta)
            return PpmTolerance.Within(featureToCheckMass,featureToCheckAgainstMass - Constants.C13MinusC12) 
                   || PpmTolerance.Within(featureToCheckMass,featureToCheckAgainstMass + Constants.C13MinusC12);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Returns features with x percentage overlap in their retention time elution
        /// </summary>
        /// <param name="featuresToCompare"></param>
        /// <param name="featureToCheck"></param>
        /// <param name="percentage"></param>
        /// <returns></returns>
        internal static IEnumerable<Ms1Feature> GetRetentionTimeOverlappedFeaturesByPercentage(Ms1Feature featureToCheck, List<Ms1Feature> featuresToCompare, int percentage)
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

        /// <summary>
        /// Finds the amount of overlap between two double ranges
        /// </summary>
        /// <param name="start1"></param>
        /// <param name="end1"></param>
        /// <param name="start2"></param>
        /// <param name="end2"></param>
        /// <returns></returns>
        internal static double FindOverlapping(double start1, double end1, double start2, double end2)
        {
            return Math.Max(0, Math.Min(end1, end2) - Math.Max(start1, start2));
        }

        internal static double FindOverlapping(DoubleRange range1, DoubleRange range2)
        {
            return FindOverlapping(range1.Minimum, range1.Maximum, range2.Minimum, range2.Maximum);
        }

        #endregion
    }

    public class Ms1Feature : MsFeature, ITsv
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

        public double RetentionTimeWidth => RetentionTimeEnd - RetentionTimeBegin;

        public Ms1Feature(string featureFileLine) : base()
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

            if (splits.IndexOf(splits.Last()) > 11)
            {
                PerformedArtifactDetection = true;
                HighHarmonic = bool.Parse(splits[13]);
                LowHarmonic = bool.Parse(splits[14]);
                Isotopologue = bool.Parse(splits[15]);
                ChargeOffByOne = bool.Parse(splits[16]);
            }
        }

        public Ms1Feature(int sampleId, int id, double mass, double intensity, double retentionTimeBegin,
            double retentionTimeEnd, double retentionTimeApex, double intensityApex, int chargeStateMinimum,
            int chargeStateMaximum, int fractionIdMinimum, int fractionIdMaximum) : base ()
        {
            SampleId = sampleId;
            Id = id;
            Mass = mass;
            Intensity = intensity;
            RetentionTimeBegin = retentionTimeBegin;
            RetentionTimeEnd = retentionTimeEnd;
            RetentionTimeApex = retentionTimeApex;
            IntensityApex = intensityApex;
            ChargeStateMinimum = chargeStateMinimum;
            ChargeStateMaximum = chargeStateMaximum;
            FractionIdMinimum = fractionIdMinimum;
            FractionIdMaximum = fractionIdMaximum;
        }

        public override string ToString()
        {
            return Id.ToString();
        }

        public string TabSeparatedHeader =>
            PerformedArtifactDetection
                ? "Sample_ID\tID\tMass\tIntensity\tTime_begin\tTime_end\tApex_time\tApex_intensity\tMinimum_charge_state\tMaximum_charge_state\tMinimum_fraction_id\tMaximum_fraction_id\tIsArtifact\tHighHarmonic\tLowHarmonic\tIsotopologue\tChargeOffByOne"
                : "Sample_ID\tID\tMass\tIntensity\tTime_begin\tTime_end\tApex_time\tApex_intensity\tMinimum_charge_state\tMaximum_charge_state\tMinimum_fraction_id\tMaximum_fraction_id";
        public string ToTsvString()
        {
            var sb = new StringBuilder();
            sb.Append($"{SampleId}\t");
            sb.Append($"{Id}\t");
            sb.Append($"{Mass}\t");
            sb.Append($"{Intensity}\t");
            sb.Append($"{Math.Round(RetentionTimeBegin, 2)}\t");
            sb.Append($"{Math.Round(RetentionTimeEnd, 2)}\t");
            sb.Append($"{Math.Round(RetentionTimeApex, 2)}\t");
            sb.Append($"{Math.Round(IntensityApex, 2)}\t");
            sb.Append($"{ChargeStateMinimum}\t");
            sb.Append($"{ChargeStateMaximum}\t");
            sb.Append($"{FractionIdMinimum}\t");
            sb.Append($"{FractionIdMaximum}\t");

            if (PerformedArtifactDetection)
            {
                sb.Append($"{Artifact}\t");
                sb.Append($"{HighHarmonic}\t");
                sb.Append($"{LowHarmonic}\t");
                sb.Append($"{Isotopologue}\t");
                sb.Append($"{ChargeOffByOne}\t");
            }

            var tsvString = sb.ToString().TrimEnd('\t');
            return tsvString;
        }
    }

    public class Ms2Feature : MsFeature
    {
        public int SpectraId { get; }
        public int FractionId { get; }
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

        public Ms2Feature(string featureFileLine) : base()
        {
            var splits = featureFileLine.Split('\t');
            SpectraId = int.Parse(splits[0]);
            FractionId = int.Parse(splits[1]);
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

        public Ms2Feature(int spectraId, int fractionId, string fileName, int scans, int ms1Id, int ms1Scans, double precursorMass, 
            double precursorIntensity, int fractionFeatureId, double fractionFeatureIntensity, double fractionFeatureScore, 
            double fractionFeatureApex, int sampleFeatureId, double sampleFeatureIntensity) : base()
        {
            SpectraId = spectraId;
            FractionId = fractionId;
            FileName = fileName;
            Scans = scans;
            Ms1Id = ms1Id;
            Ms1Scans = ms1Scans;
            PrecursorMass = precursorMass;
            PrecursorIntensity = precursorIntensity;
            FractionFeatureId = fractionFeatureId;
            FractionFeatureIntensity = fractionFeatureIntensity;
            FractionFeatureScore = fractionFeatureScore;
            FractionFeatureApex = fractionFeatureApex;
            SampleFeatureId = sampleFeatureId;
            SampleFeatureIntensity = sampleFeatureIntensity;
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
                var downNIsotopes = mass - n * Constants.C13MinusC12 -1;
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

    public static class ClassExtensions
    {
        public static bool DeepCompare(this object obj, object another, out List<object> errors)
        {
            errors = new List<object>();
            if (ReferenceEquals(obj, another)) return true;
            if ((obj == null) || (another == null)) return false;
            //Compare two object's class, return false if they are difference
            if (obj.GetType() != another.GetType()) return false;

            var result = true;
            //Get all properties of obj
            //And compare each other
            foreach (var property in obj.GetType().GetProperties())
            {
                var objValue = property.GetValue(obj);
                var anotherValue = property.GetValue(another);
                if (!objValue.Equals(anotherValue))
                {
                    result = false;
                    errors.Add(objValue);
                }
            }

            return result;
        }
    }
}
