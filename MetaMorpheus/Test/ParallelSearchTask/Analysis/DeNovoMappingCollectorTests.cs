using System.IO;
using NUnit.Framework;
using Readers.DeNovoMapping;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Analysis.Collectors;

namespace Test.ParallelSearchTask.Analysis;

// From the ManySearchTask branch. The mapping summary file now comes from mzLib (Readers.DeNovoMapping), whose writer
// names the file MappingSummary.tsv, and the file is written next to the tests instead of to the system temp folder.
[TestFixture]
public class DeNovoMappingCollectorTests
{
    [Test]
    public void CanCollectData_WithMissingDatabase_ReturnsFalse()
    {
        string path = CreateMappingFile();
        try
        {
            var collector = new DeNovoMappingCollector(path);
            var context = new TransientDatabaseContext { DatabaseName = "UnknownDb" };

            Assert.That(collector.CanCollectData(context), Is.False);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Test]
    public void CollectData_WithKnownDatabase_ProjectsExpectedMetrics()
    {
        string path = CreateMappingFile();
        try
        {
            var collector = new DeNovoMappingCollector(path);
            var context = new TransientDatabaseContext { DatabaseName = "Db_A" };

            var data = collector.CollectData(context);

            Assert.Multiple(() =>
            {
                Assert.That(data[DeNovoMappingCollector.TotalPredictions], Is.EqualTo(3));
                Assert.That(data[DeNovoMappingCollector.TargetPeptidesMapped], Is.EqualTo(2));
                Assert.That(data[DeNovoMappingCollector.DecoyPeptidesMapped], Is.EqualTo(1));
                Assert.That(data[DeNovoMappingCollector.UniquePeptidesMapped], Is.EqualTo(2));
                Assert.That(data[DeNovoMappingCollector.UniqueProteinsMapped], Is.EqualTo(1));
                Assert.That(data[DeNovoMappingCollector.MeanRtError], Is.EqualTo(2.0).Within(1e-10));
                Assert.That(data[DeNovoMappingCollector.MeanPredictionScore], Is.EqualTo(95.5).Within(1e-10));
                Assert.That((double[])data[DeNovoMappingCollector.RetentionTimeErrors], Is.EquivalentTo(new[] { 1.0, 2.0, 3.0 }));
                Assert.That((double[])data[DeNovoMappingCollector.PredictionScores], Is.EquivalentTo(new[] { 90.0, 100.0, 96.5 }));
                Assert.That((double[])data[DeNovoMappingCollector.TargetPredictionScores], Is.EquivalentTo(new[] { 90.0, 100.0 }));
                Assert.That((double[])data[DeNovoMappingCollector.DecoyPredictionScores], Is.EquivalentTo(new[] { 96.5 }));
            });
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    private static string CreateMappingFile()
    {
        string folder = Path.Combine(TestContext.CurrentContext.TestDirectory, "DeNovoMappingCollectorTests", Path.GetRandomFileName());
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, DeNovoMapping.SummaryFileName);

        var file = new DeNovoMappingResultFile
        {
            Results =
            [
                new DeNovoMappingResult
                {
                    DatabaseIdentifier = "Db_A",
                    TotalPredictions = 3,
                    TargetPredictions = 2,
                    DecoyPredictions = 1,
                    UniquePeptidesMapped = 2,
                    UniqueProteinsMapped = 1,
                    MeanRtError = 1.23,
                    MeanPredictionScore = 95.5,
                    RetentionTimeErrors = [1.0, 2.0, 3.0],
                    PredictionScores = [90.0, 100.0, 96.5],
                    TargetPredictionScores = [90.0, 100.0],
                    DecoyPredictionScores = [96.5],
                },
            ],
        };

        file.WriteResults(path);
        return path;
    }
}
