using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using TaskLayer.ParallelSearchTask.Statistics;
using TaskLayer.ParallelSearchTask.Util;
using static Nett.TomlObjectFactory;

namespace GuiFunctions.ViewModels.ParallelSearchTask;

/// <summary>
/// Base class for all statistical plot view models
/// Provides common functionality for OxyPlot generation, filtering, and data export
/// </summary>
public abstract class StatisticalPlotViewModelBase : BaseViewModel
{
    private double _alpha = 0.01;
    private bool _useQValue = true;
    protected bool _isDirty = true;
    private PlotModel? _plotModel;
    private TaxonomicGrouping _groupBy = TaxonomicGrouping.Order;
    private int _topNGroups = 20; // 0 means show all

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

    private bool _showLegend;
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

    /// <summary>
    /// Taxonomic grouping level for color coding and display
    /// </summary>
    public TaxonomicGrouping GroupBy
    {
        get => _groupBy;
        set
        {
            if (_groupBy == value) return;
            _groupBy = value;
            UpdateTaxonomyDisplayForAllResults();
            MarkDirty();
            OnPropertyChanged(nameof(GroupBy));
        }
    }

    /// <summary>
    /// Number of top groups to show in legend and filtered table (0 = show all)
    /// </summary>
    public int TopNGroups
    {
        get => _topNGroups;
        set
        {
            if (_topNGroups == value) return;
            _topNGroups = value < 0 ? 0 : value; // Ensure non-negative
            UpdateTopNResults();
            MarkDirty();
            OnPropertyChanged(nameof(TopNGroups));
        }
    }

    #endregion

    #region Test Selection

    private ObservableCollection<string> _availableTests = new ObservableCollection<string> { "Combined_All" };

    /// <summary>
    /// Available test names for filtering
    /// </summary>
    public ObservableCollection<string> AvailableTests
    {
        get => _availableTests;
        set
        {
            _availableTests = value;
            OnPropertyChanged(nameof(AvailableTests));
        }
    }


    private string _selectedTest = "Combined_All";

    /// <summary>
    /// Filter by specific test (null = all tests)
    /// </summary>
    public string SelectedTest
    {
        get => _selectedTest ??= "Combined_All";
        set
        {
            if (_selectedTest == value || value == null) 
                return;
            _selectedTest = value;
            UpdateSelectedTestForResults();
            UpdateTopNResults();
            MarkDirty();
            OnPropertyChanged(nameof(SelectedTest));
        }
    }

    #endregion

    #region Result Handling

    private List<DatabaseResultViewModel> _results = new();

    /// <summary>
    /// Database results to plot
    /// </summary>
    public List<DatabaseResultViewModel> Results
    {
        get => _results;
        set
        {
            _results = value ?? new List<DatabaseResultViewModel>();

            // Build available tests efficiently - single pass through data
            var testSet = new HashSet<string>();
            for (int i = 0; i < _results.Count; i++)
            {
                var statisticalResults = _results[i].StatisticalResults;
                for (int j = 0; j < statisticalResults.Count; j++)
                {
                    testSet.Add(statisticalResults[j].TestName);
                }
            }

            // Update observable collection only if changed
            if (testSet.Count != AvailableTests.Count || !testSet.SetEquals(AvailableTests))
            {
                AvailableTests.Clear();
                foreach (var testName in testSet.OrderBy(t => t))
                {
                    AvailableTests.Add(testName);
                }
            }

            UpdateTaxonomyDisplayForAllResults();
            UpdateTopNResults();
            MarkDirty();
            OnPropertyChanged(nameof(Results));
            OnPropertyChanged(nameof(AvailableTests));
        }
    }

    private ObservableCollection<DatabaseResultViewModel> _topNResults = new();

    /// <summary>
    /// Top N database results for the selected test
    /// </summary>
    public ObservableCollection<DatabaseResultViewModel> TopNResults
    {
        get => _topNResults;
        private set
        {
            _topNResults = value;
            OnPropertyChanged(nameof(TopNResults));
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

    /// <summary>
    /// Update Top N results based on current selected test and Top N setting
    /// </summary>
    protected virtual void UpdateTopNResults()
    {
        if (_results == null || _results.Count == 0)
        {
            TopNResults = new ObservableCollection<DatabaseResultViewModel>();
            return;
        }

        // Get results with statistical data for the selected test
        var resultsWithTest = _results
            .Where(r => r.StatisticalResults.Any(sr => 
                string.IsNullOrEmpty(SelectedTest) || sr.TestName == SelectedTest))
            .ToList();

        // Sort by significance (use Q-value if enabled, otherwise P-value)
        var sorted = resultsWithTest.OrderBy(r =>
        {
            var testResult = r.StatisticalResults.FirstOrDefault(sr =>
                string.IsNullOrEmpty(SelectedTest) || sr.TestName == SelectedTest);
            
            if (testResult == null)
                return double.MaxValue;

            return UseQValue ? testResult.QValue : testResult.PValue;
        }).ToList();

        // Take top N if specified
        var topN = _topNGroups > 0 && _topNGroups < sorted.Count
            ? sorted.Take(_topNGroups).ToList()
            : sorted;

        TopNResults = new ObservableCollection<DatabaseResultViewModel>(topN);
    }

    /// <summary>
    /// Update taxonomy display for all results based on current GroupBy setting
    /// </summary>
    protected void UpdateTaxonomyDisplayForAllResults()
    {
        if (_results == null) return;

        foreach (var result in _results)
        {
            result.TaxonomyDisplay = GetTaxonomicGroupForResult(result);
        }
        
        // Also update TopN results
        if (_topNResults != null)
        {
            foreach (var result in _topNResults)
            {
                result.TaxonomyDisplay = GetTaxonomicGroupForResult(result);
            }
        }
    }

    /// <summary>
    /// Update selected test for all results to refresh P/Q value properties
    /// </summary>
    protected void UpdateSelectedTestForResults()
    {
        if (_results == null) return;

        foreach (var result in _results)
        {
            result.UpdateSelectedTest(_selectedTest);
        }
        
        // Also update TopN results
        if (_topNResults != null)
        {
            foreach (var result in _topNResults)
            {
                result.UpdateSelectedTest(_selectedTest);
            }
        }
    }

    /// <summary>
    /// Get the taxonomic group name for a database based on the grouping level
    /// </summary>
    protected virtual string GetTaxonomicGroupForResult(DatabaseResultViewModel result)
    {
        if (_groupBy == TaxonomicGrouping.None)
            return "All";

        if (result.Taxonomy == null)
            return "Unclassified";

        var groupValue = _groupBy switch
        {
            TaxonomicGrouping.Organism => result.Taxonomy.Organism,
            TaxonomicGrouping.Kingdom => result.Taxonomy.Kingdom,
            TaxonomicGrouping.Phylum => result.Taxonomy.Phylum,
            TaxonomicGrouping.Class => result.Taxonomy.Class,
            TaxonomicGrouping.Order => result.Taxonomy.Order,
            TaxonomicGrouping.Family => result.Taxonomy.Family,
            TaxonomicGrouping.Genus => result.Taxonomy.Genus,
            TaxonomicGrouping.Species => result.Taxonomy.Species,
            _ => null
        };

        return string.IsNullOrWhiteSpace(groupValue) ? "Unclassified" : groupValue;
    }

    #endregion
}


