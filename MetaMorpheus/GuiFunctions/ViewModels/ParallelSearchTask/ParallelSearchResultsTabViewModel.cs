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
                var analysisResults = LoadAnalysisResultsFromCsv(analysisResultsPath);
                var statisticalResults = LoadStatisticalResultsFromCsv(statisticalResultsPath, analysisResults);

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
                    StatCount = statisticalResults.Count,
                    DbCount = analysisResults.Count
                };
            });

            // Back on UI thread - update view models
            ResultsViewModel.AllDatabaseResults = new System.Collections.ObjectModel.ObservableCollection<DatabaseResultViewModel>(allDatabaseResults.Results);
            
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

    /// <summary>
    /// Load analysis results from ManySearchSummary.csv
    /// </summary>
    private List<AggregatedAnalysisResult> LoadAnalysisResultsFromCsv(string filePath)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null
        });

        var records = csv.GetRecords<AggregatedAnalysisResult>().ToList();

        foreach (var record in records)
        {
            record.PopulateResultsFromProperties();
        }

        return records;
    }

    /// <summary>
    /// Load statistical results from StatisticalAnalysis_Results.csv and enrich with taxonomy
    /// </summary>
    private List<StatisticalResult> LoadStatisticalResultsFromCsv(
        string filePath,
        List<AggregatedAnalysisResult> analysisResults)
    {
        // Create lookup dictionary for analysis results
        var analysisLookup = analysisResults.ToDictionary(r => r.DatabaseName);

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null
        });

        // Read header to get all column names
        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? throw new InvalidOperationException("CSV file has no header");

        var results = new List<StatisticalResult>();

        // Find columns with p-values and q-values
        var testColumns = new HashSet<string>();
        foreach (var header in headers)
        {
            if (header.StartsWith("pValue_"))
            {
                // Extract test name from pValue_TestName
                var testName = header.Substring("pValue_".Length);
                testColumns.Add(testName);
            }
        }

        // Read each row
        while (csv.Read())
        {
            var databaseName = csv.GetField<string>("DatabaseName");

            // Get taxonomy info from analysis results
            var analysisResult = analysisLookup.GetValueOrDefault(databaseName);

            if (string.IsNullOrWhiteSpace(databaseName))
                continue;

            // Read each test result
            foreach (var testName in testColumns)
            {
                var pValueField = $"pValue_{testName}";
                var qValueField = $"qValue_{testName}";
                var isSignificantField = $"isSignificant_{testName}";

                // Read values safely
                var pValueStr = csv.GetField(pValueField);
                var qValueStr = csv.GetField(qValueField);

                if (string.IsNullOrWhiteSpace(pValueStr) || string.IsNullOrWhiteSpace(qValueStr))
                    continue;

                if (!double.TryParse(pValueStr, out var pValue) ||
                    !double.TryParse(qValueStr, out var qValue))
                    continue;

                var result = new StatisticalResult
                {
                    DatabaseName = databaseName,
                    TestName = testName,
                    MetricName = ExtractMetricName(testName),
                    PValue = pValue,
                    QValue = qValue,
                    AdditionalMetrics = new Dictionary<string, object>()
                };

                // Add taxonomy info to additional metrics if available
                if (analysisResult != null)
                {
                    result.AdditionalMetrics["StatisticalTestsPassed"] = analysisResult.StatisticalTestsPassed;
                    result.AdditionalMetrics["TotalProteins"] = analysisResult.TotalProteins;
                    result.AdditionalMetrics["TargetPsmsAtQValueThreshold"] = analysisResult.TargetPsmsAtQValueThreshold;
                    result.AdditionalMetrics["TargetPeptidesAtQValueThreshold"] = analysisResult.TargetPeptidesAtQValueThreshold;
                    result.AdditionalMetrics["TargetProteinGroupsAtQValueThreshold"] = analysisResult.TargetProteinGroupsAtQValueThreshold;
                }

                results.Add(result);
            }
        }

        return results;
    }

    /// <summary>
    /// Extract the metric name from test name
    /// e.g., "FisherExact_Peptide" -> "Peptide"
    /// </summary>
    private string ExtractMetricName(string testName)
    {
        var parts = testName.Split('_');
        return parts.Length > 1 ? string.Join("_", parts.Skip(1)) : testName;
    }

    #endregion

    private void ExecuteClearData()
    {
        ResultsViewModel = new ParallelSearchResultsViewModel();
        DirectoryFilePath = null;
    }

    #endregion
}
