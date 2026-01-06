using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using TaskLayer.ParallelSearchTask.Statistics;
using TaskLayer.ParallelSearchTask.Analysis;
using Easy.Common.Extensions;

namespace GuiFunctions.ViewModels.ParallelSearchTask;

public class ParallelSearchResultsViewModel : BaseViewModel
{
    private bool _isDirty;

    public ParallelSearchResultsViewModel() 
    {
        // Initialize plot view models
        ManhattanPlot = new ManhattanPlotViewModel();
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
            System.Diagnostics.Debug.WriteLine($"FilteredDatabaseResults accessed, _isDirty={_isDirty}, Count={_filteredDatabaseResults.Count}");
            if (_filteredDatabaseResults.Count == 0 || _isDirty)
            {
                _isDirty = false;
                System.Diagnostics.Debug.WriteLine("Updating filtered results...");

                _filteredDatabaseResults.Clear();
                foreach (var dbResult in _allDatabaseResults.OrderByDescending(p => p.StatisticalTestsPassed))
                {
                    if (dbResult.StatisticalTestsPassed >= MinTestPassedCount)
                        _filteredDatabaseResults.Add(dbResult);
                }
                System.Diagnostics.Debug.WriteLine($"Filtered results updated, new count: {_filteredDatabaseResults.Count}");
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
            OnPropertyChanged(nameof(FilteredDatabaseResults));
        }
    }

    #endregion

        #region Plot ViewModels

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
        }
    }

    private double _alpha = 0.05;
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

    private void UpdatePlotViewModels()
    {
        System.Diagnostics.Debug.WriteLine("UpdatePlotViewModels starting...");
        try
        {
            var filtered = FilteredDatabaseResults; // Access the property getter first
            var allResults = filtered.SelectMany(p => p.StatisticalResults).ToList();

            System.Diagnostics.Debug.WriteLine($"Got {allResults.Count} results for plot");

            ManhattanPlot.Results = allResults;
            ManhattanPlot.Alpha = Alpha;
            ManhattanPlot.UseQValue = FilterByQValue;
            System.Diagnostics.Debug.WriteLine("UpdatePlotViewModels completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdatePlotViewModels FAILED: {ex.Message}");
            throw;
        }
    }

    private void UpdatePlotFilters()
    {
        ManhattanPlot.Alpha = Alpha;
        ManhattanPlot.UseQValue = FilterByQValue;
    }
}