using Easy.Common.Extensions;
using OxyPlot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GuiFunctions.ViewModels.ParallelSearchTask;

public class ParallelSearchResultsViewModel : BaseViewModel
{
    private bool _isDirty;
    private bool _isPlotDirty;

    public ParallelSearchResultsViewModel() 
    {
        // Initialize plot view models
        ManhattanPlot = new ManhattanPlotViewModel();
        PhylogeneticTree = new PhylogeneticTreeViewModel();
        
        // Set default plot
        _currentPlotType = PlotType.ManhattanPlot;
        _currentPlot = ManhattanPlot;
    }

    #region Data Collections

    private readonly ObservableCollection<DatabaseResultViewModel> _allDatabaseResults = new();
    private readonly ObservableCollection<DatabaseResultViewModel> _filteredDatabaseResults = new();

    /// <summary>
    /// Filtered database results for display
    /// </summary>
    public ObservableCollection<DatabaseResultViewModel> FilteredDatabaseResults
    {
        get
        {
            if (_isDirty)
                _filteredDatabaseResults.Clear();

            if (_filteredDatabaseResults.Count == 0)
            {
                _isDirty = false;
                HashSet<string> testNamesHash = new();
                foreach (var dbResult in _allDatabaseResults.OrderByDescending(p => p.StatisticalTestsPassed))
                {
                    if (dbResult.StatisticalTestsPassed >= MinTestPassedCount)
                        _filteredDatabaseResults.Add(dbResult);

                    testNamesHash.AddRange(dbResult.StatisticalResults.Select(p => p.TestName));
                }

                var selected = SelectedTest;
                var testNames = testNamesHash.ToArray();    
                foreach (var testName in testNames)
                {
                    if (!AvailableTests.Contains(testName))
                        AvailableTests.Add(testName);
                    testNamesHash.Remove(testName);
                }

                foreach (var testName in testNamesHash.Where(AvailableTests.Contains))
                    AvailableTests.Remove(testName);

                SelectedTest = selected;
            }
            return _filteredDatabaseResults;
        }
    }

    public ObservableCollection<DatabaseResultViewModel> AllDatabaseResults
    {
        get => _allDatabaseResults;
        set
        {
            _isDirty = true;
            _allDatabaseResults.Clear();
            foreach (var dbResult in value)
            {
                dbResult.UpdateStatisticalTestsPassed(Alpha, FilterByQValue);
                _allDatabaseResults.Add(dbResult);
            }

            OnPropertyChanged(nameof(AllDatabaseResults));
        }
    }

    #endregion

    #region Plot ViewModels and Selection

    private double _alpha = 0.01;
    private int _maxPointsToPlot = 0; // 0 means show all
    private int _topNGroups = 20; // 0 means show all
    private bool _filterByQValue = true;
    private bool _showLegend;
    private string _selectedTest = "Combined_All";
    private ObservableCollection<string> _availableTests = ["Combined_All"];
    private PlotType _currentPlotType;
    private ManhattanPlotViewModel _manhattanPlot;
    private PhylogeneticTreeViewModel _phylogeneticTree;
    private StatisticalPlotViewModelBase _currentPlot;

    public ManhattanPlotViewModel ManhattanPlot
    {
        get => _manhattanPlot;
        set
        {
            _manhattanPlot = value;
            OnPropertyChanged(nameof(ManhattanPlot));
        }
    }

    public PhylogeneticTreeViewModel PhylogeneticTree
    {
        get => _phylogeneticTree;
        set
        {
            _phylogeneticTree = value;
            OnPropertyChanged(nameof(PhylogeneticTree));
        }
    }

    /// <summary>
    /// Currently selected plot type
    /// </summary>
    public PlotType CurrentPlotType
    {
        get => _currentPlotType;
        set
        {
            if (_currentPlotType == value) return;
            _currentPlotType = value;
            
            // Update current plot reference based on selection
            CurrentPlot = _currentPlotType switch
            {
                PlotType.ManhattanPlot => ManhattanPlot,
                PlotType.PhylogeneticTree => PhylogeneticTree,
                _ => ManhattanPlot
            };
            
            OnPropertyChanged(nameof(CurrentPlotType));
            OnPropertyChanged(nameof(IsManhattanPlotSelected));
            OnPropertyChanged(nameof(IsPhylogeneticTreeSelected));
        }
    }

    /// <summary>
    /// Currently active plot view model
    /// </summary>
    public StatisticalPlotViewModelBase CurrentPlot
    {
        get => _currentPlot;
        private set
        {
            if (_currentPlot == value) return;

            if (_isPlotDirty)
            {
                // Update new plot with current filtered results
                var filteredList = FilteredDatabaseResults.ToList();
                value.Results = filteredList;
                value.Alpha = _alpha;
                value.UseQValue = _filterByQValue;
                value.MaxPointsToPlot = _maxPointsToPlot;
                value.SelectedTest = _selectedTest;
                value.TopNGroups = _topNGroups;
                _isPlotDirty = false;
            }


            _currentPlot = value;
            OnPropertyChanged(nameof(CurrentPlot));
        }
    }

    /// <summary>
    /// Convenience property for XAML visibility binding
    /// </summary>
    public bool IsManhattanPlotSelected => CurrentPlotType == PlotType.ManhattanPlot;

    /// <summary>
    /// Convenience property for XAML visibility binding
    /// </summary>
    public bool IsPhylogeneticTreeSelected => CurrentPlotType == PlotType.PhylogeneticTree;


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

    /// <summary>
    /// Filter by specific test (null = all tests)
    /// </summary>
    public string SelectedTest
    {
        get => _selectedTest ??= "Combined_All";
        set
        {
            if (value == null)
                return;

            _isPlotDirty = true;
            _selectedTest = value;
            CurrentPlot.SelectedTest = value;
            OnPropertyChanged(nameof(SelectedTest));
        }
    }

    public bool ShowLegend
    {
        get => _showLegend;
        set
        {
            if (_showLegend == value) return;


            _isPlotDirty = true;
            _showLegend = value;
            CurrentPlot.ShowLegend = value;
            OnPropertyChanged(nameof(ShowLegend));
        }
    }

    /// <summary>
    /// Maximum number of data points to display (0 = show all, filters to top N most significant)
    /// Helps reduce clutter by removing less significant baseline points
    /// </summary>
    public int MaxPointsToPlot
    {
        get => _maxPointsToPlot;
        set
        {
            if (_maxPointsToPlot == value) return;
            _maxPointsToPlot = value < 0 ? 0 : value; // Ensure non-negative

            _isPlotDirty = true;
            CurrentPlot.MaxPointsToPlot = _maxPointsToPlot;
            OnPropertyChanged(nameof(MaxPointsToPlot));
        }
    }

    public bool FilterByQValue
    {
        get => _filterByQValue;
        set
        {
            if (_filterByQValue == value)
                return;
            _isDirty = true;
            _isPlotDirty = true;
            _filterByQValue = value;
            CurrentPlot.UseQValue = value;

            _allDatabaseResults.ForEach(p => p.UpdateStatisticalTestsPassed(_alpha, _filterByQValue));

            OnPropertyChanged(nameof(FilterByQValue));
            OnPropertyChanged(nameof(FilteredDatabaseResults));
        }
    }

    public double Alpha
    {
        get => _alpha;
        set
        {
            if (!(Math.Abs(_alpha - value) > 0.00001))
                return;

            _isDirty = true;
            _isPlotDirty = true;
            _alpha = value;

            _allDatabaseResults.ForEach(p => p.UpdateStatisticalTestsPassed(_alpha, _filterByQValue));

            OnPropertyChanged(nameof(Alpha));
            OnPropertyChanged(nameof(FilteredDatabaseResults));
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
            _isPlotDirty = true;
            CurrentPlot.TopNGroups = _topNGroups;
            OnPropertyChanged(nameof(TopNGroups));
        }
    }

    #endregion

    #region Filter Properties

    private int _minTestPassed = 0;
    public int MinTestPassedCount
    {
        get => _minTestPassed;
        set
        {
            if (_minTestPassed == value) 
                return;

            _isDirty = true;
            _minTestPassed = value;
            OnPropertyChanged(nameof(MinTestPassedCount));
            OnPropertyChanged(nameof(FilteredDatabaseResults));
            
            // Update plots when filter changes (this is on UI thread)
            UpdatePlotViewModels();
        }
    }

    #endregion

    /// <summary>
    /// Updates all plot viewmodels with filtered results and current filter settings
    /// Called after filtered results change
    /// IMPORTANT: Must be called from UI thread
    /// </summary>
    public void UpdatePlotViewModels()
    {
        // Pass DatabaseResultViewModel collection directly to all plots
        var filteredList = FilteredDatabaseResults.ToList();
        
        ManhattanPlot.Results = filteredList;
        PhylogeneticTree.Results = filteredList;
    }

}