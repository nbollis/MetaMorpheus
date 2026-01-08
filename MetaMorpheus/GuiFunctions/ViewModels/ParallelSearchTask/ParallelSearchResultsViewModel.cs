using System;
using System.Collections.ObjectModel;
using System.Linq;
using Easy.Common.Extensions;

namespace GuiFunctions.ViewModels.ParallelSearchTask;

public class ParallelSearchResultsViewModel : BaseViewModel
{
    private bool _isDirty;

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
                foreach (var dbResult in _allDatabaseResults.OrderByDescending(p => p.StatisticalTestsPassed))
                {
                    if (dbResult.StatisticalTestsPassed >= MinTestPassedCount)
                        _filteredDatabaseResults.Add(dbResult);
                }
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

    private ManhattanPlotViewModel _manhattanPlot;
    public ManhattanPlotViewModel ManhattanPlot
    {
        get => _manhattanPlot;
        set
        {
            _manhattanPlot = value;
            OnPropertyChanged(nameof(ManhattanPlot));
        }
    }

    private PhylogeneticTreeViewModel _phylogeneticTree;
    public PhylogeneticTreeViewModel PhylogeneticTree
    {
        get => _phylogeneticTree;
        set
        {
            _phylogeneticTree = value;
            OnPropertyChanged(nameof(PhylogeneticTree));
        }
    }

    private PlotType _currentPlotType;
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

    private StatisticalPlotViewModelBase _currentPlot;
    /// <summary>
    /// Currently active plot view model
    /// </summary>
    public StatisticalPlotViewModelBase CurrentPlot
    {
        get => _currentPlot;
        private set
        {
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

    private double _alpha = 0.01;
    public double Alpha
    {
        get => _alpha;
        set
        {
            if (!(Math.Abs(_alpha - value) > 0.00001)) 
                return;

            _isDirty = true;
            _alpha = value;

            _allDatabaseResults.ForEach(p => p.UpdateStatisticalTestsPassed(_alpha, _filterByQValue));

            // Update all plot view models
            UpdatePlotFilters();
            
            OnPropertyChanged(nameof(Alpha));
            OnPropertyChanged(nameof(FilteredDatabaseResults));
        }
    }

    private bool _filterByQValue = true;
    public bool FilterByQValue
    {
        get => _filterByQValue;
        set
        {
            if (_filterByQValue == value) 
                return;
            _isDirty = true;
            _filterByQValue = value;

            _allDatabaseResults.ForEach(p => p.UpdateStatisticalTestsPassed(_alpha, _filterByQValue));

            // Update plot view models
            UpdatePlotFilters();
            
            OnPropertyChanged(nameof(FilterByQValue));
            OnPropertyChanged(nameof(FilteredDatabaseResults));
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
        ManhattanPlot.Alpha = Alpha;
        ManhattanPlot.UseQValue = FilterByQValue;

        PhylogeneticTree.Results = filteredList;
        PhylogeneticTree.Alpha = Alpha;
        PhylogeneticTree.UseQValue = FilterByQValue;
    }

    /// <summary>
    /// Updates only the filter properties (alpha, useQValue) without reloading results
    /// Called when filter parameters change but results haven't
    /// </summary>
    private void UpdatePlotFilters()
    {
        ManhattanPlot.Alpha = Alpha;
        ManhattanPlot.UseQValue = FilterByQValue;
        
        PhylogeneticTree.Alpha = Alpha;
        PhylogeneticTree.UseQValue = FilterByQValue;
    }
}