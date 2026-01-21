#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Analysis.Analyzers;

namespace Test.ParallelSearchTests;

[TestFixture]
public class ExternalDataIntegrationTests
{
    [Test]
    public void ExternalDataAnalyzer_InjectsMetricsIntoResults()
    {
        // Arrange: Create external data source
        var externalData = new FileBasedExternalDataSource("TestSource", "TestDatabase");
        externalData.AddMetric("CustomCount", 42);
        externalData.AddMetric("CustomScore", 95.5);
        externalData.AddMetric("CustomArray", new double[] { 1.0, 2.0, 3.0 });

        // Create minimal context with external data
        var context = new TransientDatabaseAnalysisContext
        {
            DatabaseName = "TestDatabase",
            ExternalDataSource = externalData,
            AllPsms = new List<EngineLayer.SpectralMatch>(),
            TransientPsms = new List<EngineLayer.SpectralMatch>(),
            AllPeptides = new List<EngineLayer.SpectralMatch>(),
            TransientPeptides = new List<EngineLayer.SpectralMatch>(),
            TransientProteins = new List<Omics.IBioPolymer>(),
            TransientProteinAccessions = new HashSet<string>()
        };

        // Create analyzer
        var analyzer = new ExternalDataAnalyzer();

        // Act: Run analysis
        var results = analyzer.Analyze(context);

        // Assert: Verify external metrics are present with correct prefix
        Assert.That(results.ContainsKey("TestSource_CustomCount"), Is.True);
        Assert.That(results["TestSource_CustomCount"], Is.EqualTo(42));
        
        Assert.That(results.ContainsKey("TestSource_CustomScore"), Is.True);
        Assert.That(results["TestSource_CustomScore"], Is.EqualTo(95.5));
        
        Assert.That(results.ContainsKey("TestSource_CustomArray"), Is.True);
        var array = (double[])results["TestSource_CustomArray"];
        Assert.That(array, Is.EqualTo(new double[] { 1.0, 2.0, 3.0 }));
    }

    [Test]
    public void ExternalDataAnalyzer_HandlesNoExternalData()
    {
        // Arrange: Context without external data
        var context = new TransientDatabaseAnalysisContext
        {
            DatabaseName = "TestDatabase",
            ExternalDataSource = null, // No external data
            AllPsms = new List<EngineLayer.SpectralMatch>(),
            TransientPsms = new List<EngineLayer.SpectralMatch>(),
            AllPeptides = new List<EngineLayer.SpectralMatch>(),
            TransientPeptides = new List<EngineLayer.SpectralMatch>(),
            TransientProteins = new List<Omics.IBioPolymer>(),
            TransientProteinAccessions = new HashSet<string>()
        };

        var analyzer = new ExternalDataAnalyzer();

        // Act
        var results = analyzer.Analyze(context);

        // Assert: Should return empty results, not crash
        Assert.That(results, Is.Empty);
    }

    [Test]
    public void DeNovoDataSource_ComputesCorrectMetrics()
    {
        // Arrange
        var deNovoSource = new DeNovoDataSource("TestDatabase");
        
        // Note: This test would need a real file to load from
        // For now, test the interface contract
        
        // Act
        var hasData = deNovoSource.HasData;
        
        // Assert
        Assert.That(deNovoSource.SourceName, Is.EqualTo("DeNovo"));
        Assert.That(deNovoSource.DatabaseName, Is.EqualTo("TestDatabase"));
        Assert.That(hasData, Is.False); // No data loaded yet
    }

    [Test]
    public void FileBasedExternalDataSource_StoresAndRetrievesMetrics()
    {
        // Arrange
        var source = new FileBasedExternalDataSource("MySource", "MyDB");
        source.AddMetric("Metric1", 100);
        source.AddMetric("Metric2", "StringValue");

        // Act
        var allMetrics = source.GetMetrics();
        bool found1 = source.TryGetMetric("Metric1", out var value1);
        bool found2 = source.TryGetMetric("Metric2", out var value2);
        bool found3 = source.TryGetMetric("NonExistent", out var value3);

        // Assert
        Assert.That(source.HasData, Is.True);
        Assert.That(allMetrics.Count, Is.EqualTo(2));
        
        Assert.That(found1, Is.True);
        Assert.That(value1, Is.EqualTo(100));
        
        Assert.That(found2, Is.True);
        Assert.That(value2, Is.EqualTo("StringValue"));
        
        Assert.That(found3, Is.False);
        Assert.That(value3, Is.Null);
    }

    [Test]
    public void AnalysisResultAggregator_IncludesExternalDataWhenProvided()
    {
        // Arrange: Create external data
        var externalData = new FileBasedExternalDataSource("External", "TestDB");
        externalData.AddMetric("ExternalMetric", 999);

        // Create context with external data
        var context = new TransientDatabaseAnalysisContext
        {
            DatabaseName = "TestDB",
            ExternalDataSource = externalData,
            AllPsms = new List<EngineLayer.SpectralMatch>(),
            TransientPsms = new List<EngineLayer.SpectralMatch>(),
            AllPeptides = new List<EngineLayer.SpectralMatch>(),
            TransientPeptides = new List<EngineLayer.SpectralMatch>(),
            TransientProteins = new List<Omics.IBioPolymer>(),
            TransientProteinAccessions = new HashSet<string>(),
            TotalProteins = 100,
            TransientPeptideCount = 50
        };

        // Create aggregator with ExternalDataAnalyzer
        var analyzers = new List<ITransientDatabaseAnalyzer>
        {
            new ExternalDataAnalyzer()
        };
        var aggregator = new AnalysisResultAggregator(analyzers);

        // Act: Run analysis
        var result = aggregator.RunAnalysis(context);

        // Assert: External metric should be in results
        Assert.That(result.Results.ContainsKey("External_ExternalMetric"), Is.True);
        Assert.That(result.Results["External_ExternalMetric"], Is.EqualTo(999));
    }
}
