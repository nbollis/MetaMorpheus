using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.FSharp.Core;
using Plotly.NET;
using Plotly.NET.CSharp;
using Plotly.NET.LayoutObjects;
using Chart = Plotly.NET.CSharp.Chart;
using GenericChartExtensions = Plotly.NET.CSharp.GenericChartExtensions;

namespace Test
{
    internal class AveragingMassAccuracyTestAnalyzer
    {
        public List<FileMassAccuracyResults> AllFileResults { get; init; }
        public FileMassAccuracyResults OriginalResults { get; init; }

        public AveragingMassAccuracyTestAnalyzer(List<FileMassAccuracyResults> allResults, FileMassAccuracyResults originalResults)
        {
            AllFileResults = allResults;
            OriginalResults = originalResults;
        }

        internal GenericChart.GenericChart CompoundHeatMapSigmaValues(List<ResultType> resultTypes,
            string title = null)
        {
            List<GenericChart.GenericChart> charts = new();
            foreach (var resultType in resultTypes)
            {
                title ??= $"{resultType} by Sigma Values";

                var groups = AllFileResults.GroupBy(p => p.Parameters.OutlierRejectionType)
                    .Where(p => p.Key.ToString().Contains("Sigma")).ToList();
                foreach (var rejectionTypeGroup in groups)
                {
                    var minSigmaValues = rejectionTypeGroup.Select(p => p.Parameters.MinSigmaValue)
                        .Distinct().OrderBy(p => p).ToArray();
                    var maxSigmaValues = rejectionTypeGroup.Select(p => p.Parameters.MaxSigmaValue)
                        .Distinct().OrderBy(p => p).ToArray();

                    double[][] zData = new double[minSigmaValues.Length][];
                    for (int i = 0; i < minSigmaValues.Length; i++)
                    {
                        zData[i] = new double[maxSigmaValues.Length];
                        for (int j = 0; j < maxSigmaValues.Length; j++)
                        {
                            var temp2 = rejectionTypeGroup
                                .Where(p => Math.Abs(p.Parameters.MinSigmaValue - minSigmaValues[i]) < 0.0001 &&
                                            Math.Abs(p.Parameters.MaxSigmaValue - maxSigmaValues[j]) < 0.0001);

                            zData[i][j] = temp2.Select(p => p.GetAverageValue(resultType)).Average();
                        }
                    }

                    var heatmap = Chart.Heatmap<double, double, double, string>
                        (
                            zData,
                            X: new Optional<IEnumerable<double>>(minSigmaValues, true),
                            Y: new Optional<IEnumerable<double>>(maxSigmaValues, true),
                            Text: title,
                            ColorScale: new Optional<StyleParam.Colorscale>(StyleParam.Colorscale.Cividis, true)
                        )
                        .WithYAxisStyle<double, double, string>(TitleText: new Optional<string>($"Min Sigma - {resultType}", true))
                        .WithXAxisStyle<double, double, string>(TitleText: new Optional<string>($"Max Sigma - {rejectionTypeGroup.Key}", true))
                        .WithTitle(rejectionTypeGroup.Key.ToString());
                    charts.Add(heatmap);
                }
            }
            var grid = Chart.Grid(charts, 6, 3)
                .WithSize(1200, 2400);
            
            return grid;
        }

        internal GenericChart.GenericChart HeatMapSigmaValues(ResultType resultType,
            string title = null)
        {
            title ??= $"{resultType} by Sigma Values";

            var groups = AllFileResults.GroupBy(p => p.Parameters.OutlierRejectionType)
                .Where(p => p.Key.ToString().Contains("Sigma")).ToList();

            List<GenericChart.GenericChart> charts = new();
            foreach (var rejectionTypeGroup in groups)
            {
                

                var minSigmaValues = rejectionTypeGroup.Select(p => p.Parameters.MinSigmaValue)
                    .Distinct().OrderBy(p => p).ToArray();
                var maxSigmaValues = rejectionTypeGroup.Select(p => p.Parameters.MaxSigmaValue)
                    .Distinct().OrderBy(p => p).ToArray();

                double[][] zData = new double[minSigmaValues.Length][];
                for (int i = 0; i < minSigmaValues.Length; i++)
                {
                    zData[i] = new double[maxSigmaValues.Length];
                    for (int j = 0; j < maxSigmaValues.Length; j++)
                    {
                        var temp2 = rejectionTypeGroup
                            .Where(p => Math.Abs(p.Parameters.MinSigmaValue - minSigmaValues[i]) < 0.0001 &&
                                        Math.Abs(p.Parameters.MaxSigmaValue - maxSigmaValues[j]) < 0.0001);

                        zData[i][j] = temp2.Select(p => p.GetAverageValue(resultType)).Average();
                    }
                }

                var heatmap = Chart.Heatmap<double, double, double, string>
                    (
                        zData,
                        X: new Optional<IEnumerable<double>>(minSigmaValues, true),
                        Y: new Optional<IEnumerable<double>>(maxSigmaValues, true),
                        Text: title,
                        ColorScale: new Optional<StyleParam.Colorscale>(StyleParam.Colorscale.Rainbow, true)
                    )
                    .WithYAxisStyle<double, double, string>(TitleText: new Optional<string>($"Min Sigma - {resultType}", true))
                    .WithXAxisStyle<double, double, string>(TitleText: new Optional<string>($"Max Sigma - {rejectionTypeGroup.Key}", true))
                    .WithTitle(rejectionTypeGroup.Key.ToString());
                charts.Add(heatmap);
            }

            var grid = Chart.Grid(charts, 1, 3,
                    Pattern: new Optional<StyleParam.LayoutGridPattern>(StyleParam.LayoutGridPattern.Coupled, true)
                )
                .WithTitle(title)
                .WithSize(1200, 400);
            //GenericChartExtensions.Show(grid);
            return grid;
        }

        internal GenericChart.GenericChart CompoundBoxAndWhisker(List<ResultType> resultTypes, GroupingType groupingType,
            string title = null, int topNResults = 0)
        {

            List<GenericChart.GenericChart> charts = new();

            for (var i = 0; i < resultTypes.Count; i++)
            {
                var resultType = resultTypes[i];
                var chart = BoxAndWhisker(resultType, groupingType,
                    null, (i + 1).ToString(), topNResults);
                charts.Add(chart);
            }

            var grid = Chart.Grid(charts, 3, 2)
                .WithSize(1400, 1200)
                .WithTitle(title);
            

            return grid;
        }


        internal GenericChart.GenericChart BoxAndWhisker(ResultType resultType, GroupingType groupingType,
            string title = null, string yRef = null, int topNResults = 0, bool showOriginal = true)

        {
            var y = new List<double>();
            var x = new List<string>();
            title ??= $"{resultType} by {groupingType}";

            // setting x and y values
            switch (groupingType)
            {
                case GroupingType.BinSize:
                    foreach (var group in AllFileResults
                                 .GroupBy(p => p.Parameters.BinSize))
                    {
                        var toAdd = group.GetAverageValues(resultType)
                            .OrderByDescending(p => p).ToList();
                        if (resultType == ResultType.PpmErrorFromTheoretical)
                            toAdd = toAdd.OrderBy(p => p).ToList();

                        var trimmed = (topNResults != 0 ? toAdd.Take(topNResults) : toAdd).ToList();

                        y.AddRange(trimmed);
                        x.AddRange(Enumerable.Repeat(group.Key.ToString(), trimmed.Count()));
                    }

                    break;

                case GroupingType.OutlierRejection:
                    foreach (var group in AllFileResults
                                 .GroupBy(p => p.Parameters.OutlierRejectionType))
                    {
                        var toAdd = group.GetAverageValues(resultType)
                            .OrderByDescending(p => p).ToList();
                        if (resultType == ResultType.PpmErrorFromTheoretical)
                            toAdd = toAdd.OrderBy(p => p).ToList();

                        var trimmed = (topNResults != 0 ? toAdd.Take(topNResults) : toAdd).ToList();

                        y.AddRange(trimmed);
                        x.AddRange(Enumerable.Repeat(group.Key.ToString(), trimmed.Count()));
                    }

                    break;

                case GroupingType.Normalization:
                    foreach (var group in AllFileResults
                                 .GroupBy(p => p.Parameters.NormalizationType))
                    {
                        var toAdd = group.GetAverageValues(resultType)
                            .OrderByDescending(p => p).ToList();
                        if (resultType == ResultType.PpmErrorFromTheoretical)
                            toAdd = toAdd.OrderBy(p => p).ToList();

                        var trimmed = (topNResults != 0 ? toAdd.Take(topNResults) : toAdd).ToList();

                        y.AddRange(trimmed);
                        x.AddRange(Enumerable.Repeat(group.Key.ToString(), trimmed.Count()));
                    }
                    break;

                case GroupingType.Weighting:
                    foreach (var group in AllFileResults
                                 .GroupBy(p => p.Parameters.SpectralWeightingType))
                    {
                        var toAdd = group.GetAverageValues(resultType)
                            .OrderByDescending(p => p).ToList();
                        if (resultType == ResultType.PpmErrorFromTheoretical)
                            toAdd = toAdd.OrderBy(p => p).ToList();

                        var trimmed = (topNResults != 0 ? toAdd.Take(topNResults) : toAdd).ToList();

                        y.AddRange(trimmed);
                        x.AddRange(Enumerable.Repeat(group.Key.ToString(), trimmed.Count()));
                    }
                    break;

                case GroupingType.NumberOfScansAveraged:
                    foreach (var group in AllFileResults
                                 .GroupBy(p => p.Parameters.NumberOfScansToAverage).OrderBy(p => p.Key))
                    {
                        var toAdd = group.GetAverageValues(resultType)
                            .OrderByDescending(p => p).ToList();
                        if (resultType == ResultType.PpmErrorFromTheoretical)
                            toAdd = toAdd.OrderBy(p => p).ToList();

                        var trimmed = (topNResults != 0 ? toAdd.Take(topNResults) : toAdd).ToList();

                        y.AddRange(trimmed);
                        x.AddRange(Enumerable.Repeat(group.Key.ToString(), trimmed.Count()));
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(groupingType), groupingType, null);
            }

            // creating plot
            var chart = Chart.BoxPlot<string, double, string>
                    (x, y, title)
                .WithYAxisStyle<double, double, string>(TitleText: new Optional<string>(resultType.ToString(), true))
                .WithXAxisStyle<string, string, string>(TitleText: new Optional<string>(groupingType.ToString(), true))
                .WithTitle(title);

            if (groupingType != GroupingType.NumberOfScansAveraged)
            {
                var original = OriginalResults.GetAverageValue(resultType);
                var line = Shape.init<string, string, double, double>
                (
                    ShapeType: new FSharpOption<StyleParam.ShapeType>(StyleParam.ShapeType.Line),
                    Y0: original,
                    Y1: original,
                    X0: x.First(),
                    X1: x.Last(),
                    Xref: new FSharpOption<string>("x" + yRef),
                    Yref: new FSharpOption<string>("y" + yRef)
                );
                chart = chart.WithShape(line, true);
            }


            //return plot
            return chart;
        }

    }

    internal enum ResultType
    {
        MzFoundPerSpectrum,
        ChargeStateResolvablePerSpectrum,
        PpmErrorFromTheoretical,
        MedOverDev,
        PsmCount,
        IsotopicPeakCount,
    }

    internal enum GroupingType
    {
        BinSize,
        OutlierRejection,
        Normalization,
        Weighting,
        NumberOfScansAveraged
    }
}
