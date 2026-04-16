using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using EngineLayer;

namespace Benchmark;

/// <summary>
/// Focused benchmarks for the Protein Scoring and FDR Engine
/// Isolates scoring performance from parsimony
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[MarkdownExporter, JsonExporter]
public class ProteinScoringBenchmarks
{
    private BenchmarkData? _smallData;
    private BenchmarkData? _mediumData;
    private BenchmarkData? _largeData;
    
    private List<ProteinGroup>? _smallProteinGroups;
    private List<ProteinGroup>? _mediumProteinGroups;
    private List<ProteinGroup>? _largeProteinGroups;

    [GlobalSetup]
    public void Setup()
    {
        Console.WriteLine("Generating benchmark data for scoring...");
        
        // Generate data
        _smallData = BenchmarkDataGenerator.GenerateData(50, 8, 2, true, true);
        _mediumData = BenchmarkDataGenerator.GenerateData(500, 10, 3, true, true);
        _largeData = BenchmarkDataGenerator.GenerateData(2000, 12, 4, true, true);

        // Pre-run parsimony to get protein groups
        _smallProteinGroups = RunParsimony(_smallData);
        _mediumProteinGroups = RunParsimony(_mediumData);
        _largeProteinGroups = RunParsimony(_largeData);

        Console.WriteLine($"Small: {_smallProteinGroups.Count} protein groups");
        Console.WriteLine($"Medium: {_mediumProteinGroups.Count} protein groups");
        Console.WriteLine($"Large: {_largeProteinGroups.Count} protein groups");
    }

    private List<ProteinGroup> RunParsimony(BenchmarkData data)
    {
        var engine = new ProteinParsimonyEngine(
            data.FilteredPsms,
            modPeptidesAreDifferent: true,
            data.CommonParameters,
            null,
            new List<string>()
        );
        var results = (ProteinParsimonyResults)engine.Run();
        return results.ProteinGroups;
    }

    [Benchmark(Description = "Scoring Only - Small", Baseline = true)]
    public void ScoringOnly_Small()
    {
        var engine = new ProteinScoringAndFdrEngine(
            _smallProteinGroups!,
            _smallData!.FilteredPsms,
            noOneHitWonders: false,
            treatModPeptidesAsDifferentPeptides: true,
            mergeIndistinguishableProteinGroups: true,
            _smallData.CommonParameters,
            null,
            new List<string>()
        );
        
        var results = (ProteinScoringAndFdrResults)engine.Run();
    }

    [Benchmark(Description = "Scoring Only - Medium")]
    public void ScoringOnly_Medium()
    {
        var engine = new ProteinScoringAndFdrEngine(
            _mediumProteinGroups!,
            _mediumData!.FilteredPsms,
            noOneHitWonders: false,
            treatModPeptidesAsDifferentPeptides: true,
            mergeIndistinguishableProteinGroups: true,
            _mediumData.CommonParameters,
            null,
            new List<string>()
        );
        
        var results = (ProteinScoringAndFdrResults)engine.Run();
    }

    [Benchmark(Description = "Scoring Only - Large")]
    public void ScoringOnly_Large()
    {
        var engine = new ProteinScoringAndFdrEngine(
            _largeProteinGroups!,
            _largeData!.FilteredPsms,
            noOneHitWonders: false,
            treatModPeptidesAsDifferentPeptides: true,
            mergeIndistinguishableProteinGroups: true,
            _largeData.CommonParameters,
            null,
            new List<string>()
        );
        
        var results = (ProteinScoringAndFdrResults)engine.Run();
    }

    [Benchmark(Description = "Scoring - No Merging")]
    public void Scoring_NoMerging()
    {
        var engine = new ProteinScoringAndFdrEngine(
            _mediumProteinGroups!,
            _mediumData!.FilteredPsms,
            noOneHitWonders: false,
            treatModPeptidesAsDifferentPeptides: true,
            mergeIndistinguishableProteinGroups: false, // No merging
            _mediumData.CommonParameters,
            null,
            new List<string>()
        );
        
        var results = (ProteinScoringAndFdrResults)engine.Run();
    }

    [Benchmark(Description = "Scoring - With Merging")]
    public void Scoring_WithMerging()
    {
        var engine = new ProteinScoringAndFdrEngine(
            _mediumProteinGroups!,
            _mediumData!.FilteredPsms,
            noOneHitWonders: false,
            treatModPeptidesAsDifferentPeptides: true,
            mergeIndistinguishableProteinGroups: true, // With merging
            _mediumData.CommonParameters,
            null,
            new List<string>()
        );
        
        var results = (ProteinScoringAndFdrResults)engine.Run();
    }

    [Benchmark(Description = "Scoring - No One Hit Wonders")]
    public void Scoring_NoOneHitWonders()
    {
        var engine = new ProteinScoringAndFdrEngine(
            _mediumProteinGroups!,
            _mediumData!.FilteredPsms,
            noOneHitWonders: true, // Filter one-hit wonders
            treatModPeptidesAsDifferentPeptides: true,
            mergeIndistinguishableProteinGroups: true,
            _mediumData.CommonParameters,
            null,
            new List<string>()
        );
        
        var results = (ProteinScoringAndFdrResults)engine.Run();
    }
}
