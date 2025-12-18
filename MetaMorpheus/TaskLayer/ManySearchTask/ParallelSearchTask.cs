#nullable enable
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Omics.SpectrumMatch;
using ProteinGroup = EngineLayer.ProteinGroup;

namespace TaskLayer;
public class ParallelSearchTask : SearchTask
{
    private readonly object _progressLock = new object();
    private readonly ConcurrentBag<Task> _writeTasks = new();
    private ParallelSearchResultCache<TransientDatabaseSearchResults>? _resultsCache;

    public ParallelSearchTask() : base(MyTask.ParallelSearch)
    {
        // Initialize with appropriate defaults
        SearchParameters = new ParallelSearchParameters();
        CommonParameters = new(taskDescriptor: "ParallelSearchTask");
    }

    public ParallelSearchTask(List<DbForTask> transientDatabases) : base(MyTask.ParallelSearch)
    {
        // Initialize with appropriate defaults
        SearchParameters = new ParallelSearchParameters()
        {
            TransientDatabases = transientDatabases
        };
        CommonParameters = new(taskDescriptor: "ParallelSearchTask");
    }

    // Rename the TOML section for ParallelSearchParameters to avoid conflicts
    [TomlIgnore]
    public override SearchParameters SearchParameters
    {
        get => ParallelSearchParameters;
        set
        {
            if (value is ParallelSearchParameters msp)
                ParallelSearchParameters = msp;
            else
            {
                // If someone tries to set a base SearchParameters, convert it
                ParallelSearchParameters = new ParallelSearchParameters(SearchParameters);
            }
        }
    }

    public ParallelSearchParameters ParallelSearchParameters { get; set; } = new();

    #region Properties that are loaded once during Initialization

    [TomlIgnore] public MyFileManager MyFileManager = null!;
    [TomlIgnore] public List<Modification> VariableModifications { get; private set; } = [];
    [TomlIgnore] public List<Modification> FixedModifications { get; private set; } = [];
    [TomlIgnore] public List<string> LocalizableModificationTypes { get; private set; } = [];
    [TomlIgnore] public List<IBioPolymer> BaseBioPolymers { get; private set; } = [];
    [TomlIgnore] public Ms2ScanWithSpecificMass[] AllSortedMs2Scans { get; private set; } = [];
    [TomlIgnore] private SpectralMatch[] BaseSearchPsms = null!; // PSMs from base database search
    [TomlIgnore] public int TotalDatabases => ParallelSearchParameters.TransientDatabases.Count;
    [TomlIgnore] public int TotalMs2Scans => AllSortedMs2Scans.Length;

    #endregion

    protected override MyTaskResults RunSpecific(string OutputFolder,
        List<DbForTask> dbFilenameList, List<string> currentRawFileList,
        string taskId, FileSpecificParameters[] fileSettingsList)
    {
        MyTaskResults = new MyTaskResults(this);

        // Initialize all necessary data structures including base search
        Initialize(taskId, dbFilenameList, currentRawFileList, fileSettingsList);
        Status($"Starting search of {TotalDatabases} transient databases...", taskId);

        // Determine optimal thread allocation - Do this after initialization to ensure first search uses all available threads
        int totalAvailableThreads = Environment.ProcessorCount;
        int databaseParallelism = Math.Min(ParallelSearchParameters.MaxSearchesInParallel,
            ParallelSearchParameters.TransientDatabases.Count);
        int threadsPerDatabase = Math.Max(1, totalAvailableThreads / databaseParallelism);
        CommonParameters.MaxThreadsToUsePerFile = threadsPerDatabase;

        // Loop through each transient database
        Parallel.ForEach(ParallelSearchParameters.TransientDatabases,
            new ParallelOptions { MaxDegreeOfParallelism = databaseParallelism },
            transientDbPath =>
            {
                ProcessTransientDatabase(transientDbPath, OutputFolder, taskId);
            });

        // Wait for all async write operations to complete before writing summary
        Task.WaitAll(_writeTasks.ToArray());

        Status("All database searches complete. Writing summary results...", taskId);

        // Write comprehensive results summary
        WriteGlobalResultsText(_resultsCache!.AllResults, OutputFolder, taskId, currentRawFileList.Count);

        Status("Many search task complete!", taskId);

        return MyTaskResults;
    }

    private void Initialize(string taskId, List<DbForTask> dbFilenameList, List<string> currentRawFileList, FileSpecificParameters[] fileSettingsList)
    {
        // Initialize base objects
        MyFileManager = new MyFileManager(SearchParameters.DisposeOfFileWhenDone);
        _resultsCache = new ParallelSearchResultCache<TransientDatabaseSearchResults>(Path.Combine(OutputFolder, "ManySearchSummary.csv"));
        _resultsCache.InitializeCache();

        Status("Loading modifications...", taskId);

        // 1. Load modifications once
        LoadModifications(taskId, out var variableModifications,
            out var fixedModifications, out var localizableModificationTypes);
        VariableModifications = variableModifications;
        FixedModifications = fixedModifications;
        LocalizableModificationTypes = localizableModificationTypes;

        Status("Loading base database(s)...", taskId);

        // 2. Load base database(s) once
        var baseDbLoader = new DatabaseLoadingEngine(CommonParameters,
            FileSpecificParameters, [taskId], dbFilenameList, taskId,
            SearchParameters.DecoyType, SearchParameters.SearchTarget,
            LocalizableModificationTypes);
        BaseBioPolymers = (baseDbLoader.Run() as DatabaseLoadingEngineResults)!.BioPolymers;

        Status($"Loaded {BaseBioPolymers.Count} base proteins", taskId);

        // 3. Load all spectra files once and store in memory
        Status("Loading spectra files...", taskId);
        ConcurrentDictionary<string, Ms2ScanWithSpecificMass[]> loadedSpectraByFile = new();
        int totalMs2Scans = LoadSpectraFiles(currentRawFileList, fileSettingsList, MyFileManager,
            loadedSpectraByFile, taskId);
        AllSortedMs2Scans = loadedSpectraByFile
            .SelectMany(p => p.Value)
            .OrderBy(b => b.PrecursorMass)
            .ToArray();

        // 4. Perform base database search once and store results
        Status("Performing base database search...", taskId);
        BaseSearchPsms = new SpectralMatch[AllSortedMs2Scans.Length];
        PerformSearch(BaseBioPolymers, BaseSearchPsms, new List<string> { taskId });
        Status($"Base search complete. Found {BaseSearchPsms.Count(p => p != null)} PSMs.", taskId);

        // Write prose for base settings
        ProseCreatedWhileRunning.Append($"Base database contained {BaseBioPolymers.Count(p => !p.IsDecoy)} non-decoy protein entries. ");
        ProseCreatedWhileRunning.Append($"Searching {ParallelSearchParameters.TransientDatabases.Count} transient databases against {currentRawFileList.Count} spectra files. ");
    }

    private void ProcessTransientDatabase(DbForTask transientDbPath, string outputFolder, string taskId)
    {
        if (GlobalVariables.StopLoops)
            return;

        string dbName = Path.GetFileNameWithoutExtension(transientDbPath.FilePath);
        string dbOutputFolder = Path.Combine(outputFolder, dbName);
        List<string> nestedIds = [taskId, dbName];

        Status($"Processing {dbName}...", nestedIds);

        // Check if we should skip this database
        if (_resultsCache!.TryGetValue(dbName, out var value))
        {
            if (ParallelSearchParameters.OverwriteTransientSearchOutputs)
            {
                Status($"Overwriting existing results for {dbName}...", nestedIds);
                if (Directory.Exists(dbOutputFolder))
                {
                    Directory.Delete(dbOutputFolder, true);
                }
                Directory.CreateDirectory(dbOutputFolder);
                _resultsCache.Remove(value!);
            }
            else
            {
                ReportProgress(new(100, $"Skipping {dbName} - results already exist in cache", nestedIds));
                UpdateProgress(TotalDatabases, taskId);
                return;
            }
        }

        if (!Directory.Exists(dbOutputFolder))
            Directory.CreateDirectory(dbOutputFolder);

        Status($"Loading transient database {dbName}...", nestedIds);

        // Load transient database
        var transientProteins = LoadTransientDatabase(transientDbPath, nestedIds, taskId);

        if (GlobalVariables.StopLoops)
            return;

        // Create HashSet of transient protein accessions for later filtering
        var transientProteinAccessions = new HashSet<string>(
            transientProteins.Select(p => p.Accession));

        Status($"Searching {dbName} ({transientProteins.Count} transient proteins)...", nestedIds);

        // Clone the base PSMs and search only transient proteins
        SpectralMatch[] psmArray = CloneBasePsms();
        PerformSearch(transientProteins, psmArray, nestedIds);

        Status($"Performing post-search analysis for {dbName}...", nestedIds);

        int totalProteins = BaseBioPolymers.Count + transientProteins.Count;
        var dbResults = PerformPostSearchAnalysisAsync(psmArray.ToList(), dbOutputFolder, nestedIds,
            dbName, totalProteins, transientProteinAccessions);

        // Cleanup transient proteins to free memory
        transientProteins.Clear();
        transientProteins = null;

        // Update progress and cache
        var result = dbResults.Result;
        _resultsCache.AddAndWrite(result);

        UpdateProgress(TotalDatabases, taskId);

        // Compress the output folder if requested
        if (ParallelSearchParameters.CompressTransientSearchOutputs)
        {
            Status($"Compressing output for {dbName}...", nestedIds);
            CompressTransientDatabaseOutput(dbOutputFolder);
        }

        ReportProgress(new(100, $"Finished {dbName}", nestedIds));
    }

    /// <summary>
    /// Populates and returns the spectral match array using classic search engine
    /// </summary>
    private void PerformSearch(List<IBioPolymer> proteinsToSearch, SpectralMatch[] spectralMatchArray, List<string> nestedIds)
    {
        var massDiffAcceptor = GetMassDiffAcceptor(
            CommonParameters.PrecursorMassTolerance,
            SearchParameters.MassDiffAcceptorType,
            SearchParameters.CustomMdac);

        // Run the classic search engine
        var searchEngine = new StreamlinedClassicSearchEngine(
            spectralMatchArray, AllSortedMs2Scans, VariableModifications,
            FixedModifications, proteinsToSearch, massDiffAcceptor, CommonParameters,
            FileSpecificParameters, nestedIds);

        searchEngine.Run();
        ReportProgress(new(100, "Finished Classic Search...", nestedIds));
    }

    private async Task<TransientDatabaseSearchResults> PerformPostSearchAnalysisAsync(List<SpectralMatch> allPsms, string outputFolder,
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
            if (!ParallelSearchParameters.WriteTransientResultsOnly)
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
            if (!ParallelSearchParameters.WriteTransientResultsOnly)
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

        var results = new TransientDatabaseSearchResults
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
                if (!ParallelSearchParameters.WriteTransientResultsOnly)
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
                string spectralLibraryPath = Path.Combine(outputFolder, $"AllPeptidesAnd_{dbName}_SpectralLibrary.msp");
                await WriteSpectralLibraryAsync(psmsForPsmResults.OrderByDescending(p => p), spectralLibraryPath);
                FinishedWritingFile(spectralLibraryPath, nestedIds);
            }));
        }

        if (ParallelSearchParameters.WriteTransientSpectralLibrary)
        {
            _writeTasks.Add(Task.Run(async () => 
            {
                string spectralLibraryPath = Path.Combine(outputFolder, $"{dbName}_SpectralLibrary.msp");
                await WriteSpectralLibraryAsync(psmsForPsmResults.OrderByDescending(p => p), spectralLibraryPath);
                FinishedWritingFile(spectralLibraryPath, nestedIds);
            }));
        }

        // Write individual results.txt for this database
        _writeTasks.Add(Task.Run(async () => await WriteIndividualDatabaseResultsTextAsync(results, outputFolder, nestedIds)));


        return await Task.FromResult(results);
    }

    #region Result Writing

    private async Task WriteIndividualDatabaseResultsTextAsync(TransientDatabaseSearchResults results, string outputFolder, List<string> nestedIds)
    {
        var resultsPath = Path.Combine(outputFolder, "results.txt");
        await results.WriteToTextFileAsync(resultsPath, CommonParameters.QValueThreshold, SearchParameters.DoParsimony);
        FinishedWritingFile(resultsPath, nestedIds);
    }

    private void WriteGlobalResultsText(IReadOnlyDictionary<string, TransientDatabaseSearchResults> databaseResults,
        string outputFolder, string taskId, int numFiles)
    {
        // Global Summary Text File
        var summaryPath = Path.Combine(outputFolder, "ParallelSearchSummary.txt");

        using (StreamWriter file = new StreamWriter(summaryPath))
        {
            file.WriteLine("=== Parallel Search Task Summary ===");
            file.WriteLine();
            file.WriteLine($"Spectra files analyzed: {numFiles}");
            file.WriteLine($"Total MS2 scans: {TotalMs2Scans}");
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

    private List<IBioPolymer> LoadTransientDatabase(DbForTask transientDbPath,
        List<string> nestedIds, string taskId)
    {
        var transientDbList = new List<DbForTask> { transientDbPath };
        var transientDbLoader = new DatabaseLoadingEngine(CommonParameters,
            FileSpecificParameters, nestedIds, transientDbList, taskId,
            SearchParameters.DecoyType, SearchParameters.SearchTarget,
            LocalizableModificationTypes);
        var transientProteins = (transientDbLoader.Run() as DatabaseLoadingEngineResults)!.BioPolymers;

        return transientProteins;
    }

    /// <summary>
    /// Filters spectral matches to only include those that match exclusively to transient database proteins
    /// </summary>
    private IEnumerable<SpectralMatch> FilterToTransientDatabaseOnly(List<SpectralMatch> spectralMatches,
        HashSet<string> transientProteinAccessions)
    {
        var filtered = new List<SpectralMatch>(spectralMatches.Count / 10); // Estimate

        foreach (var psm in spectralMatches)
        {
            bool hasTransientProtein = false;
            foreach (var match in psm.BestMatchingBioPolymersWithSetMods)
            {
                if (transientProteinAccessions.Contains(match.SpecificBioPolymer.Parent.Accession))
                {
                    hasTransientProtein = true;
                    break;
                }
            }

            if (hasTransientProtein)
            {
                filtered.Add(psm);
            }
        }

        return filtered;
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

    #endregion

    #region UI Helpers 
    private void UpdateProgress(int totalDatabases, string taskId)
    {
        lock (_progressLock)
        {
            ReportProgress(new ProgressEventArgs(
                (int)((_resultsCache!.Count / (double)totalDatabases) * 100),
                $"Completed {_resultsCache.Count}/{totalDatabases} databases",
                new List<string> { taskId }));
        }
    }

    #endregion

    private int LoadSpectraFiles(List<string> currentRawFileList, FileSpecificParameters[] fileSettingsList,
        MyFileManager myFileManager, ConcurrentDictionary<string, Ms2ScanWithSpecificMass[]> loadedSpectraByFile,
        string taskId)
    {
        int totalMs2Scans = 0;
        int specLoadingProgress = 0;
        var specLoadingNestedIds = new List<string> { taskId, "Spectra Loading" };
        Status("Loading spectra files...", specLoadingNestedIds);

        Parallel.ForEach(currentRawFileList,
            new ParallelOptions { MaxDegreeOfParallelism = ParallelSearchParameters.MaxSearchesInParallel },
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

        return totalMs2Scans;
    }

    /// <summary>
    /// Creates a deep clone of the base PSM array to allow independent searching of transient databases.
    /// Each PSM is cloned with all its matching peptides so that transient protein searches can add/replace candidates.
    /// </summary>
    private SpectralMatch[] CloneBasePsms()
    {
        SpectralMatch[] clonedPsms = new SpectralMatch[BaseSearchPsms.Length];

        for (int i = 0; i < BaseSearchPsms.Length; i++)
        {
            if (BaseSearchPsms[i] != null)
            {
                // Create a new PSM with the same candidates as the base PSM
                // The ClassicSearchEngine will use AddOrReplace to potentially improve these matches
                var basePsm = BaseSearchPsms[i];
                var bestMatches = basePsm.BestMatchingBioPolymersWithSetMods.ToList();

                // Use the public Clone method for PeptideSpectralMatch
                clonedPsms[i] = basePsm is PeptideSpectralMatch peptidePsm
                    ? peptidePsm.Clone(bestMatches)
                    : null; // For now, OligoSpectralMatch will start fresh

                clonedPsms[i].PsmFdrInfo = null;
                clonedPsms[i].PeptideFdrInfo = null;
            }
        }

        return clonedPsms;
    }
}