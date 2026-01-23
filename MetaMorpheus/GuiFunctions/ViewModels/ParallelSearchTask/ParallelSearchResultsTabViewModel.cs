using CsvHelper;
using CsvHelper.Configuration;
using GuiFunctions.MetaDraw;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using TaskLayer.ParallelSearch;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.IO;
using TaskLayer.ParallelSearch.Statistics;

namespace GuiFunctions.ViewModels.ParallelSearchTask;

/// <summary>
/// Tab ViewModel for ParallelSearch Statistical Results visualization in MetaDraw
/// Integrates with the main ParallelSearchResultsViewModel to display plots in MetaDraw's tab system
/// </summary>
public class ParallelSearchResultsTabViewModel : MetaDrawTabViewModel
{
    private string? _statusMessage;
    private ParallelSearchResultsViewModel _resultsViewModel;

    public ParallelSearchResultsTabViewModel() : this(null) { }
    public ParallelSearchResultsTabViewModel(string? exportDirectory = null) : base(isTabEnabled: true)
    {
        TabHeader = "Parallel Search Results";
        ExportDirectory = exportDirectory;

        // Initialize the main results ViewModel
        _resultsViewModel = new ParallelSearchResultsViewModel();
        
        // Initialize commands
        LoadResultsCommand = new RelayCommand(async () => await ExecuteLoadResults());
        ClearDataCommand = new DelegateCommand(_ => ExecuteClearData());
    }

    #region Properties

    public override string TabHeader { get; init; }

    /// <summary>
    /// Main ViewModel containing all the plot ViewModels and filtering logic
    /// </summary>
    public ParallelSearchResultsViewModel ResultsViewModel
    {
        get => _resultsViewModel;
        set
        {
            _resultsViewModel = value;
            OnPropertyChanged(nameof(ResultsViewModel));
        }
    }

    private string? _directoryFilePath;
    public string? DirectoryFilePath
    {
        get => _directoryFilePath;
        set
        {
            _directoryFilePath = value;
            OnPropertyChanged(nameof(DirectoryFilePath));
            OnPropertyChanged(nameof(StatisticalResultsFileName));
            OnPropertyChanged(nameof(HasResults));
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged(nameof(StatusMessage));
        }
    }

    public string StatisticalResultsFileName => 
        !string.IsNullOrEmpty(DirectoryFilePath) 
            ? Path.GetFileName(DirectoryFilePath) 
            : "None Selected";

    public bool HasResults => !string.IsNullOrEmpty(DirectoryFilePath);

    #endregion

    #region Commands

    public ICommand LoadResultsCommand { get; }
    public ICommand ClearDataCommand { get; }

    #endregion

    #region Command Implementations

    #region Data Loading

    private async Task ExecuteLoadResults()
    {
        try
        {
            IsLoading = true;
            
            // Set default export directory if not already set
            if (string.IsNullOrEmpty(ExportDirectory))
            {
                ExportDirectory = Path.Combine(DirectoryFilePath, "MetaDrawExport");
            }

            var statisticalResultsPath = Path.Combine(DirectoryFilePath, TransientDatabaseResultsManager.StatResultFileName);
            var analysisResultsPath = Path.Combine(DirectoryFilePath, TransientDatabaseResultsManager.SummaryResultsFileName);

            if (!File.Exists(statisticalResultsPath))
            {
                StatusMessage = $"Statistical results file not found: {statisticalResultsPath}";
                return;
            }

            if (!File.Exists(analysisResultsPath))
            {
                StatusMessage = $"Analysis results file not found: {analysisResultsPath}";
                return;
            }

            StatusMessage = "Loading results...";

            // Run heavy CSV parsing on background thread
            var allDatabaseResults = await Task.Run(() =>
            {
                var analysisResults = new TransientDatabaseMetricsFile(analysisResultsPath).Results;
                var statisticalResults = new StatisticalTestResultFile(statisticalResultsPath).Results;

                List<DatabaseResultViewModel> results = new();
                foreach (var dbStatGroup in statisticalResults.GroupBy(p => p.DatabaseName))
                {
                    var analysisResult = analysisResults.FirstOrDefault(r => r.DatabaseName == dbStatGroup.Key);
                    if (analysisResult == null)
                        continue;

                    var dbResultViewModel = new DatabaseResultViewModel(dbStatGroup.ToList(), analysisResult)
                    {
                        DatabaseName = dbStatGroup.Key
                    };
                    results.Add(dbResultViewModel);
                }

                return new
                {
                    Results = results,
                    AllStatResults = statisticalResults,
                    StatCount = statisticalResults.Count,
                    DbCount = analysisResults.Count
                };
            });

            // Back on UI thread - update view models
            ResultsViewModel.AllDatabaseResults = new System.Collections.ObjectModel.ObservableCollection<DatabaseResultViewModel>(allDatabaseResults.Results);
            
            // Update AllStatisticalResults for the test detail view
            ResultsViewModel.AllStatisticalResults = allDatabaseResults.AllStatResults;
            
            // Update plot viewmodels (must be on UI thread)
            ResultsViewModel.UpdatePlotViewModels();
            
            StatusMessage = $"Loaded {allDatabaseResults.StatCount} statistical results from {allDatabaseResults.DbCount} databases";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading files: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    private void ExecuteClearData()
    {
        ResultsViewModel = new ParallelSearchResultsViewModel();
        DirectoryFilePath = null;
    }

    #endregion
}
