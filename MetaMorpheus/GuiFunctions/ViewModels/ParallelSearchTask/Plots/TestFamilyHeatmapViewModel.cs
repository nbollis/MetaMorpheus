using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearch.Statistics;

namespace GuiFunctions.ViewModels.ParallelSearchTask.Plots;

public sealed class TestFamilyHeatmapViewModel : BaseViewModel
{
    private List<StatisticalTestResult> _allResults = new();
    private double _alpha = 0.01;
    private string _selectedTestKey = string.Empty;
    private FamilySelectionSnapshot? _snapshot;
    private const int MaxHeatmapRows = 500;
    private PlotModel? _cachedHeatmapModel;
    private string _cachedHeatmapKey = string.Empty;

    public PlotModel HeatmapModel
    {
        get
        {
            string cacheKey = $"{Snapshot?.SelectedTestKey}|{Snapshot?.TestGroups.Count}|{Snapshot?.DatabaseSummaries.Count}|{SelectedTestKey}|{AllResults.Count}";
            if (_cachedHeatmapModel != null && _cachedHeatmapKey == cacheKey)
                return _cachedHeatmapModel;

            var model = new PlotModel
            {
                Title = "Test-Family Heatmap",
                DefaultFontSize = 11,
                IsLegendVisible = false,
                Padding = new OxyThickness(10)
            };

            if (Snapshot != null && Snapshot.Family.HasValue)
            {
                _cachedHeatmapKey = cacheKey;
                _cachedHeatmapModel = BuildFromSnapshot(model, Snapshot);
                return _cachedHeatmapModel;
            }

            if (string.IsNullOrEmpty(SelectedTestKey) || AllResults.Count == 0)
            {
                model.Subtitle = "Select a test from the Test Summary grid";
                _cachedHeatmapKey = cacheKey;
                _cachedHeatmapModel = model;
                return model;
            }

            var testResults = AllResults.Where(r => r.MatchesSelection(SelectedTestKey)).ToList();
            if (testResults.Count == 0)
                _cachedHeatmapKey = cacheKey;
                _cachedHeatmapModel = model;
                return model;

            var family = testResults.Select(r => r.EvidenceFamily).FirstOrDefault(f => f.HasValue);
            if (family == null)
                _cachedHeatmapKey = cacheKey;
                _cachedHeatmapModel = model;
                return model;

            var allFamilyResults = AllResults
                .Where(r => r.EvidenceFamily == family && !r.IsCombinedResult && r.IsDefined)
                .ToList();

            if (allFamilyResults.Count == 0)
                _cachedHeatmapKey = cacheKey;
                _cachedHeatmapModel = model;
                return model;

            var testGroups = allFamilyResults
                .GroupBy(r => (r.TestName, r.MetricName))
                .Select(g => $"{g.Key.TestName} | {g.Key.MetricName}")
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var dbGroups = allFamilyResults
                .GroupBy(r => r.DatabaseName)
                .Where(g => g.Any(r => r.IsDefined))
                .Select(g => g.Key)
                .OrderBy(x => x)
                .ToList();

            if (testGroups.Count == 0 || dbGroups.Count == 0)
                _cachedHeatmapKey = cacheKey;
                _cachedHeatmapModel = model;
                return model;

            int rows = dbGroups.Count;
            int cols = testGroups.Count;

            var data = new double[cols, rows];

            var dbToRow = dbGroups.Select((db, i) => (db, i)).ToDictionary(x => x.db, x => x.i);
            var testToCol = testGroups.Select((t, i) => (t, i)).ToDictionary(x => x.t, x => x.i);

            double minVal = double.MaxValue;
            double maxVal = double.MinValue;

            foreach (var result in allFamilyResults)
            {
                if (!result.IsDefined || double.IsNaN(result.PValue) || result.PValue <= 0)
                    continue;

                string testKey = $"{result.TestName} | {result.MetricName}";
                if (!testToCol.TryGetValue(testKey, out int col))
                    continue;
                if (!dbToRow.TryGetValue(result.DatabaseName, out int row))
                    continue;

                double val = -Math.Log10(result.PValue);
                data[col, row] = val;
                minVal = Math.Min(minVal, val);
                maxVal = Math.Max(maxVal, val);
            }

            if (minVal == double.MaxValue)
            {
                model.Subtitle = "No valid p-values";
                return model;
            }

            model.Axes.Add(new CategoryAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Test",
                ItemsSource = testGroups,
                Angle = 45,
                FontSize = 9
            });

            model.Axes.Add(new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "Database",
                ItemsSource = dbGroups,
                FontSize = 8
            });

            var heatMap = new HeatMapSeries
            {
                X0 = 0,
                X1 = cols - 1,
                Y0 = 0,
                Y1 = rows - 1,
                Data = data,
                Interpolate = false,
                LabelFontSize = 0
            };

            model.Series.Add(heatMap);

            var colorAxis = new LinearColorAxis
            {
                Position = AxisPosition.Right,
                Minimum = 0,
                Maximum = Math.Max(maxVal, 2.0),
                Title = "-log10(p)",
                MajorStep = 1.0,
                MinorStep = 0.5
            };
            model.Axes.Add(colorAxis);

            _cachedHeatmapKey = cacheKey;
            _cachedHeatmapModel = model;
            return model;
        }
    }

    public FamilySelectionSnapshot? Snapshot
    {
        get => _snapshot;
        set
        {
            _snapshot = value;
            _cachedHeatmapModel = null;
            OnPropertyChanged(nameof(Snapshot));
            OnPropertyChanged(nameof(HeatmapModel));
        }
    }

    public string PlotTitle => "Test-Family Heatmap";

    public List<StatisticalTestResult> AllResults
    {
        get => _allResults;
        set
        {
            _allResults = value ?? new();
            _cachedHeatmapModel = null;
            OnPropertyChanged(nameof(HeatmapModel));
        }
    }

    public double Alpha
    {
        get => _alpha;
        set
        {
            _alpha = value;
            _cachedHeatmapModel = null;
            OnPropertyChanged(nameof(HeatmapModel));
        }
    }

    public string SelectedTestKey
    {
        get => _selectedTestKey;
        set
        {
            _selectedTestKey = value ?? string.Empty;
            _cachedHeatmapModel = null;
            OnPropertyChanged(nameof(HeatmapModel));
        }
    }

    private PlotModel BuildFromSnapshot(PlotModel model, FamilySelectionSnapshot snapshot)
    {
        var testGroups = snapshot.TestGroups.Select(g => g.DisplayName).ToList();
        var dbGroups = snapshot.DatabaseSummaries
            .OrderBy(s => s.MinP)
            .Take(MaxHeatmapRows)
            .Select(s => s.DatabaseName)
            .ToList();

        if (testGroups.Count == 0 || dbGroups.Count == 0)
            return model;

        int rows = dbGroups.Count;
        int cols = testGroups.Count;
        var data = new double[cols, rows];
        var dbToRow = dbGroups.Select((db, i) => (db, i)).ToDictionary(x => x.db, x => x.i);
        var testToCol = testGroups.Select((t, i) => (t, i)).ToDictionary(x => x.t, x => x.i);

        double maxVal = double.MinValue;
        foreach (var group in snapshot.TestGroups)
        {
            if (!testToCol.TryGetValue(group.DisplayName, out int col))
                continue;

            foreach (var result in group.Results)
            {
                if (!dbToRow.TryGetValue(result.DatabaseName, out int row))
                    continue;
                if (double.IsNaN(result.PValue) || result.PValue <= 0)
                    continue;
                double val = -Math.Log10(result.PValue);
                data[col, row] = val;
                maxVal = Math.Max(maxVal, val);
            }
        }

        model.Axes.Add(new CategoryAxis
        {
            Position = AxisPosition.Bottom,
            Title = "Test",
            ItemsSource = testGroups,
            Angle = 45,
            FontSize = 9
        });

        if (rows <= 100)
        {
            model.Axes.Add(new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "Database",
                ItemsSource = dbGroups,
                FontSize = 8
            });
        }
        else
        {
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = $"Database Index (top {rows})",
                Minimum = 0,
                Maximum = rows - 1
            });
        }

        model.Series.Add(new HeatMapSeries
        {
            X0 = 0,
            X1 = cols - 1,
            Y0 = 0,
            Y1 = rows - 1,
            Data = data,
            Interpolate = false,
            LabelFontSize = 0
        });

        model.Axes.Add(new LinearColorAxis
        {
            Position = AxisPosition.Right,
            Minimum = 0,
            Maximum = Math.Max(maxVal, 2.0),
            Title = rows < snapshot.DatabaseSummaries.Count ? $"-log10(p) (top {rows})" : "-log10(p)",
            MajorStep = 1.0,
            MinorStep = 0.5
        });

        if (rows < snapshot.DatabaseSummaries.Count)
            model.Subtitle = $"Showing top {rows} databases by family significance";

        return model;
    }
}
