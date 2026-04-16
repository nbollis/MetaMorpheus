using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using EngineLayer;

namespace Benchmark;

/// <summary>
/// Benchmarks for the Protein Parsimony Engine
/// Measures performance of parsimony algorithm across different dataset sizes
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[MarkdownExporter, HtmlExporter]
public class ProteinParsimonyBenchmarks
{
    private BenchmarkData? _smallData;
    private BenchmarkData? _mediumData;
    private BenchmarkData? _largeData;
    
    [GlobalSetup]
    public void Setup()
    {
        Console.WriteLine("Generating benchmark data...");
        
        // Small dataset: Quick iteration, fast feedback
        _smallData = BenchmarkDataGenerator.GenerateData(
            proteinCount: 50,
            avgPeptidesPerProtein: 8,
            avgPsmsPerPeptide: 2,
            includeDecoys: true,
            includeModifications: true
        );
        Console.WriteLine($"Small data: {_smallData}");

        // Medium dataset: Realistic workload
        _mediumData = BenchmarkDataGenerator.GenerateData(
            proteinCount: 500,
            avgPeptidesPerProtein: 10,
            avgPsmsPerPeptide: 3,
            includeDecoys: true,
            includeModifications: true
        );
        Console.WriteLine($"Medium data: {_mediumData}");

        // Large dataset: Stress test
        _largeData = BenchmarkDataGenerator.GenerateData(
            proteinCount: 2000,
            avgPeptidesPerProtein: 12,
            avgPsmsPerPeptide: 4,
            includeDecoys: true,
            includeModifications: true
        );
        Console.WriteLine($"Large data: {_largeData}");
        
        Console.WriteLine("Data generation complete.");
    }

    [Benchmark(Description = "Parsimony - Small Dataset (50 proteins)")]
    public void Parsimony_Small()
    {
        var engine = new ProteinParsimonyEngine(
            _smallData!.FilteredPsms,
            modPeptidesAreDifferent: true,
            _smallData.CommonParameters,
            fileSpecificParameters: null,
            nestedIds: new List<string>()
        );
        
        var results = (ProteinParsimonyResults)engine.Run();
    }

    [Benchmark(Description = "Parsimony - Medium Dataset (500 proteins)", Baseline = true)]
    public void Parsimony_Medium()
    {
        var engine = new ProteinParsimonyEngine(
            _mediumData!.FilteredPsms,
            modPeptidesAreDifferent: true,
            _mediumData.CommonParameters,
            fileSpecificParameters: null,
            nestedIds: new List<string>()
        );
        
        var results = (ProteinParsimonyResults)engine.Run();
    }

    [Benchmark(Description = "Parsimony - Large Dataset (2000 proteins)")]
    public void Parsimony_Large()
    {
        var engine = new ProteinParsimonyEngine(
            _largeData!.FilteredPsms,
            modPeptidesAreDifferent: true,
            _largeData.CommonParameters,
            fileSpecificParameters: null,
            nestedIds: new List<string>()
        );
        
        var results = (ProteinParsimonyResults)engine.Run();
    }

    [Benchmark(Description = "Parsimony - Medium (ModPeptides as Different)")]
    public void Parsimony_Medium_ModsAsDifferent()
    {
        var engine = new ProteinParsimonyEngine(
            _mediumData!.FilteredPsms,
            modPeptidesAreDifferent: true,
            _mediumData.CommonParameters,
            fileSpecificParameters: null,
            nestedIds: new List<string>()
        );
        
        var results = (ProteinParsimonyResults)engine.Run();
    }

    [Benchmark(Description = "Parsimony - Medium (ModPeptides as Same)")]
    public void Parsimony_Medium_ModsAsSame()
    {
        var engine = new ProteinParsimonyEngine(
            _mediumData!.FilteredPsms,
            modPeptidesAreDifferent: false,
            _mediumData.CommonParameters,
            fileSpecificParameters: null,
            nestedIds: new List<string>()
        );
        
        var results = (ProteinParsimonyResults)engine.Run();
    }

    [Benchmark(Description = "Scoring - Small Dataset")]
    public void Scoring_Small()
    {
        // First run parsimony to get protein groups
        var parsimonyEngine = new ProteinParsimonyEngine(
            _smallData!.FilteredPsms,
            modPeptidesAreDifferent: true,
            _smallData.CommonParameters,
            fileSpecificParameters: null,
            nestedIds: new List<string>()
        );
        var parsimonyResults = (ProteinParsimonyResults)parsimonyEngine.Run();

        // Now benchmark the scoring engine
        var scoringEngine = new ProteinScoringAndFdrEngine(
            parsimonyResults.ProteinGroups,
            _smallData.FilteredPsms,
            noOneHitWonders: false,
            treatModPeptidesAsDifferentPeptides: true,
            mergeIndistinguishableProteinGroups: true,
            _smallData.CommonParameters,
            fileSpecificParameters: null,
            nestedIds: new List<string>()
        );
        
        var results = (ProteinScoringAndFdrResults)scoringEngine.Run();
    }

    [Benchmark(Description = "Scoring - Medium Dataset")]
    public void Scoring_Medium()
    {
        var parsimonyEngine = new ProteinParsimonyEngine(
            _mediumData!.FilteredPsms,
            modPeptidesAreDifferent: true,
            _mediumData.CommonParameters,
            fileSpecificParameters: null,
            nestedIds: new List<string>()
        );
        var parsimonyResults = (ProteinParsimonyResults)parsimonyEngine.Run();

        var scoringEngine = new ProteinScoringAndFdrEngine(
            parsimonyResults.ProteinGroups,
            _mediumData.FilteredPsms,
            noOneHitWonders: false,
            treatModPeptidesAsDifferentPeptides: true,
            mergeIndistinguishableProteinGroups: true,
            _mediumData.CommonParameters,
            fileSpecificParameters: null,
            nestedIds: new List<string>()
        );
        
        var results = (ProteinScoringAndFdrResults)scoringEngine.Run();
    }

    [Benchmark(Description = "Scoring - Large Dataset")]
    public void Scoring_Large()
    {
        var parsimonyEngine = new ProteinParsimonyEngine(
            _largeData!.FilteredPsms,
            modPeptidesAreDifferent: true,
            _largeData.CommonParameters,
            fileSpecificParameters: null,
            nestedIds: new List<string>()
        );
        var parsimonyResults = (ProteinParsimonyResults)parsimonyEngine.Run();

        var scoringEngine = new ProteinScoringAndFdrEngine(
            parsimonyResults.ProteinGroups,
            _largeData.FilteredPsms,
            noOneHitWonders: false,
            treatModPeptidesAsDifferentPeptides: true,
            mergeIndistinguishableProteinGroups: true,
            _largeData.CommonParameters,
            fileSpecificParameters: null,
            nestedIds: new List<string>()
        );
        
        var results = (ProteinScoringAndFdrResults)scoringEngine.Run();
    }

    [Benchmark(Description = "Full Pipeline - Medium Dataset")]
    public void FullPipeline_Medium()
    {
        // Parsimony
        var parsimonyEngine = new ProteinParsimonyEngine(
            _mediumData!.FilteredPsms,
            modPeptidesAreDifferent: true,
            _mediumData.CommonParameters,
            fileSpecificParameters: null,
            nestedIds: new List<string>()
        );
        var parsimonyResults = (ProteinParsimonyResults)parsimonyEngine.Run();

        // Scoring
        var scoringEngine = new ProteinScoringAndFdrEngine(
            parsimonyResults.ProteinGroups,
            _mediumData.FilteredPsms,
            noOneHitWonders: false,
            treatModPeptidesAsDifferentPeptides: true,
            mergeIndistinguishableProteinGroups: true,
            _mediumData.CommonParameters,
            fileSpecificParameters: null,
            nestedIds: new List<string>()
        );
        var scoringResults = (ProteinScoringAndFdrResults)scoringEngine.Run();
    }

    [Benchmark(Description = "Full Pipeline - Large Dataset")]
    public void FullPipeline_Large()
    {
        var parsimonyEngine = new ProteinParsimonyEngine(
            _largeData!.FilteredPsms,
            modPeptidesAreDifferent: true,
            _largeData.CommonParameters,
            fileSpecificParameters: null,
            nestedIds: new List<string>()
        );
        var parsimonyResults = (ProteinParsimonyResults)parsimonyEngine.Run();

        var scoringEngine = new ProteinScoringAndFdrEngine(
            parsimonyResults.ProteinGroups,
            _largeData.FilteredPsms,
            noOneHitWonders: false,
            treatModPeptidesAsDifferentPeptides: true,
            mergeIndistinguishableProteinGroups: true,
            _largeData.CommonParameters,
            fileSpecificParameters: null,
            nestedIds: new List<string>()
        );
        var scoringResults = (ProteinScoringAndFdrResults)scoringEngine.Run();
    }
}
