using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Chart = Plotly.NET.CSharp.Chart;
using Plotly.NET;
using Plotly.NET.ImageExport;
using Plotly.NET.LayoutObjects;

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
                .WithTitle($"{species} Unique Fragments per {typeText} (Ambiguity Level {ambiguityLevel})")
                .WithXAxisStyle(Title.init("Unique Fragments"))
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
                var chart = Chart.Histogram<int, int, string>(modGroup.Select(p => p.FragmentCountNeededToDifferentiate).ToArray(),
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
                var x = modGroup.Select(p => p.FragmentCountNeededToDifferentiate)
                    .Distinct().OrderBy(p => p).ToArray();
                var y = x.Select(p => modGroup.Count(m => m.FragmentCountNeededToDifferentiate <= p) / total * 100);
                var chart = Chart.Spline<int, double, string>(x, y, true, 0.2,
                    Name: $"{modGroup.Key} mods");
                toCombine.Add(chart);
            }

            string typeText = ambiguityLevel == 1
                ? "Proteoform"
                : "Protein";
            var combined = Chart.Combine(toCombine)
                .WithTitle(
                    $"{species} Cumulative Fragments Needed to Distinguish from other {typeText}s (Ambiguity Level {ambiguityLevel})")
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
