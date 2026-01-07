using System;
using System.Collections.Generic;
using System.Linq;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using TaskLayer.ParallelSearchTask.Statistics;

namespace GuiFunctions.ViewModels.ParallelSearchTask;

/// <summary>
/// ViewModel for Manhattan plot visualization
/// Shows -log10(p-value) or -log10(q-value) across databases
/// Colored by significance threshold
/// </summary>
public class ManhattanPlotViewModel : StatisticalPlotViewModelBase
{
    private List<StatisticalResult> _results = new();
    private double _alpha = 0.05;
    private bool _useQValue = true;
    private string? _selectedTest = "Combined_All";

    public ManhattanPlotViewModel()
    {
        PlotTitle = "Manhattan Plot - Statistical Significance Across Databases";
    }

    #region Properties

    /// <summary>
    /// Statistical results to plot
    /// </summary>
    public List<StatisticalResult> Results
    {
        get => _results;
        set
        {
            _results = value ?? new List<StatisticalResult>();
            MarkDirty();
            OnPropertyChanged(nameof(Results));
            OnPropertyChanged(nameof(AvailableTests));
        }
    }

    /// <summary>
    /// Alpha threshold for significance
    /// </summary>
    public double Alpha
    {
        get => _alpha;
        set
        {
            if (Math.Abs(_alpha - value) < 0.00001) return;
            _alpha = value;
            MarkDirty();
            OnPropertyChanged(nameof(Alpha));
        }
    }

    /// <summary>
    /// Use Q-value (corrected) instead of P-value
    /// </summary>
    public bool UseQValue
    {
        get => _useQValue;
        set
        {
            if (_useQValue == value) return;
            _useQValue = value;
            MarkDirty();
            OnPropertyChanged(nameof(UseQValue));
        }
    }

    /// <summary>
    /// Filter by specific test (null = all tests)
    /// </summary>
    public string? SelectedTest
    {
        get => _selectedTest;
        set
        {
            if (_selectedTest == value) return;
            _selectedTest = value;
            MarkDirty();
            OnPropertyChanged(nameof(SelectedTest));
        }
    }

    /// <summary>
    /// Available test names for filtering
    /// </summary>
    public List<string> AvailableTests =>
        Results.Select(r => r.TestName).Distinct().OrderBy(t => t).ToList();

    #endregion

    #region Plot Generation

    protected override PlotModel GeneratePlotModel()
    {
        var model = new PlotModel
        {
            Title = PlotTitle,
            DefaultFontSize = 12
        };

        if (!Results.Any())
        {
            model.Subtitle = "No data available";
            return model;
        }

        // Filter results
        var filteredResults = Results.AsEnumerable();
        if (!string.IsNullOrEmpty(SelectedTest))
        {
            filteredResults = filteredResults.Where(r => r.TestName == SelectedTest);
        }

        var dataPoints = filteredResults
            .Select((r, index) => new
            {
                Index = index,
                Result = r,
                Value = UseQValue ? r.QValue : r.PValue,
                NegLog = UseQValue ? r.NegLog10QValue : r.NegLog10PValue,
                IsSignificant = r.IsSignificant(Alpha, UseQValue)
            })
            .Where(p => !double.IsNaN(p.NegLog))
            .ToList();

        if (!dataPoints.Any())
        {
            model.Subtitle = "No valid data points";
            return model;
        }

        // Create category axis for databases
        var categoryAxis = new CategoryAxis
        {
            Position = AxisPosition.Bottom,
            Title = "Database",
            Angle = 45,
            ItemsSource = dataPoints.Select(p => p.Result.DatabaseName).ToList(),
            IsTickCentered = true
        };
        model.Axes.Add(categoryAxis);

        // Create value axis
        var valueAxis = CreateLinearAxis(
            UseQValue ? "-log10(Q-value)" : "-log10(P-value)",
            AxisPosition.Left,
            minimum: 0
        );
        model.Axes.Add(valueAxis);

        // Add significance threshold line
        double thresholdLine = CalculateNegativeLog10(Alpha);
        if (!double.IsNaN(thresholdLine))
        {
            var annotation = new LineAnnotation
            {
                Type = LineAnnotationType.Horizontal,
                Y = thresholdLine,
                Color = OxyColors.Red,
                LineStyle = LineStyle.Dash,
                Text = $"α = {Alpha}",
                TextColor = OxyColors.Red
            };
            model.Annotations.Add(annotation);
        }

        // Create scatter series
        var significantSeries = new ScatterSeries
        {
            Title = "Significant",
            MarkerType = MarkerType.Circle,
            MarkerSize = 6,
            MarkerFill = OxyColors.Red
        };

        var nonSignificantSeries = new ScatterSeries
        {
            Title = "Non-significant",
            MarkerType = MarkerType.Circle,
            MarkerSize = 4,
            MarkerFill = OxyColors.Gray
        };

        foreach (var point in dataPoints)
        {
            var scatterPoint = new ScatterPoint(point.Index, point.NegLog);

            if (point.IsSignificant)
                significantSeries.Points.Add(scatterPoint);
            else
                nonSignificantSeries.Points.Add(scatterPoint);
        }

        model.Series.Add(significantSeries);
        model.Series.Add(nonSignificantSeries);

        if (ShowLegend)
        {
            model.IsLegendVisible = true;
            model.LegendPosition = LegendPosition.TopRight;
        }

        return model;
    }

    protected override IEnumerable<string> GetExportData()
    {
        yield return "Database,TestName,MetricName,PValue,QValue,NegLog10PValue,NegLog10QValue,IsSignificant";

        foreach (var result in Results)
        {
            var negLogP = CalculateNegativeLog10(result.PValue);
            var negLogQ = CalculateNegativeLog10(result.QValue);
            var isSignificant = result.IsSignificant(Alpha, UseQValue);

            yield return $"{result.DatabaseName},{result.TestName},{result.MetricName}," +
                        $"{result.PValue},{result.QValue},{negLogP},{negLogQ},{isSignificant}";
        }
    }

    #endregion
}
