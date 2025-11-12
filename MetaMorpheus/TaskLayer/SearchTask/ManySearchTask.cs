#nullable enable
using CsvHelper;
using EngineLayer;
using EngineLayer.ClassicSearch;
using EngineLayer.DatabaseLoading;
using EngineLayer.FdrAnalysis;
using EngineLayer.SpectrumMatch;
using MassSpectrometry;
using Nett;
using Omics;
using Omics.BioPolymer;
using Omics.Digestion;
using Omics.Fragmentation;
using Omics.Modifications;
using Proteomics;
using Proteomics.ProteolyticDigestion;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper.Configuration;
using Omics.SpectrumMatch;
using ProteinGroup = EngineLayer.ProteinGroup;

namespace TaskLayer;
public class ManySearchTask : SearchTask
{
    private readonly object _progressLock = new object();
    private int _completedDatabases = 0;
    private readonly ConcurrentBag<Task> _writeTasks = new(); 
    private DatabaseResultsCache? _resultsCache;

    public ManySearchTask() : base(MyTask.ManySearch)
    {
        // Initialize with appropriate defaults
        SearchParameters = new ManySearchParameters();
        CommonParameters = new(taskDescriptor: "ManySearchTask");
    }

    public ManySearchTask(List<DbForTask> transientDatabases) : base(MyTask.ManySearch)
    {
        // Initialize with appropriate defaults
        SearchParameters = new ManySearchParameters()
        {
            TransientDatabases = transientDatabases
        };
        CommonParameters = new(taskDescriptor: "ManySearchTask");
    }

    // Rename the TOML section for ManySearchParameters to avoid conflicts
    [TomlIgnore]
    public override SearchParameters SearchParameters
    {
        get => ManySearchParameters;
        set
        {
            if (value is ManySearchParameters msp)
                ManySearchParameters = msp;
            else
            {
                // If someone tries to set a base SearchParameters, convert it
                ManySearchParameters = new ManySearchParameters(SearchParameters);
            }
        }
    }

    public ManySearchParameters ManySearchParameters { get; set; } = new();

    protected override MyTaskResults RunSpecific(string OutputFolder,
        List<DbForTask> dbFilenameList, List<string> currentRawFileList,
        string taskId, FileSpecificParameters[] fileSettingsList)
    {
        var manySearchParameters = (ManySearchParameters)SearchParameters;
        MyTaskResults = new MyTaskResults(this);
        MyFileManager myFileManager = new MyFileManager(SearchParameters.DisposeOfFileWhenDone);
        int totalAvailableThreads = Environment.ProcessorCount;
        int databaseParallelism = Math.Min(manySearchParameters.MaxSearchesInParallel,
            manySearchParameters.TransientDatabases.Count);
        int threadsPerDatabase = Math.Max(1, totalAvailableThreads / databaseParallelism);
        CommonParameters.MaxThreadsToUsePerFile = threadsPerDatabase;

        // Initialize results cache
        _resultsCache = new DatabaseResultsCache(Path.Combine(OutputFolder, "ManySearchSummary.csv"));

        Status("Loading modifications...", taskId);
        
        // 1. Load modifications once
        LoadModifications(taskId, out var variableModifications,
            out var fixedModifications, out var localizableModificationTypes);

        Status("Loading base database(s)...", taskId);
        
        // 2. Load base database(s) once
        var baseDbLoader = new DatabaseLoadingEngine(CommonParameters,
            FileSpecificParameters, [taskId], dbFilenameList, taskId,
            SearchParameters.DecoyType, SearchParameters.SearchTarget,
            localizableModificationTypes);
        var baseProteins = (baseDbLoader.Run() as DatabaseLoadingEngineResults)!.BioPolymers
            .Select(p => new CachedBioPolymer(p))
            .Cast<IBioPolymer>()
            .ToList();

        Status($"Loaded {baseProteins.Count} base proteins", taskId);
        
        // 3. Load all spectra files once and store in memory
        Status("Loading spectra files...", taskId);
        ConcurrentDictionary<string, Ms2ScanWithSpecificMass[]> loadedSpectraByFile = new();
        int totalMs2Scans = 0;

        int specLoadingProgress = 0;
        var specLoadingNestedIds = new List<string> { taskId, "Spectra Loading" };
        Status("Loading spectra files...", specLoadingNestedIds);
        Parallel.ForEach(currentRawFileList,
            new ParallelOptions { MaxDegreeOfParallelism = manySearchParameters.MaxSearchesInParallel },
            rawFile =>
            {
                var fileParams = SetAllFileSpecificCommonParams(CommonParameters,
                    fileSettingsList[currentRawFileList.IndexOf(rawFile)]);
                var msDataFile = myFileManager.LoadFile(rawFile, fileParams);
                var ms2Scans = GetMs2Scans(msDataFile, rawFile, fileParams)
                    .OrderBy(b => b.PrecursorMass).ToArray();
                loadedSpectraByFile.AddOrUpdate(rawFile, ms2Scans, (key, oldValue) => ms2Scans);
                Interlocked.Add(ref totalMs2Scans, ms2Scans.Length);
                myFileManager.DoneWithFile(rawFile);

                lock (_progressLock)
                {
                    ReportProgress(new ProgressEventArgs(
                        (int)(Interlocked.Increment(ref specLoadingProgress) / (double)currentRawFileList.Count * 100),
                        $"Loaded {Path.GetFileName(rawFile)}",
                        specLoadingNestedIds));
                }
            });

        ReportProgress(new ProgressEventArgs(100, $"Finished Loading spectra files.", specLoadingNestedIds));
        Status($"Finished Loading {currentRawFileList.Count} spectra files.", taskId);

        // Write prose for base settings
        ProseCreatedWhileRunning.Append($"Base database contained {baseProteins.Count(p => !p.IsDecoy)} non-decoy protein entries. ");
        ProseCreatedWhileRunning.Append($"Searching {manySearchParameters.TransientDatabases.Count} transient databases against {currentRawFileList.Count} spectra files. ");

        // Track results across all databases
        var databaseResults = new ConcurrentDictionary<string, DatabaseSearchResults>();
        
        // Load cached results
        var cachedResults = _resultsCache.LoadCachedResults();
        foreach (var result in cachedResults)
        {
            databaseResults[result.DatabaseName] = result;
        }
        
        int totalDatabases = manySearchParameters.TransientDatabases.Count;
        _completedDatabases = cachedResults.Count;

        if (_completedDatabases > 0)
        {
            Status($"Loaded {_completedDatabases} cached database results", taskId);
        }

        Status($"Starting search of {totalDatabases} transient databases...", taskId);

        // 4. Loop through each transient database
        Parallel.ForEach(manySearchParameters.TransientDatabases,
            new ParallelOptions { MaxDegreeOfParallelism = databaseParallelism },
            transientDbPath =>
            {
                if (GlobalVariables.StopLoops)
                    return;

                string dbName = Path.GetFileNameWithoutExtension(transientDbPath.FilePath);
                string dbOutputFolder = Path.Combine(OutputFolder, dbName);
                List<string> nestedIds = [taskId, dbName];

                Status($"Processing {dbName}...", nestedIds);

                // 4a. Check if output already exists and is complete
                if (_resultsCache.HasResult(dbName))
                {
                    if (manySearchParameters.OverwriteTransientSearchOutputs)
                    {
                        Status($"Overwriting existing results for {dbName}...", nestedIds);
                    if (Directory.Exists(dbOutputFolder))
                    {
                        Directory.Delete(dbOutputFolder, true);
                    }
                        Directory.CreateDirectory(dbOutputFolder);
                    }
                    else
                    {
                        Status($"Skipping {dbName} - results already exist in cache", nestedIds);
                        lock (_progressLock)
                        {
                            ReportProgress(new ProgressEventArgs(
                                (int)((_completedDatabases / (double)totalDatabases) * 100),
                                $"Completed {_completedDatabases}/{totalDatabases} databases",
                                new List<string> { taskId }));
                        }

                        return; // Skip to next transient database
                    }
                }
                
                if (!Directory.Exists(dbOutputFolder))
                    Directory.CreateDirectory(dbOutputFolder);

                Status($"Loading transient database {dbName}...", nestedIds);

                // 4b. Load transient database
                var transientDbList = new List<DbForTask> { transientDbPath };
                var transientDbLoader = new DatabaseLoadingEngine(CommonParameters,
                    FileSpecificParameters, nestedIds, transientDbList, taskId,
                    SearchParameters.DecoyType, SearchParameters.SearchTarget,
                    localizableModificationTypes);
                var transientProteins = (transientDbLoader.Run() as DatabaseLoadingEngineResults)!.BioPolymers;

                Status($"Loaded {transientProteins.Count} proteins from {dbName}", nestedIds);
                if (GlobalVariables.StopLoops)
                    return;

                // Create HashSet of transient protein accessions for later filtering
                var transientProteinAccessions = new HashSet<string>(
                    transientProteins.Select(p => p.Accession));

                // 4c. Combine base + transient proteins
                var combinedProteins = new List<IBioPolymer>(baseProteins);
                combinedProteins.AddRange(transientProteins);

                Status($"Searching {dbName} ({combinedProteins.Count} total proteins)...", nestedIds);

                // 4d. Search each spectra file with combined database
                var arrayOfMs2Scans = loadedSpectraByFile
                .SelectMany(p => p.Value)
                .OrderBy(b => b.PrecursorMass)
                .ToArray();
                SpectralMatch[] psmArray = new SpectralMatch[arrayOfMs2Scans.Length];

                var massDiffAcceptor = GetMassDiffAcceptor(
                    CommonParameters.PrecursorMassTolerance,
                    SearchParameters.MassDiffAcceptorType,
                    SearchParameters.CustomMdac);

                // Run the classic search engine
                var searchEngine = new ClassicSearchEngine(
                    psmArray, arrayOfMs2Scans, variableModifications,
                    fixedModifications, SearchParameters.SilacLabels,
                    SearchParameters.StartTurnoverLabel, SearchParameters.EndTurnoverLabel,
                    combinedProteins, massDiffAcceptor, CommonParameters,
                    FileSpecificParameters, null, nestedIds,
                    false, false);

                searchEngine.Run();
                ReportProgress(new(100, "Finished Classic Search...", nestedIds));
                

                Status($"Performing post-search analysis for {dbName}...", nestedIds); 

                // 4e. Perform post-search analysis for this database
                var dbResults = PerformPostSearchAnalysisAsync(psmArray.ToList(), dbOutputFolder, nestedIds,
                    dbName, combinedProteins.Count, transientProteinAccessions);

                // 4f. Cleanup transient proteins to free memory
                transientProteins.Clear();
                transientProteins = null;

                // Update progress
                lock (_progressLock)
                {
                    _completedDatabases++;
                    ReportProgress(new ProgressEventArgs(
                        (int)((_completedDatabases / (double)totalDatabases) * 100),
                        $"Completed {_completedDatabases}/{totalDatabases} databases",
                        new List<string> { taskId }));
                }

                var result = dbResults.Result;
                databaseResults[dbName] = result;
                
                // Write result to CSV cache immediately
                _resultsCache.WriteResult(result);

                // Compress the output folder if requested
                if (SearchParameters.CompressIndividualFiles)
                {
                    Status($"Compressing output for {dbName}...", nestedIds);
                    CompressTransientDatabaseOutput(dbOutputFolder);
                }

                ReportProgress(new(100, $"Finished {dbName}", nestedIds));
            });

        // Wait for all async write operations to complete before writing summary
        Task.WaitAll(_writeTasks.ToArray());

        Status("All database searches complete. Writing summary results...", taskId);

        // Write comprehensive results summary
        WriteGlobalResultsText(databaseResults, OutputFolder, taskId, totalMs2Scans, currentRawFileList.Count);

        Status("Many search task complete!", taskId);
        
        return MyTaskResults;
    }

    private async Task<DatabaseSearchResults> PerformPostSearchAnalysisAsync(List<SpectralMatch> allPsms, string outputFolder, 
        List<string> nestedIds, string dbName, int totalProteins, HashSet<string> transientProteinAccessions)
    {
        // Filter PSMs to keep only best per (file, scan, mass)
        // Create deep copies of the data structures that will be written to avoid race conditions
        // This ensures thread safety when multiple databases are being processed simultaneously
        allPsms = allPsms.Where(p => p is not null)
            .Select(p => {
                p.ResolveAllAmbiguities(); 
                return p;
            }).OrderByDescending(b => b)
            .GroupBy(b => (b.FullFilePath, b.ScanNumber, b.BioPolymerWithSetModsMonoisotopicMass))
            .Select(b => b.First()).ToList();

        int numNotches = GetNumNotches(SearchParameters.MassDiffAcceptorType, SearchParameters.CustomMdac);

        // Minimal FDR analysis - modify PSMs in place
        var fdrEngine = new FdrAnalysisEngine(
            allPsms, numNotches, CommonParameters,
            FileSpecificParameters, nestedIds, "PSM", false, outputFolder);
        fdrEngine.Run();

        // Disambiguate - modify PSMs in place
        var disambiguationEngine = new DisambiguationEngine(
            allPsms, CommonParameters, FileSpecificParameters, nestedIds);
        disambiguationEngine.Run();

        List<ProteinGroup>? proteinGroups = null;
        if (SearchParameters.DoParsimony)
        {
            Status($"Performing parsimony for {dbName}...", nestedIds);
            
            var psmForParsimony = FilteredPsms.Filter(allPsms,
                commonParams: CommonParameters,
                includeDecoys: true,
                includeContaminants: true,
                includeAmbiguous: false,
                includeHighQValuePsms: false);

            ProteinParsimonyResults proteinAnalysisResults = (ProteinParsimonyResults)new ProteinParsimonyEngine(
                psmForParsimony.FilteredPsmsList, SearchParameters.ModPeptidesAreDifferent, 
                CommonParameters, FileSpecificParameters, nestedIds).Run();

            ProteinScoringAndFdrResults proteinScoringAndFdrResults = (ProteinScoringAndFdrResults)new ProteinScoringAndFdrEngine(
                proteinAnalysisResults.ProteinGroups, psmForParsimony.FilteredPsmsList,
                SearchParameters.NoOneHitWonders, SearchParameters.ModPeptidesAreDifferent, 
                true, CommonParameters, FileSpecificParameters, nestedIds).Run();
            
            proteinGroups = proteinScoringAndFdrResults.SortedAndScoredProteinGroups;
        }

        Status($"Writing results for {dbName}...", nestedIds);

        // Filter PSMs for writing to file
        var psmsForPsmResults = FilteredPsms.Filter(allPsms,
            CommonParameters,
            includeDecoys: SearchParameters.WriteDecoys,
            includeContaminants: SearchParameters.WriteContaminants,
            includeAmbiguous: true,
            includeHighQValuePsms: SearchParameters.WriteHighQValuePsms);

        var transientPsms = FilterToTransientDatabaseOnly(
            psmsForPsmResults.FilteredPsmsList, transientProteinAccessions).ToList();

        // Write PSMs to file
        _writeTasks.Add(Task.Run(async () =>
        {
            if (SearchParameters.WriteIndividualFiles)
            {
                string psmFile = Path.Combine(outputFolder,
                    $"All{GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
                await WritePsmsToTsvAsync(psmsForPsmResults.OrderByDescending(p => p), psmFile,
                    SearchParameters.ModsToWriteSelection, false);
                FinishedWritingFile(psmFile, nestedIds);
            }

            string transientPsmFile = Path.Combine(outputFolder, $"{dbName}_All{GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
            await WritePsmsToTsvAsync(transientPsms, transientPsmFile, SearchParameters.ModsToWriteSelection, false);
            FinishedWritingFile(transientPsmFile, nestedIds);
        }));

        // Filter PSMs for peptide results
        var peptidesForPeptideResults = FilteredPsms.Filter(allPsms,
            CommonParameters,
            includeDecoys: SearchParameters.WriteDecoys,
            includeContaminants: SearchParameters.WriteContaminants,
            includeAmbiguous: true,
            includeHighQValuePsms: SearchParameters.WriteHighQValuePsms,
            filterAtPeptideLevel: true);

        var transientPeptides = FilterToTransientDatabaseOnly(
            peptidesForPeptideResults.FilteredPsmsList, transientProteinAccessions).ToList();

        // Write peptides to file
        _writeTasks.Add(Task.Run(async () =>
        {
            if (SearchParameters.WriteIndividualFiles)
            {
                string peptideFile = Path.Combine(outputFolder,
                    $"All{GlobalVariables.AnalyteType}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
                await WritePsmsToTsvAsync(peptidesForPeptideResults, peptideFile, SearchParameters.ModsToWriteSelection,
                    true);
                FinishedWritingFile(peptideFile, nestedIds);
            }

            string transientPeptideFile = Path.Combine(outputFolder, $"{dbName}_All{GlobalVariables.AnalyteType}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
            await WritePsmsToTsvAsync(transientPeptides, transientPeptideFile, SearchParameters.ModsToWriteSelection, true);
            FinishedWritingFile(transientPeptideFile, nestedIds);
        }));

        var results = new DatabaseSearchResults
        {
            DatabaseName = dbName,
            TotalProteins = totalProteins,
            TransientProteinCount = transientProteinAccessions.Count,
            TargetPsmsAtQValueThreshold = psmsForPsmResults.TargetPsmsAboveThreshold,
            TargetPsmsFromTransientDb = transientPsms.Count(p => !p.IsDecoy),
            TargetPsmsFromTransientDbAtQValueThreshold = transientPsms.Count(p => !p.IsDecoy && p.GetFdrInfo(false)!.QValue <= CommonParameters.QValueThreshold),
            TargetPeptidesAtQValueThreshold = peptidesForPeptideResults.TargetPsmsAboveThreshold,
            TargetPeptidesFromTransientDb = transientPeptides.Count(p => !p.IsDecoy),
            TargetPeptidesFromTransientDbAtQValueThreshold = transientPeptides.Count(p => !p.IsDecoy && p.PeptideFdrInfo.QValue <= CommonParameters.QValueThreshold)
        };

        if (proteinGroups is not null)
        {
            proteinGroups.ForEach(x => x.GetIdentifiedPeptidesOutput(SearchParameters.SilacLabels));

            results.TargetProteinGroupsAtQValueThreshold = proteinGroups.Count(p => p.QValue <= CommonParameters.QValueThreshold && !p.IsDecoy);
            
            // Count protein groups that contain at least one transient database protein
            var transientProteinGroups = FilterProteinGroupsToTransientDatabaseOnly(proteinGroups, transientProteinAccessions).ToList();
            results.TargetProteinGroupsFromTransientDb = transientProteinGroups.Count(p => !p.IsDecoy);
            results.TargetProteinGroupsFromTransientDbAtQValueThreshold = transientProteinGroups.Count(p => p.QValue <= CommonParameters.QValueThreshold && !p.IsDecoy);

            // Write protein groups to file
            _writeTasks.Add(Task.Run(async () =>
            {
                if (SearchParameters.WriteIndividualFiles)
                {
                    string proteinFile = Path.Combine(outputFolder,
                        $"All{GlobalVariables.AnalyteType.GetBioPolymerLabel()}Groups.tsv");
                    await WriteProteinGroupsToTsvAsync(proteinGroups, proteinFile);
                    FinishedWritingFile(proteinFile, nestedIds);
                }

                string transientProteinFile = Path.Combine(outputFolder,
                    $"{dbName}_All{GlobalVariables.AnalyteType.GetBioPolymerLabel()}Groups.tsv");
                await WriteProteinGroupsToTsvAsync(transientProteinGroups, transientProteinFile);
                FinishedWritingFile(transientProteinFile, nestedIds);
            }));
        }

        if (SearchParameters.WriteSpectralLibrary)
        {
            // Write spectral library
            _writeTasks.Add(Task.Run(async () =>
            {
                string spectralLibraryPath = Path.Combine(outputFolder, $"{dbName}_SpectralLibrary.msp");
                await WriteSpectralLibraryAsync(psmsForPsmResults.OrderByDescending(p => p), spectralLibraryPath);
                FinishedWritingFile(spectralLibraryPath, nestedIds);
            }));
        }

        // Write individual results.txt for this database
        _writeTasks.Add(Task.Run(async () => await WriteIndividualDatabaseResultsTextAsync(results, outputFolder, nestedIds)));

        // TODO: Consider if this will slow us down 
        return results;
    }

    #region Result Writing

    private async Task WriteIndividualDatabaseResultsTextAsync(DatabaseSearchResults results, string outputFolder, List<string> nestedIds)
    {
        var resultsPath = Path.Combine(outputFolder, "results.txt");
        await results.WriteToTextFileAsync(resultsPath, CommonParameters.QValueThreshold, SearchParameters.DoParsimony);
        FinishedWritingFile(resultsPath, nestedIds);
    }

    private void WriteGlobalResultsText(ConcurrentDictionary<string, DatabaseSearchResults> databaseResults,
        string outputFolder, string taskId, int totalMs2Scans, int numFiles)
    {
        // Global Summary Text File
        var summaryPath = Path.Combine(outputFolder, "ManySearchSummary.txt");

        using (StreamWriter file = new StreamWriter(summaryPath))
        {
            file.WriteLine("=== Many Search Task Summary ===");
            file.WriteLine();
            file.WriteLine($"Spectra files analyzed: {numFiles}");
            file.WriteLine($"Total MS2 scans: {totalMs2Scans}");
            file.WriteLine($"Transient databases searched: {databaseResults.Count}");
            file.WriteLine();
            file.WriteLine("=== Results by Database ===");
            file.WriteLine();

            foreach (var kvp in databaseResults.OrderBy(x => x.Key))
            {
                var dbName = kvp.Key;
                var results = kvp.Value;

                file.WriteLine($"Database: {dbName}");
                file.WriteLine($"  Total proteins: {results.TotalProteins}");
                file.WriteLine($"  Transient proteins: {results.TransientProteinCount}");
                file.WriteLine($"  Target PSMs (FDR {CommonParameters.QValueThreshold * 100}%): {results.TargetPsmsAtQValueThreshold}");
                file.WriteLine($"  Target PSMs from transient DB: {results.TargetPsmsFromTransientDb}");
                file.WriteLine($"  Target PSMs from transient DB (FDR {CommonParameters.QValueThreshold * 100}%): {results.TargetPsmsFromTransientDbAtQValueThreshold}");
                file.WriteLine($"  Target peptides (FDR {CommonParameters.QValueThreshold * 100}%): {results.TargetPeptidesAtQValueThreshold}");
                file.WriteLine($"  Target peptides from transient DB: {results.TargetPeptidesFromTransientDb}");
                file.WriteLine($"  Target peptides from transient DB (FDR {CommonParameters.QValueThreshold * 100}%): {results.TargetPeptidesFromTransientDbAtQValueThreshold}");

                if (SearchParameters.DoParsimony)
                {
                    file.WriteLine($"  Target protein groups (FDR {CommonParameters.QValueThreshold * 100}%): {results.TargetProteinGroupsAtQValueThreshold}");
                    file.WriteLine($"  Target protein groups with transient DB proteins: {results.TargetProteinGroupsFromTransientDb}");
                    file.WriteLine($"  Target protein groups with transient DB proteins (FDR {CommonParameters.QValueThreshold * 100}%): {results.TargetProteinGroupsFromTransientDbAtQValueThreshold}");
                }
                file.WriteLine();
            }

            file.WriteLine("=== Aggregate Statistics ===");
            file.WriteLine($"Total Proteins Searched: {databaseResults.Values.Sum(p => p.TransientProteinCount) + databaseResults.First().Value.TotalProteins - databaseResults.First().Value.TransientProteinCount}");
            file.WriteLine($"Total PSMs identified: {databaseResults.Values.Sum(r => r.TargetPsmsAtQValueThreshold)}");
            file.WriteLine($"Total target PSMs at {CommonParameters.QValueThreshold * 100}% FDR: {databaseResults.Values.Sum(r => r.TargetPsmsAtQValueThreshold)}");
            file.WriteLine($"Total target PSMs from transient DBs: {databaseResults.Values.Sum(r => r.TargetPsmsFromTransientDb)}");
            file.WriteLine($"Total target PSMs from transient DBs (FDR {CommonParameters.QValueThreshold * 100}%): {databaseResults.Values.Sum(r => r.TargetPsmsFromTransientDbAtQValueThreshold)}");
            file.WriteLine($"Total target peptides at {CommonParameters.QValueThreshold * 100}% FDR: {databaseResults.Values.Sum(r => r.TargetPeptidesAtQValueThreshold)}");
            file.WriteLine($"Total target peptides from transient DBs: {databaseResults.Values.Sum(r => r.TargetPeptidesFromTransientDb)}");
            file.WriteLine($"Total target peptides from transient DBs (FDR {CommonParameters.QValueThreshold * 100}%): {databaseResults.Values.Sum(r => r.TargetPeptidesFromTransientDbAtQValueThreshold)}");


            if (SearchParameters.DoParsimony)
            {
                file.WriteLine($"Total Protein Groups (FDR {CommonParameters.QValueThreshold * 100}%): {databaseResults.Values.Sum(r => r.TargetProteinGroupsAtQValueThreshold)}");
                file.WriteLine($"Total Protein Groups with transient DB proteins: {databaseResults.Values.Sum(r => r.TargetProteinGroupsFromTransientDb)}");
                file.WriteLine($"Total Protein Groups with transient DB proteins (FDR {CommonParameters.QValueThreshold * 100}%): {databaseResults.Values.Sum(r => r.TargetProteinGroupsFromTransientDbAtQValueThreshold)}");
            }
        }

        FinishedWritingFile(summaryPath, new List<string> { taskId });

        // Global Summary CSV file
        var csvPath = Path.Combine(outputFolder, "ManySearchSummary.csv");
        using var csvWriter = new CsvWriter(new StreamWriter(csvPath), System.Globalization.CultureInfo.InvariantCulture);
        csvWriter.WriteHeader<DatabaseSearchResults>();
        csvWriter.NextRecord();
        foreach(var result in databaseResults.Values.OrderByDescending(x => x.NormalizedTransientPeptideCount))
        {
            csvWriter.WriteRecord(result);
            csvWriter.NextRecord();
        }

        // Add summary to task results
        MyTaskResults.AddTaskSummaryText($"Searched {databaseResults.Count} transient databases against {numFiles} spectra files.");
        MyTaskResults.AddTaskSummaryText($"Total target PSMs at {CommonParameters.QValueThreshold * 100}% FDR: {databaseResults.Values.Sum(r => r.TargetPsmsAtQValueThreshold)}");
        MyTaskResults.AddTaskSummaryText($"Target PSMs from transient databases: {databaseResults.Values.Sum(r => r.TargetPsmsFromTransientDb)}");
        MyTaskResults.AddTaskSummaryText($"Target PSMs from transient databases at {CommonParameters.QValueThreshold * 100}% FDR: {databaseResults.Values.Sum(r => r.TargetPsmsFromTransientDbAtQValueThreshold)}");

        MyTaskResults.AddTaskSummaryText($"Total target peptides at {CommonParameters.QValueThreshold * 100}% FDR: {databaseResults.Values.Sum(r => r.TargetPeptidesAtQValueThreshold)}");
        MyTaskResults.AddTaskSummaryText($"Target peptides from transient databases: {databaseResults.Values.Sum(r => r.TargetPeptidesFromTransientDb)}");
        MyTaskResults.AddTaskSummaryText($"Target peptides from transient databases at {CommonParameters.QValueThreshold * 100}% FDR: {databaseResults.Values.Sum(r => r.TargetPeptidesFromTransientDbAtQValueThreshold)}");

        if (SearchParameters.DoParsimony)
        {
            MyTaskResults.AddTaskSummaryText($"Total Protein Groups at {CommonParameters.QValueThreshold * 100}% FDR: {databaseResults.Values.Sum(r => r.TargetProteinGroupsAtQValueThreshold)}");
            MyTaskResults.AddTaskSummaryText($"Protein Groups with transient database proteins: {databaseResults.Values.Sum(r => r.TargetProteinGroupsFromTransientDb)}");
            MyTaskResults.AddTaskSummaryText($"Protein Groups with transient database proteins at {CommonParameters.QValueThreshold * 100}% FDR: {databaseResults.Values.Sum(r => r.TargetProteinGroupsFromTransientDbAtQValueThreshold)}");
        }
    }

    private async Task WriteProteinGroupsToTsvAsync(List<ProteinGroup> proteinGroups, string filePath)
    {
        if (proteinGroups != null && proteinGroups.Any())
        {
            double qValueThreshold = Math.Min(CommonParameters.QValueThreshold, CommonParameters.PepQValueThreshold);
            using (StreamWriter output = new StreamWriter(filePath))
            {
                await output.WriteLineAsync(proteinGroups.First().GetTabSeparatedHeader());
                for (int i = 0; i < proteinGroups.Count; i++)
                {
                    if (!SearchParameters.WriteDecoys && proteinGroups[i].IsDecoy ||
                        !SearchParameters.WriteContaminants && proteinGroups[i].IsContaminant ||
                        !SearchParameters.WriteHighQValuePsms && proteinGroups[i].QValue > qValueThreshold)
                    {
                        continue;
                    }
                    else
                    {
                        await output.WriteLineAsync(proteinGroups[i].ToString());
                    }
                }
            }
        }
    }

    private async Task WritePsmsToTsvAsync(IEnumerable<SpectralMatch> psms, string filePath, IReadOnlyDictionary<string, int> modstoWritePruned, bool writePeptideLevelResults = false)
    {
        await using StreamWriter output = new StreamWriter(filePath);
        bool includeOneOverK0Column = psms.Any(p => p.ScanOneOverK0.HasValue);
        await output.WriteLineAsync(SpectralMatch.GetTabSeparatedHeader(includeOneOverK0Column));
        foreach (var psm in psms)
        {
            await output.WriteLineAsync(psm.ToString(modstoWritePruned, writePeptideLevelResults, includeOneOverK0Column));
        }
    }

    private async Task WriteSpectralLibraryAsync(IEnumerable<SpectralMatch> psms, string outFilePath)
    {
        try
        {
            var peptidesForSpectralLibrary = FilteredPsms.Filter(psms,
                CommonParameters,
                includeDecoys: false,
                includeContaminants: false,
                includeAmbiguous: false,
                includeHighQValuePsms: false);

            //group psms by peptide and charge, the psms having same sequence and same charge will be in the same group
            IEnumerable<LibrarySpectrum> spectraLibrary = peptidesForSpectralLibrary.GroupBy(p => (p.FullSequence, p.ScanPrecursorCharge))
                .Select(p => p.MaxBy(q => q.Score))
                .Where(p => p != null)
                .Select(p => new LibrarySpectrum(
                    p!.FullSequence,
                    p.ScanPrecursorMonoisotopicPeakMz,
                    p.ScanPrecursorCharge,
                    p.MatchedFragmentIons,
                    p.ScanRetentionTime));

            await using StreamWriter output = new StreamWriter(outFilePath);
            foreach (var x in spectraLibrary)
            {
                await output.WriteLineAsync(x.ToString());
            }
        }
        catch (Exception e)
        {
            EngineCrashed("SpectralLibraryGeneration", e);
        }
    }

    /// <summary>
    /// Compresses the transient database output folder
    /// </summary>
    private void CompressTransientDatabaseOutput(string outputFolder)
    {
        var directoryInfo = new DirectoryInfo(outputFolder);
        foreach (FileInfo fileToCompress in directoryInfo.GetFiles())
        {
            bool compressed = false;
            int maxRetries = 10;
            int retryCount = 0;
            while (!compressed && retryCount < maxRetries)
            {
                try
                {
                    MyFileManager.CompressFile(fileToCompress);
                    compressed = true;
                }
                catch (IOException ex) when (ex is IOException && (ex.HResult & 0xFFFF) == 32) // ERROR_SHARING_VIOLATION
                {
                    // File is being used by another process, wait and retry
                    Thread.Sleep(1000);
                    retryCount++;
                }
                catch (Exception ex)
                {
                    Warn($"Failed to compress file {fileToCompress.FullName}: {ex.Message}");
                    break;
                }
            }
            if (!compressed)
            {
                Warn($"Could not compress file {fileToCompress.FullName} after {maxRetries} retries.");
            }
        }
    }

    #endregion

    #region Transient Protein Handling

    /// <summary>
    /// Filters spectral matches to only include those that match exclusively to transient database proteins
    /// </summary>
    private IEnumerable<SpectralMatch> FilterToTransientDatabaseOnly(List<SpectralMatch> spectralMatches,
        HashSet<string> transientProteinAccessions)
    {
        return spectralMatches
            .Where(psm => psm.BestMatchingBioPolymersWithSetMods
                .Any(match => transientProteinAccessions.Contains(match.SpecificBioPolymer.Parent.Accession)));
    }

    /// <summary>
    /// Filters protein groups to only include those where all proteins are from the transient database
    /// </summary>
    private IEnumerable<ProteinGroup> FilterProteinGroupsToTransientDatabaseOnly(List<ProteinGroup> proteinGroups,
        HashSet<string> transientProteinAccessions)
    {
        return proteinGroups
            .Where(pg => pg.Proteins.Any(p => transientProteinAccessions.Contains(p.Accession)));
    }


    private bool TransientDbOutputIsFinished(string outputFolder, string dbName, List<string> nestedIds)
    {
        string resultsFile = Path.Combine(outputFolder, dbName, "results.txt");
        if (!File.Exists(resultsFile))
            return false;

        // Check for transient-specific PSMs
        string transientPsmFile = Path.Combine(outputFolder,
            $"{dbName}_All{GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        if (!File.Exists(transientPsmFile))
            return false;

        // Check for transient-specific peptides
        string transientPeptideFile = Path.Combine(outputFolder,
            $"{dbName}_All{GlobalVariables.AnalyteType}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        if (!File.Exists(transientPeptideFile))
            return false;

        // Check for transient-specific protein groups if parsimony is enabled
        if (SearchParameters.DoParsimony)
        {
            string transientProteinFile = Path.Combine(outputFolder,
                $"{dbName}_All{GlobalVariables.AnalyteType.GetBioPolymerLabel()}Groups.tsv");
            if (!File.Exists(transientProteinFile))
                return false;
        }

        return true;
    }

    #endregion

    #region Result Cache Helper

    /// <summary>
    /// Helper class for thread-safe reading and writing of database search results to CSV
    /// </summary>
    private class DatabaseResultsCache
    {
        private readonly string _csvFilePath;
        private readonly object _writeLock = new object();
        private readonly HashSet<string> _completedDatabases = new();
        private readonly object _cacheLock = new object();

        public DatabaseResultsCache(string csvFilePath)
        {
            _csvFilePath = csvFilePath;
        }

        /// <summary>
        /// Loads cached results from the CSV file if it exists
        /// </summary>
        public List<DatabaseSearchResults> LoadCachedResults()
        {
            var results = new List<DatabaseSearchResults>();

            if (!File.Exists(_csvFilePath))
                return results;

            try
            {
                lock (_writeLock)
                {
                    using var reader = new StreamReader(_csvFilePath);
                    using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        HasHeaderRecord = true,
                        MissingFieldFound = null
                    });

                    results = csv.GetRecords<DatabaseSearchResults>().ToList();

                    lock (_cacheLock)
                    {
                        foreach (var result in results)
                        {
                            _completedDatabases.Add(result.DatabaseName);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // If there's an error reading the cache, start fresh
                results.Clear();
                lock (_cacheLock)
                {
                    _completedDatabases.Clear();
                }
            }

            return results;
        }

        /// <summary>
        /// Checks if a database result already exists in cache
        /// </summary>
        public bool HasResult(string databaseName)
        {
            lock (_cacheLock)
            {
                return _completedDatabases.Contains(databaseName);
            }
        }

        /// <summary>
        /// Writes a single result to the CSV file in a thread-safe manner
        /// </summary>
        public void WriteResult(DatabaseSearchResults result)
        {
            lock (_writeLock)
            {
                bool fileExists = File.Exists(_csvFilePath);
                
                using var stream = new FileStream(_csvFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream);
                using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = !fileExists
                });

                // Write header if file is new
                if (!fileExists)
                {
                    csv.WriteHeader<DatabaseSearchResults>();
                    csv.NextRecord();
                }

                csv.WriteRecord(result);
                csv.NextRecord();
                csv.Flush();

                lock (_cacheLock)
                {
                    _completedDatabases.Add(result.DatabaseName);
                }
            }
        }
    }

    #endregion

    private class DatabaseSearchResults
    {
        public string DatabaseName { get; set; } = string.Empty;
        public int TotalProteins { get; set; }
        public int TransientProteinCount { get; set; }

        public int TargetPsmsAtQValueThreshold { get; set; }
        public int TargetPsmsFromTransientDb { get; set; }
        public int TargetPsmsFromTransientDbAtQValueThreshold { get; set; }

        public int TargetPeptidesAtQValueThreshold { get; set; }
        public int TargetPeptidesFromTransientDb { get; set; }
        public int TargetPeptidesFromTransientDbAtQValueThreshold { get; set; }

        public int TargetProteinGroupsAtQValueThreshold { get; set; }
        public int TargetProteinGroupsFromTransientDb { get; set; }
        public int TargetProteinGroupsFromTransientDbAtQValueThreshold { get; set; }

        public double NormalizedTransientPsmCount => TransientProteinCount > 0 ? (double)TargetPsmsFromTransientDbAtQValueThreshold / TransientProteinCount : 0;
        public double NormalizedTransientPeptideCount => TransientProteinCount > 0 ? (double)TargetPeptidesFromTransientDbAtQValueThreshold / TransientProteinCount : 0;
        public double NormalizedTransientProteinGroupCount => TransientProteinCount > 0 ? (double)TargetProteinGroupsFromTransientDbAtQValueThreshold / TransientProteinCount : 0;

        /// <summary>
        /// Writes the database results to a text file
        /// </summary>
        public async Task WriteToTextFileAsync(string filePath, double qValueThreshold, bool doParsimony)
        {
            await using StreamWriter file = new StreamWriter(filePath);
            await file.WriteLineAsync($"Database: {DatabaseName}");
            await file.WriteLineAsync($"Total proteins in combined database: {TotalProteins}");
            await file.WriteLineAsync($"Total proteins from transient database: {TransientProteinCount}");
            await file.WriteLineAsync();
            await file.WriteLineAsync($"Target PSMs at {qValueThreshold * 100}% FDR: {TargetPsmsAtQValueThreshold}");
            await file.WriteLineAsync($"Target PSMs from transient database: {TargetPsmsFromTransientDb}");
            await file.WriteLineAsync($"Target PSMs from transient database at {qValueThreshold * 100}% FDR: {TargetPsmsFromTransientDbAtQValueThreshold}");
            await file.WriteLineAsync();
            await file.WriteLineAsync($"Target peptides at {qValueThreshold * 100}% FDR: {TargetPeptidesAtQValueThreshold}");
            await file.WriteLineAsync($"Target peptides from transient database: {TargetPeptidesFromTransientDb}");
            await file.WriteLineAsync($"Target peptides from transient database at {qValueThreshold * 100}% FDR: {TargetPeptidesFromTransientDbAtQValueThreshold}");

            if (doParsimony)
            {
                await file.WriteLineAsync();
                await file.WriteLineAsync($"Target protein groups at {qValueThreshold * 100}% FDR: {TargetProteinGroupsAtQValueThreshold}");
                await file.WriteLineAsync($"Target protein groups with transient database proteins: {TargetProteinGroupsFromTransientDb}");
                await file.WriteLineAsync($"Target protein groups with transient database proteins at {qValueThreshold * 100}% FDR: {TargetProteinGroupsFromTransientDbAtQValueThreshold}");
            }
        }
    }
}




