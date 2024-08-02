using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using Chart = Plotly.NET.CSharp.Chart;
using Plotly.NET;
using Plotly.NET.ImageExport;
using Plotly.NET.LayoutObjects;
using Proteomics;
using Easy.Common.Extensions;
using MathNet.Numerics;
using Microsoft.FSharp.Core;
using EngineLayer;
using Nett;
using UsefulProteomicsDatabases;

namespace Test.RyanJulian
{
    internal static class Plotting
    {
        public static Layout DefaultLayoutWithLegend => Layout.init<string>(
            //PaperBGColor: Color.fromARGB(0, 0,0,0),
            //PlotBGColor: Color.fromARGB(0, 0, 0, 0),
            PaperBGColor: Color.fromKeyword(ColorKeyword.White),
            PlotBGColor: Color.fromKeyword(ColorKeyword.White),
            ShowLegend: true,
            Legend: Legend.init(X: 0.5, Y: -0.2, Orientation: StyleParam.Orientation.Horizontal, EntryWidth: 0,
                VerticalAlign: StyleParam.VerticalAlign.Bottom,
                XAnchor: StyleParam.XAnchorPosition.Center,
                YAnchor: StyleParam.YAnchorPosition.Top
            ));

        public static void CreateAminoAcidFrequencyFigure(this RadicalFragmentationExplorer explorer, bool higherResolution = false)
        {
            var modifications = GlobalVariables.AllModsKnown;
            var proteins = ProteinDbLoader.LoadProteinXML(explorer.DatabasePath, true, DecoyType.None, 
                modifications, false, new List<string>(), out var um);

            var sequences = proteins.Select(p => p.BaseSequence)
                .Distinct()
                .ToList();
            Dictionary<char, Dictionary<double, int>> aminoAcidCounts = sequences
                .SelectMany(p => p)
                .Distinct()
                .OrderBy(p => p)
                .ToDictionary(p => p, p => new Dictionary<double, int>());
            foreach (var baseSequence in sequences)
            {

                for (int i = 0; i < baseSequence.Length; i++)
                {
                    double loc = i == 0 ? 0 : Math.Round(i / (double)baseSequence.Length, higherResolution ? 2 : 1);
                    var toAdjust = aminoAcidCounts[baseSequence[i]];
                    if (!toAdjust.TryAdd(loc, 1))
                        toAdjust[loc] += 1;
                }
            }

            aminoAcidCounts = aminoAcidCounts
                .Where(p => p.Key != 'U' && p.Key != 'X' && (!higherResolution || p.Key != 'M'))
                .OrderBy(p => p.Value.Sum(m => m.Value))
                .ToDictionary(p => p.Key, p => p.Value);
            var aas = aminoAcidCounts.Select(p => p.Key).ToArray();
            var yTotals = aas.Select(p => (p, aminoAcidCounts[p].Sum(m => m.Value)))
                .ToDictionary(p => p.p, p => (double)p.Item2);
            var x = aminoAcidCounts.SelectMany(p => p.Value.Keys).Distinct()
                .OrderBy(p => p).ToArray();


            var z = aas.Select(aa => x.Select(loc =>
            {
                if (aminoAcidCounts[aa].TryGetValue(loc, out var count))
                    return (count / yTotals[aa] * 100.0).Round(1);
                return 0.0;
            }).ToArray()).ToArray();

            int places = yTotals.Max(p => p.Value.ToString(CultureInfo.InvariantCulture).Length);
            var y = aas.Select(aa =>
            {
                var yTotal = ((int)yTotals[aa]).ToString().Length;
                var spacesToAdd = places - yTotal + 1;
                return $"{aa}:{new string(' ', spacesToAdd)}{yTotals[aa].Round(0)}";
            }).ToArray();

            var annotationText = z.Select(p => p.Select(m => $"{m}%").ToArray()).ToArray();
            var distinctMap =
                Chart.Heatmap<double, double, string, string>(z, X: x, Y: y, ShowLegend:true, Name: "Percent By Location")
                    .WithXAxisStyle( Title.init("N-Term -> C-Term"))
                    .WithYAxisStyle( Title.init("Amino Acid: Total in Database"))
                    .WithZAxisStyle( Title.init("Percent By Location"))
                    .WithTitle($"{explorer.Species} Amino Acid Frequency By Location");
            string outType = higherResolution ? "_HighRes" : "";
            var outName = $"{explorer.Species}_AminoAcidFrequencyByLocation{outType}";
            explorer.SaveToFigureDirectory(distinctMap, outName, 1000, 1000);
        }

        public static void CreatePlots(this List<RadicalFragmentationExplorer> explorers)
        {
            var typeCollection = explorers.Select(p => p.AnalysisType).Distinct().ToArray();
            if (typeCollection.Count() > 1)
                throw new ArgumentException("All explorers must be of the same type");
            var type = typeCollection.First();

            foreach (var speciesGroup in explorers.GroupBy(p => p.Species)
                         .ToDictionary(p => p.Key, p => p.ToList()))
            {
                string outName;
                var hist = speciesGroup.Value.SelectMany(p => p.FragmentHistogramFile).ToList();
                var frag = speciesGroup.Value.SelectMany(p => p.MinFragmentNeededFile.Results).ToList();

                var level1 = hist.GetProteinByUniqueFragmentsLine(1, speciesGroup.Key);
                outName = $"{type}_UniqueFragmentMasses_{speciesGroup.Key}_ProteoformLevel";
                speciesGroup.Value.First().SaveToFigureDirectory(level1, outName, 1000, 600);

                var level2 = hist.GetProteinByUniqueFragmentsLine(2, speciesGroup.Key);
                outName = $"{type}_UniqueFragmentMasses_{speciesGroup.Key}_ProteinLevel";
                speciesGroup.Value.First().SaveToFigureDirectory(level2, outName, 1000, 600);

                var hist1 = frag.GetFragmentsNeededHistogram(1, speciesGroup.Key);
                outName = $"{type}_FragmentsNeeded_{speciesGroup.Key}_ProteoformLevel";
                speciesGroup.Value.First().SaveToFigureDirectory(hist1, outName, 1000, 600);

                var hist2 = frag.GetFragmentsNeededHistogram(2, speciesGroup.Key);
                outName = $"{type}_FragmentsNeeded_{speciesGroup.Key}_ProteinLevel";
                speciesGroup.Value.First().SaveToFigureDirectory(hist2, outName, 1000, 600);

                var cumHist1 = frag.GetCumulativeFragmentsNeededChart(1, speciesGroup.Key);
                outName = $"{type}_CumulativeFragmentsNeeded_{speciesGroup.Key}_ProteoformLevel";
                speciesGroup.Value.First().SaveToFigureDirectory(cumHist1, outName, 1000, 600);

                var cumHist2 = frag.GetCumulativeFragmentsNeededChart(2, speciesGroup.Key);
                outName = $"{type}_CumulativeFragmentsNeeded_{speciesGroup.Key}_ProteinLevel";
                speciesGroup.Value.First().SaveToFigureDirectory(cumHist2, outName, 1000, 600);
            }
        }

        public static GenericChart.GenericChart GetProteinByUniqueFragmentsLine(this List<FragmentHistogramRecord> records,
            int ambiguityLevel = 1, string species = "")
        {
            List<GenericChart.GenericChart> toCombine = new();

            foreach (var modGroup in records
                         .Where(p => p.AmbiguityLevel == ambiguityLevel)
                         .GroupBy(p => p.NumberOfMods))
            {
                var x = modGroup.Select(p => p.FragmentCount);
                var y = modGroup.Select(p => p.ProteinCount);
                var chart = Chart.Spline<int, int, string>(x, y, true, 2, $"{modGroup.Key} mods");
                toCombine.Add(chart);
            }

            string typeText = ambiguityLevel == 1
                ? "Proteoform"
                : "Protein";
            var combined = Chart.Combine(toCombine)
                .WithTitle($"{species} Fragments per {typeText} (Ambiguity Level {ambiguityLevel})")
                .WithXAxisStyle(Title.init("Fragment Count"))
                .WithLayout(DefaultLayoutWithLegend)
                .WithYAxisStyle(Title.init($"Count of {typeText}s"));
       
            return combined;
        }

        public static GenericChart.GenericChart GetFragmentsNeededHistogram(this List<FragmentsToDistinguishRecord> records,
            int ambiguityLevel = 1, string species = "")
        {
            List<GenericChart.GenericChart> toCombine = new();
            foreach (var modGroup in records
                         .Where(p => p.AmbiguityLevel == ambiguityLevel)
                         .GroupBy(p => p.NumberOfMods))
            {


                var temp = modGroup.GroupBy(p => p.FragmentCountNeededToDifferentiate)
                    .OrderBy(p => p.Key)
                    .Select(p => (p.Key, p.Count())).ToArray();

                var xInt = temp.Select(p => p.Key).ToArray();
                var x = xInt.Select(p => p.ToString()).ToArray();
                var y = temp.Select(p => p.Item2).ToArray();
                for (int i = 0; i < x.Length; i++)
                {
                    x[i] = x[i] switch
                    {
                        "-1" => "No ID",
                        "0" => "Precursor Only",
                        _ => x[i]
                    };
                }
           

                var chart = Chart.Column<int, string, string>(y, x,
                    Name: $"{modGroup.Key} mods");
                toCombine.Add(chart);
            }

            string typeText = ambiguityLevel == 1
                ? "Proteoform"
                : "Protein";
            var combined = Chart.Combine(toCombine)
                .WithTitle(
                    $"{species} Fragments Needed to Distinguish from other {typeText}s (Ambiguity Level {ambiguityLevel})")
                .WithXAxisStyle(Title.init("Fragments Needed"))
                .WithYAxisStyle(Title.init($"Log(Count of {typeText}s)"))
                .WithLayout(DefaultLayoutWithLegend)
                .WithYAxis(LinearAxis.init<int, int, int, int, int, int>(AxisType: StyleParam.AxisType.Log));
            return combined;
        }

        public static GenericChart.GenericChart GetCumulativeFragmentsNeededChart(
            this List<FragmentsToDistinguishRecord> records,
            int ambiguityLevel = 1, string species = "")
        {
            List<GenericChart.GenericChart> toCombine = new();
            foreach (var modGroup in records
                         .Where(p => p.AmbiguityLevel == ambiguityLevel)
                         .GroupBy(p => p.NumberOfMods))
            {
                double total = modGroup.Count();
                var toSubtract = modGroup.Count(p => p.FragmentCountNeededToDifferentiate == -1);
                var xInt = modGroup.Select(p => p.FragmentCountNeededToDifferentiate)
                    .Distinct().OrderBy(p => p)
                    .ToArray();
                var y = xInt.Select(p => (modGroup.Count(m => m.FragmentCountNeededToDifferentiate <= p) - toSubtract) / total * 100).ToArray();
                var x = xInt.Select(p => p.ToString()).ToArray();
                x[0] = "No ID";
                x[1] = "Precursor Only";

                var chart = Chart.Spline<string, double, string>(x, y, true, 0.2,
                    Name: $"{modGroup.Key} mods", MultiText: y.Select(p => $"{p.Round(2)}%").ToArray());
                toCombine.Add(chart);
            }

            string typeText = ambiguityLevel == 1
                ? "Proteoform"
                : "Protein";
            var combined = Chart.Combine(toCombine)
                .WithTitle(
                    $"{species}: {typeText}s Identified by Number of Fragments")
                .WithXAxisStyle(Title.init($"Fragment Ions Required"))
                .WithXAxis(LinearAxis.init<int, int, int, int, int, int>(Tick0: 0, DTick: 1))
                .WithLayout(DefaultLayoutWithLegend)
                .WithYAxisStyle(Title.init($"Percent of {typeText}s Identified"));
            return combined;
        }
    }

    internal static class PlottingExtensions
    {
        public static void SaveToFigureDirectory(this RadicalFragmentationExplorer explorer,
            GenericChart.GenericChart chart, string outName, int? width = null, int? height = null)
        {
            if (!Directory.Exists(explorer.FigureDirectory))
                Directory.CreateDirectory(explorer.FigureDirectory);

            var outpath = Path.Combine(explorer.FigureDirectory, outName);
            chart.SavePNG(outpath, null, width, height);
        }
    }
}
