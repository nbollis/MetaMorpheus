#nullable enable
using EngineLayer;
using EngineLayer.ClassicSearch;
using EngineLayer.DatabaseLoading;
using EngineLayer.FdrAnalysis;
using EngineLayer.SpectrumMatch;
using Nett;
using Omics;
using Omics.Modifications;
using Omics.SpectrumMatch;
using Proteomics;
using SharpLearning.Common.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Analysis.Collectors;
using TaskLayer.ParallelSearch.Statistics;
using TaskLayer.ParallelSearch.Util;
using static Nett.TomlObjectFactory;
using ProteinGroup = EngineLayer.ProteinGroup;

namespace TaskLayer.ParallelSearch;
public class ParallelSearchTask : SearchTask
{
    private readonly object _progressLock = new object();
    private readonly object _debugLogLock = new object();
    private readonly Stopwatch _taskStopwatch = new();
    private TransientDatabaseResultsManager? _resultsManager;
    private string? _debugLogPath;

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
    [TomlIgnore] public List<DbForTask> PersistentDatabases { get; private set; } = [];

    #endregion

    protected override MyTaskResults RunSpecific(string outputFolder,
        List<DbForTask> dbFilenameList, List<string> currentRawFileList,
        string taskId, FileSpecificParameters[] fileSettingsList)
    {
        _taskStopwatch.Restart();
        MyTaskResults = new MyTaskResults(this);
        PersistentDatabases = dbFilenameList;
        DebugStatus("RunSpecific start", taskId);

        // Initialize unified results manager
        _resultsManager = CreateResultsManager(outputFolder, ParallelSearchParameters.DoParsimony, ParallelSearchParameters.DeNovoMappingDataFilePath);
        DebugStatus($"Results manager created. OutputFolder={outputFolder}", taskId);
        
        // Check cache status early for fast-path optimization
        var allDatabaseNames = ParallelSearchParameters.TransientDatabases
            .Select(db => Path.GetFileNameWithoutExtension(db.FilePath))
            .ToList();
        
        var cacheSummary = _resultsManager!.GetCacheSummary(allDatabaseNames);
        Status(cacheSummary.ToString(), taskId);
        DebugStatus($"Cache summary computed. Cached={cacheSummary.CachedDatabases}, NeedProcessing={cacheSummary.DatabasesNeedingProcessing}", taskId);

        // Fast path: If all databases are cached and not overwriting, skip to finalization
        if (cacheSummary.DatabasesNeedingProcessing == 0 && !ParallelSearchParameters.OverwriteTransientSearchOutputs)
        {
            Status("All databases cached, skipping search phase and proceeding to finalization...", taskId);
            DebugStatus("Taking cache fast-path to finalization", taskId);
            goto Finalization;
        }

        // Initialize all necessary data structures including base search
        DebugStatus("Initialization start", taskId);
        Initialize(taskId, dbFilenameList, currentRawFileList, fileSettingsList, outputFolder);
        DebugStatus("Initialization complete", taskId);

        Status($"Starting search of {TotalDatabases} transient databases...", taskId);

        // Determine optimal thread allocation
        int totalAvailableThreads = Environment.ProcessorCount;
        int databaseParallelism = Math.Min(ParallelSearchParameters.MaxSearchesInParallel,
            ParallelSearchParameters.TransientDatabases.Count);
        int threadsPerDatabase = Math.Max(1, totalAvailableThreads / databaseParallelism);
        CommonParameters.MaxThreadsToUsePerFile = threadsPerDatabase;
        DebugStatus($"Parallel settings: ProcessorCount={totalAvailableThreads}, DatabaseParallelism={databaseParallelism}, ThreadsPerDatabase={threadsPerDatabase}", taskId);

        // Loop through each transient database
        Parallel.ForEach(ParallelSearchParameters.TransientDatabases,
            new ParallelOptions { MaxDegreeOfParallelism = databaseParallelism },
            transientDbPath =>
            {
                ProcessTransientDatabase(transientDbPath, outputFolder, taskId);
            });
        DebugStatus("Transient database processing loop completed", taskId);

        Finalization:
        DebugStatus("Finalization start", taskId);

        // If we have denovo results path, but it has not been collected to our results yet, collect the denovo data prior to running stats tests. 
        if (ParallelSearchParameters.DeNovoMappingDataFilePath != null && _resultsManager.TransientDatabaseMetricsDictionary.All(p => p.Value.TotalPredictions == 0))
        {
            var collector = new DeNovoMappingCollector(ParallelSearchParameters.DeNovoMappingDataFilePath);

            foreach (var (dbName, metrics) in _resultsManager.TransientDatabaseMetricsDictionary)
            {
                var dummyContext = new TransientDatabaseContext { DatabaseName = dbName};
                if (!collector.CanCollectData(dummyContext))
                {
                    // Skip this analyzer or log warning
                    Console.WriteLine($"Skipping analyzer {collector.CollectorName} due to insufficient data.");
                    DebugStatus($"Skipped de novo collector for {dbName}: insufficient data", taskId);
                    continue;
                }

                var analysisResults = collector.CollectData(dummyContext);

                // Merge results into the aggregated result
                foreach (var kvp in analysisResults)
                {
                    metrics.Results[kvp.Key] = kvp.Value;
                }

                DebugStatus($"De novo collector merged for {dbName}: {analysisResults.Count} metrics", taskId);
            }
        }


        Status("Running statistical analysis on all results...", taskId);
        DebugStatus("Statistical analysis start", taskId);
        _resultsManager!.RunStatisticalAnalysis();
        DebugStatus("Statistical analysis complete", taskId);

        Status("Writing Final Results...", taskId);
        DebugStatus("Final output writing start", taskId);
        WriteFinalOutputs(outputFolder, taskId, currentRawFileList.Count);
        DebugStatus("Final output writing complete", taskId);

        ReportProgress(new(100, "Many search task complete!", [taskId]));
        DebugStatus("RunSpecific complete", taskId);
        return MyTaskResults;
    }

    #region Initialization 

    private void Initialize(string taskId, List<DbForTask> dbFilenameList, 
        List<string> currentRawFileList, FileSpecificParameters[] fileSettingsList, 
        string outputFolder)
    {
        var initStopwatch = Stopwatch.StartNew();
        InitializeDebugLog(outputFolder, taskId);

        // Initialize base objects
        MyFileManager = new MyFileManager(SearchParameters.DisposeOfFileWhenDone);
        DebugStatus("Initialize: MyFileManager created", taskId);

        Status("Loading modifications...", taskId);

        // 1. Load modifications once
        LoadModifications(taskId, out var variableModifications,
            out var fixedModifications, out var localizableModificationTypes);
        VariableModifications = variableModifications;
        FixedModifications = fixedModifications;
        LocalizableModificationTypes = localizableModificationTypes;
        DebugStatus($"Initialize: Modifications loaded. Variable={VariableModifications.Count}, Fixed={FixedModifications.Count}, LocalizableTypes={LocalizableModificationTypes.Count}", taskId);

        Status("Loading base database(s)...", taskId);

        // 2. Load base database(s) once
        var baseDbLoader = new DatabaseLoadingEngine(CommonParameters,
            FileSpecificParameters, [taskId], dbFilenameList, taskId,
            SearchParameters.DecoyType, SearchParameters.SearchTarget,
            LocalizableModificationTypes);
        BaseBioPolymers = (baseDbLoader.Run() as DatabaseLoadingEngineResults)!.BioPolymers;

        Status($"Loaded {BaseBioPolymers.Count} base proteins", taskId);
        DebugStatus($"Initialize: Base database load complete. BaseBioPolymers={BaseBioPolymers.Count}", taskId);

        // 3. Load all spectra files once and store in memory
        Status("Loading spectra files...", taskId);
        ConcurrentDictionary<string, Ms2ScanWithSpecificMass[]> loadedSpectraByFile = new();
        int totalMs2Scans = LoadSpectraFiles(currentRawFileList, fileSettingsList, MyFileManager,
            loadedSpectraByFile, taskId);
        AllSortedMs2Scans = loadedSpectraByFile
            .SelectMany(p => p.Value)
            .OrderBy(b => b.PrecursorMass)
            .ToArray();
        DebugStatus($"Initialize: Spectra loading complete. RawFiles={currentRawFileList.Count}, TotalMs2Scans={totalMs2Scans}, SortedMs2Scans={AllSortedMs2Scans.Length}", taskId);

        // 4. Perform base database search once and store results
        Status("Performing base database search...", taskId);
        BaseSearchPsms = new SpectralMatch[AllSortedMs2Scans.Length];
        PerformSearch(BaseBioPolymers, BaseSearchPsms, new List<string> { taskId });
        Status($"Base search complete. Found {BaseSearchPsms.Count(p => p != null)} PSMs.", taskId);
        DebugStatus($"Initialize: Base search complete. BaseSearchPsmsNonNull={BaseSearchPsms.Count(p => p != null)}", taskId);

        // Write prose for base settings
        ProseCreatedWhileRunning.Append($"Base database contained {BaseBioPolymers.Count(p => !p.IsDecoy)} non-decoy protein entries. ");
        ProseCreatedWhileRunning.Append($"Searching {ParallelSearchParameters.TransientDatabases.Count} transient databases against {currentRawFileList.Count} spectra files. ");
        DebugStatus($"Initialize complete in {initStopwatch.Elapsed.TotalSeconds:F3}s", taskId);
    }

    private int LoadSpectraFiles(List<string> currentRawFileList, FileSpecificParameters[] fileSettingsList,
    MyFileManager myFileManager, ConcurrentDictionary<string, Ms2ScanWithSpecificMass[]> loadedSpectraByFile,
    string taskId)
    {
        int totalMs2Scans = 0;
        int specLoadingProgress = 0;
        var spectraLoadingStopwatch = Stopwatch.StartNew();
        var specLoadingNestedIds = new List<string> { taskId, "Spectra Loading" };
        Status("Loading spectra files...", specLoadingNestedIds);
        DebugStatus($"LoadSpectraFiles start. FileCount={currentRawFileList.Count}", specLoadingNestedIds);

        Parallel.ForEach(currentRawFileList,
            new ParallelOptions { MaxDegreeOfParallelism = ParallelSearchParameters.MaxSearchesInParallel },
            rawFile =>
            {
                var singleFileStopwatch = Stopwatch.StartNew();
                DebugStatus($"Loading spectra file start: {Path.GetFileName(rawFile)}", specLoadingNestedIds);

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

                DebugStatus($"Loading spectra file complete: {Path.GetFileName(rawFile)}. Ms2Scans={ms2Scans.Length}, Duration={singleFileStopwatch.Elapsed.TotalSeconds:F3}s", specLoadingNestedIds);
            });

        ReportProgress(new ProgressEventArgs(100, $"Finished Loading spectra files.", specLoadingNestedIds));
        Status($"Finished Loading {currentRawFileList.Count} spectra files.", taskId);
        DebugStatus($"LoadSpectraFiles complete. TotalMs2Scans={totalMs2Scans}, Duration={spectraLoadingStopwatch.Elapsed.TotalSeconds:F3}s", taskId);

        return totalMs2Scans;
    }

    /// <summary>
    /// Creates the unified results manager with configured collectors and statistical tests
    /// </summary>
    public static TransientDatabaseResultsManager CreateResultsManager(string outputFolder, bool doParsimony, string? deNovoMappingFilePath = null)
    {
        // Define cache paths
        string analysisCachePath = Path.Combine(outputFolder, "ManySearchSummary.csv");

        // Initialize collectors based on user preferences
        var collectors = new List<IMetricCollector>
        {
            new BasicMetricCollector(),
            new PsmPeptideSearchCollector("Homo sapiens"),
            new FragmentIonCollector(),
            new RetentionTimeCollector(),
        };

        var statisticalTests = new List<IStatisticalTest>();
        statisticalTests.AddRange(TestCollection.BaseTests);
        statisticalTests.AddRange(TestCollection.ScoreDistributionTest);
        statisticalTests.AddRange(TestCollection.RetentionTimeTests);
        statisticalTests.AddRange(TestCollection.FragmentationTests);

        if (doParsimony)
        {
            statisticalTests.AddRange(TestCollection.ProteinGroupTests);
            collectors.Add(new ProteinGroupCollector("Homo sapiens"));
        }

        if (deNovoMappingFilePath != null)
        {
            statisticalTests.AddRange(TestCollection.DeNovoTests);
            collectors.Add(new DeNovoMappingCollector(deNovoMappingFilePath));
        }

        var metricAggregator = new MetricAggregator(collectors);

        return new TransientDatabaseResultsManager(
            metricAggregator,
            statisticalTests,
            analysisCachePath
        );
    }

    #endregion

    #region Search

    private void ProcessTransientDatabase(DbForTask transientDb, string outputFolder, string taskId)
    {
        if (GlobalVariables.StopLoops)
            return;

        string dbName = Path.GetFileNameWithoutExtension(transientDb.FilePath);
        string dbOutputFolder = Path.Combine(outputFolder, dbName);
        List<string> nestedIds = [taskId, dbName];
        var dbStopwatch = Stopwatch.StartNew();

        Status($"Processing {dbName}...", nestedIds);
        DebugStatus("ProcessTransientDatabase start", nestedIds, dbName, dbStopwatch);

        // Check if we should skip or overwrite this database
        bool shouldProcess = !_resultsManager!.HasCachedResults(dbName) || 
                           ParallelSearchParameters.OverwriteTransientSearchOutputs;

        if (!shouldProcess)
        {
            DebugStatus("Skipping database because cached result exists and overwrite is false", nestedIds, dbName, dbStopwatch);
            ReportProgress(new(100, $"Skipping {dbName} - results already exist in cache", nestedIds));
            UpdateProgress(TotalDatabases, taskId);
            return;
        }

        // Handle overwrite scenario
        if (ParallelSearchParameters.OverwriteTransientSearchOutputs && _resultsManager.HasCachedResults(dbName))
        {
            Status($"Overwriting existing results for {dbName}...", nestedIds);
            DebugStatus("Overwrite enabled and cached result exists", nestedIds, dbName, dbStopwatch);
            if (Directory.Exists(dbOutputFolder))
            {
                Directory.Delete(dbOutputFolder, true);
                DebugStatus($"Deleted existing output directory: {dbOutputFolder}", nestedIds, dbName, dbStopwatch);
            }
        }

        if (!Directory.Exists(dbOutputFolder))
            Directory.CreateDirectory(dbOutputFolder);

        Status($"Loading transient database {dbName}...", nestedIds);
        DebugStatus("Transient database loading start", nestedIds, dbName, dbStopwatch);

        // Load transient database
        var transientProteins = LoadTransientDatabase(transientDb, nestedIds, taskId);
        DebugStatus($"Transient database loading complete. ProteinCount={transientProteins.Count}", nestedIds, dbName, dbStopwatch);

        if (GlobalVariables.StopLoops)
        {
            DebugStatus("StopLoops requested after database load", nestedIds, dbName, dbStopwatch);
            return;
        }

        // Create HashSet of transient protein accessions for later filtering
        var transientProteinAccessions = new HashSet<string>(
            transientProteins.Select(p => p.Accession));

        // Calculate transient peptide count
        Status($"Calculating peptide count for {dbName}...", nestedIds);
        int transientPeptideCount = CalculateTransientPeptideCount(transientProteins);
        DebugStatus($"Transient peptide count computed. PeptideCount={transientPeptideCount}", nestedIds, dbName, dbStopwatch);

        Status($"Searching {dbName} ({transientProteins.Count} transient proteins)...", nestedIds);
        DebugStatus("Classic search start", nestedIds, dbName, dbStopwatch);

        // Clone the base PSMs and search only transient proteins
        SpectralMatch[] psmArray = CloneBasePsms();
        DebugStatus($"Base PSM clone complete. CloneLength={psmArray.Length}", nestedIds, dbName, dbStopwatch);
        PerformSearch(transientProteins, psmArray, nestedIds);
        DebugStatus($"Classic search complete. NonNullPsms={psmArray.Count(p => p != null)}", nestedIds, dbName, dbStopwatch);

        Status($"Performing post-search analysis for {dbName}...", nestedIds);
        DebugStatus("Post-search analysis start", nestedIds, dbName, dbStopwatch);

        int totalProteins = BaseBioPolymers.Count + transientProteins.Count;
        
        // Process database through unified manager (handles analysis + statistical caching)
        var dbResults = PerformPostSearchAnalysis(
            psmArray,
            dbOutputFolder, 
            nestedIds,
            dbName, 
            totalProteins, 
            transientProteinAccessions, 
            transientPeptideCount, 
            transientProteins, 
            transientDb
        ).GetAwaiter().GetResult();
        DebugStatus($"Post-search analysis complete. TestsPassed={dbResults.StatisticalTestsPassed}, TargetPsmsAtQ={dbResults.TargetPsmsAtQValueThreshold}, TargetPeptidesAtQ={dbResults.TargetPeptidesAtQValueThreshold}", nestedIds, dbName, dbStopwatch);

        // Cleanup transient proteins to free memory
        transientProteins.Clear();
        DebugStatus("Transient protein list cleared", nestedIds, dbName, dbStopwatch);

        UpdateProgress(TotalDatabases, taskId);

        // Compress the output folder if requested
        if (ParallelSearchParameters.CompressTransientSearchOutputs)
        {
            Status($"Compressing output for {dbName}...", nestedIds);
            DebugStatus("Output compression start", nestedIds, dbName, dbStopwatch);
            CompressTransientDatabaseOutput(dbOutputFolder);
            DebugStatus("Output compression complete", nestedIds, dbName, dbStopwatch);
        }

        DebugStatus("ProcessTransientDatabase complete", nestedIds, dbName, dbStopwatch);
        ReportProgress(new(100, $"Finished {dbName}", nestedIds));
    }

    /// <summary>
    /// Populates and returns the spectral match array using classic search engine
    /// </summary>
    private void PerformSearch(List<IBioPolymer> proteinsToSearch, SpectralMatch[] spectralMatchArray, List<string> nestedIds)
    {
        var searchStopwatch = Stopwatch.StartNew();
        DebugStatus($"PerformSearch start. ProteinCount={proteinsToSearch.Count}, PsmArrayLength={spectralMatchArray.Length}", nestedIds);

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
        DebugStatus($"PerformSearch complete in {searchStopwatch.Elapsed.TotalSeconds:F3}s. NonNullPsms={spectralMatchArray.Count(p => p != null)}", nestedIds);
        ReportProgress(new(100, "Finished Classic Search...", nestedIds));
    }

    private async Task<TransientDatabaseMetrics> PerformPostSearchAnalysis(
        SpectralMatch[] allPsms,
        string outputFolder,
        List<string> nestedIds, 
        string dbName, 
        int totalProteins, 
        HashSet<string> transientProteinAccessions, 
        int transientPeptideCount, 
        List<IBioPolymer> transientProteins, 
        DbForTask transientDatabase)
    {
        var postAnalysisStopwatch = Stopwatch.StartNew();
        DebugStatus("PerformPostSearchAnalysis start", nestedIds, dbName, postAnalysisStopwatch);

        var bestPsms = allPsms.Where(p => p != null)
            .OrderByDescending(p => p).ToList();
        DebugStatus($"Best-PSM selection complete. InputPsms={allPsms.Length}, BestPsms={bestPsms.Count}", nestedIds, dbName, postAnalysisStopwatch);

        int numNotches = GetNumNotches(SearchParameters.MassDiffAcceptorType, SearchParameters.CustomMdac);
        DebugStatus($"FDR setup complete. NumNotches={numNotches}", nestedIds, dbName, postAnalysisStopwatch);

        // Minimal FDR analysis - modify PSMs in place
        var fdrEngine = new FdrAnalysisEngine(
            bestPsms, numNotches, CommonParameters,
            FileSpecificParameters, nestedIds, "PSM", false, outputFolder, alreadySorted: true);
        await fdrEngine.RunAsync();
        DebugStatus("FDR analysis complete", nestedIds, dbName, postAnalysisStopwatch);

        // Disambiguate - modify PSMs in place - Only used for notch disambiguation and parallel search does not use notches. 
        //var disambiguationEngine = new DisambiguationEngine(
        //    bestPsms, CommonParameters, FileSpecificParameters, nestedIds);
        //await disambiguationEngine.RunAsync();
        //DebugStatus("Disambiguation complete", nestedIds, dbName, postAnalysisStopwatch);

        int qualifyingTransientPsmCount = bestPsms.Count(psm =>
            !psm.IsDecoy
            && !psm.IsContaminant
            && psm.BestMatchingBioPolymersWithSetMods.Any(match =>
                !match.IsDecoy
                && transientProteinAccessions.Contains(match.SpecificBioPolymer.Parent.Accession)));
        bool shouldRunParsimony = SearchParameters.DoParsimony && qualifyingTransientPsmCount > 0;
        DebugStatus($"Parsimony gate evaluated. DoParsimony={SearchParameters.DoParsimony}, QualifyingTransientPsms={qualifyingTransientPsmCount}, RunParsimony={shouldRunParsimony}", nestedIds, dbName, postAnalysisStopwatch);

        List<ProteinGroup>? proteinGroups = null;
        if (shouldRunParsimony)
        {
            Status($"Performing parsimony for {dbName}...", nestedIds);

            var psmForParsimony = FilteredPsms.Filter(bestPsms,
                commonParams: CommonParameters,
                includeDecoys: true,
                includeContaminants: true,
                includeAmbiguous: false,
                includeHighQValuePsms: false);

            ProteinParsimonyResults proteinAnalysisResults = (ProteinParsimonyResults)await new ProteinParsimonyEngine(
                psmForParsimony.FilteredPsmsList, SearchParameters.ModPeptidesAreDifferent,
                CommonParameters, FileSpecificParameters, nestedIds).RunAsync();

            ProteinScoringAndFdrResults proteinScoringAndFdrResults = (ProteinScoringAndFdrResults)await new ProteinScoringAndFdrEngine(
                proteinAnalysisResults.ProteinGroups, psmForParsimony.FilteredPsmsList,
                SearchParameters.NoOneHitWonders, SearchParameters.ModPeptidesAreDifferent,
                true, CommonParameters, FileSpecificParameters, nestedIds).RunAsync();

            proteinGroups = proteinScoringAndFdrResults.SortedAndScoredProteinGroups;
            DebugStatus($"Parsimony complete. ProteinGroups={proteinGroups.Count}", nestedIds, dbName, postAnalysisStopwatch);
        }
        else if (SearchParameters.DoParsimony)
        {
            DebugStatus("Parsimony skipped because no qualifying non-decoy/non-contaminant transient PSMs were found in bestPsms", nestedIds, dbName, postAnalysisStopwatch);
        }

        Status($"Writing results for {dbName}...", nestedIds);

        // Filter PSMs for writing to file
        var psmsForPsmResults = FilteredPsms.Filter(bestPsms,
            CommonParameters,
            includeDecoys: SearchParameters.WriteDecoys,
            includeContaminants: SearchParameters.WriteContaminants,
            includeAmbiguous: true,
            includeHighQValuePsms: SearchParameters.WriteHighQValuePsms);

        var orderedPsmsForResults = psmsForPsmResults.OrderByDescending(p => p).ToList();

        var transientPsms = FilterToTransientDatabaseOnly(
            psmsForPsmResults.FilteredPsmsList, transientProteinAccessions).ToList();
        DebugStatus($"PSM filtering complete. AllFilteredPsms={psmsForPsmResults.FilteredPsmsList.Count}, TransientPsms={transientPsms.Count}", nestedIds, dbName, postAnalysisStopwatch);

        // Write PSMs to file
        if (!ParallelSearchParameters.WriteTransientResultsOnly)
        {
            string psmFile = Path.Combine(outputFolder,
                $"All{GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
            await WritePsmsToTsvAsync(orderedPsmsForResults, psmFile,
                SearchParameters.ModsToWriteSelection, false);
            FinishedWritingFile(psmFile, nestedIds);
        }

        string transientPsmFile = Path.Combine(outputFolder, $"{dbName}_All{GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        await WritePsmsToTsvAsync(transientPsms, transientPsmFile, SearchParameters.ModsToWriteSelection, false);
        FinishedWritingFile(transientPsmFile, nestedIds);

        // Filter PSMs for peptide results
        var peptidesForPeptideResults = FilteredPsms.Filter(bestPsms,
            CommonParameters,
            includeDecoys: SearchParameters.WriteDecoys,
            includeContaminants: SearchParameters.WriteContaminants,
            includeAmbiguous: true,
            includeHighQValuePsms: SearchParameters.WriteHighQValuePsms,
            filterAtPeptideLevel: true);

        var transientPeptides = FilterToTransientDatabaseOnly(
            peptidesForPeptideResults.FilteredPsmsList, transientProteinAccessions).ToList();
        DebugStatus($"Peptide filtering complete. AllFilteredPeptides={peptidesForPeptideResults.FilteredPsmsList.Count}, TransientPeptides={transientPeptides.Count}", nestedIds, dbName, postAnalysisStopwatch);

        // Write peptides to file
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

        List<ProteinGroup>? transientProteinGroups = null;
        if (proteinGroups is not null)
        {
            proteinGroups.ForEach(x => x.GetIdentifiedPeptidesOutput(SearchParameters.SilacLabels));

            // Count protein groups that contain at least one transient database protein
            transientProteinGroups = FilterProteinGroupsToTransientDatabaseOnly(proteinGroups, transientProteinAccessions).ToList();
            DebugStatus($"Transient protein group filtering complete. TransientProteinGroups={transientProteinGroups.Count}", nestedIds, dbName, postAnalysisStopwatch);

            // Write protein groups to file
            if (!ParallelSearchParameters.WriteTransientResultsOnly)
            {
                string proteinFile = Path.Combine(outputFolder,
                    $"All{GlobalVariables.AnalyteType.GetBioPolymerLabel()}Groups.tsv");
                await WriteProteinGroupsToTsvAsync(proteinGroups, proteinFile);
                FinishedWritingFile(proteinFile, nestedIds);
            }

            if (transientProteinGroups.Any())
            {
                string transientProteinFile = Path.Combine(outputFolder,
                    $"{dbName}_All{GlobalVariables.AnalyteType.GetBioPolymerLabel()}Groups.tsv");
                await WriteProteinGroupsToTsvAsync(transientProteinGroups, transientProteinFile);
                FinishedWritingFile(transientProteinFile, nestedIds);
            }
        }

        // Create analysis context
        var analysisContext = new TransientDatabaseContext
        {
            DatabaseName = dbName,
            TransientDatabase = transientDatabase,
            TransientProteins = transientProteins,
            TransientProteinAccessions = transientProteinAccessions,
            AllPsms = bestPsms,
            TransientPsms = transientPsms,
            AllPeptides = peptidesForPeptideResults.FilteredPsmsList,
            TransientPeptides = transientPeptides,
            ProteinGroups = proteinGroups,
            TransientProteinGroups = transientProteinGroups,
            CommonParameters = CommonParameters,
            TotalProteins = totalProteins,
            TransientPeptideCount = transientPeptideCount,
            OutputFolder = outputFolder,
            NestedIds = nestedIds
        };

        // Process through unified manager (caches analysis results)
        // Statistical tests will be run on all databases together in RunStatisticalAnalysis
        Status($"Running analysis for {dbName}...", nestedIds);
        var aggregatedResult = _resultsManager!.ProcessDatabase(
            analysisContext, 
            forceRecompute: ParallelSearchParameters.OverwriteTransientSearchOutputs
        );
        DebugStatus($"Metric aggregation complete. StatTestsRun={aggregatedResult.StatisticalTestsRun}, StatTestsPassed={aggregatedResult.StatisticalTestsPassed}", nestedIds, dbName, postAnalysisStopwatch);

        if (SearchParameters.WriteSpectralLibrary)
        {
            // Write spectral library
            string spectralLibraryPath = Path.Combine(outputFolder, $"AllPeptidesAnd_{dbName}_SpectralLibrary.msp");
            await WriteSpectralLibraryAsync(orderedPsmsForResults, spectralLibraryPath);
            FinishedWritingFile(spectralLibraryPath, nestedIds);
        }

        if (ParallelSearchParameters.WriteTransientSpectralLibrary)
        {
            string spectralLibraryPath = Path.Combine(outputFolder, $"{dbName}_SpectralLibrary.msp");
            await WriteSpectralLibraryAsync(orderedPsmsForResults, spectralLibraryPath);
            FinishedWritingFile(spectralLibraryPath, nestedIds);
        }

        // Write individual results.txt for this database
        await WriteIndividualDatabaseResultsTextAsync(aggregatedResult, outputFolder, nestedIds);
        DebugStatus($"PerformPostSearchAnalysis complete in {postAnalysisStopwatch.Elapsed.TotalSeconds:F3}s", nestedIds, dbName, postAnalysisStopwatch);

        return await Task.FromResult(aggregatedResult);
    }

    #endregion

    #region Result Writing

    private async Task WriteIndividualDatabaseResultsTextAsync(TransientDatabaseMetrics results, string outputFolder, List<string> nestedIds)
    {
        var resultsPath = Path.Combine(outputFolder, "results.txt");
        await results.WriteToTextFileAsync(resultsPath, CommonParameters.QValueThreshold, SearchParameters.DoParsimony);
        FinishedWritingFile(resultsPath, nestedIds);
    }

    /// <summary>
    /// Writes all final outputs including analysis results and statistical results
    /// </summary>
    private void WriteFinalOutputs(string outputFolder, string taskId, int numFiles)
    {
        var finalOutputsStopwatch = Stopwatch.StartNew();
        DebugStatus("WriteFinalOutputs start", taskId);

        var writtenFiles = _resultsManager!.WriteAllResults(outputFolder);
        foreach (var writtenFile in writtenFiles)
            FinishedWritingFile(writtenFile, [taskId]); 
        DebugStatus($"Core CSV outputs written. FileCount={writtenFiles.Count}", taskId);

        // Write global summary text file
        WriteGlobalResultsText(_resultsManager.TransientDatabaseMetricsDictionary, outputFolder, taskId, numFiles);
        DebugStatus("Global summary text written", taskId);

        // Deal with custom reduced database writing
        int sigPassedCutoff = (int)(_resultsManager.StatisticalTestCount * ParallelSearchParameters.TestRatioForWriting);
        var statsByDatabase = _resultsManager.StatisticalTestResultList
            .GroupBy(p => p.DatabaseName)
            .Where(p => p.Count(t => t.IsSignificant()) >= sigPassedCutoff)
            .ToDictionary(
                p => ParallelSearchParameters.TransientDatabases.First(db => Path.GetFileNameWithoutExtension(db.FileName) == p.Key),
                p => p.OrderBy(t => t.ToString()).ToList());

        Task[] dbWritingTasks = new Task[3];
        if (statsByDatabase.Count > 0)
        {
            Log($"Found {statsByDatabase.Count} significant databases passing cutoff ({sigPassedCutoff} tests)", [taskId]);
            DebugStatus($"Significant database selection complete. Count={statsByDatabase.Count}, Cutoff={sigPassedCutoff}", taskId);

            dbWritingTasks[0] = ParallelSearchParameters.DatabasesToWriteAndSearch[DatabaseToProduce.AllSignificantOrganisms].Write
                ? Task.Run(() => CreateCombinedDatabaseWithAllProteins(taskId, statsByDatabase.Select(p => p.Key), outputFolder))
                : Task.CompletedTask;

            dbWritingTasks[1] = ParallelSearchParameters.DatabasesToWriteAndSearch[DatabaseToProduce.AllDetectedProteinsFromSignificantOrganisms].Write
                ? Task.Run(() => CreateCombinedDatabaseWithDetectedProteins(taskId, statsByDatabase.Select(p => p.Key), outputFolder))
                : Task.CompletedTask;

            dbWritingTasks[2] = ParallelSearchParameters.DatabasesToWriteAndSearch[DatabaseToProduce.AllDetectedPeptidesFromSignificantOrganisms].Write
                ? Task.Run(() => CreateCombinedDatabaseWithDetectedPeptides(taskId, statsByDatabase.Select(p => p.Key), outputFolder))
                : Task.CompletedTask;

            Task.WaitAll(dbWritingTasks);
            DebugStatus("Combined FASTA writing tasks completed", taskId);
        }
        else
        {
            Log("No databases passed the significance cutoff for combined FASTA output", new List<string> { taskId });
            DebugStatus("No significant databases for combined FASTA outputs", taskId);
        }

        DebugStatus($"WriteFinalOutputs complete in {finalOutputsStopwatch.Elapsed.TotalSeconds:F3}s", taskId);
    }

    private void WriteGlobalResultsText(IReadOnlyDictionary<string, TransientDatabaseMetrics> databaseResults,
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
                file.WriteLine($"  Statistical tests passed: {results.StatisticalTestsPassed}");

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

    internal Task CreateCombinedDatabaseWithAllProteins(string taskId, IEnumerable<DbForTask> sigDatabases, string outputFolder)
    {
        var outputPath = Path.Combine(outputFolder, DatabaseToProduce.AllSignificantOrganisms.GetFileName());
        if (File.Exists(outputPath) && !ParallelSearchParameters.OverwriteTransientSearchOutputs)
            return Task.CompletedTask;

        FastaStreamReader.CombineFastas(sigDatabases.Select(db => db.FilePath), outputPath);
        FinishedWritingFile(outputPath, new List<string> { taskId });
        AddFollowUpSearchTask(taskId, outputPath, DatabaseToProduce.AllSignificantOrganisms);
        return Task.CompletedTask;
    }

    internal Task CreateCombinedDatabaseWithDetectedProteins(string taskId, IEnumerable<DbForTask> sigDatabases, string outputFolder)
    {
        var outputPath = Path.Combine(outputFolder, DatabaseToProduce.AllDetectedProteinsFromSignificantOrganisms.GetFileName());
        if (File.Exists(outputPath) && !ParallelSearchParameters.OverwriteTransientSearchOutputs)
            return Task.CompletedTask;

        int proteinGroupQColumn = -1;
        int proteinTargetDecoyColumn = -1;
        int proteinGroupAccessionColumn = -1;

        // Use FastaStreamWriter to append proteins from all significant databases
        using var fastaWriter = new FastaStreamWriter(outputPath, append: false, checkDuplicates: true);

        Parallel.ForEach(sigDatabases, database =>
        {
            var expectedOutputDir = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(database.FilePath));

            if (!Directory.Exists(expectedOutputDir))
            {
                Warn($"Expected output directory not found: {expectedOutputDir}");
                return;
            }

            var proteinGroupsPath = Directory.GetFiles(expectedOutputDir, "*_AllProteinGroups.tsv").FirstOrDefault();

            if (proteinGroupsPath == null)
            {
                return;
            }

            // Read protein groups file to get detected accessions
            using var reader = new StreamReader(proteinGroupsPath);
            string headerLine = reader.ReadLine() ?? string.Empty;
            var headers = headerLine.Split('\t');

            // Find column indices (only once)
            if (proteinGroupQColumn == -1)
                proteinGroupQColumn = Array.IndexOf(headers, "Protein QValue");
            if (proteinGroupAccessionColumn == -1)
                proteinGroupAccessionColumn = Array.IndexOf(headers, "Protein Accession");
            if (proteinTargetDecoyColumn == -1)
                proteinTargetDecoyColumn = Array.IndexOf(headers, "Protein Decoy/Contaminant/Target");

            var detectedAccessions = new HashSet<string>();
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line == null)
                    continue;
                var columns = line.Split('\t');

                // Skip if not a target protein
                if (!columns[proteinTargetDecoyColumn].Contains("T"))
                    continue;

                // Skip if Q-value too high
                if (!double.TryParse(columns[proteinGroupQColumn], out double qValue) ||
                    !(qValue <= CommonParameters.QValueThreshold))
                    continue;

                var accessions = columns[proteinGroupAccessionColumn].Split('|', ';');
                foreach (var accession in accessions)
                {
                    detectedAccessions.Add(accession);
                }
            }

            // Write detected proteins from this database to the combined FASTA
            if (detectedAccessions.Count > 0)
            {
                foreach (var (header, sequence) in FastaStreamReader.ReadFasta(database.FilePath, false))
                {
                    var accession = detectedAccessions.FirstOrDefault(header.Split('|')[1].Contains);
                    if (accession == null)
                    {
                        continue;
                    }

                    detectedAccessions.Remove(accession);
                    fastaWriter.WriteProtein(header, sequence);
                }
            }
        });

        fastaWriter.Flush();
        if (fastaWriter.ProteinsWritten > 0)
        {
            FinishedWritingFile(outputPath, new List<string> { taskId });
            AddFollowUpSearchTask(taskId, outputPath, DatabaseToProduce.AllDetectedProteinsFromSignificantOrganisms);
        }
        return Task.CompletedTask;
    }

    internal Task CreateCombinedDatabaseWithDetectedPeptides(string taskId, IEnumerable<DbForTask> sigDatabases, string outputFolder)
    {
        var outputPath = Path.Combine(outputFolder, DatabaseToProduce.AllDetectedPeptidesFromSignificantOrganisms.GetFileName());
        if (File.Exists(outputPath) && !ParallelSearchParameters.OverwriteTransientSearchOutputs)
            return Task.CompletedTask;

        // Column indices for peptide file
        int peptideQColumn = -1;
        int peptideProteinAccessionColumn = -1;
        int peptideTargetDecoyColumn = -1;

        // Use FastaStreamWriter to append proteins from all significant databases
        using var fastaWriter = new FastaStreamWriter(outputPath, append: false, checkDuplicates: true);

        Parallel.ForEach(sigDatabases, database =>
        {
            var expectedOutputDir = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(database.FilePath));

            if (!Directory.Exists(expectedOutputDir))
            {
                Warn($"Expected output directory not found: {expectedOutputDir}");
                return;
            }

            // Look for peptide file with transient database prefix
            var peptideFilePath = Directory.GetFiles(expectedOutputDir, $"*_AllPeptides.psmtsv")
                .FirstOrDefault();

            if (peptideFilePath == null)
            {
                return;
            }

            // Read peptide file to get protein accessions with detected peptides
            using var reader = new StreamReader(peptideFilePath);
            string headerLine = reader.ReadLine() ?? string.Empty;
            var headers = headerLine.Split('\t');

            // Find column indices (only once)
            if (peptideQColumn == -1)
                peptideQColumn = Array.IndexOf(headers, "QValue");
            if (peptideProteinAccessionColumn == -1)
                peptideProteinAccessionColumn = Array.IndexOf(headers, "Accession");
            if (peptideTargetDecoyColumn == -1)
                peptideTargetDecoyColumn = Array.IndexOf(headers, "Decoy/Contaminant/Target");

            var proteinsWithDetectedPeptides = new HashSet<string>();
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line == null)
                    continue;
                var columns = line.Split('\t');

                // Skip if not a target peptide
                if (!columns[peptideTargetDecoyColumn].Contains("T"))
                    continue;

                // Skip if Q-value too high
                if (!double.TryParse(columns[peptideQColumn], out double qValue) ||
                    !(qValue <= CommonParameters.QValueThreshold))
                    continue;

                // Parse protein accession(s) - handle multiple accessions separated by '|'
                var accessions = columns[peptideProteinAccessionColumn].Split('|', ';');
                foreach (var accession in accessions)
                {
                    var trimmedAccession = accession.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmedAccession))
                        proteinsWithDetectedPeptides.Add(trimmedAccession);
                }
            }

            // Write proteins with detected peptides from this database to the combined FASTA
            if (proteinsWithDetectedPeptides.Count > 0)
            {
                // Write detected proteins from this database to the combined FASTA
                if (proteinsWithDetectedPeptides.Count > 0)
                {
                    foreach (var (header, sequence) in FastaStreamReader.ReadFasta(database.FilePath, false))
                    {
                        var accession = proteinsWithDetectedPeptides.FirstOrDefault(header.Split('|')[1].Contains);
                        if (accession == null)
                        {
                            continue;
                        }

                        proteinsWithDetectedPeptides.Remove(accession);
                        fastaWriter.WriteProtein(header, sequence);
                    }
                }
            }
        });

        fastaWriter.Flush();
        if (fastaWriter.ProteinsWritten > 0)
        {
            FinishedWritingFile(outputPath, new List<string> { taskId });
            AddFollowUpSearchTask(taskId, outputPath, DatabaseToProduce.AllDetectedPeptidesFromSignificantOrganisms);
        }
        return Task.CompletedTask;
    }

    private object _followUpLock = new();
    private int _followUpSearchCount = 0;
    private void AddFollowUpSearchTask(string taskId, string fastaPath, DatabaseToProduce type)
    {
        if (ParallelSearchParameters.DatabasesToWriteAndSearch[type].Search == false)
            return;

        int currentTaskId = int.Parse(taskId.Split('-')[0].Replace("Task", ""));
        lock (_followUpLock)
        {
            _followUpSearchCount++;
            string followUpTaskId = $"Task{currentTaskId + _followUpSearchCount}-{type.GetTaskIdText()}";
            SearchTask followUpTask = new SearchTask()
            {
                CommonParameters = CommonParameters,
                FileSpecificParameters = FileSpecificParameters,
                SearchParameters = SearchParameters,
            };
            List<DbForTask> followUpDatabases = PersistentDatabases.Concat([new DbForTask(fastaPath, false)]).ToList();
            MyTaskResults.FollowUpTasks.Add((followUpTaskId, followUpTask, followUpDatabases));
        }

    }

    #endregion

    #region Transient Protein Handling

    /// <summary>
    /// Calculates the total theoretical peptide count for a database
    /// </summary>
    private int CalculateTransientPeptideCount(List<IBioPolymer> transientProteins)
    {
        int count = 0;
        foreach (var protein in transientProteins)
        {
            if (protein is Protein prot)
            {
                var peptides = prot.Digest(CommonParameters.DigestionParams, FixedModifications, VariableModifications);
                count += peptides.Count();
            }
        }
        return count;
    }

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
                (int)((_resultsManager!.CachedAnalysisCount / (double)totalDatabases) * 100),
                $"Completed {_resultsManager.CachedAnalysisCount}/{totalDatabases} databases",
                new List<string> { taskId }));
        }
    }

    #endregion

    #region Debug Logging

    private void DebugStatus(string message, string taskId)
    {
        AppendDebugLine(FormatDebugMessage(message, null, _taskStopwatch));
    }

    private void DebugStatus(string message, List<string> nestedIds)
    {
        AppendDebugLine(FormatDebugMessage(message, null, _taskStopwatch));
    }

    private void DebugStatus(string message, List<string> nestedIds, string dbName, Stopwatch stopwatch)
    {
        AppendDebugLine(FormatDebugMessage(message, dbName, stopwatch));
    }

    private static string FormatDebugMessage(string message, string? dbName, Stopwatch stopwatch)
    {
        string dbTag = dbName is null ? string.Empty : $"; db={dbName}";
        return $"[DBG {DateTimeOffset.UtcNow:O}] t+{stopwatch.Elapsed.TotalSeconds:F3}s; thread={Environment.CurrentManagedThreadId}{dbTag}; {message}";
    }

    private void InitializeDebugLog(string outputFolder, string taskId)
    {
        Directory.CreateDirectory(outputFolder);
        _debugLogPath = Path.Combine(outputFolder,
            $"ParallelSearchDebug_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.txt");

        lock (_debugLogLock)
        {
            File.AppendAllText(_debugLogPath,
                $"=== ParallelSearch Debug Log ({DateTimeOffset.UtcNow:O}) Task={taskId} ==={Environment.NewLine}");
        }

        Status($"Debug log file: {_debugLogPath}", taskId);
    }

    private void AppendDebugLine(string line)
    {
        if (string.IsNullOrWhiteSpace(_debugLogPath))
        {
            return;
        }

        lock (_debugLogLock)
        {
            File.AppendAllText(_debugLogPath, line + Environment.NewLine);
        }
    }

    #endregion

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
