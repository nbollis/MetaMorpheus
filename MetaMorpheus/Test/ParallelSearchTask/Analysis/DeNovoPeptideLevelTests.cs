using System.Linq;
using NUnit.Framework;
using Readers.DeNovoMapping;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Analysis.Collectors;
using TaskLayer.ParallelSearch.Statistics;

namespace Test.ParallelSearchTask.Analysis;

/// <summary>The de novo tests take one value per mapped peptide, not one per prediction.</summary>
[TestFixture]
public class DeNovoPeptideLevelTests
{
    private static TransientDatabaseMetrics MetricsFor(DeNovoMappingResult result)
    {
        var metrics = new TransientDatabaseMetrics("Db");
        foreach (var (key, value) in DeNovoMappingCollector.ToResults(result))
            metrics.Results[key] = value;
        metrics.PopulatePropertiesFromResults();
        return metrics;
    }

    [Test]
    public void PerPeptideValues_ReachTheTestedProperties()
    {
        var result = new DeNovoMappingResult
        {
            RetentionTimeErrors = [0, 1, 3, 2],          // four predictions...
            PeptideRetentionTimeErrors = [1, 2],         // ...of two peptides
            PredictionScores = [0.9, 0.6, 0.7, 0.8],
            PeptidePredictionScores = [0.7, 0.8],
            UniquePeptidesMapped = 2,
            UniqueDecoyPeptidesMapped = 1,
        };

        var metrics = MetricsFor(result);

        Assert.Multiple(() =>
        {
            Assert.That(metrics.DeNovoHasHost, Is.False);
            Assert.That(metrics.DeNovoTestedPeptideRetentionTimeErrors, Is.EqualTo(new[] { 1d, 2d }));
            Assert.That(metrics.DeNovoTestedPeptidePredictionScores, Is.EqualTo(new[] { 0.7, 0.8 }));
            Assert.That(metrics.DeNovoTestedUniqueDecoyPeptides, Is.EqualTo(1));
        });
    }

    [Test]
    public void WithAHost_TheUnambiguousPeptideValuesAreTested()
    {
        var result = new DeNovoMappingResult
        {
            HostProteinCount = 5,
            PeptideRetentionTimeErrors = [1, 2],
            UnambiguousPeptideRetentionTimeErrors = [2],
            PeptidePredictionScores = [0.7, 0.8],
            UnambiguousPeptidePredictionScores = [0.8],
            UniqueDecoyPeptidesMapped = 3,
            UniqueUnambiguousDecoyPeptidesMapped = 1,
        };

        var metrics = MetricsFor(result);

        Assert.Multiple(() =>
        {
            Assert.That(metrics.DeNovoTestedPeptideRetentionTimeErrors, Is.EqualTo(new[] { 2d }));
            Assert.That(metrics.DeNovoTestedPeptidePredictionScores, Is.EqualTo(new[] { 0.8 }));
            Assert.That(metrics.DeNovoTestedUniqueDecoyPeptides, Is.EqualTo(1));
        });
    }

    [Test]
    public void AnOldSummaryWithoutPeptideColumns_GivesNoPeptideValues()
    {
        var metrics = new TransientDatabaseMetrics("Db") { TotalPredictions = 3, TargetPredictions = 3 };
        metrics.PopulateResultsFromProperties();
        metrics.Results.Remove(DeNovoMappingCollector.PeptideRetentionTimeErrors);
        metrics.Results.Remove(DeNovoMappingCollector.UniqueDecoyPeptidesMapped);
        metrics.PopulatePropertiesFromResults();

        Assert.That(metrics.DeNovoTestedPeptideRetentionTimeErrors, Is.Empty);
        Assert.That(metrics.DeNovoTestedUniqueDecoyPeptides, Is.EqualTo(0), "-1 (unknown) counts as none");
    }

    [Test]
    public void TheDeNovoSuite_ReadsPerPeptideValues()
    {
        var suite = new TestSuiteBuilder().AddDeNovoTests().Build().ToList();
        var metrics = MetricsFor(new DeNovoMappingResult
        {
            RetentionTimeErrors = [5, 5, 5, 5, 5],
            PeptideRetentionTimeErrors = [1, 2],
        });

        var rt = suite.OfType<SampleMeanTest>().Single(t => t.MetricName == "DeNovo-AbsoluteRtError");
        Assert.That(rt.IsDefinedFor(metrics), Is.True, "two peptide values meet the minimum");
        Assert.That(suite.Select(t => t.MetricName), Has.None.EqualTo("DeNovo-Predictions"));
        Assert.That(suite.Select(t => t.MetricName), Has.None.EqualTo("DeNovo-Targets"));
    }
}
