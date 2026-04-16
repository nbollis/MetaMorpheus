using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using EngineLayer;
using EngineLayer.FdrAnalysis;
using EngineLayer.SpectrumMatch;
using MassSpectrometry;
using Omics.Fragmentation;
using Omics.Modifications;
using Proteomics;
using Proteomics.ProteolyticDigestion;

namespace Benchmark;

/// <summary>
/// Benchmarks for FDR Analysis Engine - Q-value calculation without PEP
/// Measures performance of Q-value calculation across different dataset sizes and configurations
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[MarkdownExporter, HtmlExporter]
public class FdrAnalysisBenchmarks
{
    private List<SpectralMatch>? _smallPsmList;
    private List<SpectralMatch>? _mediumPsmList;
    private List<SpectralMatch>? _largePsmList;
    private CommonParameters? _commonParams;
    private List<(string fileName, CommonParameters fileSpecificParameters)>? _fileSpecificParams;

    [GlobalSetup]
    public void Setup()
    {
        Console.WriteLine("Generating FDR benchmark data...");

        _commonParams = new CommonParameters();
        _fileSpecificParams = new List<(string fileName, CommonParameters fileSpecificParameters)>
        {
            ("test_file.mzML", _commonParams)
        };

        // Small dataset
        _smallPsmList = GeneratePsmList(100, targetToDecoyRatio: 0.9);
        Console.WriteLine($"Small: {_smallPsmList.Count} PSMs");

        // Medium dataset  
        _mediumPsmList = GeneratePsmList(5000, targetToDecoyRatio: 0.9);
        Console.WriteLine($"Medium: {_mediumPsmList.Count} PSMs");

        // Large dataset
        _largePsmList = GeneratePsmList(25000, targetToDecoyRatio: 0.9);
        Console.WriteLine($"Large: {_largePsmList.Count} PSMs");

        Console.WriteLine("FDR data generation complete.");
    }

    private List<SpectralMatch> GeneratePsmList(int count, double targetToDecoyRatio)
    {
        var psms = new List<SpectralMatch>();
        var random = new Random(42); // Fixed seed for reproducibility
        int targetCount = (int)(count * targetToDecoyRatio);

        // Create fake scan
        var fakeScan = new MsDataScan(
            new MzSpectrum(new double[] { 100, 200 }, new double[] { 1000, 2000 }, false),
            1, 2, true, Polarity.Positive, 10.0, null, null, MZAnalyzerType.Orbitrap, 3000, null, null, "scan=1",
            double.NaN, null, null, double.NaN, null, DissociationType.HCD, 0, null
        );

        for (int i = 0; i < count; i++)
        {
            bool isDecoy = i >= targetCount;
            string sequence = isDecoy ? "DECOYSEQ" + i : "TARGETSEQ" + i;
            
            var protein = new Protein(sequence + "PEPTIDE", isDecoy ? $"DECOY_P{i}" : $"P{i}", isDecoy: isDecoy);
            var digestionParams = new DigestionParams(minPeptideLength: 5);
            var peptides = protein.Digest(digestionParams, new List<Modification>(), new List<Modification>()).ToList();
            
            if (peptides.Count == 0) continue;

            var peptide = peptides[0];
            var scan = new Ms2ScanWithSpecificMass(fakeScan, peptide.MonoisotopicMass, 2, "test_file.mzML", new CommonParameters());
            
            // Generate realistic scores - targets score higher than decoys
            double score = isDecoy 
                ? random.Next(5, 50)  // Lower scores for decoys
                : random.Next(30, 100); // Higher scores for targets

            var psm = new PeptideSpectralMatch(peptide, 0, score, i, scan, new CommonParameters(), new List<MatchedFragmentIon>());
            psm.SetFdrValues(0, 0, 0, 0, 0, 0, 0, 0);
            psm.ResolveAllAmbiguities();

            psms.Add(psm);
        }

        return psms;
    }

    [Benchmark(Description = "QValue Calculation - Small (100 PSMs)", Baseline = true)]
    public void QValueCalculation_Small()
    {
        var psmsCopy = _smallPsmList!.ToList();
        FdrAnalysisEngine.CalculateQValue(psmsCopy, peptideLevelCalculation: false, pepCalculation: false);
    }

    [Benchmark(Description = "QValue Calculation - Medium (5K PSMs)")]
    public void QValueCalculation_Medium()
    {
        var psmsCopy = _mediumPsmList!.ToList();
        FdrAnalysisEngine.CalculateQValue(psmsCopy, peptideLevelCalculation: false, pepCalculation: false);
    }

    [Benchmark(Description = "QValue Calculation - Large (25K PSMs)")]
    public void QValueCalculation_Large()
    {
        var psmsCopy = _largePsmList!.ToList();
        FdrAnalysisEngine.CalculateQValue(psmsCopy, peptideLevelCalculation: false, pepCalculation: false);
    }

    [Benchmark(Description = "QValue Traditional - Small")]
    public void QValueTraditional_Small()
    {
        // Use a small list to trigger traditional Q-value calculation (< 1000 PSMs)
        var psmsCopy = _smallPsmList!.Take(50).ToList();
        FdrAnalysisEngine.CalculateQValue(psmsCopy, peptideLevelCalculation: false, pepCalculation: false);
    }

    [Benchmark(Description = "QValue Inverted - Medium")]
    public void QValueInverted_Medium()
    {
        // Inverted Q-value is used for larger datasets (>= 1000 PSMs)
        var psmsCopy = _mediumPsmList!.ToList();
        FdrAnalysisEngine.CalculateQValue(psmsCopy, peptideLevelCalculation: false, pepCalculation: false);
    }

    [Benchmark(Description = "Peptide Level QValue - Medium")]
    public void PeptideLevelQValue_Medium()
    {
        var psmsCopy = _mediumPsmList!.ToList();
        FdrAnalysisEngine.CalculateQValue(psmsCopy, peptideLevelCalculation: true, pepCalculation: false);
    }

    [Benchmark(Description = "Full FDR Engine - Small")]
    public void FullFdrEngine_Small()
    {
        var psmsCopy = _smallPsmList!.ToList();
        var engine = new FdrAnalysisEngine(
            psmsCopy,
            massDiffAcceptorNumNotches: 1,
            _commonParams!,
            _fileSpecificParams!,
            new List<string>(),
            analysisType: "PSM",
            doPEP: false, // No PEP calculation
            outputFolder: null
        );
        engine.Run();
    }

    [Benchmark(Description = "Full FDR Engine - Medium")]
    public void FullFdrEngine_Medium()
    {
        var psmsCopy = _mediumPsmList!.ToList();
        var engine = new FdrAnalysisEngine(
            psmsCopy,
            massDiffAcceptorNumNotches: 1,
            _commonParams!,
            _fileSpecificParams!,
            new List<string>(),
            analysisType: "PSM",
            doPEP: false,
            outputFolder: null
        );
        engine.Run();
    }

    [Benchmark(Description = "Full FDR Engine - Large")]
    public void FullFdrEngine_Large()
    {
        var psmsCopy = _largePsmList!.ToList();
        var engine = new FdrAnalysisEngine(
            psmsCopy,
            massDiffAcceptorNumNotches: 1,
            _commonParams!,
            _fileSpecificParams!,
            new List<string>(),
            analysisType: "PSM",
            doPEP: false,
            outputFolder: null
        );
        engine.Run();
    }

    [Benchmark(Description = "Notch Ambiguous PSM Handling - Medium")]
    public void NotchAmbiguousPsmHandling_Medium()
    {
        // Create PSMs with multiple notches for ambiguity testing
        var psmsWithAmbiguity = GeneratePsmListWithNotchAmbiguity(1000);
        FdrAnalysisEngine.CalculateQValue(psmsWithAmbiguity, peptideLevelCalculation: false, pepCalculation: false);
    }

    private List<SpectralMatch> GeneratePsmListWithNotchAmbiguity(int count)
    {
        var psms = GeneratePsmList(count, 0.9);
        var random = new Random(42);

        // Make some PSMs notch-ambiguous by adding additional peptide matches
        for (int i = 0; i < psms.Count / 10; i++) // 10% ambiguous
        {
            var psm = psms[random.Next(psms.Count)];
            var firstPeptide = psm.BestMatchingBioPolymersWithSetMods.First().SpecificBioPolymer as PeptideWithSetModifications;
            
            if (firstPeptide != null)
            {
                // Add another match with a different notch
                psm.AddOrReplace(firstPeptide, psm.Score, notch: 1, true, new List<MatchedFragmentIon>());
            }
        }

        return psms;
    }

    [Benchmark(Description = "CountPsm - Medium")]
    public void CountPsmBenchmark_Medium()
    {
        var psmsCopy = _mediumPsmList!.ToList();
        
        // First run FDR to set Q-values
        FdrAnalysisEngine.CalculateQValue(psmsCopy, peptideLevelCalculation: false, pepCalculation: false);
        
        // Now benchmark the CountPsm method
        FdrAnalysisEngine.CountPsm(psmsCopy);
    }

    [Benchmark(Description = "Multiple Protease Groups - Medium")]
    public void MultipleProteaseGroups_Medium()
    {
        // Create PSMs with different proteases to test grouping logic
        var psms = new List<SpectralMatch>();
        int psmsPerProtease = _mediumPsmList!.Count / 3;

        for (int proteaseIdx = 0; proteaseIdx < 3; proteaseIdx++)
        {
            var proteasePsms = _mediumPsmList.Skip(proteaseIdx * psmsPerProtease).Take(psmsPerProtease).ToList();
            psms.AddRange(proteasePsms);
        }

        var engine = new FdrAnalysisEngine(
            psms,
            massDiffAcceptorNumNotches: 1,
            _commonParams!,
            _fileSpecificParams!,
            new List<string>(),
            analysisType: "PSM",
            doPEP: false,
            outputFolder: null
        );
        engine.Run();
    }

    [Benchmark(Description = "High Decoy Ratio - Medium")]
    public void HighDecoyRatio_Medium()
    {
        // Test with higher proportion of decoys (more challenging scenario)
        var psmsHighDecoy = GeneratePsmList(5000, targetToDecoyRatio: 0.5); // 50% decoys
        
        var engine = new FdrAnalysisEngine(
            psmsHighDecoy,
            massDiffAcceptorNumNotches: 1,
            _commonParams!,
            _fileSpecificParams!,
            new List<string>(),
            analysisType: "PSM",
            doPEP: false,
            outputFolder: null
        );
        engine.Run();
    }
}
