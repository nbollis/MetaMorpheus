using EngineLayer;
using EngineLayer.SpectrumMatch;
using MassSpectrometry;
using Omics.Fragmentation;
using Omics.Modifications;
using Proteomics;
using Proteomics.ProteolyticDigestion;

namespace Benchmark;

/// <summary>
/// Generates realistic test data for protein parsimony and scoring benchmarks
/// </summary>
public static class BenchmarkDataGenerator
{
    private static readonly Random _random = new Random(42); // Fixed seed for reproducibility

    public static BenchmarkData GenerateData(int proteinCount, int avgPeptidesPerProtein, int avgPsmsPerPeptide, 
        bool includeDecoys = true, bool includeModifications = true)
    {
        var proteins = GenerateProteins(proteinCount, includeDecoys);
        var (peptides, proteinToPeptides) = DigestProteins(proteins, includeModifications);
        var psms = GeneratePsms(peptides, proteinToPeptides, avgPsmsPerPeptide);
        
        var commonParams = new CommonParameters();
        var filteredPsms = FilteredPsms.Filter(psms, commonParams);

        return new BenchmarkData
        {
            Proteins = proteins,
            Peptides = peptides,
            Psms = psms,
            FilteredPsms = filteredPsms,
            CommonParameters = commonParams
        };
    }

    private static List<Protein> GenerateProteins(int count, bool includeDecoys)
    {
        var proteins = new List<Protein>(count);
        int targetCount = includeDecoys ? count * 2 / 3 : count; // 2:1 ratio target:decoy
        
        for (int i = 0; i < targetCount; i++)
        {
            var sequence = GenerateProteinSequence(50, 500);
            proteins.Add(new Protein(sequence, $"P{i:D6}"));
        }

        if (includeDecoys)
        {
            for (int i = 0; i < count - targetCount; i++)
            {
                var sequence = GenerateProteinSequence(50, 500);
                proteins.Add(new Protein(sequence, $"DECOY_P{i:D6}", isDecoy: true));
            }
        }

        return proteins;
    }

    private static string GenerateProteinSequence(int minLength, int maxLength)
    {
        // Common amino acids with realistic distribution
        char[] aminoAcids = "ACDEFGHIKLMNPQRSTVWY".ToCharArray();
        
        // More realistic distribution (some amino acids are more common)
        char[] distributedAA = "AAAAAALLLLLLIIIIIGGGGGVVVVVVEEEESSSSTTTTKKKKRRRRNNNNDDDDQQQQFFPPHHMMYYCCWW".ToCharArray();
        
        int length = _random.Next(minLength, maxLength);
        var sequence = new char[length];
        
        for (int i = 0; i < length; i++)
        {
            sequence[i] = distributedAA[_random.Next(distributedAA.Length)];
        }
        
        return new string(sequence);
    }

    private static (List<PeptideWithSetModifications>, Dictionary<Protein, List<PeptideWithSetModifications>>) 
        DigestProteins(List<Protein> proteins, bool includeModifications)
    {
        var allPeptides = new List<PeptideWithSetModifications>();
        var proteinToPeptides = new Dictionary<Protein, List<PeptideWithSetModifications>>();
        
        var digestionParams = new DigestionParams(
            protease: "trypsin",
            minPeptideLength: 7,
            maxPeptideLength: 50,
            maxMissedCleavages: 2
        );

        var variableMods = new List<Modification>();
        if (includeModifications)
        {
            ModificationMotif.TryGetMotif("M", out ModificationMotif motifM);
            variableMods.Add(new Modification(
                _originalId: "Oxidation of M",
                _modificationType: "Common Variable",
                _target: motifM,
                _locationRestriction: "Anywhere.",
                _monoisotopicMass: 15.99491461957
            ));
        }

        var fixedMods = new List<Modification>();

        foreach (var protein in proteins)
        {
            var peptides = protein.Digest(digestionParams, fixedMods, variableMods).ToList();
            
            // Take a subset of peptides (not all theoretical peptides are observed)
            int pepCount = Math.Max(1, (int)(peptides.Count * 0.3)); // ~30% coverage
            var observedPeptides = peptides.OrderBy(p => _random.Next()).Take(pepCount).ToList();
            
            allPeptides.AddRange(observedPeptides);
            proteinToPeptides[protein] = observedPeptides;
        }

        return (allPeptides.Distinct().ToList(), proteinToPeptides);
    }

    private static List<SpectralMatch> GeneratePsms(
        List<PeptideWithSetModifications> peptides,
        Dictionary<Protein, List<PeptideWithSetModifications>> proteinToPeptides,
        int avgPsmsPerPeptide)
    {
        var psms = new List<SpectralMatch>();
        var commonParams = new CommonParameters();
        
        // Create a fake scan for PSM construction (using positional arguments like in tests)
        var fakeScan = new MsDataScan(
            new MzSpectrum(new double[] { 100, 200, 300 }, new double[] { 1000, 2000, 1500 }, false),
            1, 2, true, Polarity.Positive, 10.0, null, null, MZAnalyzerType.Orbitrap, 4500, null, null, "scan=1",
            double.NaN, null, null, double.NaN, null, DissociationType.HCD, 0, null
        );

        int scanNumber = 0;
        foreach (var peptide in peptides)
        {
            // Generate multiple PSMs for this peptide
            int numPsms = Math.Max(1, avgPsmsPerPeptide + _random.Next(-2, 3));
            
            for (int i = 0; i < numPsms; i++)
            {
                scanNumber++;
                var scan = new Ms2ScanWithSpecificMass(fakeScan, 2, 0, $"File_{scanNumber % 5}", commonParams);
                
                // Generate realistic score (higher for targets, lower for decoys)
                bool isDecoy = peptide.Parent.IsDecoy;
                double score = isDecoy 
                    ? _random.Next(5, 30)    // Lower scores for decoys
                    : _random.Next(20, 100); // Higher scores for targets
                
                var psm = new PeptideSpectralMatch(peptide, 0, score, scanNumber, scan, commonParams, new List<MatchedFragmentIon>());
                
                // Set FDR values
                double qValue = isDecoy ? _random.NextDouble() * 0.5 : _random.NextDouble() * 0.05;
                psm.SetFdrValues(
                    cumulativeTarget: scanNumber,
                    cumulativeDecoy: scanNumber / 10,
                    qValue: qValue,
                    cumulativeTargetNotch: scanNumber,
                    cumulativeDecoyNotch: scanNumber / 10,
                    qValueNotch: qValue,
                    pep: 0.0,
                    pepQValue: qValue
                );
                
                psm.ResolveAllAmbiguities();
                psms.Add(psm);
            }
        }

        return psms;
    }

    /// <summary>
    /// Creates shared peptides between proteins for more realistic parsimony scenarios
    /// </summary>
    public static BenchmarkData GenerateDataWithSharedPeptides(int proteinCount, int sharedPeptidePercentage = 20)
    {
        var data = GenerateData(proteinCount, avgPeptidesPerProtein: 10, avgPsmsPerPeptide: 3);
        
        // TODO: Could add logic to create shared peptide scenarios
        // This would involve creating proteins with overlapping sequences
        
        return data;
    }
}

public class BenchmarkData
{
    public List<Protein> Proteins { get; set; } = new();
    public List<PeptideWithSetModifications> Peptides { get; set; } = new();
    public List<SpectralMatch> Psms { get; set; } = new();
    public FilteredPsms FilteredPsms { get; set; } = null!;
    public CommonParameters CommonParameters { get; set; } = null!;

    public int ProteinCount => Proteins.Count;
    public int PeptideCount => Peptides.Count;
    public int PsmCount => Psms.Count;

    public override string ToString()
    {
        return $"Proteins: {ProteinCount}, Peptides: {PeptideCount}, PSMs: {PsmCount}";
    }
}
