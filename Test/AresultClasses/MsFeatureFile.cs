using GuiFunctions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Plotly.NET;
using Test.AveragingPaper;
using Chart = Plotly.NET.CSharp.Chart;

namespace Test
{
    public class Ms1FeatureFile : ITsv
    {
        private string _filePath;

        public List<Ms1Feature> Features { get; }
        public string DataSet { get; private set; }
        public string FileName => Path.GetFileNameWithoutExtension(_filePath);
        public string FileGroup { get; private set; }
        public DeconResults.DeconSoftware Software { get; private set; }
        public int FeatureCount { get; private set; }
        public int ArtifactCount { get; private set; }
        public int IsotopologueCount { get; private set; }
        public int LowHarmonicCount { get; private set; }
        public int HighHarmonicCount { get; private set; }
        public int ChargeOffByOneCount { get; private set; }


        // IMPORTANT: Any artifact detection must be run for this class to work properly, constructors handles this
        public Ms1FeatureFile(string filePath, DeconResults.DeconSoftware software, string dataSet, string fileGroup)
        {
            Features = MsFeature.GetMs1FeaturesFromFile(filePath).ToList();
            _filePath = filePath;
            Software = software;
            DataSet = dataSet;
            FileGroup = fileGroup;

            //if (Features.Any(p => !p.PerformedArtifactDetection))
            //    MsFeature.PerformArtifactDetection(Features);

            FeatureCount = Features.Count;
            ArtifactCount = Features.Count(p => p.Artifact);
            IsotopologueCount = Features.Count(p => p.Isotopologue);
            LowHarmonicCount = Features.Count(p => p.LowHarmonic);
            HighHarmonicCount = Features.Count(p => p.HighHarmonic);
            ChargeOffByOneCount = Features.Count(p => p.ChargeOffByOne);
        }

        public string TabSeparatedHeader => "DataSet\tFileName\tFileGroup\tSoftware\tFeatureCount\tIsArtifact\tHighHarmonic\tLowHarmonic\tIsotopologue\tChargeOffByOne";

        public string ToTsvString() => string.Join("\t",
            new List<string>()
            {
                DataSet, FileName, FileGroup, Software.ToString(), FeatureCount.ToString(), ArtifactCount.ToString(),
                HighHarmonicCount.ToString(), LowHarmonicCount.ToString(), IsotopologueCount.ToString(),
                ChargeOffByOneCount.ToString()
            });

        #region Ploting

        public GenericChart.GenericChart GetTicChart()
        {
            var maxIntensity = Features.Max(p => p.Intensity);
            var xValues = Features.Select(p => p.RetentionTimeApex).ToArray();
            var yValues = Features.Select(p => p.Intensity / maxIntensity).ToArray();

            var plot = Chart.Line<double, double, string>(xValues, yValues)
                .WithXAxisStyle(Title.init("Retention Time"))
                .WithYAxisStyle(Title.init("Relative Intensity"))
                .WithTitle($"{DataSet} - {FileGroup} - {FileName}");

            return plot;
        }

        #endregion


    }

    
}
