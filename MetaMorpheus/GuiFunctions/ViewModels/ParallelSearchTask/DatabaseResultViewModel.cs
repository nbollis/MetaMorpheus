using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TaskLayer.ParallelSearchTask.Analysis;
using TaskLayer.ParallelSearchTask.Statistics;
using TaskLayer.ParallelSearchTask.Util;

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
    public AggregatedAnalysisResult AnalysisResult { get; } = new();
    public ObservableCollection<StatisticalResult> StatisticalResults { get; } = new();

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
        get
        {
            var selected = StatisticalResults.FirstOrDefault(r => r.TestName == _selectedTestName);
            return selected?.PValue ?? double.NaN;
        }
    }

    /// <summary>
    /// Q-value for the currently selected test
    /// </summary>
    public double SelectedTestQValue
    {
        get
        {
            var selected = StatisticalResults.FirstOrDefault(r => r.TestName == _selectedTestName);
            return selected?.QValue ?? double.NaN;
        }
    }

    private string _selectedTestName = "Combined_All";

    /// <summary>
    /// Update the selected test name and notify property changes
    /// </summary>
    public void UpdateSelectedTest(string testName)
    {
        if (_selectedTestName == testName) return;
        _selectedTestName = testName;
        OnPropertyChanged(nameof(SelectedTestPValue));
        OnPropertyChanged(nameof(SelectedTestQValue));
    }

    public DatabaseResultViewModel() { }

    public DatabaseResultViewModel(List<StatisticalResult> results, AggregatedAnalysisResult analysisResult)
    {
        if (results.Any())
            DatabaseName = results.First().DatabaseName;

        AnalysisResult = analysisResult;
        Taxonomy = TaxonomyMapping.GetTaxonomyInfo(DatabaseName);

        // First, populate the collection
        foreach (var result in results)
            StatisticalResults.Add(result);

        // Then, search for the combined result
        var combined = StatisticalResults.FirstOrDefault(p => p.TestName == "Combined_All");
        if (combined != null)
        {
            CombinedPValue = combined.PValue;
            CombinedQValue = combined.QValue;
        }
    }

    public void UpdateStatisticalTestsPassed(double alpha, bool useQValue)
    {
        StatisticalTestsPassed = StatisticalResults.Count(r => r.IsSignificant(alpha, useQValue));
        AnalysisResult.StatisticalTestsPassed = StatisticalTestsPassed;
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