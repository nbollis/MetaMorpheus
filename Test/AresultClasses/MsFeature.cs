using MzLibUtil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Constants = Chemistry.Constants;

namespace Test
{
    public abstract class MsFeature
    {
        #region Artifact Detection Properties

        public bool Artifact => Isotopologue || LowHarmonic || HighHarmonic || ChargeOffByOne;
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
            return (from c in cRange from k in kRange select c * (featureToCompareAgainstMass + k * Constants.C13MinusC12))
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
            return (from c in cRange from k in kRange select (featureToCompareAgainstMass + k * Constants.C13MinusC12) / c)
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
            foreach (var c in cRange.Where(c => featureToCheckAgainstMass / c - featureToCheckAgainstMass / (c + 1) < 4))
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
            return PpmTolerance.Within(featureToCheckMass, featureToCheckAgainstMass - Constants.C13MinusC12)
                   || PpmTolerance.Within(featureToCheckMass, featureToCheckAgainstMass + Constants.C13MinusC12);
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
        internal static int Truth(params bool[] booleans)
        {
            return booleans.Count(b => b);
        }

        #endregion
    }
}
