using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearch.Statistics;

namespace GuiFunctions.ViewModels.ParallelSearchTask.Plots;

public sealed class FamilyCombinationValidityViewModel : BaseViewModel
{
    private List<StatisticalTestResult> _allResults = new();
    private double _alpha = 0.01;
    private string _selectedTestKey = string.Empty;
    private FamilySelectionSnapshot? _snapshot;
    private PlotModel? _cachedBestPlotModel;
    private PlotModel? _cachedMeanPlotModel;
    private PlotModel? _cachedWorstPlotModel;
    private string _cachedBestPlotKey = string.Empty;
    private string _cachedMeanPlotKey = string.Empty;
    private string _cachedWorstPlotKey = string.Empty;

    public string PlotTitle => "Combination Validity";

    public string SelectedFamilyName { get; private set; } = string.Empty;

    public int CountAboveMin { get; private set; }
    public int CountOnMin { get; private set; }
    public int CountBelowMin { get; private set; }
    public int CountAboveMean { get; private set; }
    public int CountOnMean { get; private set; }
    public int CountBelowMean { get; private set; }
    public int CountAboveMax { get; private set; }
    public int CountOnMax { get; private set; }
    public int CountBelowMax { get; private set; }

    public PlotModel BestPlotModel => GetCachedPanel(CombinationPanel.Min);
    public PlotModel MeanPlotModel => GetCachedPanel(CombinationPanel.Mean);
    public PlotModel WorstPlotModel => GetCachedPanel(CombinationPanel.Max);

    private enum CombinationPanel { Min, Mean, Max }

    public List<StatisticalTestResult> AllResults
    {
        get => _allResults;
        set
        {
            _allResults = value ?? new();
            InvalidatePlotCaches();
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
            InvalidatePlotCaches();
            Refresh();
            OnPropertyChanged(nameof(Snapshot));
        }
    }

    public double Alpha
    {
        get => _alpha;
        set
        {
            _alpha = value;
            InvalidatePlotCaches();
            Refresh();
            OnPropertyChanged(nameof(Alpha));
        }
    }

    public string SelectedTestKey
    {
        get => _selectedTestKey;
        set
        {
            _selectedTestKey = value ?? string.Empty;
            InvalidatePlotCaches();
            Refresh();
            OnPropertyChanged(nameof(SelectedTestKey));
        }
    }

    public void Refresh()
    {
        int aboveMin = 0, onMin = 0, belowMin = 0;
        int aboveMean = 0, onMean = 0, belowMean = 0;
        int aboveMax = 0, onMax = 0, belowMax = 0;
        SelectedFamilyName = string.Empty;

        if (Snapshot != null && Snapshot.Family.HasValue)
        {
            SelectedFamilyName = Snapshot.SelectedFamilyName;
            foreach (var dbGroup in Snapshot.DatabaseSummaries)
            {
                var combined = dbGroup.CombinedResult;
                if (combined == null || double.IsNaN(combined.PValue) || combined.PValue <= 0)
                    continue;

                int sigCount = dbGroup.DefinedResults.Count(r => r.PValue <= Alpha);
                if (!double.IsNaN(dbGroup.MinP) && dbGroup.MinP > 0)
                    AddToCounts(combined.PValue, dbGroup.MinP, ref aboveMin, ref onMin, ref belowMin);
                if (!double.IsNaN(dbGroup.MeanP) && dbGroup.MeanP > 0)
                    AddToCounts(combined.PValue, dbGroup.MeanP, ref aboveMean, ref onMean, ref belowMean);
                if (!double.IsNaN(dbGroup.MaxP) && dbGroup.MaxP > 0)
                    AddToCounts(combined.PValue, dbGroup.MaxP, ref aboveMax, ref onMax, ref belowMax);
            }
        }
        else if (!string.IsNullOrEmpty(SelectedTestKey) && AllResults.Count > 0)
        {
            var testResults = AllResults.Where(r => r.MatchesSelection(SelectedTestKey)).ToList();
            if (testResults.Count > 0)
            {
                var family = testResults.Select(r => r.EvidenceFamily).FirstOrDefault(f => f.HasValue);
                if (family != null)
                {
                    SelectedFamilyName = family.Value.ToString();
                    var familyResults = AllResults.Where(r => r.EvidenceFamily == family).ToList();

                    var combinedByDb = familyResults
                        .Where(r => r.IsCombinedResult)
                        .GroupBy(r => r.DatabaseName)
                        .ToDictionary(g => g.Key, g => g.First());

                    var individualByDb = familyResults
                        .Where(r => !r.IsCombinedResult && r.IsDefined)
                        .GroupBy(r => r.DatabaseName)
                        .ToList();

                    foreach (var dbGroup in individualByDb)
                    {
                        string db = dbGroup.Key;
                        if (!combinedByDb.TryGetValue(db, out var combined))
                            continue;

                        double combinedP = combined.PValue;
                        if (double.IsNaN(combinedP) || combinedP <= 0)
                            continue;

                        var defined = dbGroup.Where(r => r.IsDefined && !double.IsNaN(r.PValue) && r.PValue > 0).ToList();
                        if (defined.Count == 0)
                            continue;

                        double minP = defined.Min(r => r.PValue);
                        double maxP = defined.Max(r => r.PValue);
                        double meanP = defined.Average(r => r.PValue);

                        AddToCounts(combinedP, minP, ref aboveMin, ref onMin, ref belowMin);
                        AddToCounts(combinedP, meanP, ref aboveMean, ref onMean, ref belowMean);
                        AddToCounts(combinedP, maxP, ref aboveMax, ref onMax, ref belowMax);
                    }
                }
            }
        }

        CountAboveMin = aboveMin; CountOnMin = onMin; CountBelowMin = belowMin;
        CountAboveMean = aboveMean; CountOnMean = onMean; CountBelowMean = belowMean;
        CountAboveMax = aboveMax; CountOnMax = onMax; CountBelowMax = belowMax;
        NotifyAllChanged();
    }

    private static void AddToCounts(double combinedP, double referenceP,
        ref int above, ref int on, ref int below)
    {
        double ratio = combinedP / referenceP;
        if (ratio < 0.95)
            below++;
        else if (ratio > 1.05)
            above++;
        else
            on++;
    }

    private PlotModel BuildPanel(CombinationPanel panel)
    {
        string title = panel switch
        {
            CombinationPanel.Min => "Combined vs Best (Min P)",
            CombinationPanel.Mean => "Combined vs Mean (Avg P)",
            CombinationPanel.Max => "Combined vs Worst (Max P)",
            _ => "Combined vs Reference"
        };

        string xLabel = panel switch
        {
            CombinationPanel.Min => "-log10(Min P)",
            CombinationPanel.Mean => "-log10(Mean P)",
            CombinationPanel.Max => "-log10(Max P)",
            _ => "-log10(Reference P)"
        };

        var model = new PlotModel
        {
            Title = title,
            DefaultFontSize = 11,
            IsLegendVisible = false,
            Padding = new OxyThickness(8)
        };

        if (string.IsNullOrEmpty(SelectedTestKey) || AllResults.Count == 0)
        {
            model.Subtitle = "Select a test from the Test Summary grid";
            return model;
        }

        var series0 = new ScatterSeries
        {
            Title = "0 sig. tests",
            MarkerType = MarkerType.Circle,
            MarkerSize = 4,
            MarkerFill = OxyColors.Gray,
            MarkerStroke = OxyColors.DarkGray,
            MarkerStrokeThickness = 0.5
        };

        var series1 = new ScatterSeries
        {
            Title = "1-2 sig. tests",
            MarkerType = MarkerType.Circle,
            MarkerSize = 4,
            MarkerFill = OxyColors.Goldenrod,
            MarkerStroke = OxyColors.DarkGoldenrod,
            MarkerStrokeThickness = 0.5
        };

        var series2 = new ScatterSeries
        {
            Title = "3+ sig. tests",
            MarkerType = MarkerType.Circle,
            MarkerSize = 5,
            MarkerFill = OxyColors.Red,
            MarkerStroke = OxyColors.DarkRed,
            MarkerStrokeThickness = 0.5
        };

        double maxX = 0, maxY = 0;

        if (Snapshot != null && Snapshot.Family.HasValue)
        {
            foreach (var summary in Snapshot.DatabaseSummaries)
            {
                var combined = summary.CombinedResult;
                if (combined == null || double.IsNaN(combined.PValue) || combined.PValue <= 0)
                    continue;

                double refP = panel switch
                {
                    CombinationPanel.Min => summary.MinP,
                    CombinationPanel.Mean => summary.MeanP,
                    CombinationPanel.Max => summary.MaxP,
                    _ => summary.MinP
                };

                if (double.IsNaN(refP) || refP <= 0)
                    continue;

                double xVal = -Math.Log10(refP);
                double yVal = -Math.Log10(combined.PValue);
                int sigCount = summary.DefinedResults.Count(r => r.PValue <= Alpha);
                var target = sigCount >= 3 ? series2 : sigCount >= 1 ? series1 : series0;
                target.Points.Add(new ScatterPoint(xVal, yVal, value: sigCount) { Tag = summary.DatabaseName });
                maxX = Math.Max(maxX, xVal);
                maxY = Math.Max(maxY, yVal);
            }
        }
        else
        {
            var testResults = AllResults.Where(r => r.MatchesSelection(SelectedTestKey)).ToList();
            if (testResults.Count == 0)
                return model;

            var family = testResults.Select(r => r.EvidenceFamily).FirstOrDefault(f => f.HasValue);
            if (family == null)
                return model;

            var familyResults = AllResults.Where(r => r.EvidenceFamily == family).ToList();

            var combinedByDb = familyResults
                .Where(r => r.IsCombinedResult)
                .GroupBy(r => r.DatabaseName)
                .ToDictionary(g => g.Key, g => g.First());

            var individualByDb = familyResults
                .Where(r => !r.IsCombinedResult && r.IsDefined)
                .GroupBy(r => r.DatabaseName)
                .ToList();

            if (individualByDb.Count == 0)
                return model;

            foreach (var dbGroup in individualByDb)
            {
                string db = dbGroup.Key;
                if (!combinedByDb.TryGetValue(db, out var combined))
                    continue;
                double combinedP = combined.PValue;
                if (double.IsNaN(combinedP) || combinedP <= 0)
                    continue;
                var defined = dbGroup.Where(r => r.IsDefined && !double.IsNaN(r.PValue) && r.PValue > 0).ToList();
                if (defined.Count == 0)
                    continue;
                double refP = panel switch
                {
                    CombinationPanel.Min => defined.Min(r => r.PValue),
                    CombinationPanel.Mean => defined.Average(r => r.PValue),
                    CombinationPanel.Max => defined.Max(r => r.PValue),
                    _ => defined.Min(r => r.PValue)
                };
                double xVal = -Math.Log10(refP);
                double yVal = -Math.Log10(combinedP);
                int sigCount = defined.Count(r => r.PValue <= Alpha);
                var target = sigCount >= 3 ? series2 : sigCount >= 1 ? series1 : series0;
                target.Points.Add(new ScatterPoint(xVal, yVal, value: sigCount) { Tag = db });
                maxX = Math.Max(maxX, xVal);
                maxY = Math.Max(maxY, yVal);
            }
        }

        if (maxX == 0 && maxY == 0)
            return model;

        double plotMax = Math.Max(maxX, maxY) * 1.1;

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = xLabel,
            Minimum = 0,
            Maximum = plotMax,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColors.LightGray
        });

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "-log10(Combined P)",
            Minimum = 0,
            Maximum = plotMax,
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
        diagonal.Points.Add(new DataPoint(plotMax, plotMax));
        model.Series.Add(diagonal);

        string trackerFmt = "Database: {Tag}\n{1}: {2:F3}\n{3}: {4:F3}\nSig tests: {5:F0}";
        series0.TrackerFormatString = trackerFmt;
        series1.TrackerFormatString = trackerFmt;
        series2.TrackerFormatString = trackerFmt;

        if (series0.Points.Count > 0) model.Series.Add(series0);
        if (series1.Points.Count > 0) model.Series.Add(series1);
        if (series2.Points.Count > 0) model.Series.Add(series2);

        return model;
    }

    private PlotModel GetCachedPanel(CombinationPanel panel)
    {
        string key = $"{panel}|{Snapshot?.SelectedTestKey}|{Snapshot?.DatabaseSummaries.Count}|{SelectedTestKey}|{Alpha}|{AllResults.Count}";
        return panel switch
        {
            CombinationPanel.Min when _cachedBestPlotModel != null && _cachedBestPlotKey == key => _cachedBestPlotModel,
            CombinationPanel.Mean when _cachedMeanPlotModel != null && _cachedMeanPlotKey == key => _cachedMeanPlotModel,
            CombinationPanel.Max when _cachedWorstPlotModel != null && _cachedWorstPlotKey == key => _cachedWorstPlotModel,
            _ => CachePanel(panel, key)
        };
    }

    private PlotModel CachePanel(CombinationPanel panel, string key)
    {
        var model = BuildPanel(panel);
        switch (panel)
        {
            case CombinationPanel.Min:
                _cachedBestPlotModel = model;
                _cachedBestPlotKey = key;
                break;
            case CombinationPanel.Mean:
                _cachedMeanPlotModel = model;
                _cachedMeanPlotKey = key;
                break;
            case CombinationPanel.Max:
                _cachedWorstPlotModel = model;
                _cachedWorstPlotKey = key;
                break;
        }
        return model;
    }

    private void InvalidatePlotCaches()
    {
        _cachedBestPlotModel = null;
        _cachedMeanPlotModel = null;
        _cachedWorstPlotModel = null;
        _cachedBestPlotKey = string.Empty;
        _cachedMeanPlotKey = string.Empty;
        _cachedWorstPlotKey = string.Empty;
    }

    private void NotifyAllChanged()
    {
        OnPropertyChanged(nameof(BestPlotModel));
        OnPropertyChanged(nameof(MeanPlotModel));
        OnPropertyChanged(nameof(WorstPlotModel));
        OnPropertyChanged(nameof(SelectedFamilyName));
        OnPropertyChanged(nameof(CountAboveMin));
        OnPropertyChanged(nameof(CountOnMin));
        OnPropertyChanged(nameof(CountBelowMin));
        OnPropertyChanged(nameof(CountAboveMean));
        OnPropertyChanged(nameof(CountOnMean));
        OnPropertyChanged(nameof(CountBelowMean));
        OnPropertyChanged(nameof(CountAboveMax));
        OnPropertyChanged(nameof(CountOnMax));
        OnPropertyChanged(nameof(CountBelowMax));
    }
}
