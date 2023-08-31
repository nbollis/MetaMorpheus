using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Plotly.NET.TraceObjects;

namespace Test
{
    public class Histogram
    {
        private double min;
        private double max;

        public List<double> Values { get; init; }

        public double BinSize { get; init; }

        public HistogramBin[] Bins { get; private set; }

        public string Identifier { get; init; }

        public double AverageValue { get; init; }
        public double StandardDeviationValue { get; init; }

        public Histogram(IEnumerable<double> values, double binSize, double? min = null, double? max = null, string identifier = "")
        {
            Values = values.Where(p => !double.IsNaN(p)).ToList();
            BinSize = binSize;
            this.min = min ?? Values.Min();
            this.max = max ?? Values.Max() + BinSize;
            Identifier = identifier;
            AverageValue = Values.Average();
            StandardDeviationValue = Values.StandardDeviation();

            DoTheHistingAndGraming();
        }

        private void DoTheHistingAndGraming()
        {
            var bins = new List<HistogramBin>();
            // create bins
            for (double i = min; i < max; i += BinSize)
            {
                bins.Add(new HistogramBin(i, i + BinSize, 0));
            }
            //bins.Add(new HistogramBin(max, 100 * max, 0));

            // populate bins
            foreach (var val in Values)
            {
                var binOfInterest = bins.First(bin => val >= bin.Start && val < bin.End);
                binOfInterest.Count++;
            }

            Bins = bins.ToArray();
        }

    }

    public class HistogramBin
    {
        public double Start;
        public double End;
        public double Count;

        public HistogramBin(double start, double end, double count)
        {
            Start = start;
            End = end;
            Count = count;
        }

        public override string ToString()
        {
            return $"{End},{Count}";
        }
    };


    public static class HistogramExtensions
    {
        public static void ExportHistogram(this Histogram histogram, string outPath, bool withPercent = false)
        {
            using (var sw = new StreamWriter(File.Create(outPath)))
            {
                if (withPercent)
                {
                    sw.WriteLine("Bin,Frequency,Percent");
                    var total = histogram.Bins.Sum(p => p.Count);
                    foreach (var bin in histogram.Bins)
                    {
                        sw.WriteLine($"{bin},{bin.Count / total * 100}");
                    }
                }
                else
                {
                    sw.WriteLine("Bin,Frequency");
                    foreach (var bin in histogram.Bins)
                    {
                        sw.WriteLine(bin);
                    }
                }

                
            }
        }

        public static void ExportMultipleGrams(this List<Histogram> histograms, string outPath)
        {
            if (histograms.Select(p => p.Bins.Length).Distinct().Skip(1).Any())
                throw new ArgumentException("All histograms must have same number of bins");

            var identifiers = string.Join(',', histograms.Select(p => p.Identifier)) + ","
                + string.Join(',', histograms.Select(p => p.AverageValue)) + ","
                + string.Join(',', histograms.Select(p => p.StandardDeviationValue));
            var lineValues = new double[histograms.First().Bins.Length][];

            // get values in jagged array ready for output
            for (int binIndex = 0; binIndex < histograms.First().Bins.Length; binIndex++)
            {
                lineValues[binIndex] = new double[histograms.Count];
                for (int histogramIndex = 0; histogramIndex < histograms.Count; histogramIndex++)
                {
                    lineValues[binIndex][histogramIndex] = histograms[histogramIndex].Bins[binIndex].Count;
                }
            }

            using (var sw = new StreamWriter(File.Create(outPath)))
            {
                sw.WriteLine($"Bin,{identifiers}");
                for (int i = 0; i < lineValues.Length; i++)
                {
                    sw.WriteLine($"{histograms.First().Bins[i].End},{string.Join(',', lineValues[i])}");
                }
            }
        }
    }
}
