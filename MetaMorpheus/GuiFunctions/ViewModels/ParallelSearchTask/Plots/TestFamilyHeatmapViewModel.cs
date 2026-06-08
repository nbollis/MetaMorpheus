using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearch.Statistics;
using TaskLayer.ParallelSearch.Util;

namespace GuiFunctions.ViewModels.ParallelSearchTask.Plots;

public sealed class TestFamilyHeatmapViewModel : BaseViewModel
{
    private List<StatisticalTestResult> _allResults = new();
    private double _alpha = 0.01;
    private string _selectedTestKey = string.Empty;
    private FamilySelectionSnapshot? _snapshot;
    private const int MaxHeatmapRows = 25;
    private const int HighlightedDatabaseLabels = 25;
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

            var organismLabels = dbGroups.Select(GetOrganismLabel).ToList();

            model.Axes.Add(CreateTestAxis(testGroups));
            model.Axes.Add(CreateDatabaseCategoryAxis(organismLabels));

            model.Series.Add(CreateHeatMapSeries(data, cols, rows));
            model.Series.Add(CreateHeatmapTrackerOverlay(testGroups, organismLabels, data, cols, rows));

            model.Axes.Add(CreateColorAxis("-log10(p)", Math.Max(maxVal, 2.0)));

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
        var organismLabels = dbGroups.Select(GetOrganismLabel).ToList();

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

        model.Axes.Add(CreateTestAxis(testGroups));

        if (rows <= 100)
        {
            model.Axes.Add(CreateDatabaseCategoryAxis(organismLabels));
        }
        else
        {
            model.Axes.Add(CreateDatabaseIndexAxis(organismLabels, rows));
        }

        model.Series.Add(CreateHeatMapSeries(data, cols, rows));
        model.Series.Add(CreateHeatmapTrackerOverlay(testGroups, organismLabels, data, cols, rows));

        model.Axes.Add(CreateColorAxis(rows < snapshot.DatabaseSummaries.Count ? $"-log10(p) (top {rows})" : "-log10(p)", Math.Max(maxVal, 2.0)));

        if (rows < snapshot.DatabaseSummaries.Count)
            model.Subtitle = $"Showing top {rows} databases by family significance";

        return model;
    }

    private static CategoryAxis CreateTestAxis(List<string> testGroups)
    {
        return new CategoryAxis
        {
            Position = AxisPosition.Bottom,
            Title = "Test",
            ItemsSource = testGroups,
            Angle = 45,
            FontSize = 9
        };
    }

    private static CategoryAxis CreateDatabaseCategoryAxis(List<string> dbGroups)
    {
        return new CategoryAxis
        {
            Position = AxisPosition.Left,
            Title = "Database",
            ItemsSource = dbGroups,
            FontSize = 8
        };
    }

    private static LinearAxis CreateDatabaseIndexAxis(List<string> dbGroups, int rows)
    {
        return new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = $"Database Index (top {rows})",
            Minimum = 0,
            Maximum = rows - 1,
            MajorStep = 1,
            LabelFormatter = value =>
            {
                int index = (int)Math.Round(value);
                if (index >= 0 && index < Math.Min(HighlightedDatabaseLabels, dbGroups.Count) && Math.Abs(value - index) < 0.25)
                {
                    return dbGroups[index];
                }

                return string.Empty;
            }
        };
    }

    private static LinearColorAxis CreateColorAxis(string title, double maximum)
    {
        return new LinearColorAxis
        {
            Position = AxisPosition.Right,
            Minimum = 0,
            Maximum = maximum,
            Title = title,
            MajorStep = 1.0,
            MinorStep = 0.5,
            MajorTickSize = 0,
            MinorTickSize = 0,
            TicklineColor = OxyColors.Transparent,
            MinorTicklineColor = OxyColors.Transparent
        };
    }

    private static string GetOrganismLabel(string databaseName)
    {
        var taxonomy = TaxonomyMapping.GetTaxonomyInfo(databaseName);
        return string.IsNullOrWhiteSpace(taxonomy?.Species) ? databaseName : taxonomy.Species;
    }

    private static HeatMapSeries CreateHeatMapSeries(double[,] data, int cols, int rows)
    {
        return new NonTrackingHeatMapSeries
        {
            X0 = 0,
            X1 = cols - 1,
            Y0 = 0,
            Y1 = rows - 1,
            Data = data,
            Interpolate = false,
            LabelFontSize = 0
        };
    }

    private static ScatterSeries CreateHeatmapTrackerOverlay(List<string> testGroups, List<string> organismLabels, double[,] data, int cols, int rows)
    {
        var overlay = new ScatterSeries
        {
            MarkerType = MarkerType.Square,
            MarkerFill = OxyColor.FromAColor(1, OxyColors.Transparent),
            MarkerStroke = OxyColor.FromAColor(1, OxyColors.Transparent),
            MarkerStrokeThickness = 0,
            MarkerSize = 12,
            TrackerFormatString = "{Tag}"
        };

        for (int col = 0; col < cols; col++)
        {
            for (int row = 0; row < rows; row++)
            {
                double value = data[col, row];
                string trackerText = $"Organism: {organismLabels[row]}\nTest: {testGroups[col]}\n-log10(p): {value:0.###}";
                overlay.Points.Add(new ScatterPoint(col, row, tag: trackerText));
            }
        }

        return overlay;
    }

    private sealed class NonTrackingHeatMapSeries : HeatMapSeries
    {
        public override TrackerHitResult GetNearestPoint(ScreenPoint point, bool interpolate)
        {
            return null;
        }
    }
}
