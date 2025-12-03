#nullable enable
using EngineLayer;
using Nett;
using Omics.Digestion;
using Omics.Modifications;
using pepXML.Generated;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UsefulProteomicsDatabases;

namespace TaskLayer;

public class PostHocDecorator
{
    private const string CsvFileName = "ManySearchSummary.csv";
    private const string CsvProcessedFileName = "ManySearchSummary_Processed2.csv";
    private const string ScoreColumn = "Score";
    private const string TDCColumn = "Matched Ion Mass-To-Charge Ratios";
    private const string QColumn = "Cumulative Decoy Notch";
    private const string OrganismColumn = "Organism Name";
    private const double QCutoff = 0.01;
    private const int SmallFileInitialCapacity = 8; // Optimized for small files

    public string SearchDirectory { get; init; }

    private readonly Dictionary<string, string> DatabaseNameToPathLookup;
    private readonly CommonParameters CommonParameters;
    
    public PostHocDecorator(string searchDir, string dbDir, string tomlPath)
    {
        SearchDirectory = searchDir;
        DatabaseNameToPathLookup = Directory.GetFiles(dbDir, "*.fasta")
            .ToDictionary(Path.GetFileNameWithoutExtension);

        ParallelSearchTask task = Toml.ReadFile<ParallelSearchTask>(tomlPath, MetaMorpheusTask.tomlConfig);
        CommonParameters = task.CommonParameters;
    }

    public void DecorateAndWrite()
    {
        string resultCsv = Path.Combine(SearchDirectory, CsvFileName);
        string processedResultCsv = Path.Combine(SearchDirectory, CsvProcessedFileName);
        LoadModifications(out var variableMods, out var fixedMods, out var localizableMods);

        ParallelSearchResultCache<TransientDatabaseSearchResults> cache = new ParallelSearchResultCache<TransientDatabaseSearchResults>(resultCsv);
        cache.InitializeCache();

        ParallelSearchResultCache<ExtendedTransientDatabaseSearchResults> processedCache = new ParallelSearchResultCache<ExtendedTransientDatabaseSearchResults>(processedResultCsv);
        processedCache.InitializeCache();

        int count = 0;
        
        Parallel.ForEach(cache.AllResults, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            dbResult =>
            {
                string dbName = dbResult.Key;

                if (processedCache.Contains(dbName))
                {
                    Interlocked.Increment(ref count);
                    return;
                }

                string resultDir = Path.Combine(SearchDirectory, dbName);
                if (!Directory.Exists(resultDir))
                    throw new InvalidDataException();

                string dbPath = DatabaseNameToPathLookup[dbName];
                
                // Optimize: Use EnumerateFiles with patterns to avoid allocating full file list
                string? psmPath = Directory.EnumerateFiles(resultDir, "*_AllPSMs.psmtsv").FirstOrDefault();
                string? peptidePath = Directory.EnumerateFiles(resultDir, "*_AllPeptides.psmtsv").FirstOrDefault();
                string? proteinGroupPath = Directory.EnumerateFiles(resultDir, "*_AllProteinGroups.tsv").FirstOrDefault();

                if (psmPath == null || peptidePath == null)
                    return;

                int peptideCount = GetPeptideCount(dbPath, CommonParameters.DigestionParams, variableMods, fixedMods);

                TransientDatabaseSearchResults results = dbResult.Value;
                ExtendedTransientDatabaseSearchResults exResults = new()
                {
                    DatabaseName = results.DatabaseName,
                    TotalProteins = results.TotalProteins,
                    TransientProteinCount = results.TransientProteinCount,
                    PsmTargets = results.TargetPsmsAtQValueThreshold,
                    PeptideTargets = results.TargetPeptidesAtQValueThreshold,
                    ProteinGroupTargets = results.TargetProteinGroupsAtQValueThreshold,
                    TransientPeptideCount = peptideCount
                };

                DecorateWithScores(psmPath, false, exResults);
                DecorateWithScores(peptidePath, true, exResults);
                DecorateWithScores_ProteinGroups(proteinGroupPath, exResults);

                processedCache.AddAndWrite(exResults);
                Interlocked.Increment(ref count);
            });
    }

    private static void DecorateWithScores(string inputFile, bool isPeptideLevel, ExtendedTransientDatabaseSearchResults result)
    {
        // Optimize: Use smaller buffer for small files to reduce memory overhead
        using var fileStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096);
        using var reader = new StreamReader(fileStream);
        var header = reader.ReadLine();
        if (header == null)
            return;

        // Optimize: Use stackalloc for small arrays (header splits) when possible
        Span<Range> headerRanges = stackalloc Range[56];
        var headerSpan = header.AsSpan();
        int headerCount = headerSpan.Split(headerRanges, '\t');
        
        int scoreIndex = FindColumnIndex(headerSpan, headerRanges, headerCount, ScoreColumn);
        int qIndex = FindColumnIndex(headerSpan, headerRanges, headerCount, QColumn);
        int tdcIndex = FindColumnIndex(headerSpan, headerRanges, headerCount, TDCColumn);
        int orgIndex = FindColumnIndex(headerSpan, headerRanges, headerCount, OrganismColumn);
        
        if (scoreIndex == -1 || qIndex == -1 || tdcIndex == -1 || orgIndex == -1)
            return;

        // Optimize: Smaller initial capacity for few rows scenario
        List<double> targetScores = new List<double>(capacity: SmallFileInitialCapacity);
        List<double> decoyScores = new List<double>(capacity: SmallFileInitialCapacity);

        // Cache string constant as ReadOnlySpan for comparison
        ReadOnlySpan<char> homoSapiensSpan = "Homo sapiens";

        Span<Range> ranges = stackalloc Range[56];
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var lineSpan = line.AsSpan();
            int splitCount = lineSpan.Split(ranges, '\t');

            if (splitCount <= Math.Max(Math.Max(scoreIndex, qIndex), Math.Max(tdcIndex, orgIndex)))
                continue;

            // Not confident hit
            var qValueSpan = lineSpan[ranges[qIndex]];
            if (!double.TryParse(qValueSpan, out double qValue) || qValue > QCutoff)
                continue;

            // Optimize: Use AsSpan for string comparisons to avoid allocations
            var orgSpan = lineSpan[ranges[orgIndex]];
            bool isHumanAmbiguous = orgSpan.Contains(homoSapiensSpan, StringComparison.Ordinal);

            var scoreSpan = lineSpan[ranges[scoreIndex]];
            if (!double.TryParse(scoreSpan, out double score))
                return;

            // Optimize: Use span-based character search
            var tdcSpan = lineSpan[ranges[tdcIndex]];
            bool isDecoy = tdcSpan.Contains('D');

            // Decoys
            if (isDecoy)
            {
                if (isPeptideLevel)
                {
                    result.PeptideBacterialDecoys++;
                    if (!isHumanAmbiguous)
                        result.PeptideBacterialUnambiguousDecoys++;
                }
                else
                {
                    result.PsmBacterialDecoys++;
                    if (!isHumanAmbiguous)
                        result.PsmBacterialUnambiguousDecoys++;
                }

                if (!isHumanAmbiguous)
                    decoyScores.Add(score);
            }
            // Targets
            else
            {
                if (isPeptideLevel)
                {
                    result.PeptideBacterialTargets++;
                    if (!isHumanAmbiguous)
                        result.PeptideBacterialUnambiguousTargets++;
                }
                else
                {
                    result.PsmBacterialTargets++;
                    if (!isHumanAmbiguous)
                        result.PsmBacterialUnambiguousTargets++;
                }

                if (!isHumanAmbiguous)
                    targetScores.Add(score);
            }
        }

        if (isPeptideLevel)
        {
            result.PeptideBacterialUnambiguousTargetScores = targetScores.ToArray();
            result.PeptideBacterialUnambiguousDecoyScores = decoyScores.ToArray();
        }
        else
        {
            result.PsmBacterialUnambiguousTargetScores = targetScores.ToArray();
            result.PsmBacterialUnambiguousDecoyScores = decoyScores.ToArray();
        }
    }

    private static int FindColumnIndex(ReadOnlySpan<char> headerSpan, Span<Range> ranges, int count, string columnName)
    {
        ReadOnlySpan<char> columnSpan = columnName.AsSpan();
        for (int i = 0; i < count; i++)
        {
            if (headerSpan[ranges[i]].Equals(columnSpan, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }
    
    private static void DecorateWithScores_ProteinGroups(string? inputFile, ExtendedTransientDatabaseSearchResults result)
    {
        if (inputFile is null)
            return;

        // Optimize: Use smaller buffer for small files
        using var fileStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096);
        using var reader = new StreamReader(fileStream);
        var header = reader.ReadLine();
        if (header == null)
            return;

        // Optimize: Use stackalloc for header parsing
        Span<Range> headerRanges = stackalloc Range[50];
        var headerSpan = header.AsSpan();
        int headerCount = headerSpan.Split(headerRanges, '\t');
        
        int qIndex = FindColumnIndex(headerSpan, headerRanges, headerCount, "Protein QValue");
        int tdcIndex = FindColumnIndex(headerSpan, headerRanges, headerCount, "Protein Decoy/Contaminant/Target");
        int orgIndex = FindColumnIndex(headerSpan, headerRanges, headerCount, "Organism");
        int peptidesIndex = FindColumnIndex(headerSpan, headerRanges, headerCount, "Number of Peptides");
        
        if (qIndex == -1 || tdcIndex == -1 || orgIndex == -1)
            return;

        // Cache string constant as ReadOnlySpan for comparison
        ReadOnlySpan<char> homoSapiensSpan = "Homo sapiens";

        Span<Range> ranges = stackalloc Range[50];
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var lineSpan = line.AsSpan();
            int splitCount = lineSpan.Split(ranges, '\t');

            if (splitCount <= Math.Max(Math.Max(qIndex, tdcIndex), orgIndex))
                continue;

            // Not confident hit
            var qValueSpan = lineSpan[ranges[qIndex]];
            if (!double.TryParse(qValueSpan, out double qValue) || qValue > QCutoff)
                continue;

            // fewer than 2 peptides
            if (peptidesIndex != -1 && peptidesIndex < splitCount)
            {
                var pepCountSpan = lineSpan[ranges[peptidesIndex]];
                if (int.TryParse(pepCountSpan, out int pepCount) && pepCount < 2)
                    continue;
            }

            // Optimize: Use span-based comparisons
            var orgSpan = lineSpan[ranges[orgIndex]];
            bool isHumanAmbiguous = orgSpan.Contains(homoSapiensSpan, StringComparison.Ordinal);
            
            var tdcSpan = lineSpan[ranges[tdcIndex]];
            bool isDecoy = tdcSpan.Contains('D');

            // Decoys
            if (isDecoy)
            {
                result.ProteinGroupBacterialDecoys++;
                if (!isHumanAmbiguous)
                    result.ProteinGroupBacterialUnambiguousDecoys++;
            }
            // Targets
            else
            {
                result.ProteinGroupBacterialTargets++;
                if (!isHumanAmbiguous)
                    result.ProteinGroupBacterialUnambiguousTargets++;
            }
        }
    }

    private int GetPeptideCount(string dbPath, IDigestionParams dig, List<Modification> variableModifications, List<Modification> fixedModifications)
    {
        var prots = ProteinDbLoader.LoadProteinFasta(dbPath, true, DecoyType.None, false, out _);
        var count = prots.Sum(p => p.Digest(dig, fixedModifications, variableModifications).Count());
        return count;
    }

    private void LoadModifications(out List<Modification> variableModifications, out List<Modification> fixedModifications, out List<string> localizableModificationTypes)
    {
        switch (GlobalVariables.AnalyteType)
        {
            case AnalyteType.Oligo:
                variableModifications = GlobalVariables.AllRnaModsKnown
                    .Where(b => CommonParameters.ListOfModsVariable.Contains((b.ModificationType, b.IdWithMotif)))
                    .ToList();
                fixedModifications = GlobalVariables.AllRnaModsKnown
                    .Where(b => CommonParameters.ListOfModsFixed.Contains((b.ModificationType, b.IdWithMotif)))
                    .ToList();
                localizableModificationTypes = GlobalVariables.AllRnaModTypesKnown.ToList();
                break;

            case AnalyteType.Peptide:
            case AnalyteType.Proteoform:
            default:
                variableModifications = GlobalVariables.AllModsKnown
                    .Where(b => CommonParameters.ListOfModsVariable.Contains((b.ModificationType, b.IdWithMotif)))
                    .ToList();
                fixedModifications = GlobalVariables.AllModsKnown
                    .Where(b => CommonParameters.ListOfModsFixed.Contains((b.ModificationType, b.IdWithMotif)))
                    .ToList();
                localizableModificationTypes = GlobalVariables.AllModTypesKnown.ToList();
                break;
        }

        var recognizedVariable = variableModifications.Select(p => p.IdWithMotif);
        var recognizedFixed = fixedModifications.Select(p => p.IdWithMotif);
        var unknownMods = CommonParameters.ListOfModsVariable.Select(p => p.Item2).Except(recognizedVariable).ToList();
        unknownMods.AddRange(CommonParameters.ListOfModsFixed.Select(p => p.Item2).Except(recognizedFixed));
    }
}