using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test.ChimeraPaper.ResultFiles;
using Plotly.NET;
using Plotly.NET.ImageExport;
using Chart = Plotly.NET.CSharp.Chart;
using System.IO;


namespace Test.ChimeraPaper
{
    public static class Plotting
    {
        #region Filters

        public static string[] AcceptableConditionsToPlotIndividualFileComparison =
        {
            "MsFragger", "MsFraggerDDA+", 
            "MetaMorpheusFraggerEquivalentChimeras_IndividualFiles", "MetaMorpheusFraggerEquivalentNoChimeras_IndividualFiles",
            "MetaMorpheusWithLibrary", "MetaMorpheusNoChimerasWithLibrary"
        };

        public static string[] AcceptableConditionsToPlotInternalMMComparison =
        {
            "MetaMorpheusWithLibrary", "MetaMorpheusNoChimerasWithLibrary"
        };

        public static string[] AcceptableConditionsToPlotBulkResultComparison =
        {
            "MsFragger", "MsFraggerDDA+", "MetaMorpheusWithLibrary", "MetaMorpheusNoChimerasWithLibrary"
        };

        public static Dictionary<string, Color> ConditionToColorDictionary = new Dictionary<string, Color>()
        {
            {"MetaMorpheusWithLibrary", Color.fromKeyword(ColorKeyword.Purple) },
            {"MetaMorpheusNoChimerasWithLibrary", Color.fromKeyword(ColorKeyword.Plum) },
            {"MsFragger", Color.fromKeyword(ColorKeyword.LightAkyBlue) },
            {"MsFraggerDDA+", Color.fromKeyword(ColorKeyword.RoyalBlue) },
            {"MetaMorpheusFraggerEquivalentChimeras_IndividualFiles", Color.fromKeyword(ColorKeyword.SpringGreen) },
            {"MetaMorpheusFraggerEquivalentNoChimeras_IndividualFiles", Color.fromKeyword(ColorKeyword.Green) },
        };

        #endregion

        #region Plotly Things

        public static Layout DefaultLayout => Layout.init<string>(PaperBGColor: Color.fromKeyword(ColorKeyword.White), PlotBGColor: Color.fromKeyword(ColorKeyword.White));

        #endregion

        #region Cell Line

        public static void PlotIndividualFileResults(this CellLineResults cellLine)
        {
            var individualFileResults = cellLine.Results.Select(p => p.IndividualFileComparisonFile)
                .Where(p => AcceptableConditionsToPlotIndividualFileComparison.Contains(p.Results.First().Condition))
                .ToList();
            var labels = individualFileResults.SelectMany(p => p.Results.Select(m => m.FileName))
                .Distinct()
                .ToList();

            var chart = Chart.Combine(individualFileResults.Select(p =>
                Chart2D.Chart.Column<int, string, string, int, int>(p.Results.Select(m => m.OnePercentPeptideCount), labels, null,
                    p.Results.First().Condition)
            ));
            chart.WithTitle("1% FDR Peptides")
                .WithXAxisStyle(Title.init("File"))
                .WithYAxisStyle(Title.init("Count"))
                .WithLayout(DefaultLayout)
                .WithSize(40 * labels.Count + 10 * individualFileResults.Count, 400);

            string outPath = Path.Combine(cellLine.GetFigureDirectory(), $"{cellLine.CellLine}_{FileIdentifiers.IndividualFileComparisonFigure}");
            chart.SavePNG(outPath);
            chart.Show();
        }

        #endregion

        #region Bulk Result

        public static void PlotInternalMMComparison(this AllResults allResults)
        {
            var results = allResults.CellLineResults.SelectMany(p => p.BulkResultCountComparisonFile.Results)
                .Where(p => AcceptableConditionsToPlotInternalMMComparison.Contains(p.Condition))
                .ToList();
            var labels = results.Select(p => p.DatasetName).Distinct().ToList();

            var noChimeras = results.Where(p => p.Condition.Contains("NoChimeras")).ToList();
            var withChimeras = results.Where(p => !p.Condition.Contains("NoChimeras")).ToList();

            var psmChart = Chart.Combine(new[]
            {
                Chart2D.Chart.Column<int, string, string, int, int>(noChimeras.Select(p => p.OnePercentPsmCount),
                    labels, null, "No Chimeras", MarkerColor: ConditionToColorDictionary[noChimeras.First().Condition]),
                Chart2D.Chart.Column<int, string, string, int, int>(withChimeras.Select(p => p.OnePercentPsmCount),
                    labels, null, "Chimeras", MarkerColor: ConditionToColorDictionary[withChimeras.First().Condition])
            });
            psmChart.WithTitle("MetaMorpheus 1% FDR Psms")
                .WithXAxisStyle(Title.init("Cell Line"))
                .WithYAxisStyle(Title.init("Count"))
                .WithLayout(DefaultLayout);
            string psmOutpath = Path.Combine(allResults.GetFigureDirectory(), "InternalMetaMorpheusComparison_Psms.png");
            psmChart.SavePNG(psmOutpath);

            var peptideChart = Chart.Combine(new[]
            {
                Chart2D.Chart.Column<int, string, string, int, int>(noChimeras.Select(p => p.OnePercentPeptideCount),
                    labels, null, "No Chimeras", MarkerColor: ConditionToColorDictionary[noChimeras.First().Condition]),
                Chart2D.Chart.Column<int, string, string, int, int>(withChimeras.Select(p => p.OnePercentPeptideCount),
                    labels, null, "Chimeras", MarkerColor: ConditionToColorDictionary[withChimeras.First().Condition])
            });
            peptideChart.WithTitle("MetaMorpheus 1% FDR Peptides")
                .WithXAxisStyle(Title.init("Cell Line"))
                .WithYAxisStyle(Title.init("Count"))
                .WithLayout(DefaultLayout);
            string peptideOutpath = Path.Combine(allResults.GetFigureDirectory(), "InternalMetaMorpheusComparison_Peptides.png");
            peptideChart.SavePNG(peptideOutpath);

            var proteinChart = Chart.Combine(new[]
            {
                Chart2D.Chart.Column<int, string, string, int, int>(noChimeras.Select(p => p.OnePercentProteinGroupCount),
                    labels, null, "No Chimeras", MarkerColor: ConditionToColorDictionary[noChimeras.First().Condition]),
                Chart2D.Chart.Column<int, string, string, int, int>(withChimeras.Select(p => p.OnePercentProteinGroupCount),
                    labels, null, "Chimeras", MarkerColor: ConditionToColorDictionary[withChimeras.First().Condition])
            });
            proteinChart.WithTitle("MetaMorpheus 1% FDR Proteins")
                .WithXAxisStyle(Title.init("Cell Line"))
                .WithYAxisStyle(Title.init("Count"))
                .WithLayout(DefaultLayout);
            string proteinOutpath = Path.Combine(allResults.GetFigureDirectory(), "InternalMetaMorpheusComparison_Proteins.png");
            proteinChart.SavePNG(proteinOutpath);
        }

        public static void PlotBulkResultComparison(this AllResults allResults)
        {
            var results = allResults.CellLineResults.SelectMany(p => p.BulkResultCountComparisonFile.Results)
                .Where(p => AcceptableConditionsToPlotBulkResultComparison.Contains(p.Condition))
                .ToList();
            var labels = results.Select(p => p.DatasetName).Distinct().ToList();

            var psmCharts = new List<GenericChart.GenericChart>();
            var peptideCharts = new List<GenericChart.GenericChart>();
            var proteinCharts = new List<GenericChart.GenericChart>();
            foreach (var condition in results.Select(p => p.Condition).Distinct())
            {
                var conditionSpecificResults = results.Where(p => p.Condition == condition).ToList();

                psmCharts.Add(Chart2D.Chart.Column<int, string, string, int, int>(
                    conditionSpecificResults.Select(p => p.OnePercentPsmCount), labels, null, condition,
                    MarkerColor: ConditionToColorDictionary[condition]));
                peptideCharts.Add(Chart2D.Chart.Column<int, string, string, int, int>(
                    conditionSpecificResults.Select(p => p.OnePercentPeptideCount), labels, null, condition,
                    MarkerColor: ConditionToColorDictionary[condition]));
                proteinCharts.Add(Chart2D.Chart.Column<int, string, string, int, int>(
                    conditionSpecificResults.Select(p => p.OnePercentProteinGroupCount), labels, null, condition,
                    MarkerColor: ConditionToColorDictionary[condition]));
            }

            var psmChart = Chart.Combine(psmCharts).WithTitle("1% FDR Psms")
                .WithXAxisStyle(Title.init("Cell Line"))
                .WithYAxisStyle(Title.init("Count"))

                .WithLayout(DefaultLayout);
            var peptideChart = Chart.Combine(peptideCharts).WithTitle("1% FDR Peptides")
                .WithXAxisStyle(Title.init("Cell Line"))
                .WithYAxisStyle(Title.init("Count"))
                .WithLayout(DefaultLayout);
            var proteinChart = Chart.Combine(proteinCharts).WithTitle("1% FDR Proteins")
                .WithXAxisStyle(Title.init("Cell Line"))
                .WithYAxisStyle(Title.init("Count"))
                .WithLayout(DefaultLayout);

            var psmPath = Path.Combine(allResults.GetFigureDirectory(), "BulkResultComparison_Psms.png");
            var peptidePath = Path.Combine(allResults.GetFigureDirectory(), "BulkResultComparison_Peptides.png");
            var proteinpath = Path.Combine(allResults.GetFigureDirectory(), "BulkResultComparison_Proteins.png");
            psmChart.SavePNG(psmPath);
            peptideChart.SavePNG(peptidePath);
            proteinChart.SavePNG(proteinpath);

            psmChart.Show();
            peptideChart.Show();
            proteinChart.Show();
        }

        #endregion


        public static string GetFigureDirectory(this AllResults allResults)
        {
            var directory = Path.Combine(allResults.DirectoryPath, "Figures");
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            return directory;
        }

        public static string GetFigureDirectory(this CellLineResults cellLine)
        {
            var directory = Path.Combine(Path.GetDirectoryName(cellLine.DirectoryPath)!, "Figures");
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            return directory;
        }
    }
}
