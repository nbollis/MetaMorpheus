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
using MassSpectrometry;
using MathNet.Numerics;
using MzLibUtil;
using NUnit.Framework;
using Org.BouncyCastle.Bcpg.Sig;
using Plotly.NET;
using Test.AveragingPaper;
using Plotly.NET.CSharp;
using static iText.StyledXmlParser.Jsoup.Select.Evaluator;
using static Plotly.NET.StyleParam.DrawingStyle;

namespace Test
{
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

            if (splits.Length == 11)
            {
                IntensityApex = 0;
                ChargeStateMinimum = int.Parse(splits[7]);
                ChargeStateMaximum = int.Parse(splits[8]);
                FractionIdMinimum = int.Parse(splits[9]);
                FractionIdMaximum = int.Parse(splits[10]);
            }
            else if (splits.Length == 12)
            {
                IntensityApex = double.Parse(splits[7]);
                ChargeStateMinimum = int.Parse(splits[8]);
                ChargeStateMaximum = int.Parse(splits[9]);
                FractionIdMinimum = int.Parse(splits[10]);
                FractionIdMaximum = int.Parse(splits[11]);
            }

            if (splits.Length > 12)
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
            int chargeStateMaximum, int fractionIdMinimum, int fractionIdMaximum) : base()
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

        #region IO
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

        #endregion

    }


    public static class MyExtensions 
    {
        public static bool DeepCompare(this object obj, object another, out List<object> errors)
        {
            errors = new List<object>();
            if (ReferenceEquals(obj, another)) return true;
            if (obj == null || another == null) return false;
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
