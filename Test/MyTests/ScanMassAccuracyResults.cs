using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GuiFunctions;
using MassSpectrometry;
using MathNet.Numerics.Statistics;
using SpectralAveraging;

namespace Test
{
    internal record ScanMassAccuracyResults : ITsv
    {
        internal int ScanNumber { get; init; }
        internal double MedianOverStdOfPeaks { get; init; }
        internal int MzFound { get; init; }
        internal int ChargeResolved { get; init; }
        internal double MzPpmError { get; init; }
        internal double AverageIsotopicPeaksPerEnvelope { get; init; }

        /// <summary>
        /// Dictionary with the mz of each peak as the key and results as the value
        /// </summary>
        internal Dictionary<double, IndividualPeakResult> IndividualPeakResults { get; set; }

        public ScanMassAccuracyResults((int Charge, double Mz)[] chargeMzPairs, MsDataScan scan, List<IndividualPeakResult> individualResults)
        {
            ScanNumber = scan.OneBasedScanNumber;

            var median = scan.MassSpectrum.YArray.Median();
            var deviation = scan.MassSpectrum.YArray.StandardDeviation();
            MedianOverStdOfPeaks = median / deviation;

            IndividualPeakResults = new();
            for (int i = 0; i < chargeMzPairs.Length; i++)
            {
                IndividualPeakResults.Add(chargeMzPairs[i].Mz, individualResults[i]);
            }

            MzFound = IndividualPeakResults
                .Count(p => p.Value.FoundAboveCutoff);
            ChargeResolved = IndividualPeakResults
                .Count(p => p.Value.ChargeStateResolvable);
            MzPpmError = IndividualPeakResults.Values
                .Average(p => p.MzPpmError);
            AverageIsotopicPeaksPerEnvelope = IndividualPeakResults
                .Average(p => p.Value.NumberOfIsotopicPeaks);
        }

        public ScanMassAccuracyResults(string tsvLine)
        {
            IndividualPeakResults = new();
            var splits = tsvLine.Split('\t');
            ScanNumber = int.Parse(splits[0]);
            MedianOverStdOfPeaks = double.Parse(splits[1]);
            MzFound = int.Parse(splits[2]);
            ChargeResolved = int.Parse(splits[3]);
            MzPpmError = double.Parse(splits[4]);
            AverageIsotopicPeaksPerEnvelope = double.Parse(splits[5]);
        }

        public static IEnumerable<ScanMassAccuracyResults> LoadResultsListFromTsv(string tsvPath)
        {
            var scansLines = File.ReadAllLines(tsvPath);
            List<ScanMassAccuracyResults> scanResults = new();
            for (int i = 1; i < scansLines.Length; i++)
            {
                yield return new ScanMassAccuracyResults(scansLines[i]);
            }
        }

        public string TabSeparatedHeader
        {
            get
            {
                var sb = new StringBuilder();

                sb.Append("ScanNumber\t");
                sb.Append("MedianOverStdOfPeaks\t");
                sb.Append("MzFound\t");
                sb.Append("ChargeResolved\t");
                sb.Append("MzPpmError\t");
                sb.Append("AverageIsotopicPeaksPerEnvelope\t");

                var tsvString = sb.ToString().TrimEnd('\t');
                return tsvString;
            }
        }
        public string ToTsvString()
        {
            var sb = new StringBuilder();

            sb.Append($"{ScanNumber}\t");
            sb.Append($"{MedianOverStdOfPeaks}\t");
            sb.Append($"{MzFound}\t");
            sb.Append($"{ChargeResolved}\t");
            sb.Append($"{MzPpmError}\t");
            sb.Append($"{AverageIsotopicPeaksPerEnvelope}\t");

            var tsvString = sb.ToString().TrimEnd('\t');
            return tsvString;
        }
    }
}
