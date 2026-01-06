using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using TaskLayer.ParallelSearchTask.Statistics;
using TaskLayer.ParallelSearchTask.Util;

namespace GuiFunctions.ViewModels.ParallelSearchTask;

/// <summary>
/// Base class for all statistical plot view models
/// Provides common functionality for OxyPlot generation, filtering, and data export
/// </summary>
public abstract class StatisticalPlotViewModelBase : BaseViewModel
{
    protected bool _isDirty = true;
    private PlotModel? _plotModel;

    protected StatisticalPlotViewModelBase()
    {
        ExportPlotDataCommand = new DelegateCommand(ExecuteExportPlotData, CanExecuteExportPlotData);
        RefreshPlotCommand = new DelegateCommand(_ => ExecuteRefreshPlot());
    }

    #region Properties

    /// <summary>
    /// The OxyPlot model for binding to PlotView
    /// </summary>
    public PlotModel? PlotModel
    {
        get
        {
            if (_plotModel == null || _isDirty)
            {
                _isDirty = false;
                _plotModel = GeneratePlotModel();
            }
            return _plotModel;
        }
        protected set
        {
            _plotModel = value;
            OnPropertyChanged(nameof(PlotModel));
        }
    }

    private string _plotTitle = "Statistical Analysis";
    public string PlotTitle
    {
        get => _plotTitle;
        set
        {
            if (_plotTitle == value) return;
            _plotTitle = value;
            _isDirty = true;
            OnPropertyChanged(nameof(PlotTitle));
            OnPropertyChanged(nameof(PlotModel));
        }
    }

    private bool _showLegend = true;
    public bool ShowLegend
    {
        get => _showLegend;
        set
        {
            if (_showLegend == value) return;
            _showLegend = value;
            _isDirty = true;
            OnPropertyChanged(nameof(ShowLegend));
            OnPropertyChanged(nameof(PlotModel));
        }
    }

    #endregion

    #region Commands

    public ICommand ExportPlotDataCommand { get; }
    public ICommand RefreshPlotCommand { get; }

    #endregion

    #region Abstract Methods

    /// <summary>
    /// Generate the OxyPlot PlotModel - must be implemented by derived classes
    /// </summary>
    protected abstract PlotModel GeneratePlotModel();

    /// <summary>
    /// Get the data to export as CSV - must be implemented by derived classes
    /// </summary>
    protected abstract IEnumerable<string> GetExportData();

    #endregion

    #region Command Implementations

    private void ExecuteRefreshPlot()
    {
        _isDirty = true;
        OnPropertyChanged(nameof(PlotModel));
    }

    private bool CanExecuteExportPlotData(object? parameter)
    {
        return PlotModel != null;
    }

    private void ExecuteExportPlotData(object? parameter)
    {
        if (parameter is not string filePath)
        {
            // Could show dialog here if needed
            return;
        }

        try
        {
            var exportData = GetExportData().ToList();
            File.WriteAllLines(filePath, exportData);
        }
        catch (Exception ex)
        {
            // Log error - could expose via property for UI binding
            Console.WriteLine($"Error exporting plot data: {ex.Message}");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Create a standard linear axis with common settings
    /// </summary>
    protected LinearAxis CreateLinearAxis(string title, AxisPosition position, 
        double? minimum = null, double? maximum = null)
    {
        return new LinearAxis
        {
            Title = title,
            Position = position,
            Minimum = minimum ?? double.NaN,
            Maximum = maximum ?? double.NaN,
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColors.LightGray,
            MinorGridlineColor = OxyColors.LightGray
        };
    }

    /// <summary>
    /// Create a category axis for database names or test names
    /// </summary>
    protected CategoryAxis CreateCategoryAxis(string title, AxisPosition position, IEnumerable<string> labels)
    {
        return new CategoryAxis
        {
            Title = title,
            Position = position,
            ItemsSource = labels.ToList(),
            Angle = 45,
            IsTickCentered = true
        };
    }

    /// <summary>
    /// Calculate -log10(p-value) for volcano/manhattan plots
    /// Handles edge cases (p=0, p=1, NaN)
    /// </summary>
    protected double CalculateNegativeLog10(double pValue)
    {
        if (double.IsNaN(pValue) || pValue <= 0)
            return double.NaN;
        if (pValue >= 1)
            return 0;
        return -Math.Log10(pValue);
    }

    /// <summary>
    /// Get color based on significance threshold
    /// </summary>
    protected OxyColor GetSignificanceColor(double pValue, double alpha, bool isSignificant)
    {
        if (isSignificant)
            return OxyColors.Red;
        else if (pValue < alpha * 2)
            return OxyColors.Orange;
        else
            return OxyColors.Gray;
    }

    /// <summary>
    /// Mark the plot as dirty to force regeneration
    /// </summary>
    protected void MarkDirty()
    {
        _isDirty = true;
        OnPropertyChanged(nameof(PlotModel));
    }

    #endregion
}


