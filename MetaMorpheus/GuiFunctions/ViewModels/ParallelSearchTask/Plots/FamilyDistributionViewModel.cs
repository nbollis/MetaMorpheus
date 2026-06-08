using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TaskLayer.ParallelSearch.Statistics;

namespace GuiFunctions.ViewModels.ParallelSearchTask.Plots;

public sealed class FamilyDistributionViewModel : BaseViewModel
{
    private static readonly int MinBinCount = 5;
    private static readonly int MaxBinCount = 200;

    private List<StatisticalTestResult> _allResults = new();
    private double _alpha = 0.01;
    private int _binCount = 20;
    private bool _useLogScale;
    private string _selectedTestKey = string.Empty;
    private FamilySelectionSnapshot? _snapshot;

    public string PlotTitle => "Family Distribution";

    public bool HasTestRows => TestRows.Count > 0;
    public ObservableCollection<TestRow> TestRows { get; } = new();
    public string SelectedFamilyName { get; private set; } = string.Empty;

    public List<StatisticalTestResult> AllResults
    {
        get => _allResults;
        set
        {
            _allResults = value ?? new();
            Refresh();
            OnPropertyChanged(nameof(AllResults));
        }
    }

    public FamilySelectionSnapshot? Snapshot
    {
        get => _snapshot;
        set
        {
            _snapshot = value;
            Refresh();
            OnPropertyChanged(nameof(Snapshot));
        }
    }

    public double Alpha
    {
        get => _alpha;
        set
        {
            if (Math.Abs(_alpha - value) < 0.00001) return;
            _alpha = value;
            Refresh();
            OnPropertyChanged(nameof(Alpha));
        }
    }

    public int BinCount
    {
        get => _binCount;
        set
        {
            _binCount = Math.Clamp(value, MinBinCount, MaxBinCount);
            Refresh();
            OnPropertyChanged(nameof(BinCount));
        }
    }

    public bool UseLogScale
    {
        get => _useLogScale;
        set
        {
            _useLogScale = value;
            Refresh();
            OnPropertyChanged(nameof(UseLogScale));
        }
    }

    public string SelectedTestKey
    {
        get => _selectedTestKey;
        set
        {
            if (_selectedTestKey == value) return;
            _selectedTestKey = value ?? string.Empty;
            Refresh();
            OnPropertyChanged(nameof(SelectedTestKey));
        }
    }

    public void Refresh()
    {
        TestRows.Clear();
        SelectedFamilyName = string.Empty;

        if (Snapshot != null && Snapshot.Family.HasValue)
        {
            SelectedFamilyName = Snapshot.SelectedFamilyName;
            for (int i = 0; i < Snapshot.TestGroups.Count; i += 2)
            {
                var left = BuildTestGroup(Snapshot.TestGroups[i]);
                var right = i + 1 < Snapshot.TestGroups.Count ? BuildTestGroup(Snapshot.TestGroups[i + 1]) : null;
                TestRows.Add(new TestRow { Left = left, Right = right });
            }

            NotifyAllChanged();
            return;
        }

        if (string.IsNullOrEmpty(SelectedTestKey) || AllResults.Count == 0)
        {
            NotifyAllChanged();
            return;
        }

        var testResults = AllResults.Where(r => r.MatchesSelection(SelectedTestKey)).ToList();
        if (testResults.Count == 0)
        {
            NotifyAllChanged();
            return;
        }

        var selectedFamily = testResults
            .Select(r => r.EvidenceFamily)
            .FirstOrDefault(f => f.HasValue);

        if (selectedFamily == null)
        {
            NotifyAllChanged();
            return;
        }

        SelectedFamilyName = selectedFamily.Value.ToString();

        var individualTests = AllResults
            .Where(r => r.EvidenceFamily == selectedFamily && !r.IsCombinedResult && r.IsDefined)
            .GroupBy(r => (r.TestName, r.MetricName))
            .OrderBy(g => g.Key.TestName)
            .ThenBy(g => g.Key.MetricName)
            .ToList();

        for (int i = 0; i < individualTests.Count; i += 2)
        {
            var left = BuildTestGroup(individualTests[i].Key.TestName, individualTests[i].Key.MetricName,
                individualTests[i].ToList());
            var right = i + 1 < individualTests.Count
                ? BuildTestGroup(individualTests[i + 1].Key.TestName, individualTests[i + 1].Key.MetricName,
                    individualTests[i + 1].ToList())
                : null;

            TestRows.Add(new TestRow { Left = left, Right = right });
        }

        NotifyAllChanged();
    }

    private TestDistributionGroup BuildTestGroup(FamilyTestGroupSnapshot group)
    {
        int significantByP = group.Results.Count(r => r.IsDefined && r.PValue <= Alpha);
        int significantByQ = group.Results.Count(r => r.IsDefined && r.QValue <= Alpha);

        var rawPlot = BuildHistogramPlot(group.RawValues.ToList(), "Raw Value", "Count", OxyColors.Green);
        var pPlot = BuildHistogramPlotWithThreshold(group.PValues.ToList(), "P-Value", "Count", OxyColors.SteelBlue);
        var qPlot = BuildHistogramPlotWithThreshold(group.QValues.ToList(), "Q-Value", "Count", OxyColors.Orange);

        return new TestDistributionGroup
        {
            DisplayName = group.DisplayName,
            RawPlotModel = rawPlot,
            PValuePlotModel = pPlot,
            QValuePlotModel = qPlot,
            DataPointCount = group.DefinedCount,
            MeanPValue = group.MeanPValue,
            MeanQValue = group.MeanQValue,
            SignificantByPCount = significantByP,
            SignificantByQCount = significantByQ,
        };
    }

    private TestDistributionGroup BuildTestGroup(string testName, string metricName,
        List<StatisticalTestResult> results, bool highlight = false)
    {
        var label = highlight ? $"Combined | {metricName}" : $"{testName} | {metricName}";

        var pValues = results
            .Select(r => r.PValue)
            .Where(p => !double.IsNaN(p) && p > 0 && p <= 1.0)
            .ToList();

        var qValues = results
            .Select(r => r.QValue)
            .Where(q => !double.IsNaN(q) && q > 0 && q <= 1.0)
            .ToList();

        var rawValues = results
            .Select(r => r.TestStatistic)
            .Where(s => s.HasValue && !double.IsNaN(s.Value) && !double.IsInfinity(s.Value))
            .Select(s => s!.Value)
            .ToList();

        int n = results.Count(r => r.IsDefined);
        double meanP = pValues.Count > 0 ? pValues.Average() : double.NaN;
        double meanQ = qValues.Count > 0 ? qValues.Average() : double.NaN;
        int significantByP = results.Count(r => r.IsDefined && r.PValue <= Alpha);
        int significantByQ = results.Count(r => r.IsDefined && r.QValue <= Alpha);

        var rawPlot = BuildHistogramPlot(rawValues, "Raw Value", "Count", highlight ? OxyColors.DarkGreen : OxyColors.Green);
        var pPlot = BuildHistogramPlotWithThreshold(pValues, "P-Value", "Count", OxyColors.SteelBlue);
        var qPlot = BuildHistogramPlotWithThreshold(qValues, "Q-Value", "Count", OxyColors.Orange);

        return new TestDistributionGroup
        {
            DisplayName = label,
            RawPlotModel = rawPlot,
            PValuePlotModel = pPlot,
            QValuePlotModel = qPlot,
            DataPointCount = n,
            MeanPValue = meanP,
            MeanQValue = meanQ,
            SignificantByPCount = significantByP,
            SignificantByQCount = significantByQ,
            IsHighlighted = highlight
        };
    }

    private PlotModel BuildHistogramPlot(List<double> values, string xTitle, string yTitle, OxyColor color)
    {
        var model = new PlotModel
        {
            DefaultFontSize = 10,
            IsLegendVisible = false,
            Padding = new OxyThickness(8)
        };

        if (values.Count == 0)
        {
            model.Subtitle = "No data";
            return model;
        }

        var histogram = CreateHistogram(values);
        if (histogram.Count == 0)
            return model;

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = xTitle,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColors.LightGray
        });

        model.Axes.Add(CreateYAxis(yTitle));
        AddHistogramSeries(model, histogram, color);
        return model;
    }

    private PlotModel BuildHistogramPlotWithThreshold(List<double> values, string xTitle, string yTitle, OxyColor color)
    {
        var model = BuildHistogramPlot(values, xTitle, yTitle, color);
        if (values.Count > 0)
            AddSignificanceThreshold(model);
        return model;
    }

    private List<HistogramBin> CreateHistogram(List<double> values)
    {
        if (values.Count == 0)
            return new List<HistogramBin>();

        int numBins = Math.Clamp(BinCount, MinBinCount, MaxBinCount);

        double min = values.Min();
        double max = values.Max();
        double range = max - min;

        if (range < 1e-10)
        {
            return new List<HistogramBin>
            {
                new() { Start = min - 0.5, End = min + 0.5, Count = values.Count }
            };
        }

        double binWidth = range / numBins;
        var bins = new List<HistogramBin>();
        for (int i = 0; i < numBins; i++)
        {
            bins.Add(new HistogramBin
            {
                Start = min + i * binWidth,
                End = min + (i + 1) * binWidth,
                Count = 0
            });
        }

        foreach (var value in values)
        {
            int idx = (int)((value - min) / binWidth);
            idx = Math.Clamp(idx, 0, numBins - 1);
            bins[idx].Count++;
        }

        return bins;
    }

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

    private void AddSignificanceThreshold(PlotModel model)
    {
        model.Annotations.Add(new LineAnnotation
        {
            Type = LineAnnotationType.Vertical,
            X = Alpha,
            Color = OxyColors.Red,
            LineStyle = LineStyle.Dash,
            StrokeThickness = 2,
            Text = $"\u03b1 = {Alpha}",
            TextColor = OxyColors.Red,
            TextHorizontalAlignment = HorizontalAlignment.Right
        });
    }

    private Axis CreateYAxis(string title)
    {
        if (UseLogScale)
        {
            return new LogarithmicAxis
            {
                Position = AxisPosition.Left,
                Title = $"{title} (log)",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray,
                MinorGridlineStyle = LineStyle.Dot,
                MinorGridlineColor = OxyColors.LightGray,
                Minimum = 0.1
            };
        }

        return new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = title,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColors.LightGray,
            Minimum = 0
        };
    }

    private void NotifyAllChanged()
    {
        OnPropertyChanged(nameof(TestRows));
        OnPropertyChanged(nameof(HasTestRows));
        OnPropertyChanged(nameof(SelectedFamilyName));
    }
}

public sealed class TestDistributionGroup
{
    public string DisplayName { get; set; } = string.Empty;
    public PlotModel RawPlotModel { get; set; } = new();
    public PlotModel PValuePlotModel { get; set; } = new();
    public PlotModel QValuePlotModel { get; set; } = new();
    public int DataPointCount { get; set; }
    public double MeanPValue { get; set; }
    public double MeanQValue { get; set; }
    public int SignificantByPCount { get; set; }
    public int SignificantByQCount { get; set; }
    public bool IsHighlighted { get; set; }
}

public sealed class TestRow
{
    public TestDistributionGroup? Left { get; set; }
    public TestDistributionGroup? Right { get; set; }
    public bool HasRight => Right != null;
}
