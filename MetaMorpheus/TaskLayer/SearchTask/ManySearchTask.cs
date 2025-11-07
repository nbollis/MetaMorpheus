#nullable enable
using CsvHelper;
using EngineLayer;
using EngineLayer.ClassicSearch;
using EngineLayer.DatabaseLoading;
using EngineLayer.FdrAnalysis;
using EngineLayer.SpectrumMatch;
using Nett;
using Omics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProteinGroup = EngineLayer.ProteinGroup;

namespace TaskLayer;
public class ManySearchTask : SearchTask
{
    private readonly object _progressLock = new object();
    private int _completedDatabases = 0;
    
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
        var baseProteins = (baseDbLoader.Run() as DatabaseLoadingEngineResults)!.BioPolymers;

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

        Status($"Finished Loading spectra files.", specLoadingNestedIds);
        Status($"Finished Loading {currentRawFileList.Count} spectra files.", taskId);

        // Write prose for base settings
        ProseCreatedWhileRunning.Append($"Base database contained {baseProteins.Count(p => !p.IsDecoy)} non-decoy protein entries. ");
        ProseCreatedWhileRunning.Append($"Searching {manySearchParameters.TransientDatabases.Count} transient databases against {currentRawFileList.Count} spectra files. ");

        // Track results across all databases
        var databaseResults = new ConcurrentDictionary<string, DatabaseSearchResults>();
        int totalDatabases = manySearchParameters.TransientDatabases.Count;
        _completedDatabases = 0;

        Status($"Starting search of {totalDatabases} transient databases...", taskId);

        // 4. Loop through each transient database
        Parallel.ForEach(manySearchParameters.TransientDatabases,
            new ParallelOptions { MaxDegreeOfParallelism = manySearchParameters.MaxSearchesInParallel },
            transientDbPath =>
            {
                if (GlobalVariables.StopLoops)
                    return;

                string dbName = Path.GetFileNameWithoutExtension(transientDbPath.FilePath);
                string dbOutputFolder = Path.Combine(OutputFolder, dbName);
                List<string> nestedIds = [taskId, dbName];

                Status($"Processing {dbName}...", nestedIds);

                // 4a. Check if output already exists and is complete
                if (Directory.Exists(dbOutputFolder))
                {
                    bool isComplete = TransientDbOutputIsFinished(OutputFolder, dbName, nestedIds);

                    if (isComplete)
                    {
                        if (manySearchParameters.OverwriteTransientSearchOutputs)
                        {
                            Status($"Overwriting existing results for {dbName}...", nestedIds);
                            Directory.Delete(dbOutputFolder, true);
                            Directory.CreateDirectory(dbOutputFolder);
                        }
                        else
                        {
                            Status($"Skipping {dbName} - results already exist", nestedIds);
                            lock (_progressLock)
                            {
                                _completedDatabases++;
                                ReportProgress(new ProgressEventArgs(
                                    (int)((_completedDatabases / (double)totalDatabases) * 100),
                                    $"Completed {_completedDatabases}/{totalDatabases} databases",
                                    new List<string> { taskId }));
                            }

                            return; // Skip to next transient database
                        }
                    }
                }
                else
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
                int transientProteinCount = transientProteins.Count;

                // 4c. Combine base + transient proteins
                var combinedProteins = new List<IBioPolymer>(baseProteins);
                combinedProteins.AddRange(transientProteins);

                Status($"Searching {dbName} ({combinedProteins.Count} total proteins)...", nestedIds);

                // 4d. Search each spectra file with combined database
                List<SpectralMatch> allPsmsForThisDb = new();

                foreach (var rawFile in currentRawFileList)
                {
                    if (GlobalVariables.StopLoops)
                        break;

                    Status($"Searching {Path.GetFileName(rawFile)}...", nestedIds);
                    var searchNestedID = nestedIds.Concat([Path.GetFileNameWithoutExtension(rawFile)]).ToList();

                    var arrayOfMs2Scans = loadedSpectraByFile[rawFile];
                    SpectralMatch[] fileSpecificPsms = new SpectralMatch[arrayOfMs2Scans.Length];

                    var combinedParams = SetAllFileSpecificCommonParams(CommonParameters,
                        fileSettingsList[currentRawFileList.IndexOf(rawFile)]);

                    var massDiffAcceptor = GetMassDiffAcceptor(
                        combinedParams.PrecursorMassTolerance,
                        SearchParameters.MassDiffAcceptorType,
                        SearchParameters.CustomMdac);

                    // Run the classic search engine
                    var searchEngine = new ClassicSearchEngine(
                        fileSpecificPsms, arrayOfMs2Scans, variableModifications,
                        fixedModifications, SearchParameters.SilacLabels,
                        SearchParameters.StartTurnoverLabel, SearchParameters.EndTurnoverLabel,
                        combinedProteins, massDiffAcceptor, combinedParams,
                        FileSpecificParameters, null, searchNestedID,
                        false, false);

                    searchEngine.Run();
                    allPsmsForThisDb.AddRange(fileSpecificPsms);
                    ReportProgress(new(100, "Finished Classic Search...", searchNestedID));
                }

                Status($"Performing post-search analysis for {dbName}...", nestedIds); 
                if (GlobalVariables.StopLoops)
                    return;

                // 4e. Perform post-search analysis for this database
                var dbResults = PerformPostSearchAnalysis(allPsmsForThisDb, dbOutputFolder, nestedIds,
                    dbName, combinedProteins.Count, transientProteinAccessions);
                dbResults.TransientProteinCount = transientProteinCount;
                databaseResults[dbName] = dbResults;

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

                ReportProgress(new(100, $"Finished {dbName}", nestedIds));
            });

        Status("All database searches complete. Writing summary results...", taskId);

        // Write comprehensive results summary
        WriteGlobalResultsText(databaseResults, OutputFolder, taskId, totalMs2Scans, currentRawFileList.Count);

        Status("Many search task complete!", taskId);
        
        return MyTaskResults;
    }

    private DatabaseSearchResults PerformPostSearchAnalysis(List<SpectralMatch> allPsms, string outputFolder, 
        List<string> nestedIds, string dbName, int totalProteins, HashSet<string> transientProteinAccessions)
    {
        // Filter PSMs to keep only best per (file, scan, mass)
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
        string psmFile = Path.Combine(outputFolder, $"All{GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        WritePsmsToTsv(psmsForPsmResults.OrderByDescending(p => p), psmFile, SearchParameters.ModsToWriteSelection, false);
        FinishedWritingFile(psmFile, nestedIds);

        string transientPsmFile = Path.Combine(outputFolder, $"{dbName}_All{GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        WritePsmsToTsv(transientPsms, transientPsmFile, SearchParameters.ModsToWriteSelection, false);
        FinishedWritingFile(transientPsmFile, nestedIds);

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
        string peptideFile = Path.Combine(outputFolder, $"All{GlobalVariables.AnalyteType}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        WritePsmsToTsv(peptidesForPeptideResults, peptideFile, SearchParameters.ModsToWriteSelection, true);
        FinishedWritingFile(peptideFile, nestedIds);

        string transientPeptideFile = Path.Combine(outputFolder, $"{dbName}_All{GlobalVariables.AnalyteType}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        WritePsmsToTsv(transientPeptides, transientPeptideFile, SearchParameters.ModsToWriteSelection, true);
        FinishedWritingFile(transientPeptideFile, nestedIds);

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
            string proteinFile = Path.Combine(outputFolder, $"All{GlobalVariables.AnalyteType.GetBioPolymerLabel()}Groups.tsv");
            WriteProteinGroupsToTsv(proteinGroups, proteinFile, nestedIds);
            FinishedWritingFile(proteinFile, nestedIds);

            string transientProteinFile = Path.Combine(outputFolder, $"{dbName}_All{GlobalVariables.AnalyteType.GetBioPolymerLabel()}Groups.tsv");
            WriteProteinGroupsToTsv(transientProteinGroups, transientProteinFile, nestedIds);
            FinishedWritingFile(transientProteinFile, nestedIds);
        }

        // Write individual results.txt for this database
        WriteIndividualDatabaseResultsText(results, outputFolder, nestedIds);

        return results;
    }

    #region Result Writing

    private void WriteIndividualDatabaseResultsText(DatabaseSearchResults results, string outputFolder, List<string> nestedIds)
    {
        var resultsPath = Path.Combine(outputFolder, "results.txt");
        using (StreamWriter file = new StreamWriter(resultsPath))
        {
            file.WriteLine($"Database: {results.DatabaseName}");
            file.WriteLine($"Total proteins in combined database: {results.TotalProteins}");
            file.WriteLine($"Total proteins from transient database: {results.TransientProteinCount}");
            file.WriteLine();
            file.WriteLine($"Target PSMs at {CommonParameters.QValueThreshold * 100}% FDR: {results.TargetPsmsAtQValueThreshold}");
            file.WriteLine($"Target PSMs from transient database: {results.TargetPsmsFromTransientDb}");
            file.WriteLine($"Target PSMs from transient database at {CommonParameters.QValueThreshold * 100}% FDR: {results.TargetPsmsFromTransientDbAtQValueThreshold}");
            file.WriteLine();
            file.WriteLine($"Target peptides at {CommonParameters.QValueThreshold * 100}% FDR: {results.TargetPeptidesAtQValueThreshold}");
            file.WriteLine($"Target peptides from transient database: {results.TargetPeptidesFromTransientDb}");
            file.WriteLine($"Target peptides from transient database at {CommonParameters.QValueThreshold * 100}% FDR: {results.TargetPeptidesFromTransientDbAtQValueThreshold}");
            
            if (SearchParameters.DoParsimony)
            {
                file.WriteLine();
                file.WriteLine($"Target protein groups at {CommonParameters.QValueThreshold * 100}% FDR: {results.TargetProteinGroupsAtQValueThreshold}");
                file.WriteLine($"Target protein groups with transient database proteins: {results.TargetProteinGroupsFromTransientDb}");
                file.WriteLine($"Target protein groups with transient database proteins at {CommonParameters.QValueThreshold * 100}% FDR: {results.TargetProteinGroupsFromTransientDbAtQValueThreshold}");
            }
        }
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

    private void WriteProteinGroupsToTsv(List<ProteinGroup> proteinGroups, string filePath, List<string> nestedIds)
    {
        if (proteinGroups != null && proteinGroups.Any())
        {
            double qValueThreshold = Math.Min(CommonParameters.QValueThreshold, CommonParameters.PepQValueThreshold);
            using (StreamWriter output = new StreamWriter(filePath))
            {
                output.WriteLine(proteinGroups.First().GetTabSeparatedHeader());
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
                        output.WriteLine(proteinGroups[i]);
                    }
                }
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
        string psmFile = Path.Combine(outputFolder, dbName, $"All{GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        if (!File.Exists(psmFile))
            return false;

        string peptideFile = Path.Combine(outputFolder, dbName, $"All{GlobalVariables.AnalyteType}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        if (!File.Exists(peptideFile))
            return false;

        if (SearchParameters.DoParsimony)
        {
            string proteinFile = Path.Combine(outputFolder, dbName, $"All{GlobalVariables.AnalyteType.GetBioPolymerLabel()}Groups.tsv");
            if (!File.Exists(proteinFile))
                return false;
        }
        
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
    }
}