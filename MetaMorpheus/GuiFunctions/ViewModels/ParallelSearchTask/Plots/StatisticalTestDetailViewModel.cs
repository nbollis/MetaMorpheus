using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearch.Statistics;

namespace GuiFunctions.ViewModels.ParallelSearchTask.Plots;

/// <summary>
/// ViewModel for detailed statistical test visualization
/// Shows distributions of raw values, p-values, and q-values for a specific test
/// </summary>
public class StatisticalTestDetailViewModel : StatisticalPlotViewModelBase
{
    private static int MinBinCount = 5;
    private static int MaxBinCount = 200;

    private List<StatisticalTestResult> _allStatisticalResults = new();
    private TestSummary _testSummary;
    private int _binCount = 20;
    private bool _useLogScale = false;

    public override PlotType PlotType => PlotType.StatisticalTestDetail;

    public PlotModel QQPlotModel => BuildQQPlot();

    public PlotModel VolcanoPlotModel => BuildVolcanoPlot();

    public StatisticalTestDetailViewModel()
    {
        PlotTitle = "Statistical Test Detail";
    }

    #region Properties

    /// <summary>
    /// Number of bins for histogram display
    /// </summary>
    public int BinCount
    {
        get => _binCount;
        set
        {
            if (_binCount == value) return;
            _binCount = value < MinBinCount ? MinBinCount : value > MaxBinCount ? MaxBinCount : value; // Clamp between MinBinCount and MaxBinCount
            MarkDirty();
            OnPropertyChanged(nameof(BinCount));
            OnPropertyChanged(nameof(RawValuePlotModel));
            OnPropertyChanged(nameof(PValuePlotModel));
            OnPropertyChanged(nameof(QValuePlotModel));
            OnPropertyChanged(nameof(QQPlotModel));
            OnPropertyChanged(nameof(VolcanoPlotModel));
        }
    }

    /// <summary>
    /// Whether to use logarithmic scale for Y-axis
    /// </summary>
    public bool UseLogScale
    {
        get => _useLogScale;
        set
        {
            if (_useLogScale == value) return;
            _useLogScale = value;
            MarkDirty();
            OnPropertyChanged(nameof(UseLogScale));
            OnPropertyChanged(nameof(RawValuePlotModel));
            OnPropertyChanged(nameof(PValuePlotModel));
            OnPropertyChanged(nameof(QValuePlotModel));
            OnPropertyChanged(nameof(QQPlotModel));
            OnPropertyChanged(nameof(VolcanoPlotModel));
        }
    }

    /// <summary>
    /// Currently selected test name for detailed view
    /// </summary>
    public override string SelectedTest
    {
        get => _selectedTest;
        set
        {
            if (_selectedTest == value) return;
            _selectedTest = value;

            UpdateTestSummary();
            UpdateSelectedTestForResults();
            UpdateTopNResults();
            MarkDirty();
            OnPropertyChanged(nameof(SelectedTest));
            OnPropertyChanged(nameof(TestSummary));
            OnPropertyChanged(nameof(PlotModel));
            OnPropertyChanged(nameof(QQPlotModel));
            OnPropertyChanged(nameof(VolcanoPlotModel));
        }
    }

    /// <summary>
    /// All statistical results from all databases
    /// </summary>
    public List<StatisticalTestResult> AllStatisticalResults
    {
        get => _allStatisticalResults;
        set
        {
            _allStatisticalResults = value ?? new();
            UpdateTestSummary();
            MarkDirty();
            OnPropertyChanged(nameof(AllStatisticalResults));
            OnPropertyChanged(nameof(TestSummary));
            OnPropertyChanged(nameof(PlotModel));
            OnPropertyChanged(nameof(QQPlotModel));
            OnPropertyChanged(nameof(VolcanoPlotModel));
        }
    }

    /// <summary>
    /// Summary statistics for the selected test
    /// </summary>
    public TestSummary TestSummary
    {
        get => _testSummary;
        private set
        {
            _testSummary = value;
            OnPropertyChanged(nameof(TestSummary));
        }
    }

    #endregion

    #region Plot Generation

    protected override PlotModel GeneratePlotModel()
    {
        // Redraw each of the plots 
        OnPropertyChanged(nameof(RawValuePlotModel));
        OnPropertyChanged(nameof(PValuePlotModel));
        OnPropertyChanged(nameof(QValuePlotModel));

        // return something. This doesn't matter as the UI is binding to the individual plots below. 
        return RawValuePlotModel;
    }

    protected override IEnumerable<string> GetExportData()
    {
        yield return "DatabaseName,TestName,MetricName,RawValue,PValue,QValue,IsSignificantByP,IsSignificantByQ";

        var testResults = AllStatisticalResults
            .Where(r => r.MatchesSelection(SelectedTest))
            .ToList();

        foreach (var result in testResults)
        {
            var rawValue = ExtractRawValue(result);
            var isSignificantByP = result.PValue <= Alpha;
            var isSignificantByQ = result.QValue <= Alpha;

            yield return $"{result.DatabaseName},{result.TestName},{result.MetricName}," +
                        $"{rawValue},{result.PValue:E4},{result.QValue:E4}," +
                        $"{isSignificantByP},{isSignificantByQ}";
        }
    }

    #endregion

    #region Three Individual Plot Models for UI

    /// <summary>
    /// Plot model for raw value distribution
    /// </summary>
    public PlotModel RawValuePlotModel
    {
        get
        {
            var model = new PlotModel
            {
                Title = "Raw Value Distribution",
                DefaultFontSize = 11,
                IsLegendVisible = false,
                Padding = new OxyThickness(10)
            };

            var testResults = AllStatisticalResults
                .Where(r => r.MatchesSelection(SelectedTest))
                .ToList();

            if (!testResults.Any())
                return model;

            var rawValues = ExtractRawValues(testResults);
            if (!rawValues.Any())
            {
                model.Subtitle = "No raw values available";
                return model;
            }

            var histogram = CreateHistogram(rawValues, "Raw Values", uiBinCount: BinCount);

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Raw Value",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray
            });

            model.Axes.Add(CreateYAxis("Count"));

            AddHistogramSeries(model, histogram, OxyColors.Green);

            return model;
        }
    }

    /// <summary>
    /// Plot model for p-value distribution
    /// </summary>
    public PlotModel PValuePlotModel
    {
        get
        {
            var model = new PlotModel
            {
                Title = "P-Value Distribution",
                DefaultFontSize = 11,
                IsLegendVisible = false,
                Padding = new OxyThickness(10)
            };

            var testResults = AllStatisticalResults
                .Where(r => r.MatchesSelection(SelectedTest))
                .ToList();

            if (!testResults.Any())
                return model;

            var pValues = testResults.Select(r => r.PValue)
                .Where(p => !double.IsNaN(p) && p > 0 && p <= 1.0)
                .ToList();

            if (!pValues.Any())
            {
                model.Subtitle = "No valid p-values";
                return model;
            }

            var histogram = CreateHistogram(pValues, "P-Values", 0, 1.0, BinCount);

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "P-Value",
                Minimum = 0,
                Maximum = 1.0,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray
            });

            model.Axes.Add(CreateYAxis("Count"));

            AddHistogramSeries(model, histogram, OxyColors.SteelBlue);
            AddSignificanceThreshold(model);

            return model;
        }
    }

    /// <summary>
    /// Plot model for q-value distribution
    /// </summary>
    public PlotModel QValuePlotModel
    {
        get
        {
            var model = new PlotModel
            {
                Title = "Q-Value Distribution",
                DefaultFontSize = 11,
                IsLegendVisible = false,
                Padding = new OxyThickness(10)
            };

            var testResults = AllStatisticalResults
                .Where(r => r.MatchesSelection(SelectedTest))
                .ToList();

            if (!testResults.Any())
                return model;

            var qValues = testResults.Select(r => r.QValue)
                .Where(q => !double.IsNaN(q) && q > 0 && q <= 1.0)
                .ToList();

            if (!qValues.Any())
            {
                model.Subtitle = "No valid q-values";
                return model;
            }

            var histogram = CreateHistogram(qValues, "Q-Values", 0, 1.0, BinCount);

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Q-Value",
                Minimum = 0,
                Maximum = 1.0,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray
            });

            model.Axes.Add(CreateYAxis("Count"));

            AddHistogramSeries(model, histogram, OxyColors.Orange);
            AddSignificanceThreshold(model);

            return model;
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Build Q-Q plot for calibration assessment
    /// Plots observed -log10(p-values) against expected uniform quantiles
    /// A well-calibrated test follows the diagonal red line
    /// </summary>
    private PlotModel BuildQQPlot()
    {
        var model = new PlotModel
        {
            Title = "Q-Q Plot (Calibration)",
            DefaultFontSize = 11,
            IsLegendVisible = false,
            Padding = new OxyThickness(10)
        };

        var testResults = AllStatisticalResults
            .Where(r => r.MatchesSelection(SelectedTest))
            .ToList();

        if (!testResults.Any())
            return model;

        var pValues = testResults
            .Select(r => r.PValue)
            .Where(p => !double.IsNaN(p) && p > 0 && p <= 1.0)
            .OrderBy(p => p)
            .ToList();

        if (pValues.Count < 2)
        {
            model.Subtitle = "Insufficient data for Q-Q plot";
            return model;
        }

        int n = pValues.Count;
        var expected = Enumerable.Range(1, n)
            .Select(i => -Math.Log10((double)i / (n + 1)))
            .ToList();
        var observed = pValues.Select(p => -Math.Log10(p)).ToList();

        double maxVal = Math.Max(expected.Max(), observed.Max()) * 1.05;

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = "Expected -log10(p)",
            Minimum = 0,
            Maximum = maxVal,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColors.LightGray
        });

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "Observed -log10(p)",
            Minimum = 0,
            Maximum = maxVal,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColors.LightGray
        });

        var diagonal = new LineSeries
        {
            Color = OxyColors.Red,
            StrokeThickness = 1,
            LineStyle = LineStyle.Dash
        };
        diagonal.Points.Add(new DataPoint(0, 0));
        diagonal.Points.Add(new DataPoint(maxVal, maxVal));
        model.Series.Add(diagonal);

        var scatter = new ScatterSeries
        {
            MarkerType = MarkerType.Circle,
            MarkerSize = 3,
            MarkerFill = OxyColors.SteelBlue,
            MarkerStroke = OxyColors.DarkBlue,
            MarkerStrokeThickness = 0.5
        };

        for (int i = 0; i < n; i++)
            scatter.Points.Add(new ScatterPoint(expected[i], observed[i]));

        model.Series.Add(scatter);

        return model;
    }

    /// <summary>
    /// Build volcano plot: effect size vs -log10(p-value)
    /// Each point is a database. Red = significant, gray = not.
    /// </summary>
    private PlotModel BuildVolcanoPlot()
    {
        var model = new PlotModel
        {
            Title = "Volcano Plot (Effect vs Significance)",
            DefaultFontSize = 11,
            IsLegendVisible = true,
            Padding = new OxyThickness(10)
        };

        var testResults = AllStatisticalResults
            .Where(r => r.MatchesSelection(SelectedTest))
            .ToList();

        if (!testResults.Any())
            return model;

        var nonSignificant = new ScatterSeries
        {
            Title = "Not significant",
            MarkerType = MarkerType.Circle,
            MarkerSize = 4,
            MarkerFill = OxyColors.Gray,
            MarkerStroke = OxyColors.DarkGray,
            MarkerStrokeThickness = 0.5
        };

        var significant = new ScatterSeries
        {
            Title = $"Significant (p ≤ {Alpha})",
            MarkerType = MarkerType.Circle,
            MarkerSize = 5,
            MarkerFill = OxyColors.Red,
            MarkerStroke = OxyColors.DarkRed,
            MarkerStrokeThickness = 0.5
        };

        foreach (var result in testResults)
        {
            if (!result.IsDefined)
                continue;

            double effectSize = result.EffectSize ?? 0;
            double logP = result.PValue > 0 ? -Math.Log10(result.PValue) : 0;
            bool isSig = result.PValue <= Alpha;

            var pt = new ScatterPoint(effectSize, logP);

            if (isSig)
                significant.Points.Add(pt);
            else
                nonSignificant.Points.Add(pt);
        }

        model.Series.Add(nonSignificant);
        model.Series.Add(significant);

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = "Effect Size",
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColors.LightGray
        });

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "-log10(p-value)",
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColors.LightGray
        });

        if (testResults.Any(r => r.IsDefined && r.PValue > 0))
        {
            double sigThreshold = -Math.Log10(Alpha);
            model.Annotations.Add(new LineAnnotation
            {
                Type = LineAnnotationType.Horizontal,
                Y = sigThreshold,
                Color = OxyColors.Red,
                LineStyle = LineStyle.Dash,
                StrokeThickness = 1,
                Text = $"p = {Alpha}",
                TextColor = OxyColors.Red,
                TextHorizontalAlignment = HorizontalAlignment.Right
            });
        }

        return model;
    }

    /// <summary>
    /// Update test summary when selected test or data changes
    /// </summary>
    private void UpdateTestSummary()
    {
        if (string.IsNullOrEmpty(SelectedTest) || !AllStatisticalResults.Any())
        {
            TestSummary = null;
            return;
        }

        var testResults = AllStatisticalResults
            .Where(r => r.MatchesSelection(SelectedTest))
            .ToList();

        if (!testResults.Any())
        {
            TestSummary = null;
            return;
        }

        var metricName = testResults.First().MetricName;
        var validDatabases = testResults.Count(r => r.IsDefined);
        var significantByP = testResults.Count(r => r.IsDefined && r.PValue <= Alpha);
        var significantByQ = testResults.Count(r => r.IsDefined && r.QValue <= Alpha);

        TestSummary = new TestSummary
        {
            TestName = SelectedTest,
            MetricName = metricName,
            EvidenceFamily = testResults.Select(r => r.EvidenceFamily).Distinct().Count() == 1 ? testResults.First().EvidenceFamily : null,
            ValidDatabases = validDatabases,
            UndefinedDatabases = testResults.Count(r => !r.IsDefined),
            SignificantByP = significantByP,
            SignificantByQ = significantByQ
        };
    }

    /// <summary>
    /// Extract raw values from statistical results
    /// </summary>
    private List<double> ExtractRawValues(List<StatisticalTestResult> results)
    {
        var values = new List<double>();
        foreach (var result in results)
        {
            var rawValue = ExtractRawValue(result);
            if (!double.IsNaN(rawValue))
                values.Add(rawValue);
        }
        return values;
    }

    /// <summary>
    /// Extract raw value from a single result
    /// </summary>
    private double ExtractRawValue(StatisticalTestResult result)
    {
        // Try to get raw value from AdditionalMetrics
        return result.TestStatistic ?? double.NaN;
    }

    /// <summary>
    /// Create Y-axis with support for linear or logarithmic scale
    /// </summary>
    private Axis CreateYAxis(string title)
    {
        if (UseLogScale)
        {
            return new LogarithmicAxis
            {
                Position = AxisPosition.Left,
                Title = $"{title} (log scale)",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray,
                MinorGridlineStyle = LineStyle.Dot,
                MinorGridlineColor = OxyColors.LightGray,
                Minimum = 0.1 // Avoid log(0)
            };
        }
        else
        {
            return new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = title,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray,
                Minimum = 0
            };
        }
    }

    /// <summary>
    /// Create histogram bins from data with custom bin count
    /// </summary>
    private List<HistogramBin> CreateHistogram(List<double> values, string label, double? minValue = null, double? maxValue = null, int? uiBinCount = null)
    {
        if (!values.Any())
            return new List<HistogramBin>();

        // Calculate using Sturges' rule if it is less than what is in the ui. 
        int sturgesBinCount = (int)Math.Ceiling(Math.Log2(values.Count) + 1);

        int numBins = uiBinCount.HasValue
            ? /*Math.Min(sturgesBinCount, */uiBinCount.Value
            : Math.Clamp(sturgesBinCount, MinBinCount, MaxBinCount);

        int distinctValues = values.Distinct().Count();

        double min = minValue ?? values.Min();
        double max = maxValue ?? values.Max();
        double range = max - min;

        if (range < 1e-10)
            return new List<HistogramBin>();

        double binWidth = range / numBins;

        var bins = new List<HistogramBin>();
        for (int i = 0; i < numBins; i++)
        {
            double binStart = min + i * binWidth;
            double binEnd = binStart + binWidth;
            bins.Add(new HistogramBin
            {
                Start = binStart,
                End = binEnd,
                Count = 0
            });
        }

        // Populate bins
        foreach (var value in values)
        {
            int binIndex = (int)((value - min) / binWidth);
            if (binIndex >= numBins)
                binIndex = numBins - 1;
            if (binIndex < 0)
                binIndex = 0;
            bins[binIndex].Count++;
        }

        return bins;
    }

    /// <summary>
    /// Add histogram series to plot model
    /// </summary>
    private void AddHistogramSeries(PlotModel model, List<HistogramBin> histogram, OxyColor color)
    {
        var series = new RectangleBarSeries
        {
            FillColor = color,
            StrokeThickness = 1,
            StrokeColor = OxyColors.Black,
        };

        foreach (var bin in histogram)
        {
            series.Items.Add(new RectangleBarItem
            {
                X0 = bin.Start,
                X1 = bin.End,
                Y0 = 0,
                Y1 = bin.Count
            });
        }

        model.Series.Add(series);
    }

    /// <summary>
    /// Add significance threshold line annotation
    /// </summary>
    private void AddSignificanceThreshold(PlotModel model)
    {
        model.Annotations.Add(new LineAnnotation
        {
            Type = LineAnnotationType.Vertical,
            X = Alpha,
            Color = OxyColors.Red,
            LineStyle = LineStyle.Dash,
            StrokeThickness = 2,
            Text = $"α = {Alpha}",
            TextColor = OxyColors.Red,
            TextHorizontalAlignment = HorizontalAlignment.Right
        });
    }

    #endregion
}

/// <summary>
/// Histogram bin for distribution visualization
/// </summary>
public class HistogramBin
{
    public double Start { get; set; }
    public double End { get; set; }
    public int Count { get; set; }
}
