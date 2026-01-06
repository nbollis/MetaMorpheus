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
}