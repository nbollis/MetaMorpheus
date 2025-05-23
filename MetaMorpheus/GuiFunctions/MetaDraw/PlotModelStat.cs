﻿using EngineLayer;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using Omics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using Proteomics.RetentionTimePrediction;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Globalization;
using Readers;
using ThermoFisher.CommonCore.Data.Business;

namespace GuiFunctions
{
    public class PlotModelStat : INotifyPropertyChanged, IPlotModel
    {
        private PlotModel privateModel;
        private readonly ObservableCollection<SpectrumMatchFromTsv> allSpectralMatches;
        private readonly Dictionary<string, ObservableCollection<SpectrumMatchFromTsv>> psmsBySourceFile;

        public static List<string> PlotNames = new List<string> {
            "Histogram of Precursor PPM Errors (around 0 Da mass-difference notch only)",
            "Histogram of Fragment PPM Errors",
            "Histogram of Precursor Charges",
            "Histogram of Fragment Charges",
            "Histogram of Precursor Masses",
            "Histogram of Precursor m/z",
            "Histogram of Hydrophobicity scores",
            "Precursor PPM Error vs. RT",
            "Histogram of PTM Spectral Counts",
            "Predicted RT vs. Observed RT"
        };

        private static Dictionary<ProductType, OxyColor> productTypeDrawColors = new Dictionary<ProductType, OxyColor>
        {
            { ProductType.b, OxyColors.Blue },
            { ProductType.y, OxyColors.Red },
            { ProductType.c, OxyColors.Gold },
            { ProductType.zPlusOne, OxyColors.Orange },
            { ProductType.D, OxyColors.DodgerBlue },
            { ProductType.M, OxyColors.Firebrick }
        };

        private static List<OxyColor> columnColors = new List<OxyColor>
        {
            OxyColors.Blue, OxyColors.Red, OxyColors.Green, OxyColors.DarkGoldenrod, OxyColors.DarkViolet,
            OxyColors.DeepPink, OxyColors.SkyBlue, OxyColors.LawnGreen, OxyColors.Sienna, OxyColors.DarkBlue,
            OxyColors.PeachPuff, OxyColors.DarkSlateGray, OxyColors.SpringGreen, OxyColors.Peru, OxyColors.OrangeRed

        };

        public PlotModel Model
        {
            get
            {
                return privateModel;
            }
            private set
            {
                privateModel = value;
                NotifyPropertyChanged("Model");
            }
        }

        public OxyColor Background => OxyColors.White;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public PlotModelStat(string plotName, ObservableCollection<SpectrumMatchFromTsv> sms, Dictionary<string, ObservableCollection<SpectrumMatchFromTsv>> smsBySourceFile)
        {
            privateModel = new PlotModel { Title = plotName, DefaultFontSize = 14 };
            allSpectralMatches = sms;
            this.psmsBySourceFile = smsBySourceFile;
            createPlot(plotName);
            privateModel.DefaultColors = columnColors;
        }

        private void createPlot(string plotType)
        {
            switch (plotType)
            {
                case "Histogram of Precursor PPM Errors (around 0 Da mass-difference notch only)":
                    histogramPlot(1);
                    break;
                case "Histogram of Fragment PPM Errors": 
                    histogramPlot(2);
                    break;
                case "Histogram of Precursor Charges":
                    histogramPlot(3);
                    break;
                case "Histogram of Fragment Charges":
                    histogramPlot(4);
                    break;
                case "Histogram of Precursor Masses":
                    histogramPlot(6);
                    break;
                case "Histogram of Precursor m/z":
                    histogramPlot(7);
                    break;
                case "Histogram of Hydrophobicity scores":
                    histogramPlot(8);
                    break;
                case "Precursor PPM Error vs. RT":
                    linePlot(1);
                    break;
                case "Histogram of PTM Spectral Counts":
                    histogramPlot(5);
                    break;
                case "Predicted RT vs. Observed RT":
                    linePlot(3);
                    break;
            }
        }

        private void histogramPlot(int plotType)
        {
            privateModel.LegendTitle = "Source file(s)";
            string yAxisTitle = "Count";
            string xAxisTitle = "";
            double binSize = -1;
            double labelAngle = 0;
            SortedList<double, double> numCategory = new SortedList<double, double>();
            Dictionary<string, IEnumerable<double>> numbersBySourceFile = new Dictionary<string, IEnumerable<double>>();    // key is file name, value is data from that file
            Dictionary<string, Dictionary<string, int>> dictsBySourceFile = new Dictionary<string, Dictionary<string, int>>();   // key is file name, value is dictionary of bins and their counts

            switch (plotType)
            {
                case 1: // Histogram of Precursor PPM Errors (around 0 Da mass-difference notch only)
                    xAxisTitle = "Precursor error (ppm)";
                    binSize = 0.1;
                    foreach (string key in psmsBySourceFile.Keys)
                    {
                        numbersBySourceFile.Add(key, psmsBySourceFile[key].Where(p => !p.MassDiffDa.Contains("|") && Math.Round(double.Parse(p.MassDiffDa, CultureInfo.InvariantCulture), 0) == 0).Select(p => double.Parse(p.MassDiffPpm, CultureInfo.InvariantCulture)));
                        var results = numbersBySourceFile[key].GroupBy(p => roundToBin(p, binSize)).OrderBy(p => p.Key).Select(p => p);
                        dictsBySourceFile.Add(key, results.ToDictionary(p => p.Key.ToString(), v => v.Count()));
                    }
                    break;
                case 2: // Histogram of Fragment PPM Errors 
                    xAxisTitle = "Fragment error (ppm)";
                    binSize = 1;
                    foreach (string key in psmsBySourceFile.Keys)
                    {
                        numbersBySourceFile.Add(key, psmsBySourceFile[key].SelectMany(p => p.MatchedIons.Select(v => v.MassErrorPpm)));
                        var results = numbersBySourceFile[key].GroupBy(p => roundToBin(p, binSize)).OrderBy(p => p.Key).Select(p => p);
                        dictsBySourceFile.Add(key, results.ToDictionary(p => p.Key.ToString(), v => v.Count()));
                    }
                    break;
                case 3: // Histogram of Precursor Charges
                    xAxisTitle = "Precursor charge";
                    binSize = 1;
                    foreach (string key in psmsBySourceFile.Keys)
                    {
                        numbersBySourceFile.Add(key, psmsBySourceFile[key].Select(p => (double)(p.PrecursorCharge)));
                        var results = numbersBySourceFile[key].GroupBy(p => p).OrderBy(p => p.Key).Select(p => p);
                        dictsBySourceFile.Add(key, results.ToDictionary(p => p.Key.ToString(), v => v.Count()));
                    }
                    break;
                case 4: // Histogram of Fragment Charges
                    xAxisTitle = "Fragment charge";
                    binSize = 1;
                    foreach (string key in psmsBySourceFile.Keys)
                    {
                        numbersBySourceFile.Add(key, psmsBySourceFile[key].SelectMany(p => p.MatchedIons.Select(v => (double)v.Charge)));
                        var results = numbersBySourceFile[key].GroupBy(p => p).OrderBy(p => p.Key).Select(p => p);
                        dictsBySourceFile.Add(key, results.ToDictionary(p => p.Key.ToString(), v => v.Count()));
                    }
                    break;
                case 5: // Histogram of PTM Spectral Counts
                    xAxisTitle = "Modification";
                    labelAngle = -50;
                    foreach (string key in psmsBySourceFile.Keys)
                    {
                        var psmsWithMods = psmsBySourceFile[key].Where(p => !p.FullSequence.Contains("|") && p.FullSequence.Contains("["));
                        var mods = psmsWithMods.Select(p => new PeptideWithSetModifications(p.FullSequence, GlobalVariables.AllModsKnownDictionary)).Select(p => p.AllModsOneIsNterminus).SelectMany(p => p.Values);
                        var groupedMods = mods.GroupBy(p => p.IdWithMotif).ToList();
                        dictsBySourceFile.Add(key, groupedMods.ToDictionary(p => p.Key, v => v.Count()));
                    }
                    break;
                case 6: // Histogram of Precursor mass
                    xAxisTitle = "Precursor Mass (Da)";
                    binSize = 100;
                    labelAngle = -50;
                    foreach (string key in psmsBySourceFile.Keys)
                    {
                        numbersBySourceFile.Add(key, psmsBySourceFile[key].Select(p => (double)(p.PrecursorMass)));
                        var results = numbersBySourceFile[key].GroupBy(p => roundToBin(p, binSize)).OrderBy(p => p.Key).Select(p => p);
                        dictsBySourceFile.Add(key, results.ToDictionary(p => p.Key.ToString(), v => v.Count()));
                    }
                    break;
                case 7: // Histogram of Precursor m/z
                    xAxisTitle = "Precursor mass/charge";
                    binSize = 50;
                    labelAngle = -50;
                    foreach (string key in psmsBySourceFile.Keys)
                    {
                        numbersBySourceFile.Add(key, psmsBySourceFile[key].Select(p => (double)(p.PrecursorMz)));
                        var results = numbersBySourceFile[key].GroupBy(p => roundToBin(p, binSize)).OrderBy(p => p.Key).Select(p => p);
                        dictsBySourceFile.Add(key, results.ToDictionary(p => p.Key.ToString(), v => v.Count()));
                    }
                    break;
                case 8: // Histogram of Hydrophobicity Scores
                    xAxisTitle = "Hydrophobicity Score (Determined by SSRCalc)";
                    binSize = 2;
                    labelAngle = -50;
                    SSRCalc3 sSRCalc3 = new SSRCalc3("A100", SSRCalc3.Column.A100);
                    foreach (string key in psmsBySourceFile.Keys)
                    {
                        var values = new List<double>();
                        foreach (var psm in psmsBySourceFile[key])
                        {
                            values.Add(sSRCalc3.ScoreSequence(new PeptideWithSetModifications(psm.BaseSeq.Split("|")[0], null)));
                           
                        }
                        numbersBySourceFile.Add(key, values);
                        var results = numbersBySourceFile[key].GroupBy(p => roundToBin(p, binSize)).OrderBy(p => p.Key).Select(p => p);
                        dictsBySourceFile.Add(key, results.ToDictionary(p => p.Key.ToString(), v => v.Count()));
                    }
                    break;
            }

            String[] category;  // for labeling bottom axis
            int[] totalCounts;  // for having the tracker show total count across all files
            if (plotType == 5)  // category histogram
            {
                // assign all categories their index on the x axis
                IEnumerable<string> allCategories = dictsBySourceFile.Values.Select(p => p.Keys).SelectMany(p => p);
                Dictionary<string, int> categoryIDs = new Dictionary<string, int>();
                int counter = 0;
                foreach (string s in allCategories)
                {
                    if (!categoryIDs.ContainsKey(s))
                    {
                        categoryIDs.Add(s, counter++);
                    }
                }
                category = new string[counter];
                totalCounts = new int[counter];

                // calculate category totals across all files
                foreach (string cat in categoryIDs.Keys)
                {
                    foreach (Dictionary<string, int> dict in dictsBySourceFile.Values)
                    {
                        totalCounts[categoryIDs[cat]] += dict.ContainsKey(cat) ? dict[cat] : 0;
                    }
                }

                // add a column series for each file
                foreach (string key in dictsBySourceFile.Keys)
                {
                    ColumnSeries column = new ColumnSeries { ColumnWidth = 200, IsStacked = true, Title = key, TrackerFormatString = "Bin: {bin}\n{0}: {2}\nTotal: {total}" };
                    foreach (var d in dictsBySourceFile[key])
                    {
                        int id = categoryIDs[d.Key];
                        column.Items.Add(new HistItem(d.Value, id, d.Key, totalCounts[id]));
                        category[categoryIDs[d.Key]] = d.Key;
                    }
                    privateModel.Series.Add(column);
                }
            }
            else    // numerical histogram
            {
                IEnumerable<double> allNumbers = numbersBySourceFile.Values.SelectMany(x => x);

                int end = dictsBySourceFile.Values.Max(p => p.Max(v => int.Parse(v.Key)));
                int start = dictsBySourceFile.Values.Min(p => p.Min(v => int.Parse(v.Key)));
                int numBins = end - start + 1;
                int minBinLabels = 22;  // the number of labeled bins will be between minBinLabels and 2 * minBinLabels
                int skipBinLabel = numBins < minBinLabels ? 1 : numBins / minBinLabels;

                // assign axis labels, skip labels based on skipBinLabel, calculate bin totals across all files
                category = new string[numBins];
                totalCounts = new int[numBins];
                for (int i = start; i <= end; i++)
                {
                    if (i % skipBinLabel == 0)
                    {
                        category[i - start] = Math.Round((i * binSize), 2).ToString(CultureInfo.InvariantCulture);
                    }
                    foreach (Dictionary<string, int> dict in dictsBySourceFile.Values)
                    {
                        totalCounts[i - start] += dict.ContainsKey(i.ToString(CultureInfo.InvariantCulture)) ? dict[i.ToString(CultureInfo.InvariantCulture)] : 0;
                    }
                }

                // add a column series for each file
                foreach (string key in dictsBySourceFile.Keys)
                {
                    var column = new ColumnSeries { ColumnWidth = 200, IsStacked = true, Title = key, TrackerFormatString = "Bin: {bin}\n{0}: {2}\nTotal: {total}" };
                    foreach (var d in dictsBySourceFile[key])
                    {
                        int bin = int.Parse(d.Key);
                        column.Items.Add(new HistItem(d.Value, bin - start, (bin * binSize).ToString(CultureInfo.InvariantCulture), totalCounts[bin - start]));
                    }
                    privateModel.Series.Add(column);
                }
            }

            // add axes
            privateModel.Axes.Add(new CategoryAxis
            {
                Position = AxisPosition.Bottom,
                ItemsSource = category,
                Title = xAxisTitle,
                GapWidth = 0.3,
                Angle = labelAngle,
            });
            privateModel.Axes.Add(new LinearAxis { Title = yAxisTitle, Position = AxisPosition.Left, AbsoluteMinimum = 0 });
        }

        private void linePlot(int plotType)
        {
            string yAxisTitle = "";
            string xAxisTitle = "";
            ScatterSeries series = new ScatterSeries
            {
                MarkerFill = OxyColors.Blue,
                MarkerSize = 0.5,
                TrackerFormatString = "{1}: {2:0.###}\n{3}: {4:0.###}\nFull sequence: {Tag}"
            };
            ScatterSeries variantSeries = new ScatterSeries
            {
                MarkerFill = OxyColors.DarkRed,
                MarkerSize = 1.5,
                MarkerType = MarkerType.Circle,
                TrackerFormatString = "{1}: {2:0.###}\n{3}: {4:0.###}\nFull sequence: {Tag}"
            };
            List<Tuple<double, double, string>> xy = new List<Tuple<double, double, string>>();
            List<Tuple<double, double, string>> variantxy = new List<Tuple<double, double, string>>();
            var filteredList = allSpectralMatches.Where(p => !p.MassDiffDa.Contains("|") && Math.Round(double.Parse(p.MassDiffDa, CultureInfo.InvariantCulture), 0) == 0).ToList();
            var test = allSpectralMatches.SelectMany(p => p.MatchedIons.Select(v => v.MassErrorPpm));
            switch (plotType)
            {
                case 1: // Precursor PPM Error vs. RT
                    yAxisTitle = "Precursor error (ppm)";
                    xAxisTitle = "Retention time";
                    foreach (var psm in filteredList)
                    {
                        if (psm.IdentifiedSequenceVariations == null || psm.IdentifiedSequenceVariations.Equals(""))
                        {
                            xy.Add(new Tuple<double, double, string>(double.Parse(psm.MassDiffPpm, CultureInfo.InvariantCulture), (double)psm.RetentionTime, psm.FullSequence));
                        }
                        else
                        {
                            variantxy.Add(new Tuple<double, double, string>(double.Parse(psm.MassDiffPpm, CultureInfo.InvariantCulture), (double)psm.RetentionTime, psm.FullSequence));
                        }
                    }
                    break;
                case 3: // Predicted RT vs. Observed RT
                    yAxisTitle = "Predicted Hydrophobicity";
                    xAxisTitle = "Observed retention time";
                    SSRCalc3 sSRCalc3 = new SSRCalc3("A100", SSRCalc3.Column.A100);
                    foreach (var psm in allSpectralMatches)
                    {
                        if (psm.IdentifiedSequenceVariations == null || psm.IdentifiedSequenceVariations.Equals(""))
                        {
                            xy.Add(new Tuple<double, double, string>(sSRCalc3.ScoreSequence(new PeptideWithSetModifications(psm.BaseSeq.Split('|')[0], null)),
                            (double)psm.RetentionTime, psm.FullSequence));
                        }
                        else
                        {
                            variantxy.Add(new Tuple<double, double, string>(sSRCalc3.ScoreSequence(new PeptideWithSetModifications(psm.BaseSeq.Split('|')[0], null)),
                            (double)psm.RetentionTime, psm.FullSequence));
                        }
                    }
                    break;
            }
            if (xy.Count != 0)
            {
                // plot each peptide
                IOrderedEnumerable<Tuple<double, double, string>> sorted = xy.OrderBy(x => x.Item1);
                foreach (var val in sorted)
                {
                    series.Points.Add(new ScatterPoint(val.Item2, val.Item1, tag: val.Item3));
                }
                privateModel.Series.Add(series);

                // add series displayed in legend, the real series will show up with a tiny dot for the symbol
                privateModel.Series.Add(new ScatterSeries { Title = "non-variant PSMs", MarkerFill = OxyColors.Blue });
            }

            if (variantxy.Count != 0)
            {
                // plot each variant peptide
                IOrderedEnumerable<Tuple<double, double, string>> variantSorted = variantxy.OrderBy(x => x.Item1);
                foreach (var val in variantSorted)
                {
                    variantSeries.Points.Add(new ScatterPoint(val.Item2, val.Item1, tag: val.Item3));
                }
                privateModel.Series.Add(variantSeries);

                // add series displayed in legend, the real series will show up with a tiny dot for the symbol
                privateModel.Series.Add(new ScatterSeries { Title = "variant PSMs", MarkerFill = OxyColors.DarkRed });
            }
            privateModel.Axes.Add(new LinearAxis { Title = xAxisTitle, Position = AxisPosition.Bottom });
            privateModel.Axes.Add(new LinearAxis { Title = yAxisTitle, Position = AxisPosition.Left });
        }

        // returns a bin index of number relative to 0, midpoints are rounded towards zero
        private static int roundToBin(double number, double binSize)
        {
            int sign = number < 0 ? -1 : 1;
            double d = number * sign;
            double remainder = d % binSize;
            int i = remainder < 0.5 * binSize ? (int)(d / binSize + 0.001) : (int)(d / binSize + 1.001);
            return i * sign;
        }

        // used by histogram plots, gives additional properies for the tracker to display
        private class HistItem : ColumnItem
        {
            public int total { get; set; }
            public string bin { get; set; }
            public HistItem(double value, int categoryIndex, string bin, int total) : base(value, categoryIndex)
            {
                this.total = total;
                this.bin = bin;
            }
        }

        //unused interface methods
        public void Update(bool updateData) { }
        public void Render(IRenderContext rc, double width, double height) { }
        public void AttachPlotView(IPlotView plotView) { }
    }
}