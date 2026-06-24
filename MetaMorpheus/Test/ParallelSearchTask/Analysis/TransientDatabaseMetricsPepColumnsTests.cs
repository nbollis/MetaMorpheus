using NUnit.Framework;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Analysis.Collectors;

namespace Test.ParallelSearchTask.Analysis;

/// <summary>
/// Verifies the PEP-based 1% / 5% confident-count columns survive the property &lt;-&gt; Results-dictionary
/// round-trip used to (de)serialize TransientDatabaseMetrics to/from CSV.
/// </summary>
[TestFixture]
public class TransientDatabaseMetricsPepColumnsTests
{
    [Test]
    public void PepQValueColumns_RoundTripThroughResultsDictionary()
    {
        var metrics = new TransientDatabaseMetrics("UP000464024")
        {
            TargetPsmsFromTransientDbAtPepQ01 = 91,
            TargetPsmsFromTransientDbAtPepQ05 = 96,
            TargetPeptidesFromTransientDbAtPepQ01 = 30,
            TargetPeptidesFromTransientDbAtPepQ05 = 31,
        };

        metrics.PopulateResultsFromProperties();

        // The four new keys must be present in the serialized dictionary with their values.
        Assert.Multiple(() =>
        {
            Assert.That(metrics.Results[BasicMetricCollector.TargetPsmsFromTransientDbAtPepQ01], Is.EqualTo(91));
            Assert.That(metrics.Results[BasicMetricCollector.TargetPsmsFromTransientDbAtPepQ05], Is.EqualTo(96));
            Assert.That(metrics.Results[BasicMetricCollector.TargetPeptidesFromTransientDbAtPepQ01], Is.EqualTo(30));
            Assert.That(metrics.Results[BasicMetricCollector.TargetPeptidesFromTransientDbAtPepQ05], Is.EqualTo(31));
        });

        // And they must read back into a fresh metrics object.
        var restored = new TransientDatabaseMetrics("UP000464024") { Results = metrics.Results };
        restored.PopulatePropertiesFromResults();

        Assert.Multiple(() =>
        {
            Assert.That(restored.TargetPsmsFromTransientDbAtPepQ01, Is.EqualTo(91));
            Assert.That(restored.TargetPsmsFromTransientDbAtPepQ05, Is.EqualTo(96));
            Assert.That(restored.TargetPeptidesFromTransientDbAtPepQ01, Is.EqualTo(30));
            Assert.That(restored.TargetPeptidesFromTransientDbAtPepQ05, Is.EqualTo(31));
        });
    }
}
