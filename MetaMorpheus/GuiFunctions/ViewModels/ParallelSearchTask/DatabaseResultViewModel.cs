using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Statistics;
using TaskLayer.ParallelSearch.Util;

namespace GuiFunctions.ViewModels.ParallelSearchTask;

/// <summary>
/// ViewModel for a single database's statistical results
/// </summary>
public class DatabaseResultViewModel : BaseViewModel
{
    public string DatabaseName { get; set; } = string.Empty;
    public string OrganismName => Taxonomy.Organism;
    public double CombinedPValue { get; private set; }
    public double CombinedQValue { get; private set; }
    public TaxonomyInfo Taxonomy { get; }
    public TransientDatabaseMetrics AnalysisResult { get; } = new();
    public ObservableCollection<StatisticalTestResult> StatisticalResults { get; } = new();
    private StatisticalTestResult? _selectedTestResult;

    private int _statisticalTestsPassed = 0;
    public int StatisticalTestsPassed
    {
        get => _statisticalTestsPassed;
        set
        {
            _statisticalTestsPassed = value;
            OnPropertyChanged(nameof(StatisticalTestsPassed));
        }
    }

    private int _statisticalFamiliesPassed = 0;
    public int StatisticalFamiliesPassed
    {
        get => _statisticalFamiliesPassed;
        set
        {
            _statisticalFamiliesPassed = value;
            OnPropertyChanged(nameof(StatisticalFamiliesPassed));
        }
    }

    private string _taxonomyDisplay = string.Empty;
    /// <summary>
    /// Display value for current taxonomy grouping level
    /// Updated dynamically when GroupBy changes
    /// </summary>
    public string TaxonomyDisplay
    {
        get => _taxonomyDisplay;
        set
        {
            if (_taxonomyDisplay == value) return;
            _taxonomyDisplay = value;
            OnPropertyChanged(nameof(TaxonomyDisplay));
        }
    }

    /// <summary>
    /// P-value for the currently selected test
    /// </summary>
    public double SelectedTestPValue
    {
        get => _selectedTestResult?.PValue ?? double.NaN;
    }

    /// <summary>
    /// Q-value for the currently selected test
    /// </summary>
    public double SelectedTestQValue
    {
        get => _selectedTestResult?.QValue ?? double.NaN;
    }

    public double SelectedTestValue
    {
        get => _selectedTestResult?.TestStatistic ?? double.NaN;
    }

    public double SummaryAnomalyScore => AnalysisResult.SummaryAnomalyScore;

    public double FullAnomalyScore => AnalysisResult.FullAnomalyScore;

    private string _selectedTestName = "Combined_All";

    /// <summary>
    /// Update the selected test name and notify property changes
    /// </summary>
    public void UpdateSelectedTest(string testName)
    {
        if (_selectedTestName == testName) return;
        _selectedTestName = testName;
        _selectedTestResult = StatisticalResults.FirstOrDefault(r => r.MatchesSelection(_selectedTestName));
        OnPropertyChanged(nameof(SelectedTestPValue));
        OnPropertyChanged(nameof(SelectedTestQValue));
        OnPropertyChanged(nameof(SelectedTestValue));
    }

    public StatisticalTestResult? GetSelectedTestResult() => _selectedTestResult;

    public DatabaseResultViewModel() { }

    public DatabaseResultViewModel(List<StatisticalTestResult> results, TransientDatabaseMetrics analysisResult)
    {
        if (results.Any())
            DatabaseName = results.First().DatabaseName;

        AnalysisResult = analysisResult;
        Taxonomy = TaxonomyMapping.GetTaxonomyInfo(DatabaseName);

        // First, populate the collection
        foreach (var result in results)
            StatisticalResults.Add(result);

        // Then, search for the combined result
        var combined = StatisticalResults.FirstOrDefault(p => p.MatchesSelection(CombinedResultNames.GetCacheKey(CombinedResultNames.AllMetricName)));
        if (combined != null)
        {
            CombinedPValue = combined.PValue;
            CombinedQValue = combined.QValue;
        }

        _selectedTestResult = StatisticalResults.FirstOrDefault(r => r.MatchesSelection(_selectedTestName));
    }

    public void UpdateStatisticalTestsPassed(double alpha, bool useQValue)
    {
        var nonCombined = StatisticalResults.Where(r => !r.IsCombinedResult).ToList();
        StatisticalTestsPassed = nonCombined.Count(r => r.IsSignificant(alpha, useQValue));
        StatisticalFamiliesPassed = nonCombined.Where(r => r.EvidenceFamily.HasValue && r.IsSignificant(alpha, useQValue))
            .Select(r => r.EvidenceFamily!.Value)
            .Distinct()
            .Count();
        AnalysisResult.StatisticalTestsPassed = StatisticalTestsPassed;
        AnalysisResult.PassedTestCount = StatisticalTestsPassed;
        AnalysisResult.PassedFamilyCount = StatisticalFamiliesPassed;
    }

    /// <summary>
    /// Get taxonomy value for a specific level
    /// </summary>
    public string GetTaxonomicValueForLevel(TaxonomicGrouping level)
    {
        return level switch
        {
            TaxonomicGrouping.Organism => Taxonomy.Organism,
            TaxonomicGrouping.Kingdom => Taxonomy.Kingdom,
            TaxonomicGrouping.Phylum => Taxonomy.Phylum,
            TaxonomicGrouping.Class => Taxonomy.Class,
            TaxonomicGrouping.Order => Taxonomy.Order,
            TaxonomicGrouping.Family => Taxonomy.Family,
            TaxonomicGrouping.Genus => Taxonomy.Genus,
            TaxonomicGrouping.Species => Taxonomy.Species,
            _ => string.Empty
        };
    }
}
