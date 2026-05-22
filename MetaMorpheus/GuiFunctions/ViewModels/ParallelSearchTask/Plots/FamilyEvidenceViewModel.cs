using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearch.Statistics;

namespace GuiFunctions.ViewModels.ParallelSearchTask.Plots;

public sealed class FamilyEvidenceViewModel : BaseViewModel
{
    private List<StatisticalTestResult> _allResults = new();
    private double _alpha = 0.01;
    private string _selectedTestKey = string.Empty;

    public PlotModel ChartModel
    {
        get
        {
            var model = new PlotModel
            {
                Title = "Family Evidence",
                DefaultFontSize = 12,
                IsLegendVisible = false,
                Padding = new OxyThickness(10)
            };

            if (string.IsNullOrEmpty(SelectedTestKey) || AllResults.Count == 0)
            {
                model.Subtitle = "Select a test from the Test Summary grid";
                return model;
            }

            var testResults = AllResults.Where(r => r.MatchesSelection(SelectedTestKey)).ToList();
            if (testResults.Count == 0)
                return model;

            var family = testResults.Select(r => r.EvidenceFamily).FirstOrDefault(f => f.HasValue);
            if (family == null)
                return model;

            var familyTests = AllResults
                .Where(r => r.EvidenceFamily == family && !r.IsCombinedResult && r.IsDefined)
                .GroupBy(r => (r.TestName, r.MetricName))
                .OrderBy(g => g.Key.TestName)
                .ThenBy(g => g.Key.MetricName)
                .ToList();

            if (familyTests.Count == 0)
                return model;

            var bars = new List<(string Label, double Strength, bool IsSig)>();

            foreach (var group in familyTests)
            {
                var results = group.ToList();
                var meanP = results
                    .Where(r => r.IsDefined && !double.IsNaN(r.PValue) && r.PValue > 0)
                    .Select(r => r.PValue)
                    .DefaultIfEmpty(1.0)
                    .Average();
                double strength = -Math.Log10(meanP);
                bool isSig = meanP <= Alpha;
                string label = $"{group.Key.TestName} | {group.Key.MetricName}";
                bars.Add((label, strength, isSig));
            }

            if (bars.Count == 0)
                return model;

            var categoryAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "Test",
                ItemsSource = bars.Select(b => b.Label).ToList(),
                GapWidth = 0.3,
                MajorGridlineStyle = LineStyle.None
            };
            model.Axes.Add(categoryAxis);

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "-log10(Mean P-Value)",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray
            });

            var significantSeries = new BarSeries
            {
                Title = "Significant",
                FillColor = OxyColors.SteelBlue,
                StrokeThickness = 0.5,
                StrokeColor = OxyColors.Black
            };

            var notSignificantSeries = new BarSeries
            {
                Title = "Not Significant",
                FillColor = OxyColors.LightGray,
                StrokeThickness = 0.5,
                StrokeColor = OxyColors.Gray
            };

            for (int i = 0; i < bars.Count; i++)
            {
                var bar = bars[i];
                var item = new BarItem { Value = bar.Strength };
                if (bar.IsSig)
                    significantSeries.Items.Add(item);
                else
                    notSignificantSeries.Items.Add(item);
            }

            if (significantSeries.Items.Count > 0)
                model.Series.Add(significantSeries);
            if (notSignificantSeries.Items.Count > 0)
                model.Series.Add(notSignificantSeries);

            return model;
        }
    }

    public string PlotTitle => "Family Evidence";

    public List<StatisticalTestResult> AllResults
    {
        get => _allResults;
        set
        {
            _allResults = value ?? new();
            OnPropertyChanged(nameof(ChartModel));
        }
    }

    public double Alpha
    {
        get => _alpha;
        set
        {
            _alpha = value;
            OnPropertyChanged(nameof(ChartModel));
        }
    }

    public string SelectedTestKey
    {
        get => _selectedTestKey;
        set
        {
            _selectedTestKey = value ?? string.Empty;
            OnPropertyChanged(nameof(ChartModel));
        }
    }
}
