#nullable enable
using EngineLayer;
using EngineLayer.ClassicSearch;
using EngineLayer.DatabaseLoading;
using EngineLayer.FdrAnalysis;
using EngineLayer.ParallelSearch;
using EngineLayer.ParallelSearch.FdrAlignment;
using EngineLayer.SpectrumMatch;
using EngineLayer.Util;
using FlashLFQ;
using Nett;
using Omics;
using Omics.Modifications;
using Omics.SpectrumMatch;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Analysis.Collectors;
using TaskLayer.ParallelSearch.Statistics;
using TaskLayer.ParallelSearch.Util;
using ProteinGroup = EngineLayer.ProteinGroup;

namespace TaskLayer.ParallelSearch;
public class ParallelSearchTask : SearchTask
{
    private readonly object _progressLock = new object();
    private readonly object _dashboardLock = new object();
    private TransientDatabaseResultsManager? _resultsManager;
    private Channel<(TransientDatabaseContext Context, TransientDatabaseMetrics Metrics)>? _completedDatabaseWriteChannel;
    private Task? _completedDatabaseWriterTask;
    private int _dashboardCachedAtStart;
    private int _dashboardInitialFinishedCount;
    private int _dashboardCompletedThisRun;

    private const string DashboardPhaseInitializing = "Initializing";
    private const string DashboardPhaseSearching = "Searching";
    private const string DashboardPhaseStatisticalAnalysis = "Statistical analysis";
    private const string DashboardPhaseWritingFinalResults = "Writing final results";
    private const string DashboardPhaseCompleted = "Completed";

    private const int DashboardDatabaseProcessingProgress = 1;
    private const int DashboardDatabaseOverwriteProgress = 2;
    private const int DashboardDatabaseLoadingProgress = 5;
    private const int DashboardDatabaseSearchStartProgress = 5;
    private const int DashboardDatabaseSearchEndProgress = 70;
    private const int DashboardDatabasePostSearchProgress = 70;
    private const int DashboardDatabaseFdrProgress = 74;
    private const int DashboardDatabaseParsimonyProgress = 84;
    private const int DashboardDatabaseAnalysisProgress = 92;
    private const int DashboardDatabaseWritingProgress = 95;
    private const int DashboardDatabaseCompressionProgress = 98;
    private const int DashboardDatabaseFinishedProgress = 100;

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

    [TomlIgnore] public List<DbForTask> PersistentDatabases { get; private set; } = [];

    // Normal Search Stuff
    [TomlIgnore] public MyFileManager MyFileManager = null!;
    [TomlIgnore] public List<Modification> VariableModifications { get; private set; } = [];
    [TomlIgnore] public List<Modification> FixedModifications { get; private set; } = [];
    [TomlIgnore] public List<string> LocalizableModificationTypes { get; private set; } = [];
    [TomlIgnore] public List<IBioPolymer> BaseBioPolymers { get; private set; } = [];
    [TomlIgnore] public Ms2ScanWithSpecificMass[] AllSortedMs2Scans { get; private set; } = [];
    [TomlIgnore] private SpectralMatch[] BaseSearchPsms = null!; // PSMs from base database search


    // Optimization caches for FDR alignment and parsimony
    [TomlIgnore] private readonly PsmSpectralMatchFdrAlignmentService _psmFdrAlignmentService = new();
    [TomlIgnore] private readonly PeptideSpectralMatchFdrAlignmentService _peptideFdrAlignmentService = new();
    [TomlIgnore] private readonly ProteinGroupFdrAlignmentService _proteinGroupFdrAlignmentService = new();
    [TomlIgnore] private List<CachedProteinGroup> CachedBaselineProteinGroups { get; set; } = [];
    [TomlIgnore] private readonly Dictionary<string, List<int>> _baselinePeptideKeyToScanIndexes = new(StringComparer.Ordinal);

    // Properties to track progress and results across databases
    [TomlIgnore] public int TotalDatabases => ParallelSearchParameters.TransientDatabases.Count;
    [TomlIgnore] public int TotalMs2Scans => AllSortedMs2Scans.Length;
    [TomlIgnore] public int PersistentDatabasePeptideCount { get; set; } = -1; // populated during initialization for use in analysis

    #endregion

    protected override MyTaskResults RunSpecific(string outputFolder,
        List<DbForTask> dbFilenameList, List<string> currentRawFileList,
        string taskId, FileSpecificParameters[] fileSettingsList)
    {
        MyTaskResults = new MyTaskResults(this);
        PersistentDatabases = dbFilenameList;

        if (currentRawFileList.All(p => p.EndsWith(".mgf", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".msAlign", StringComparison.OrdinalIgnoreCase)))
        {
            CommonParameters.DoPrecursorDeconvolution = false;
            CommonParameters.UseProvidedPrecursorInfo = true;
        }

        // Initialize unified results manager
        _resultsManager = CreateResultsManager(outputFolder);
        
        // Check cache status early for fast-path optimization
        var allDatabaseNames = ParallelSearchParameters.TransientDatabases
            .Select(db => Path.GetFileNameWithoutExtension(db.FilePath))
            .ToList();
         
        var cacheSummary = _resultsManager!.GetCacheSummary(allDatabaseNames);
        Status(cacheSummary.ToString(), taskId);
        InitializeDashboardState(cacheSummary);
        ReportTaskDashboard(taskId, ParallelSearchDashboardUpdateKind.Initialize, DashboardPhaseInitializing, "Checking existing cache...");

        // Fast path: If all databases are cached and not overwriting, skip to finalization
        if (cacheSummary.DatabasesNeedingProcessing == 0 && !ParallelSearchParameters.OverwriteTransientSearchOutputs)
        {
            Status("All databases cached, skipping search phase and proceeding to finalization...", taskId);
            ReportTaskDashboard(taskId, ParallelSearchDashboardUpdateKind.TaskStatus, DashboardPhaseInitializing,
                "All databases cached, skipping search phase and proceeding to finalization...");
            goto Finalization;
        }

        // Initialize all necessary data structures including base search
        Initialize(taskId, dbFilenameList, currentRawFileList, fileSettingsList, outputFolder);
        InitializeCompletedDatabaseWriter();

        Status($"Starting search of {TotalDatabases} transient databases...", taskId);
        ReportTaskDashboard(taskId, ParallelSearchDashboardUpdateKind.TaskStatus, DashboardPhaseSearching,
            $"Searching {TotalDatabases} transient databases...");

        // Determine optimal thread allocation
        int totalAvailableThreads = Environment.ProcessorCount;
         int databaseParallelism = Math.Min(ParallelSearchParameters.MaxSearchesInParallel,
             ParallelSearchParameters.TransientDatabases.Count);
         int threadsPerDatabase = Math.Max(1, totalAvailableThreads / databaseParallelism);
         CommonParameters.MaxThreadsToUsePerFile = threadsPerDatabase;

        // Loop through each transient database
         try
         {
             Parallel.ForEach(ParallelSearchParameters.TransientDatabases,
                 new ParallelOptions { MaxDegreeOfParallelism = databaseParallelism },
                 transientDbPath =>
                 {
                     ProcessTransientDatabase(transientDbPath, outputFolder, taskId);
                 });
         }
         finally
         {
             CompleteCompletedDatabaseWriter();
         }

          Finalization:

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
                     continue;
                 }

                 var analysisResults = collector.CollectData(dummyContext);

                 // Merge results into the aggregated result
                 foreach (var kvp in analysisResults)
                 {
                     metrics.Results[kvp.Key] = kvp.Value;
                 }
             }
        }

        if (CommonParameters.DoPrecursorDeconvolution)
        {
            //var precursorBackfill = new PrecursorDeconTsvBackfillService();
            //if (precursorBackfill.BackfillIfNeeded(
            //        outputFolder,
            //        _resultsManager.TransientDatabaseMetricsDictionary.Values.ToList(),
            //        currentRawFileList,
            //        fileSettingsList,
            //        CommonParameters,
            //        Math.Min(CommonParameters.QValueThreshold, CommonParameters.PepQValueThreshold),
            //        SearchParameters.DisposeOfFileWhenDone))
            //{
            //    _resultsManager.PersistAnalysisCache();
            //}
        }


        Status("Running statistical analysis on all results...", taskId);
        ReportTaskDashboard(taskId, ParallelSearchDashboardUpdateKind.TaskStatus, DashboardPhaseStatisticalAnalysis,
            "Running statistical analysis on all results...");
        _resultsManager!.RunStatisticalAnalysis();

        Status("Writing Final Results...", taskId);
        ReportTaskDashboard(taskId, ParallelSearchDashboardUpdateKind.TaskStatus, DashboardPhaseWritingFinalResults,
            "Writing final results...");
        WriteFinalOutputs(outputFolder, taskId, currentRawFileList.Count);

        ReportTaskDashboard(taskId, ParallelSearchDashboardUpdateKind.TaskCompleted, DashboardPhaseCompleted,
            "Many search task complete!");

        ReportProgress(new(100, "Many search task complete!", [taskId]));
        return MyTaskResults;
    }

    #region Initialization

    private void Initialize(string taskId, List<DbForTask> dbFilenameList,
        List<string> currentRawFileList, FileSpecificParameters[] fileSettingsList,
        string outputFolder)
    {
        // Initialize base objects
        MyFileManager = new MyFileManager(SearchParameters.DisposeOfFileWhenDone);

        Status("Loading modifications...", taskId);
        ReportTaskDashboard(taskId, ParallelSearchDashboardUpdateKind.TaskStatus, DashboardPhaseInitializing,
            "Loading modifications...");

        // 1. Load modifications once
        LoadModifications(taskId, out var variableModifications,
            out var fixedModifications, out var localizableModificationTypes);
        VariableModifications = variableModifications;
        FixedModifications = fixedModifications;
        LocalizableModificationTypes = localizableModificationTypes;

        Status("Loading base database(s)...", taskId);
        ReportTaskDashboard(taskId, ParallelSearchDashboardUpdateKind.TaskStatus, DashboardPhaseInitializing,
            "Loading base database(s)...");

        // 2. Load base database(s) once
        var baseDbLoader = new DatabaseLoadingEngine(CommonParameters,
            FileSpecificParameters, [taskId], dbFilenameList, taskId,
            SearchParameters.DecoyType, SearchParameters.SearchTarget,
            LocalizableModificationTypes);
        BaseBioPolymers = (baseDbLoader.Run() as DatabaseLoadingEngineResults)!.BioPolymers;

        Status($"Loaded {BaseBioPolymers.Count} base proteins", taskId);

        // 3. Load all spectra files once and store in memory
        Status("Loading spectra files...", taskId);
        ReportTaskDashboard(taskId, ParallelSearchDashboardUpdateKind.TaskStatus, DashboardPhaseInitializing,
            "Loading spectra files...");
        ConcurrentDictionary<string, Ms2ScanWithSpecificMass[]> loadedSpectraByFile = new();
        int totalMs2Scans = LoadSpectraFiles(currentRawFileList, fileSettingsList, MyFileManager,
            loadedSpectraByFile, taskId);
        AllSortedMs2Scans = loadedSpectraByFile
            .SelectMany(p => p.Value)
            .OrderBy(b => b.PrecursorMass)
            .ToArray();

        // 4. Perform base database search once and store results
        Status("Performing base database search...", taskId);
        ReportTaskDashboard(taskId, ParallelSearchDashboardUpdateKind.TaskStatus, DashboardPhaseInitializing,
            "Performing base database search...");
        BaseSearchPsms = new SpectralMatch[AllSortedMs2Scans.Length];
        PerformSearch(BaseBioPolymers, BaseSearchPsms, [taskId], out _, out int persistentDatabasePeptideCount);
        PersistentDatabasePeptideCount = persistentDatabasePeptideCount;
        Status($"Base search complete. Found {BaseSearchPsms.Count(p => p != null)} PSMs.", taskId);

        // 5. Run baseline FDR once and cache score->q-value mapping for transient alignment
        var baselinePsms = BaseSearchPsms.Where(p => p != null)
            .OrderByDescending(p => p).ToList();

        if (baselinePsms.Count > 0)
        {
            int numNotches = GetNumNotches(SearchParameters.MassDiffAcceptorType, SearchParameters.CustomMdac);
            var baselineFdrEngine = new FdrAnalysisEngine(
                baselinePsms, numNotches, CommonParameters,
                FileSpecificParameters, [taskId], "PSM", false, outputFolder, alreadySorted: true);
            baselineFdrEngine.Run();
        }

        _psmFdrAlignmentService.BuildBaselineCache(baselinePsms);
        _peptideFdrAlignmentService.BuildBaselineCache(baselinePsms);
        BuildBaselinePeptideToScanIndexLookup();

        if (SearchParameters.DoParsimony)
        {
            Status("Preparing baseline parsimony cache...", taskId);
            ReportTaskDashboard(taskId, ParallelSearchDashboardUpdateKind.TaskStatus, DashboardPhaseInitializing,
                "Preparing baseline parsimony cache...");

            var baselinePsmsForParsimony = FilteredPsms.Filter(baselinePsms,
                commonParams: CommonParameters,
                includeDecoys: true,
                includeContaminants: true,
                includeAmbiguous: false,
                includeHighQValuePsms: true);

            ProteinParsimonyResults baselineParsimonyResults = (ProteinParsimonyResults)new ProteinParsimonyEngine(
                baselinePsmsForParsimony, SearchParameters.ModPeptidesAreDifferent,
                CommonParameters, FileSpecificParameters, [taskId]).Run();

            if (baselineParsimonyResults.ProteinGroups.Count > 0)
            {
                ProteinScoringAndFdrResults baselineProteinScoringResults =
                    (ProteinScoringAndFdrResults)new ProteinScoringAndFdrEngine(
                        baselineParsimonyResults.ProteinGroups,
                        baselinePsms,
                        SearchParameters.NoOneHitWonders,
                        SearchParameters.ModPeptidesAreDifferent,
                        true,
                        CommonParameters,
                        FileSpecificParameters,
                        [taskId]).Run();

                CachedBaselineProteinGroups = baselineProteinScoringResults.SortedAndScoredProteinGroups
                    .Select(p => new CachedProteinGroup(p))
                    .ToList();

                _proteinGroupFdrAlignmentService.BuildBaselineCache(baselineProteinScoringResults
                    .SortedAndScoredProteinGroups);
            }
            else
            {
                CachedBaselineProteinGroups = [];
                _proteinGroupFdrAlignmentService.BuildBaselineCache([]);
            }
        }

        // Write Psms and Peptides from base search
        string psmFile = Path.Combine(outputFolder,
            $"Base{GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        var psmTask = WritePsmsToTsvAsync(BaseSearchPsms.Where(p => p != null).OrderByDescending(p => p), psmFile, SearchParameters.ModsToWriteSelection, false)
            .ContinueWith(_ => FinishedWritingFile(psmFile, [taskId]));

        var allPeptides = BaseSearchPsms.Where(p => p != null).CollapseToPeptides(true).OrderByDescending(p => p).ToList();
        string peptideFile = Path.Combine(outputFolder,
            $"Base{GlobalVariables.AnalyteType}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        var peptideTask = WritePsmsToTsvAsync(allPeptides, peptideFile, SearchParameters.ModsToWriteSelection, true)
            .ContinueWith(_ => FinishedWritingFile(peptideFile, [taskId]));

        Task proteinTask = Task.CompletedTask;
        if (SearchParameters.DoParsimony && CachedBaselineProteinGroups.Any())
        {
            var groups = CachedBaselineProteinGroups.Select(p => {
                var group = p.GetProteinGroup();
                group.GetIdentifiedPeptidesOutput(SearchParameters.SilacLabels);
                return group;                
                });
            string proteinFile = Path.Combine(outputFolder,
                $"Base{GlobalVariables.AnalyteType.GetBioPolymerLabel()}Groups.tsv");
            proteinTask = WriteProteinGroupsToTsvAsync(groups.ToList(), proteinFile)
                .ContinueWith(_ => FinishedWritingFile(proteinFile, [taskId]));
        }

        // Write prose for base settings
        ProseCreatedWhileRunning.Append(
            $"Base database contained {BaseBioPolymers.Count(p => !p.IsDecoy)} non-decoy protein entries. ");
        ProseCreatedWhileRunning.Append(
            $"Searching {ParallelSearchParameters.TransientDatabases.Count} transient databases against {currentRawFileList.Count} spectra files. ");

        Task.WhenAll([psmTask, peptideTask, proteinTask]).Wait();
    }

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
    /// Creates the unified results manager with configured collectors and statistical tests
    /// </summary>
    private TransientDatabaseResultsManager CreateResultsManager(string outputFolder)
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

        var testBuilder = new TestSuiteBuilder()
            .AddCountEnrichmentTests()
            .AddAmbiguityOrTargetDecoyTests()
            .AddScoreDistributionTests()
            .AddRetentionTimeTests()
            .AddFragmentationTests();

        if (ParallelSearchParameters.DoParsimony)
        {
            testBuilder.AddProteinGroupTests();
            collectors.Add(new ProteinGroupCollector("Homo sapiens"));
        }

        if (ParallelSearchParameters.DeNovoMappingDataFilePath != null && File.Exists(ParallelSearchParameters.DeNovoMappingDataFilePath))
        {
            testBuilder.AddDeNovoTests();
            collectors.Add(new DeNovoMappingCollector(ParallelSearchParameters.DeNovoMappingDataFilePath));
        }

        if (CommonParameters.DoPrecursorDeconvolution)
        {
            testBuilder.AddPrecursorDeconvolutionTests();
            // Decon results are hoovered up by teh psm peptide collector. 
        }

        var metricAggregator = new MetricAggregator(collectors);
        var tests = testBuilder.Build().ToList();

        return new TransientDatabaseResultsManager(
            metricAggregator,
            tests,
            analysisCachePath,
            qValueCutoff: Math.Min(CommonParameters.QValueThreshold, CommonParameters.PepQValueThreshold)
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

        Status($"Processing {dbName}...", nestedIds);

         // Check if we should skip or overwrite this database
         bool shouldProcess = !_resultsManager!.HasCachedResults(dbName) || 
                            ParallelSearchParameters.OverwriteTransientSearchOutputs;

        if (!shouldProcess)
        {
            ReportProgress(new(100, $"Skipping {dbName} - results already exist in cache", nestedIds));
            UpdateProgress(TotalDatabases, taskId);
            return;
        }

        ReportDatabaseDashboard(taskId, ParallelSearchDashboardUpdateKind.DatabaseStarted, dbName,
            $"Processing {dbName}...", DashboardDatabaseProcessingProgress);

         // Handle overwrite scenario
        if (ParallelSearchParameters.OverwriteTransientSearchOutputs && _resultsManager.HasCachedResults(dbName))
        {
            Status($"Overwriting existing results for {dbName}...", nestedIds);
            ReportDatabaseDashboard(taskId, ParallelSearchDashboardUpdateKind.DatabaseProgress, dbName,
                $"Overwriting existing results for {dbName}...", DashboardDatabaseOverwriteProgress);
            if (Directory.Exists(dbOutputFolder))
            {
                Directory.Delete(dbOutputFolder, true);
            }
        }

         if (!Directory.Exists(dbOutputFolder))
             Directory.CreateDirectory(dbOutputFolder);

        Status($"Loading transient database {dbName}...", nestedIds);
        ReportDatabaseDashboard(taskId, ParallelSearchDashboardUpdateKind.DatabaseProgress, dbName,
            $"Loading transient database {dbName}...", DashboardDatabaseLoadingProgress);

         // Load transient database
         var transientProteins = LoadTransientDatabase(transientDb, nestedIds, taskId);

         if (GlobalVariables.StopLoops)
         {
             return;
         }

         // Create HashSet of transient bioPolymer accessions for later filtering
         var transientProteinAccessions = new HashSet<string>(
             transientProteins.Select(p => p.Accession));

        Status($"Searching {dbName} ({transientProteins.Count} transient proteins)...", nestedIds);
        ReportDatabaseDashboard(taskId, ParallelSearchDashboardUpdateKind.DatabaseProgress, dbName,
            $"Searching {dbName} ({transientProteins.Count} transient proteins)...", DashboardDatabaseSearchStartProgress,
            DashboardDatabaseSearchStartProgress, DashboardDatabaseSearchEndProgress);

         // Reuse baseline PSMs with copy-on-write in peptide/proteoform mode.
         SpectralMatch[] psmArray = BaseSearchPsms.ToArray();
         PerformSearch(transientProteins, psmArray, nestedIds, out HashSet<int> updatedPsmIndexes, out int transientPeptideCount, useCopyOnWrite: true);

        Status($"Performing post-search analysis for {dbName}...", nestedIds);
        ReportDatabaseDashboard(taskId, ParallelSearchDashboardUpdateKind.DatabaseProgress, dbName,
            $"Performing post-search analysis for {dbName}...", DashboardDatabasePostSearchProgress);

         int totalProteins = BaseBioPolymers.Count + transientProteins.Count;
         
         // Process database through unified manager (handles analysis + statistical caching)
         var (analysisContext, dbResults) = PerformPostSearchAnalysis(
              psmArray,
              dbOutputFolder, 
              nestedIds,
             dbName, 
             totalProteins, 
             transientProteinAccessions, 
             transientPeptideCount, 
             transientProteins, 
              transientDb,
              updatedPsmIndexes
          ).GetAwaiter().GetResult();

         _completedDatabaseWriteChannel!.Writer.WriteAsync((analysisContext, dbResults)).AsTask().GetAwaiter().GetResult();

        // Cleanup transient proteins to free memory
        transientProteins.Clear();

        ReportProgress(new(100, $"Finished analysis for {dbName}; queued output writing", nestedIds));
    }

    /// <summary>
    /// Populates and returns the spectral match array using classic search engine
    /// </summary>
     private void PerformSearch(List<IBioPolymer> proteinsToSearch, SpectralMatch[] spectralMatchArray, List<string> nestedIds, out HashSet<int> updatedPsmIndexes, out int peptidesSearched, bool useCopyOnWrite = false)
     {
         var massDiffAcceptor = GetMassDiffAcceptor(
             CommonParameters.PrecursorMassTolerance,
             SearchParameters.MassDiffAcceptorType,
             SearchParameters.CustomMdac);

         // Run the classic search engine
          var searchEngine = new TransientClassicSearchEngine(
              spectralMatchArray, AllSortedMs2Scans, VariableModifications,
              FixedModifications, proteinsToSearch, massDiffAcceptor, CommonParameters,
              FileSpecificParameters, nestedIds, copyOnWriteEnabled: useCopyOnWrite);

         var results = searchEngine.Run();
         updatedPsmIndexes = (results as TransientSearchEngineResults)!.UpdatedSpectralMatchIndexes;
         peptidesSearched = (results as TransientSearchEngineResults)!.PeptidesSearched;
         ReportProgress(new(100, "Finished Classic Search...", nestedIds));
     }

    private async Task<(TransientDatabaseContext Context, TransientDatabaseMetrics Metrics)> PerformPostSearchAnalysis(
        SpectralMatch[] allPsms,
        string outputFolder,
        List<string> nestedIds,
        string dbName,
        int totalProteins,
        HashSet<string> transientProteinAccessions,
        int transientPeptideCount,
        List<IBioPolymer> transientProteins,
        DbForTask transientDatabase,
        HashSet<int> updatedPsmIndexes)
    {
        bool writeAllResults = !ParallelSearchParameters.WriteTransientResultsOnly;

        List<SpectralMatch> psmList = allPsms.Where(p => p != null)
            .OrderByDescending(p => p).ToList();

        #region FDR and Parsiomony

        Status($"Performing FDR Analysis for {dbName}...", nestedIds);
        ReportDatabaseDashboard(nestedIds[0], ParallelSearchDashboardUpdateKind.DatabaseProgress, dbName,
            $"Performing FDR Analysis for {dbName}...", DashboardDatabaseFdrProgress);

        // Disambiguate - modify PSMs in place - Only used for notch disambiguation and parallel search does not use notches. 
        //var disambiguationEngine = new DisambiguationEngine(
        //    psmList, CommonParameters, FileSpecificParameters, nestedIds);
        //await disambiguationEngine.RunAsync();
        //DebugStatus("Disambiguation complete", nestedIds, dbName, postAnalysisStopwatch);

        // Apply FDR Alignment to PSMs
        var transientPsms = FilterToTransientDatabaseOnly(psmList, transientProteinAccessions).ToList();
        _ = _psmFdrAlignmentService.ApplyBaseline(transientPsms);

        // Collapse to peptides and apply FDR alignment to peptides
        var allPeptides = psmList.CollapseToPeptides(true).ToList();
        var transientPeptides = FilterToTransientDatabaseOnly(allPeptides, transientProteinAccessions).ToList();
        _ = _peptideFdrAlignmentService.ApplyBaseline(transientPeptides);

        List<ProteinGroup>? proteinGroups = null;
        if (SearchParameters.DoParsimony && transientPsms.Count > 0)
        {
            Status($"Performing parsimony for {dbName}...", nestedIds);
            ReportDatabaseDashboard(nestedIds[0], ParallelSearchDashboardUpdateKind.DatabaseProgress, dbName,
                $"Performing parsimony for {dbName}...", DashboardDatabaseParsimonyProgress);

            var transientParsimonyEngine = new TransientProteinParsimonyEngine(
                allPsms,
                BaseSearchPsms,
                updatedPsmIndexes,
                transientProteins,
                _baselinePeptideKeyToScanIndexes,
                SearchParameters.ModPeptidesAreDifferent,
                CommonParameters,
                FileSpecificParameters,
                nestedIds);

            ProteinParsimonyResults proteinAnalysisResults =
                (ProteinParsimonyResults)await transientParsimonyEngine.RunAsync();
            List<ProteinGroup> transientParsimonyGroups = proteinAnalysisResults.ProteinGroups;
            List<SpectralMatch> transientPsmsForParsimony = transientParsimonyEngine.NeighborhoodPsms;

            //var baselineRuntimeGroups = CachedBaselineProteinGroups
            //    .Select(group => group.CreateRuntimeCopy())
            //    .ToList();

            if (CachedBaselineProteinGroups.Count > 0 || transientParsimonyGroups.Count > 0)
            {
                // Writing is a mutation process for Protein Groups, so if we are writing ALL (not just transient) we need to make a copy.
                // If we are only writing transient results, we can get away with not copying the baseline groups.
                List<ProteinGroup> baselineProteinGroups =  CachedBaselineProteinGroups
                    .Select(pg => writeAllResults ? pg.CreateRuntimeCopy() : pg.GetProteinGroup())
                    .ToList();

                ProteinScoringAndFdrResults proteinScoringAndFdrResults =
                        (ProteinScoringAndFdrResults)await new TransientProteinScoringAndFdrEngine(
                            baselineProteinGroups,
                            transientParsimonyGroups,
                            transientPsmsForParsimony,
                            _proteinGroupFdrAlignmentService,
                            SearchParameters.NoOneHitWonders, SearchParameters.ModPeptidesAreDifferent,
                            true, CommonParameters, FileSpecificParameters, nestedIds).RunAsync();

                proteinGroups = proteinScoringAndFdrResults.SortedAndScoredProteinGroups;
            }
            else
            {
                proteinGroups = [];
            }
        }

        #endregion

        List<ProteinGroup>? transientProteinGroups = null;
        if (proteinGroups is not null)
        {
            // Count bioPolymer groups that contain at least one transient database bioPolymer
            transientProteinGroups =
                FilterProteinGroupsToTransientDatabaseOnly(proteinGroups, transientProteinAccessions)
                .Select(p => new CachedProteinGroup(p).CreateRuntimeCopy())
                .ToList();
        }

        #region Result Caching and Analysis

        // Create analysis context
        var analysisContext = new TransientDatabaseContext
        {
            DatabaseName = dbName,
            TransientDatabase = transientDatabase,
            TransientProteins = transientProteins,
            TransientProteinAccessions = transientProteinAccessions,
            AllPsms = psmList,
            TransientPsms = transientPsms,
            AllPeptides = allPeptides,
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
        ReportDatabaseDashboard(nestedIds[0], ParallelSearchDashboardUpdateKind.DatabaseProgress, dbName,
            $"Running analysis for {dbName}...", DashboardDatabaseAnalysisProgress);
        var aggregatedResult = _resultsManager!.ProcessDatabase(
            analysisContext,
            forceRecompute: ParallelSearchParameters.OverwriteTransientSearchOutputs
        );

        #endregion

        return await Task.FromResult((analysisContext, aggregatedResult));
    }

    #endregion

    #region Result Writing

    private void InitializeCompletedDatabaseWriter()
    {
        _completedDatabaseWriteChannel = Channel.CreateBounded<(TransientDatabaseContext Context, TransientDatabaseMetrics Metrics)>(
            new BoundedChannelOptions(2)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });
        _completedDatabaseWriterTask = Task.Run(RunCompletedDatabaseWriterLoopAsync);
    }

    private void CompleteCompletedDatabaseWriter()
    {
        if (_completedDatabaseWriteChannel == null)
            return;

        _completedDatabaseWriteChannel.Writer.TryComplete();
        _completedDatabaseWriterTask?.GetAwaiter().GetResult();
        _completedDatabaseWriteChannel = null;
        _completedDatabaseWriterTask = null;
    }

    private async Task RunCompletedDatabaseWriterLoopAsync()
    {
        var channel = _completedDatabaseWriteChannel ?? throw new InvalidOperationException("Completed database writer channel is not initialized.");

        try
        {
            await foreach (var (context, metrics) in channel.Reader.ReadAllAsync())
            {
                await WriteCompletedDatabaseOutputsAsync(context, metrics);
            }
        }
        catch (Exception ex)
        {
            channel.Writer.TryComplete(ex);
            throw;
        }
    }

    private async Task WriteCompletedDatabaseOutputsAsync(TransientDatabaseContext context, TransientDatabaseMetrics metrics)
    {
        string dbName = context.DatabaseName;
        string outputFolder = context.OutputFolder;
        List<string> nestedIds = context.NestedIds;
        bool writeAllResults = !ParallelSearchParameters.WriteTransientResultsOnly;

        Status($"Writing results for {dbName}...", nestedIds);
        ReportDatabaseDashboard(nestedIds[0], ParallelSearchDashboardUpdateKind.DatabaseProgress, dbName,
            $"Writing results for {dbName}...", DashboardDatabaseWritingProgress);

        string transientPsmFile = Path.Combine(outputFolder,
            $"{dbName}_All{GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        await WritePsmsToTsvAsync(context.TransientPsms, transientPsmFile, SearchParameters.ModsToWriteSelection, false);
        FinishedWritingFile(transientPsmFile, nestedIds);

        if (writeAllResults)
        {
            string psmFile = Path.Combine(outputFolder,
                $"All{GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
            await WritePsmsToTsvAsync(context.AllPsms, psmFile, SearchParameters.ModsToWriteSelection, false);
            FinishedWritingFile(psmFile, nestedIds);
        }

        if (context.TransientPeptides is { Count: > 0 })
        {
            string transientPeptideFile = Path.Combine(outputFolder,
            $"{dbName}_All{GlobalVariables.AnalyteType}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
            await WritePsmsToTsvAsync(context.TransientPeptides, transientPeptideFile, SearchParameters.ModsToWriteSelection, true);
            FinishedWritingFile(transientPeptideFile, nestedIds);
        }

        if (writeAllResults)
        {
            string peptideFile = Path.Combine(outputFolder,
                $"All{GlobalVariables.AnalyteType}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
            await WritePsmsToTsvAsync(context.AllPeptides, peptideFile, SearchParameters.ModsToWriteSelection, true);
            FinishedWritingFile(peptideFile, nestedIds);
        }

        if (context.TransientProteinGroups is { Count: > 0 })
        {
            context.TransientProteinGroups.ForEach(x => x.GetIdentifiedPeptidesOutput(SearchParameters.SilacLabels));
            string transientProteinFile = Path.Combine(outputFolder,
                $"{dbName}_All{GlobalVariables.AnalyteType.GetBioPolymerLabel()}Groups.tsv");
            await WriteProteinGroupsToTsvAsync(context.TransientProteinGroups, transientProteinFile);
            FinishedWritingFile(transientProteinFile, nestedIds);
        }

        if (writeAllResults && context.ProteinGroups is { Count: > 0 })
        {
            context.ProteinGroups.ForEach(x => x.GetIdentifiedPeptidesOutput(SearchParameters.SilacLabels));
            string proteinFile = Path.Combine(outputFolder,
                $"All{GlobalVariables.AnalyteType.GetBioPolymerLabel()}Groups.tsv");
            await WriteProteinGroupsToTsvAsync(context.ProteinGroups, proteinFile);
            FinishedWritingFile(proteinFile, nestedIds);
        }

        if (SearchParameters.WriteSpectralLibrary)
        {
            string spectralLibraryPath = Path.Combine(outputFolder, $"AllPeptidesAnd_{dbName}_SpectralLibrary.msp");
            await WriteSpectralLibraryAsync(context.AllPsms, spectralLibraryPath);
            FinishedWritingFile(spectralLibraryPath, nestedIds);
        }

        if (ParallelSearchParameters.WriteTransientSpectralLibrary)
        {
            string spectralLibraryPath = Path.Combine(outputFolder, $"{dbName}_SpectralLibrary.msp");
            await WriteSpectralLibraryAsync(context.TransientPsms, spectralLibraryPath);
            FinishedWritingFile(spectralLibraryPath, nestedIds);
        }

        await WriteIndividualDatabaseResultsTextAsync(metrics, outputFolder, nestedIds);

        if (ParallelSearchParameters.CompressTransientSearchOutputs)
        {
            Status($"Compressing output for {dbName}...", nestedIds);
            ReportDatabaseDashboard(nestedIds[0], ParallelSearchDashboardUpdateKind.DatabaseProgress, dbName,
                $"Compressing output for {dbName}...", DashboardDatabaseCompressionProgress);
            CompressTransientDatabaseOutput(outputFolder);
        }

        _resultsManager!.AppendCheckpoint(metrics);
        MarkDatabaseCompleted();
        UpdateProgress(TotalDatabases, nestedIds[0]);
        ReportDatabaseDashboard(nestedIds[0], ParallelSearchDashboardUpdateKind.DatabaseFinished, dbName,
            $"Finished {dbName}", DashboardDatabaseFinishedProgress);
        ReportProgress(new(100, $"Finished {dbName}", nestedIds));
    }

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
         var writtenFiles = _resultsManager!.WriteAllResults(outputFolder);
         foreach (var writtenFile in writtenFiles)
             FinishedWritingFile(writtenFile, [taskId]); 

         // Write global summary text file
         WriteGlobalResultsText(_resultsManager.TransientDatabaseMetricsDictionary, outputFolder, taskId, numFiles);

        // Deal with custom reduced database writing
        // TODO: Revise this to only be the family one. 
        bool useFamilyRanking = ParallelSearchParameters.UseFamilyAwareRanking;
        var statsByDatabase = useFamilyRanking
            ? SelectDatabasesForWritingByFamily()
            : SelectDatabasesForWritingByTestRatio();

         Task[] dbWritingTasks = new Task[3];
         if (statsByDatabase.Count > 0)
         {
              Log($"Found {statsByDatabase.Count} significant databases passing cutoff (mode: {(useFamilyRanking ? "family-aware" : "test-ratio")})", [taskId]);

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
         }
         else
         {
             Log("No databases passed the significance cutoff for combined FASTA output", new List<string> { taskId });
         }
     }

    private Dictionary<DbForTask, List<StatisticalTestResult>> SelectDatabasesForWritingByTestRatio()
    {
        int sigPassedCutoff = (int)(_resultsManager!.StatisticalTestCount * ParallelSearchParameters.TestRatioForWriting);
        return _resultsManager.StatisticalTestResultList
            .GroupBy(p => p.DatabaseName)
            .Where(p => p.Count(t => t.IsSignificant()) >= sigPassedCutoff)
            .ToDictionary(
                p => ParallelSearchParameters.TransientDatabases.First(db => Path.GetFileNameWithoutExtension(db.FileName) == p.Key),
                p => p.OrderBy(t => t.ToString()).ToList());
    }

    private Dictionary<DbForTask, List<StatisticalTestResult>> SelectDatabasesForWritingByFamily()
    {
        double qValueThreshold = CommonParameters.QValueThreshold;
        int minFamilyPasses = Math.Max(1, ParallelSearchParameters.TestRatioForWriting > 0
            ? (int)Math.Ceiling(ParallelSearchParameters.TestRatioForWriting * 7)
            : 1);

        return _resultsManager!.StatisticalTestResultList
            .GroupBy(p => p.DatabaseName)
            .Where(p =>
            {
                if (!_resultsManager!.TransientDatabaseMetricsDictionary.TryGetValue(p.Key, out var metrics))
                    return false;

                int passedFamilies = metrics.PassedFamilyCount;
                double combinedQ = metrics.CombinedQValue;

                return passedFamilies >= minFamilyPasses
                    && !double.IsNaN(combinedQ)
                    && combinedQ <= qValueThreshold;
            })
            .ToDictionary(
                p => ParallelSearchParameters.TransientDatabases.First(db => Path.GetFileNameWithoutExtension(db.FileName) == p.Key),
                p => p.OrderBy(t => t.ToString()).ToList());
    }



    private void WriteGlobalResultsText(IReadOnlyDictionary<string, TransientDatabaseMetrics> databaseResults,
        string outputFolder, string taskId, int numFiles)
    {
        var orderedResults = databaseResults
            .OrderBy(kvp => kvp.Key)
            .ToList();

        var perDatabaseSummaries = orderedResults
            .Select(kvp => new
            {
                DatabaseName = kvp.Key,
                kvp.Value.TotalProteins,
                kvp.Value.TransientProteinCount,
                kvp.Value.TransientPeptideCount,
                kvp.Value.TargetPsmsAtQValueThreshold,
                kvp.Value.TargetPsmsFromTransientDb,
                kvp.Value.TargetPsmsFromTransientDbAtQValueThreshold,
                kvp.Value.TargetPeptidesAtQValueThreshold,
                kvp.Value.TargetPeptidesFromTransientDb,
                kvp.Value.TargetPeptidesFromTransientDbAtQValueThreshold,
                kvp.Value.ValidTestCount,
                kvp.Value.StatisticalTestsPassed,
                kvp.Value.ValidFamilyCount,
                kvp.Value.PassedFamilyCount,
                kvp.Value.TargetProteinGroupsAtQValueThreshold,
                kvp.Value.TargetProteinGroupsFromTransientDb,
                kvp.Value.TargetProteinGroupsFromTransientDbAtQValueThreshold,
            })
            .ToList();

        // Calculate aggregate values before writing (use long to avoid Sum(int) overflow).
        double qValuePercent = CommonParameters.QValueThreshold * 100;
        long totalProteins = perDatabaseSummaries.Sum(r => (long)r.TotalProteins);
        long totalTransientPeptides = perDatabaseSummaries.Sum(r => (long)r.TransientPeptideCount);
        long totalTargetPsmsAtQValueThreshold = perDatabaseSummaries.Sum(r => (long)r.TargetPsmsAtQValueThreshold);
        long totalTargetPsmsFromTransientDb = perDatabaseSummaries.Sum(r => (long)r.TargetPsmsFromTransientDb);
        long totalTargetPsmsFromTransientDbAtQValueThreshold = perDatabaseSummaries.Sum(r => (long)r.TargetPsmsFromTransientDbAtQValueThreshold);
        long totalTargetPeptidesAtQValueThreshold = perDatabaseSummaries.Sum(r => (long)r.TargetPeptidesAtQValueThreshold);
        long totalTargetPeptidesFromTransientDb = perDatabaseSummaries.Sum(r => (long)r.TargetPeptidesFromTransientDb);
        long totalTargetPeptidesFromTransientDbAtQValueThreshold = perDatabaseSummaries.Sum(r => (long)r.TargetPeptidesFromTransientDbAtQValueThreshold);
        long totalTransientProteinCount = perDatabaseSummaries.Sum(r => (long)r.TransientProteinCount);
        long totalTargetProteinGroupsAtQValueThreshold = perDatabaseSummaries.Sum(r => (long)r.TargetProteinGroupsAtQValueThreshold);
        long totalTargetProteinGroupsFromTransientDb = perDatabaseSummaries.Sum(r => (long)r.TargetProteinGroupsFromTransientDb);
        long totalTargetProteinGroupsFromTransientDbAtQValueThreshold = perDatabaseSummaries.Sum(r => (long)r.TargetProteinGroupsFromTransientDbAtQValueThreshold);

        long baselineProteinCount = perDatabaseSummaries.Count > 0
            ? Math.Max(0, (long)perDatabaseSummaries[0].TotalProteins - perDatabaseSummaries[0].TransientProteinCount)
            : 0;
        long totalProteinsSearched = baselineProteinCount + totalTransientProteinCount;

        var summaryPath = Path.Combine(outputFolder, "ParallelSearchSummary.txt");

        using (StreamWriter file = new StreamWriter(summaryPath))
        {
            file.WriteLine("=== Parallel Search Task Summary ===");
            file.WriteLine();
            file.WriteLine($"Spectra files analyzed: {numFiles}");
            file.WriteLine($"Total MS2 scans: {TotalMs2Scans}");
            file.WriteLine($"Transient databases searched: {perDatabaseSummaries.Count}");
            file.WriteLine();

            file.WriteLine("=== Aggregate Statistics ===");
            file.WriteLine($"Total Proteins Searched: {totalProteinsSearched}");
            file.WriteLine($"Total PSMs identified: {totalTargetPsmsAtQValueThreshold}");
            file.WriteLine($"Total target PSMs at {qValuePercent}% FDR: {totalTargetPsmsAtQValueThreshold}");
            file.WriteLine($"Total target PSMs from transient DBs: {totalTargetPsmsFromTransientDb}");
            file.WriteLine($"Total target PSMs from transient DBs (FDR {qValuePercent}%): {totalTargetPsmsFromTransientDbAtQValueThreshold}");
            file.WriteLine($"Total target peptides at {qValuePercent}% FDR: {totalTargetPeptidesAtQValueThreshold}");
            file.WriteLine($"Total target peptides from transient DBs: {totalTargetPeptidesFromTransientDb}");
            file.WriteLine($"Total target peptides from transient DBs (FDR {qValuePercent}%): {totalTargetPeptidesFromTransientDbAtQValueThreshold}");
            file.WriteLine();

            file.WriteLine("=== Results by Database ===");
            file.WriteLine();

            foreach (var result in perDatabaseSummaries)
            {
                file.WriteLine($"Database: {result.DatabaseName}");
                file.WriteLine($"  Passed families: {result.PassedFamilyCount}/{result.ValidFamilyCount}");
                file.WriteLine($"  Passed tests: {result.StatisticalTestsPassed}/{result.ValidTestCount}");
                file.WriteLine($"  Total proteins: {result.TotalProteins}");
                file.WriteLine($"  Transient proteins: {result.TransientProteinCount}");
                file.WriteLine($"  Target PSMs (FDR {qValuePercent}%): {result.TargetPsmsAtQValueThreshold}");
                file.WriteLine($"  Target PSMs from transient DB: {result.TargetPsmsFromTransientDb}");
                file.WriteLine($"  Target PSMs from transient DB (FDR {qValuePercent}%): {result.TargetPsmsFromTransientDbAtQValueThreshold}");
                file.WriteLine($"  Target peptides (FDR {qValuePercent}%): {result.TargetPeptidesAtQValueThreshold}");
                file.WriteLine($"  Target peptides from transient DB: {result.TargetPeptidesFromTransientDb}");
                file.WriteLine($"  Target peptides from transient DB (FDR {qValuePercent}%): {result.TargetPeptidesFromTransientDbAtQValueThreshold}");

                if (SearchParameters.DoParsimony)
                {
                    file.WriteLine($"  Target protein groups (FDR {qValuePercent}%): {result.TargetProteinGroupsAtQValueThreshold}");
                    file.WriteLine($"  Target protein groups with transient DB proteins: {result.TargetProteinGroupsFromTransientDb}");
                    file.WriteLine($"  Target protein groups with transient DB proteins (FDR {qValuePercent}%): {result.TargetProteinGroupsFromTransientDbAtQValueThreshold}");
                }

                file.WriteLine();
            }

            if (SearchParameters.DoParsimony)
            {
                file.WriteLine($"Total Protein Groups (FDR {qValuePercent}%): {totalTargetProteinGroupsAtQValueThreshold}");
                file.WriteLine($"Total Protein Groups with transient DB proteins: {totalTargetProteinGroupsFromTransientDb}");
                file.WriteLine($"Total Protein Groups with transient DB proteins (FDR {qValuePercent}%): {totalTargetProteinGroupsFromTransientDbAtQValueThreshold}");
            }
        }

        FinishedWritingFile(summaryPath, new List<string> { taskId });

        // Add summary to task results
        MyTaskResults.AddTaskSummaryText($"Searched {perDatabaseSummaries.Count} transient databases against {numFiles} spectra files.");
        MyTaskResults.AddTaskSummaryText($"  Total proteins: {totalProteins}");
        MyTaskResults.AddTaskSummaryText($"  Transient proteins: {totalTransientProteinCount}");
        MyTaskResults.AddTaskSummaryText($"  Total Peptides: {totalTransientPeptides + PersistentDatabasePeptideCount}");
        MyTaskResults.AddTaskSummaryText($"  Transient peptides: {totalTransientPeptides}");

        MyTaskResults.AddTaskSummaryText("\n");
        MyTaskResults.AddTaskSummaryText($"Total target PSMs at {qValuePercent}% FDR: {totalTargetPsmsAtQValueThreshold}");
        MyTaskResults.AddTaskSummaryText($"Target PSMs from transient databases: {totalTargetPsmsFromTransientDb}");
        MyTaskResults.AddTaskSummaryText($"Target PSMs from transient databases at {qValuePercent}% FDR: {totalTargetPsmsFromTransientDbAtQValueThreshold}");

        MyTaskResults.AddTaskSummaryText("\n");
        MyTaskResults.AddTaskSummaryText($"Total target peptides at {qValuePercent}% FDR: {totalTargetPeptidesAtQValueThreshold}");
        MyTaskResults.AddTaskSummaryText($"Target peptides from transient databases: {totalTargetPeptidesFromTransientDb}");
        MyTaskResults.AddTaskSummaryText($"Target peptides from transient databases at {qValuePercent}% FDR: {totalTargetPeptidesFromTransientDbAtQValueThreshold}");

        if (SearchParameters.DoParsimony)
        {
            MyTaskResults.AddTaskSummaryText("\n");
            MyTaskResults.AddTaskSummaryText($"Total Protein Groups at {qValuePercent}% FDR: {totalTargetProteinGroupsAtQValueThreshold}");
            MyTaskResults.AddTaskSummaryText($"Protein Groups with transient database proteins: {totalTargetProteinGroupsFromTransientDb}");
            MyTaskResults.AddTaskSummaryText($"Protein Groups with transient database proteins at {qValuePercent}% FDR: {totalTargetProteinGroupsFromTransientDbAtQValueThreshold}");
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

            // Read bioPolymer groups file to get detected accessions
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

                // Skip if not a target bioPolymer
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

            // Read peptide file to get bioPolymer accessions with detected peptides
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

                // Parse bioPolymer accession(s) - handle multiple accessions separated by '|'
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
        HashSet<string> transientProteinAccessions, bool allowAmbiguous = true)
    {
        foreach (var psm in spectralMatches)
        {
            bool hasTransientProtein = false;

            if (allowAmbiguous)
            {
                foreach (var match in psm.BestMatchingBioPolymersWithSetMods)
                {
                    if (transientProteinAccessions.Contains(match.SpecificBioPolymer.Parent.Accession))
                    {
                        hasTransientProtein = true;
                        break;
                    }
                }
            }
            else if (psm.BestMatchingBioPolymersWithSetMods.All(p => transientProteinAccessions.Contains(p.SpecificBioPolymer.Parent.Accession)))
                hasTransientProtein = true;

            if (hasTransientProtein)
            {
                yield return psm;
            }
        }
    }

    /// <summary>
    /// Filters bioPolymer groups to only include those where all proteins are from the transient database
    /// </summary>
    private IEnumerable<ProteinGroup> FilterProteinGroupsToTransientDatabaseOnly(List<ProteinGroup> proteinGroups,
        HashSet<string> transientProteinAccessions)
    {
        return proteinGroups
            .Where(pg => pg.Proteins.Any(p => transientProteinAccessions.Contains(p.Accession)));
    }

    private void BuildBaselinePeptideToScanIndexLookup()
    {
        _baselinePeptideKeyToScanIndexes.Clear();

        for (int scanIndex = 0; scanIndex < BaseSearchPsms.Length; scanIndex++)
        {
            var psm = BaseSearchPsms[scanIndex];
            if (psm is null)
                continue;

            HashSet<string> keysSeenForScan = new(StringComparer.Ordinal);
            foreach (var hypothesis in psm.BestMatchingBioPolymersWithSetMods)
            {
                string peptideKey = TransientProteinParsimonyEngine.GetParsimonyPeptideKey(
                    hypothesis.SpecificBioPolymer,
                    SearchParameters.ModPeptidesAreDifferent);
                if (!keysSeenForScan.Add(peptideKey))
                    continue;

                if (!_baselinePeptideKeyToScanIndexes.TryGetValue(peptideKey, out var scanIndexes))
                {
                    scanIndexes = [];
                    _baselinePeptideKeyToScanIndexes.Add(peptideKey, scanIndexes);
                }

                scanIndexes.Add(scanIndex);
            }
        }
    }

    #endregion

    #region UI Helpers 
    private void InitializeDashboardState(CacheSummary cacheSummary)
    {
        lock (_dashboardLock)
        {
            _dashboardCachedAtStart = cacheSummary.CachedDatabases;
            _dashboardCompletedThisRun = 0;
            _dashboardInitialFinishedCount = ParallelSearchParameters.OverwriteTransientSearchOutputs
                ? 0
                : cacheSummary.CachedDatabases;
        }
    }

    private void MarkDatabaseCompleted()
    {
        lock (_dashboardLock)
        {
            _dashboardCompletedThisRun++;
        }
    }

    private (int Finished, int Todo, int Cached) GetDashboardCounts()
    {
        lock (_dashboardLock)
        {
            int finished = _dashboardInitialFinishedCount + _dashboardCompletedThisRun;
            finished = Math.Min(finished, TotalDatabases);
            int todo = Math.Max(0, TotalDatabases - finished);
            return (finished, todo, _dashboardCachedAtStart);
        }
    }

    private void ReportTaskDashboard(string taskId, ParallelSearchDashboardUpdateKind updateKind, string taskPhase, string statusText)
    {
        var counts = GetDashboardCounts();
        ReportParallelSearchDashboard(new ParallelSearchDashboardEventArgs(
            taskId,
            updateKind,
            taskPhase,
            counts.Finished,
            TotalDatabases,
            counts.Todo,
            counts.Cached,
            statusText: statusText));
    }

    private void ReportDatabaseDashboard(string taskId, ParallelSearchDashboardUpdateKind updateKind, string databaseName,
        string statusText, int progressPercent, int? engineProgressMinimum = null, int? engineProgressMaximum = null)
    {
        var counts = GetDashboardCounts();
        ReportParallelSearchDashboard(new ParallelSearchDashboardEventArgs(
            taskId,
            updateKind,
            DashboardPhaseSearching,
            counts.Finished,
            TotalDatabases,
            counts.Todo,
            counts.Cached,
            databaseName,
            statusText,
            progressPercent,
            engineProgressMinimum,
            engineProgressMaximum));
    }

    private void UpdateProgress(int totalDatabases, string taskId)
    {
        lock (_progressLock)
        {
            var counts = GetDashboardCounts();
            ReportProgress(new ProgressEventArgs(
                totalDatabases == 0 ? 100 : (int)(counts.Finished / (double)totalDatabases * 100),
                $"Completed {counts.Finished}/{totalDatabases} databases",
                new List<string> { taskId }));
        }
    }

    #endregion
}
