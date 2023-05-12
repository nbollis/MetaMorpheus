using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
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
}
