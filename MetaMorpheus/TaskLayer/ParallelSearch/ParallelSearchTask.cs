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
using Chromatography.RetentionTimePrediction;
using Omics;
using Omics.Fragmentation;
using Omics.Modifications;
using Omics.SpectrumMatch;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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

    // Phase timing (P0 of GPU plan): reveals where wall-clock actually goes so GPU effort
    // targets the real hot spot. Inner-loop split is accumulated across the parallel
    // per-database loop via Interlocked; coarse phases are timed in RunSpecific.
    private long _searchEngineTicks;     // time inside TransientClassicSearchEngine.Run
    private long _postAnalysisTicks;     // time inside PerformPostSearchAnalysis

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

    // Calibration LEARNED from the base search (S1), applied to the .msl candidate pre-filter (S3):
    // precursor tolerance, and the iRT→observed-RT regression + window. Null until learned (and stays
    // null when no transient database is a .msl library). See LearnMslCalibration.
    [TomlIgnore] private MslCandidateCalibration? _mslCalibration;
    // Scan precursor masses (ascending) + their RTs, cached once (identical across all .msl databases).
    [TomlIgnore] private double[]? _sortedScanMasses;
    [TomlIgnore] private double[]? _scanRetentionTimes;

    // PEP model TRAINED ONCE on the base (human) search and reused to assign a PEP to every transient
    // database's PSMs (they're too small to train their own model, and are out-of-sample vs the base).
    [TomlIgnore] private PepAnalysisEngine? _pepEngine;
    // The PEP model's RT-feature predictor (Chronologer); lives as long as _pepEngine, disposed at task end.
    [TomlIgnore] private Chromatography.RetentionTimePrediction.IRetentionTimePredictor? _pepRtPredictor;


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

        // Phase timing (P0 of GPU plan) — coarse wall-clock per phase.
        var swInit = Stopwatch.StartNew();

        // Initialize all necessary data structures including base search
        Initialize(taskId, dbFilenameList, currentRawFileList, fileSettingsList, outputFolder);
        InitializeCompletedDatabaseWriter();
        swInit.Stop();

        Status($"Starting search of {TotalDatabases} transient databases...", taskId);
        ReportTaskDashboard(taskId, ParallelSearchDashboardUpdateKind.TaskStatus, DashboardPhaseSearching,
            $"Searching {TotalDatabases} transient databases...");

        // MERGED-INDEX mode: one .msl file holds many databases (entries tagged "db|accession"). The number
        // of databases to search comes from inside the file, not the (count==1) file list, so parallelism
        // must not be capped by the file count.
        // MERGED-INDEX mode also supports SHARDING: several merged .msl files, each holding many
        // databases (entries tagged "db|accession"). A single merged file exceeds the format's 2^31
        // precursor cap at production scale, so the library is split into shards with each database
        // kept WHOLE in exactly one shard — the per-database union across shards is therefore clean.
        bool mergedMode = ParallelSearchParameters.UseMergedTransientLibrary
            && ParallelSearchParameters.TransientDatabases.Count >= 1
            && ParallelSearchParameters.TransientDatabases.All(d => d.FilePath.EndsWith(".msl", StringComparison.OrdinalIgnoreCase));

        // Determine optimal thread allocation
        int totalAvailableThreads = Environment.ProcessorCount;
         int databaseParallelism = mergedMode
             ? ParallelSearchParameters.MaxSearchesInParallel
             : Math.Min(ParallelSearchParameters.MaxSearchesInParallel,
                 ParallelSearchParameters.TransientDatabases.Count);
         int threadsPerDatabase = Math.Max(1, totalAvailableThreads / databaseParallelism);
         CommonParameters.MaxThreadsToUsePerFile = threadsPerDatabase;

        // S4: LOAD-AHEAD PRODUCER/CONSUMER. Database loading (now an I/O-bound .msl index query + candidate
        // fragment fetch) runs ahead on producer threads and hands PREPARED databases to searcher threads
        // through a bounded queue, so loading overlaps with the CPU-bound search instead of blocking it.
        // The result WRITE channel was already pipelined; this pipelines the read side too. The bounded
        // capacity caps how many loaded databases are held in memory at once.
         var swTransientLoop = Stopwatch.StartNew();
         // Abort signal: if a CONSUMER (search) faults, cancel so the producer stops blocking on Add into a
         // bounded queue that is no longer being drained, instead of being abandoned until queue disposal.
         using (var loadAbort = new System.Threading.CancellationTokenSource())
         using (var loadedQueue = new System.Collections.Concurrent.BlockingCollection<LoadedTransientDatabase>(
                    boundedCapacity: Math.Max(2, databaseParallelism)))
         {
             // MERGED-INDEX mode (mergedMode computed above): a single .msl holds every database (each entry
             // tagged "db|accession"). Load it ONCE and run the candidate filter ONCE, then emit one prepared
             // database per source db-group. Each is searched INDEPENDENTLY by the consumer below (own
             // base-PSM copy) — databases never compete; this only collapses 1000s of file opens into one
             // shared in-memory index.

             // Producers: load databases concurrently, blocking on the bounded queue when it is full.
             var producer = Task.Run(() =>
             {
                 try
                 {
                     if (mergedMode)
                     {
                         // One or more merged shards. Load each in turn, run the candidate filter once per
                         // shard, and emit one prepared database per source db-group. Because every database
                         // is wholly contained in a single shard, db-groups never collide across shards.
                         var priors = BuildCandidatePriors();
                         // Parallelize the per-shard load+filter. It was serial (~72s/shard: LoadIndexOnly +
                         // single-threaded candidate filter over ~18M entries + GetEntry fetch), i.e. ~3.2h at
                         // 160 shards and the wall bottleneck (the consumer search parallelizes fine). Each shard
                         // has its OWN index/file handle, so K shards load concurrently; GetEntry within a shard
                         // stays single-threaded (one FileStream). Bounded by MM_PARALLELSEARCH_PRODUCERS (default
                         // 4) to not oversubscribe the consumer search or the disk; the loadedQueue still caps RAM.
                         int producerDop = int.TryParse(Environment.GetEnvironmentVariable("MM_PARALLELSEARCH_PRODUCERS"), out var pdv) && pdv > 0
                             ? pdv : 4;
                         producerDop = Math.Max(1, Math.Min(producerDop, ParallelSearchParameters.TransientDatabases.Count));
                         Parallel.ForEach(ParallelSearchParameters.TransientDatabases,
                             new ParallelOptions { MaxDegreeOfParallelism = producerDop },
                             mergedDb =>
                             {
                                 if (GlobalVariables.StopLoops) return;
                                 string shardName = Path.GetFileNameWithoutExtension(mergedDb.FilePath);
                                 Status($"Loading merged transient index {shardName}...", taskId);
                                 var grouped = MslPeptideReader.ReadCandidatesGroupedByDatabase(
                                     mergedDb.FilePath, priors);
                                 Status($"Merged index {shardName}: {grouped.Count} databases with candidates.", taskId);
                                 // Emit this shard's prepared db-groups serially (light work; the outer loop is
                                 // already K-way parallel, so an inner Parallel.ForEach would just oversubscribe).
                                 foreach (var kvp in grouped)
                                 {
                                     if (GlobalVariables.StopLoops) return;
                                     var loaded = BuildLoadedFromCandidates(kvp.Key, kvp.Value, outputFolder, taskId);
                                     if (loaded != null) loadedQueue.Add(loaded, loadAbort.Token);
                                 }
                             });
                     }
                     else
                     {
                         Parallel.ForEach(ParallelSearchParameters.TransientDatabases,
                             new ParallelOptions { MaxDegreeOfParallelism = databaseParallelism },
                             transientDbPath =>
                             {
                                 if (GlobalVariables.StopLoops) return;
                                 LoadedTransientDatabase? loaded;
                                 try
                                 {
                                     loaded = LoadTransientDatabaseForPipeline(transientDbPath, outputFolder, taskId);
                                 }
                                 catch (Exception ex)
                                 {
                                     WriteTransientProcessError(transientDbPath, outputFolder, ex);
                                     throw;
                                 }
                                 if (loaded != null)
                                     loadedQueue.Add(loaded, loadAbort.Token);
                             });
                     }
                 }
                 finally
                 {
                     loadedQueue.CompleteAdding();
                 }
             });

             try
             {
                 // Consumers: search each loaded database as it becomes available.
                 Parallel.ForEach(loadedQueue.GetConsumingEnumerable(),
                     new ParallelOptions { MaxDegreeOfParallelism = databaseParallelism },
                     loaded =>
                     {
                         if (GlobalVariables.StopLoops) return;
                         try
                         {
                             SearchLoadedTransientDatabase(loaded, taskId);
                         }
                         catch (Exception ex)
                         {
                             WriteTransientProcessError(loaded.TransientDb, outputFolder, ex);
                             throw;
                         }
                     });
             }
             catch
             {
                 // A searcher faulted and Parallel.ForEach stopped draining the queue. Signal the producer so a
                 // load thread blocked on the bounded Add unwinds promptly instead of being abandoned; then rethrow.
                 loadAbort.Cancel();
                 throw;
             }
             finally
             {
                 CompleteCompletedDatabaseWriter();
                 // ALWAYS observe the producer task: on the happy path this surfaces a load exception; on the
                 // failure path it prevents an unobserved task / abandoned loader. When WE triggered the abort
                 // above, the producer's resulting cancellation is a consequence, not the root cause — swallow it.
                 try { producer.GetAwaiter().GetResult(); }
                 catch (Exception) when (loadAbort.IsCancellationRequested) { }
             }
         }
         swTransientLoop.Stop();
         LogPhaseTimingBreakdown(taskId, outputFolder, swInit.Elapsed, swTransientLoop.Elapsed, databaseParallelism);

         // PEP RT predictor (Chronologer/TorchSharp) is finished once the transient loop is done; release it.
         _pepRtPredictor?.Dispose();
         _pepRtPredictor = null;

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
        try
        {
            WriteFinalOutputs(outputFolder, taskId, currentRawFileList.Count);
        }
        catch (Exception ex)
        {
            // Dump the full stack to a labeled file — the task runner only surfaces the message.
            try { File.WriteAllText(Path.Combine(outputFolder, "WriteFinalOutputs_ERROR.txt"), ex.ToString()); } catch { }
            throw;
        }

        ReportTaskDashboard(taskId, ParallelSearchDashboardUpdateKind.TaskCompleted, DashboardPhaseCompleted,
            "Many search task complete!");

        ReportProgress(new(100, "Many search task complete!", [taskId]));
        return MyTaskResults;
    }

    /// <summary>
    /// Logs where wall-clock time went so GPU effort targets the real hot spot.
    /// The per-database search and post-analysis times are summed across all parallel
    /// workers (CPU-seconds); dividing by the database parallelism approximates the
    /// wall-clock each contributed to the transient loop.
    /// </summary>
    private void LogPhaseTimingBreakdown(string taskId, string outputFolder, TimeSpan init, TimeSpan transientLoop, int databaseParallelism)
    {
        double searchCpuSec = _searchEngineTicks / (double)Stopwatch.Frequency;
        double postCpuSec = _postAnalysisTicks / (double)Stopwatch.Frequency;
        int par = Math.Max(1, databaseParallelism);

        string summary =
            "Phase timing — " +
            $"Initialize (load + base search): {init.TotalSeconds:F1}s | " +
            $"Transient loop wall-clock: {transientLoop.TotalSeconds:F1}s | " +
            $"search engine: {searchCpuSec:F1} CPU-s (~{searchCpuSec / par:F1}s wall) | " +
            $"post-search analysis: {postCpuSec:F1} CPU-s (~{postCpuSec / par:F1}s wall) | " +
            $"db parallelism: {par}.";

        Status(summary, taskId);

        // Also persist to a flushed file so the profile survives any later-stage failure
        // (the breakdown is the whole point of the run; don't let finalization bugs eat it).
        try
        {
            string detail =
                summary + Environment.NewLine + Environment.NewLine +
                "Breakdown (transient loop only; Initialize is a one-time cost):" + Environment.NewLine +
                $"  search engine (TransientClassicSearchEngine):  {searchCpuSec,10:F1} CPU-s" + Environment.NewLine +
                $"  post-search analysis (stats/collectors/FDR):   {postCpuSec,10:F1} CPU-s" + Environment.NewLine +
                $"  search : post ratio = {searchCpuSec / Math.Max(1e-9, postCpuSec):F2} : 1" + Environment.NewLine +
                $"  db parallelism = {par}" + Environment.NewLine;
            File.WriteAllText(Path.Combine(outputFolder, "PhaseTiming.txt"), detail);
        }
        catch { /* timing file is best-effort */ }
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

        // S1: when any transient database is a .msl library, learn the precursor tolerance and the
        // RT (iRT→observed) calibration from the confident base-search PSMs. These become the .msl
        // candidate pre-filter priors (S3). Cheap relative to the search and done once.
        if (ParallelSearchParameters.TransientDatabases.Any(
                d => d.FilePath.EndsWith(".msl", StringComparison.OrdinalIgnoreCase)))
        {
            _mslCalibration = LearnMslCalibration(baselinePsms, taskId);
            // Cache the (mass-sorted) scan mass + RT arrays ONCE — they are identical for every .msl
            // database, so rebuilding them per database (1000s of times) would be pure waste.
            _sortedScanMasses = Array.ConvertAll(AllSortedMs2Scans, s => s.PrecursorMass);
            _scanRetentionTimes = Array.ConvertAll(AllSortedMs2Scans, s => s.RetentionTime);
        }

        // PEP: train ONE model on the (large, high-quality) base human PSMs and reuse it to assign a PEP
        // to every transient database's PSMs — those databases are far too small to train their own model.
        // Independent of the .msl path, so FASTA transient searches get PEP too. Uses Chronologer for the
        // RT-residual feature. Best-effort: any failure leaves transient PEP unset.
        TrainBasePepModel(baselinePsms, outputFolder, taskId);

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

    /// <summary>
    /// A transient database after the LOAD stage (producer): proteins/peptides ready, fragments fetched.
    /// Carried through the bounded channel to a searcher (consumer) so loading overlaps with searching.
    /// </summary>
    private sealed class LoadedTransientDatabase
    {
        public DbForTask TransientDb = null!;
        public string DbName = null!;
        public string DbOutputFolder = null!;
        public List<string> NestedIds = null!;
        public List<IBioPolymer> TransientProteins = null!;
        public List<(IBioPolymerWithSetMods Peptide, List<Product> Fragments)>? PrecomputedPeptides;
        public HashSet<string> TransientProteinAccessions = null!;
    }

    /// <summary>Builds the .msl candidate pre-filter priors (scan masses + RTs, learned precursor/RT
    /// calibration). The scan arrays are cached once (identical for every database).</summary>
    private MslPeptideReader.CandidatePriors BuildCandidatePriors()
    {
        var sortedScanMasses = _sortedScanMasses ?? Array.ConvertAll(AllSortedMs2Scans, s => s.PrecursorMass);
        var scanRetentionTimes = _scanRetentionTimes ?? Array.ConvertAll(AllSortedMs2Scans, s => s.RetentionTime);
        var cal = _mslCalibration ?? new MslCandidateCalibration(
            CommonParameters.PrecursorMassTolerance.Value, 1, 0, double.PositiveInfinity);
        return new MslPeptideReader.CandidatePriors(
            sortedScanMasses, scanRetentionTimes,
            precursorTolPpm: cal.PrecursorTolPpm, rtSlope: cal.RtSlope,
            rtIntercept: cal.RtIntercept, rtWindowMin: cal.RtWindowMin);
    }

    /// <summary>
    /// Builds a prepared database from a merged-index db-group's candidate peptides (no file load). The
    /// returned database is searched INDEPENDENTLY by the consumer, identical to the per-file path.
    /// </summary>
    private LoadedTransientDatabase? BuildLoadedFromCandidates(string dbName,
        List<(IBioPolymerWithSetMods Peptide, List<Product> Fragments)> candidates, string outputFolder, string taskId)
    {
        if (GlobalVariables.StopLoops)
            return null;

        List<string> nestedIds = [taskId, dbName];
        bool shouldProcess = !_resultsManager!.HasCachedResults(dbName) || ParallelSearchParameters.OverwriteTransientSearchOutputs;
        if (!shouldProcess)
        {
            ReportProgress(new(100, $"Skipping {dbName} - results already exist in cache", nestedIds));
            UpdateProgress(TotalDatabases, taskId);
            return null;
        }

        var transientProteins = candidates.Select(p => p.Peptide.Parent).Distinct().ToList();
        return new LoadedTransientDatabase
        {
            // Synthetic per-group identity (name only); no per-database file in merged mode.
            TransientDb = new DbForTask(dbName, false),
            DbName = dbName,
            DbOutputFolder = Path.Combine(outputFolder, dbName),
            NestedIds = nestedIds,
            TransientProteins = transientProteins,
            PrecomputedPeptides = candidates,
            TransientProteinAccessions = new HashSet<string>(transientProteins.Select(p => p.Accession)),
        };
    }

    /// <summary>
    /// PRODUCER stage: skip/overwrite handling + load the transient database (FASTA digest set, or the
    /// .msl mass+RT-filtered candidate peptides with their fragments). Returns null when the database is
    /// skipped (cached) or the run is stopping. I/O-bound; runs ahead of the searchers.
    /// </summary>
    private LoadedTransientDatabase? LoadTransientDatabaseForPipeline(DbForTask transientDb, string outputFolder, string taskId)
    {
        if (GlobalVariables.StopLoops)
            return null;

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
            return null;
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

        // NOTE: the output folder is created lazily by the writer, and ONLY for databases that produced
        // transient PSMs — at 1000s of databases the vast majority match nothing, so creating a folder +
        // header-only files for each was a large fraction of the runtime. See WriteCompletedDatabaseOutputsAsync.

        Status($"Loading transient database {dbName}...", nestedIds);
        ReportDatabaseDashboard(taskId, ParallelSearchDashboardUpdateKind.DatabaseProgress, dbName,
            $"Loading transient database {dbName}...", DashboardDatabaseLoadingProgress);

        // Load transient database. FASTA/XML -> proteins (digested during search); a .msl spectral
        // library -> precomputed peptides paired with their stored (float) fragments (the search
        // iterates these and matches the stored fragments directly, skipping digestion+fragmentation).
        List<IBioPolymer> transientProteins;
        List<(IBioPolymerWithSetMods Peptide, List<Product> Fragments)>? precomputedPeptides = null;
        bool isMslLibrary = transientDb.FilePath.EndsWith(".msl", StringComparison.OrdinalIgnoreCase);
        if (isMslLibrary)
        {
            // S3: index-only candidate pre-filter on precursor mass AND Chronologer-iRT (learned from the
            // base search, S1). See BuildCandidatePriors.
            precomputedPeptides = MslPeptideReader.ReadPeptides(transientDb.FilePath, dbName, BuildCandidatePriors());
            // shared parent proteins (one per accession) back the accession filter and counts
            transientProteins = precomputedPeptides.Select(p => p.Peptide.Parent).Distinct().ToList();
        }
        else
        {
            transientProteins = LoadTransientDatabase(transientDb, nestedIds, taskId);
        }

        if (GlobalVariables.StopLoops)
            return null;

        return new LoadedTransientDatabase
        {
            TransientDb = transientDb,
            DbName = dbName,
            DbOutputFolder = dbOutputFolder,
            NestedIds = nestedIds,
            TransientProteins = transientProteins,
            PrecomputedPeptides = precomputedPeptides,
            // Create HashSet of transient bioPolymer accessions for later filtering
            TransientProteinAccessions = new HashSet<string>(transientProteins.Select(p => p.Accession)),
        };
    }

    /// <summary>
    /// CONSUMER stage: search the loaded database against the shared spectra and run post-analysis, then
    /// hand the results to the (already-pipelined) write channel. CPU-bound; overlaps with loaders.
    /// </summary>
    private void SearchLoadedTransientDatabase(LoadedTransientDatabase loaded, string taskId)
    {
        if (GlobalVariables.StopLoops)
            return;

        DbForTask transientDb = loaded.TransientDb;
        string dbName = loaded.DbName;
        string dbOutputFolder = loaded.DbOutputFolder;
        List<string> nestedIds = loaded.NestedIds;
        List<IBioPolymer> transientProteins = loaded.TransientProteins;
        HashSet<string> transientProteinAccessions = loaded.TransientProteinAccessions;

        Status($"Searching {dbName} ({transientProteins.Count} transient proteins)...", nestedIds);
        ReportDatabaseDashboard(taskId, ParallelSearchDashboardUpdateKind.DatabaseProgress, dbName,
            $"Searching {dbName} ({transientProteins.Count} transient proteins)...", DashboardDatabaseSearchStartProgress,
            DashboardDatabaseSearchStartProgress, DashboardDatabaseSearchEndProgress);

        // Reuse baseline PSMs with copy-on-write in peptide/proteoform mode.
        SpectralMatch[] psmArray = BaseSearchPsms.ToArray();
        long searchStart = Stopwatch.GetTimestamp();
        PerformSearch(transientProteins, psmArray, nestedIds, out HashSet<int> updatedPsmIndexes, out int transientPeptideCount, useCopyOnWrite: true, precomputedPeptides: loaded.PrecomputedPeptides);
        Interlocked.Add(ref _searchEngineTicks, Stopwatch.GetTimestamp() - searchStart);

        Status($"Performing post-search analysis for {dbName}...", nestedIds);
        ReportDatabaseDashboard(taskId, ParallelSearchDashboardUpdateKind.DatabaseProgress, dbName,
            $"Performing post-search analysis for {dbName}...", DashboardDatabasePostSearchProgress);

        int totalProteins = BaseBioPolymers.Count + transientProteins.Count;

        // Process database through unified manager (handles analysis + statistical caching)
        long postAnalysisStart = Stopwatch.GetTimestamp();
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
        Interlocked.Add(ref _postAnalysisTicks, Stopwatch.GetTimestamp() - postAnalysisStart);

        // (PEP is now assigned inside PerformPostSearchAnalysis, before the metric collectors run.)
        _completedDatabaseWriteChannel!.Writer.WriteAsync((analysisContext, dbResults)).AsTask().GetAwaiter().GetResult();

        // Cleanup transient proteins to free memory
        transientProteins.Clear();

        ReportProgress(new(100, $"Finished analysis for {dbName}; queued output writing", nestedIds));
    }

    /// <summary>Writes a per-database &lt;db&gt;_PROCESS_ERROR.txt diagnostic; never throws.</summary>
    private static void WriteTransientProcessError(DbForTask transientDb, string outputFolder, Exception ex)
    {
        try
        {
            string dbName = Path.GetFileNameWithoutExtension(transientDb.FilePath);
            File.WriteAllText(Path.Combine(outputFolder, dbName + "_PROCESS_ERROR.txt"), ex.ToString());
        }
        catch { }
    }

    /// <summary>Calibration learned from the base search and applied to the .msl candidate pre-filter.</summary>
    private readonly struct MslCandidateCalibration
    {
        public readonly double PrecursorTolPpm;            // symmetric precursor tolerance (covers offset+spread)
        public readonly double RtSlope, RtIntercept;       // observedRT = RtSlope*iRT + RtIntercept
        public readonly double RtWindowMin;                // +/- observed-RT window (k * residual SD); +inf = no RT filter
        public MslCandidateCalibration(double tolPpm, double slope, double intercept, double rtWindowMin)
        { PrecursorTolPpm = tolPpm; RtSlope = slope; RtIntercept = intercept; RtWindowMin = rtWindowMin; }
    }

    /// <summary>
    /// S1 — learn the .msl candidate pre-filter priors from confident base-search PSMs:
    ///   • precursor tolerance = |mean| + 3·SD of the precursor mass error (clamped to the configured tol),
    ///   • RT calibration = OLS of observed RT on Chronologer iRT, with a ±2·residualSD window.
    /// On any failure (too few PSMs, predictor/model unavailable) returns a SAFE fallback: the configured
    /// precursor tolerance and NO RT filter (RtWindowMin = +inf) so nothing is lost.
    /// </summary>
    /// <summary>
    /// Trains ONE PEP model on the base (human) search PSMs and keeps it for assigning PEP to each transient
    /// database's hits (the databases are far too small to train their own). Uses Chronologer for the
    /// RT-residual feature. Best-effort: any failure simply leaves the transient PEP unassigned.
    /// </summary>
    private void TrainBasePepModel(List<SpectralMatch> baselinePsms, string outputFolder, string taskId)
    {
        try
        {
            var trainingPsms = baselinePsms.Where(p => p != null).ToList();
            if (trainingPsms.Count < 100)
            {
                Status($"PEP: only {trainingPsms.Count} base PSMs — skipping PEP model.", taskId);
                return;
            }
            _pepRtPredictor = RetentionTimePredictorFactory.Create(PredictorType.Chronologer);
            var engine = new PepAnalysisEngine(trainingPsms, "standard", FileSpecificParameters, outputFolder, _pepRtPredictor);
            if (engine.TrainSingleModelAndAssignBasePep())
            {
                _pepEngine = engine;
                Status("PEP: trained model on base search; assigning PEP to transient PSMs.", taskId);
            }
            else
            {
                Status("PEP: base PSMs lacked target/decoy training examples — PEP disabled.", taskId);
                _pepRtPredictor.Dispose();
                _pepRtPredictor = null;
            }
        }
        catch (Exception ex)
        {
            Status($"PEP training failed ({ex.GetType().Name}: {ex.Message}); PEP disabled.", taskId);
            _pepRtPredictor?.Dispose();
            _pepRtPredictor = null;
        }
    }

    private MslCandidateCalibration LearnMslCalibration(List<SpectralMatch> baselinePsms, string taskId)
    {
        double configuredTolPpm = CommonParameters.PrecursorMassTolerance.Value;
        var fallback = new MslCandidateCalibration(configuredTolPpm, 1, 0, double.PositiveInfinity);
        try
        {
            // Confident, unambiguous target PSMs.
            var conf = baselinePsms.Where(p => p != null && !p.IsDecoy
                    && p.PsmFdrInfo != null && p.PsmFdrInfo.QValue < 0.01
                    && p.BestMatchingBioPolymersWithSetMods.Count() == 1)
                .ToList();
            if (conf.Count < 50)
            {
                Status($"S1: only {conf.Count} confident base PSMs — using safe fallback (no RT filter).", taskId);
                return fallback;
            }

            // Precursor mass error (ppm) and the (peptide, observedRT) pairs for the RT regression.
            var ppmErrors = new List<double>(conf.Count);
            var obsByPeptide = new Dictionary<IRetentionPredictable, double>(ReferenceEqualityComparer.Instance);
            var peptides = new List<IRetentionPredictable>(conf.Count);
            foreach (var psm in conf)
            {
                var pep = psm.BestMatchingBioPolymersWithSetMods.First().SpecificBioPolymer;
                double mass = pep.MonoisotopicMass;
                if (mass <= 0) continue;
                ppmErrors.Add((psm.ScanPrecursorMass - mass) / mass * 1e6);
                if (pep is IRetentionPredictable rp && !obsByPeptide.ContainsKey(rp))
                {
                    obsByPeptide[rp] = psm.ScanRetentionTime;
                    peptides.Add(rp);
                }
            }

            double pMean = ppmErrors.Average();
            double pSd = Math.Sqrt(ppmErrors.Sum(e => (e - pMean) * (e - pMean)) / ppmErrors.Count);
            // Trust the calibration: the real precursor accuracy (|mean|+3σ of the confident base PSMs) IS
            // the tolerance, regardless of the looser configured/standard value. This is applied to BOTH the
            // candidate pre-filter AND the transient search's acceptor (see PerformSearch), so they are
            // consistent — peptides outside it are rejected as loose-tolerance false positives, not lost.
            double tolPpm = Math.Max(2.0, Math.Abs(pMean) + 3.0 * pSd);

            // Chronologer iRT for the confident peptides, then OLS observedRT vs iRT.
            using var predictor = RetentionTimePredictorFactory.Create(PredictorType.Chronologer);
            var predRt = new List<double>(peptides.Count);
            var obsRt = new List<double>(peptides.Count);
            foreach (var r in predictor.PredictRetentionTimeEquivalents(peptides,
                         maxThreads: Math.Max(1, Environment.ProcessorCount - 2)))
            {
                if (r.PredictedValue is null) continue;
                if (obsByPeptide.TryGetValue(r.Peptide, out double rt)) { predRt.Add(r.PredictedValue.Value); obsRt.Add(rt); }
            }
            if (predRt.Count < 50)
            {
                Status($"S1: only {predRt.Count} RT predictions — precursor tol {tolPpm:F1} ppm, no RT filter.", taskId);
                return new MslCandidateCalibration(tolPpm, 1, 0, double.PositiveInfinity);
            }

            int n = predRt.Count;
            double mx = predRt.Average(), my = obsRt.Average();
            double sxy = 0, sxx = 0;
            for (int i = 0; i < n; i++) { double dx = predRt[i] - mx; sxy += dx * (obsRt[i] - my); sxx += dx * dx; }
            double slope = sxx > 0 ? sxy / sxx : 1.0;
            double intercept = my - slope * mx;
            double residSs = 0;
            for (int i = 0; i < n; i++) { double e = obsRt[i] - (slope * predRt[i] + intercept); residSs += e * e; }
            double residSd = Math.Sqrt(residSs / n);
            double rtWindow = Math.Max(1.0, 2.0 * residSd); // ±2σ; floor 1 min

            Status($"S1 learned: precursor ±{tolPpm:F1} ppm (μ={pMean:F2},σ={pSd:F2}); " +
                   $"RT obs={slope:F3}·iRT+{intercept:F2} residSD={residSd:F2}min → window ±{rtWindow:F1}min (n={n}).", taskId);
            return new MslCandidateCalibration(tolPpm, slope, intercept, rtWindow);
        }
        catch (Exception ex)
        {
            Status($"S1 calibration failed ({ex.GetType().Name}: {ex.Message}); safe fallback (no RT filter).", taskId);
            return fallback;
        }
    }

    /// <summary>
    /// Populates and returns the spectral match array using classic search engine
    /// </summary>
     private void PerformSearch(List<IBioPolymer> proteinsToSearch, SpectralMatch[] spectralMatchArray, List<string> nestedIds, out HashSet<int> updatedPsmIndexes, out int peptidesSearched, bool useCopyOnWrite = false, List<(IBioPolymerWithSetMods Peptide, List<Product> Fragments)> precomputedPeptides = null)
     {
         // For a .msl search, use the LEARNED precursor tolerance (S1) so the search engine and the
         // candidate pre-filter agree — the calibration is trusted over the looser configured tolerance.
         MzLibUtil.Tolerance precursorTolerance = CommonParameters.PrecursorMassTolerance;
         if (precomputedPeptides != null && _mslCalibration.HasValue)
             precursorTolerance = new MzLibUtil.PpmTolerance(_mslCalibration.Value.PrecursorTolPpm);

         var massDiffAcceptor = GetMassDiffAcceptor(
             precursorTolerance,
             SearchParameters.MassDiffAcceptorType,
             SearchParameters.CustomMdac);

         // Run the classic search engine. When precomputedPeptides is supplied (from a .msl library),
         // the engine iterates those peptides directly instead of digesting proteinsToSearch.
          var searchEngine = new TransientClassicSearchEngine(
              spectralMatchArray, AllSortedMs2Scans, VariableModifications,
              FixedModifications, proteinsToSearch, massDiffAcceptor, CommonParameters,
              FileSpecificParameters, nestedIds, copyOnWriteEnabled: useCopyOnWrite, precomputedPeptides: precomputedPeptides);

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

        // PEP: assign each transient PSM/peptide a posterior error probability + PEP_QValue (mapped onto the
        // background curve) BEFORE the metric collectors and statistics run, so confident counts and family
        // tests can use PEP_QValue. Runs after the FDR alignment so the borrowed score-based QValue is in place.
        if (_pepEngine != null)
        {
            _pepEngine.AssignPepFromTrainedModel(transientPsms, peptideLevel: false);
            if (transientPeptides.Count > 0)
                _pepEngine.AssignPepFromTrainedModel(transientPeptides, peptideLevel: true);
        }

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
        // The completed-database output (per-db folder + PSM/peptide/results files) was written by ONE
        // consumer draining a capacity-2 channel, so at 1000s of databases the parallel searchers blocked
        // on a single serial writer (low CPU utilization). Run several writer consumers — each writes a
        // different database's folder, so file writes never collide — and widen the channel so searchers
        // don't stall waiting for a write slot. The three pieces of shared bookkeeping the consumers touch
        // are each synchronized: AppendCheckpoint -> ParallelSearchResultCache.AppendToFile (lock _writeLock
        // + _cacheLock over a ConcurrentDictionary), MarkDatabaseCompleted (lock _dashboardLock), and
        // UpdateProgress (lock _progressLock).
        int writerCount = Math.Max(2, Environment.ProcessorCount / 4);
        _completedDatabaseWriteChannel = Channel.CreateBounded<(TransientDatabaseContext Context, TransientDatabaseMetrics Metrics)>(
            new BoundedChannelOptions(Math.Max(writerCount * 4, 32))
            {
                SingleReader = false, // multiple parallel writer consumers
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });
        var writers = new Task[writerCount];
        for (int i = 0; i < writerCount; i++)
            writers[i] = Task.Run(RunCompletedDatabaseWriterLoopAsync);
        _completedDatabaseWriterTask = Task.WhenAll(writers);
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

    /// <summary>Confidence thresholds for what gets written to the per-database output files.</summary>
    private const double OutputQValueThreshold = 0.05;
    private const double OutputPepQValueThreshold = 0.05;

    /// <summary>
    /// A match is confident when its PEP-based q-value is below <see cref="OutputPepQValueThreshold"/> — the
    /// PEP_QValue is assigned by mapping the match's model PEP onto the background curve (see PepAnalysisEngine),
    /// so it is meaningful even though the tiny transient databases can't compute their own. When PEP is
    /// unavailable (no trained model), falls back to the borrowed score-based QValue.
    /// </summary>
    private bool IsConfident(SpectralMatch p, bool peptideLevel)
        => IsConfidentMatch(p?.GetFdrInfo(peptideLevel), _pepEngine != null, OutputPepQValueThreshold, OutputQValueThreshold);

    /// <summary>
    /// Pure confidence decision (extracted for testing): when <paramref name="pepActive"/>, a match is confident
    /// if its PEP_QValue is below <paramref name="pepQThreshold"/>; otherwise it falls back to the score-based
    /// QValue being at or below <paramref name="qThreshold"/>. A null FdrInfo is never confident.
    /// </summary>
    internal static bool IsConfidentMatch(EngineLayer.FdrAnalysis.FdrInfo info, bool pepActive, double pepQThreshold, double qThreshold)
    {
        if (info == null)
            return false;
        return pepActive
            ? info.PEP_QValue < pepQThreshold
            : info.QValue <= qThreshold;
    }

    /// <summary>
    /// Row-level confidence filter (see <see cref="IsConfident"/>). Uses the peptide-level FdrInfo when
    /// <paramref name="peptideLevel"/> is set, otherwise the PSM-level one. Returns the input unchanged when
    /// null/empty so writers behave as before for empty lists.
    /// </summary>
    private List<SpectralMatch> FilterToConfident(List<SpectralMatch> matches, bool peptideLevel)
    {
        if (matches == null || matches.Count == 0)
            return matches;
        return matches.Where(p => IsConfident(p, peptideLevel)).ToList();
    }

    private async Task WriteCompletedDatabaseOutputsAsync(TransientDatabaseContext context, TransientDatabaseMetrics metrics)
    {
        string dbName = context.DatabaseName;
        string outputFolder = context.OutputFolder;
        List<string> nestedIds = context.NestedIds;
        bool writeAllResults = !ParallelSearchParameters.WriteTransientResultsOnly;

        // Write per-database output ONLY for databases with a transient result at <= 5% FDR. At 1000s of
        // databases the vast majority match nothing (or only sub-threshold noise); writing a folder + files
        // for each was a dominant cost. The database is still recorded in the cross-database summary/checkpoint
        // below, so it counts as processed (and resume still works — HasCachedResults reads the checkpoint).
        bool hasTransientOutput =
            (context.TransientPeptides != null && context.TransientPeptides.Any(p => IsConfident(p, true)))
            || (context.TransientPsms != null && context.TransientPsms.Any(p => IsConfident(p, false)));
        if (!hasTransientOutput)
        {
            _resultsManager!.AppendCheckpoint(metrics);
            MarkDatabaseCompleted();
            UpdateProgress(TotalDatabases, nestedIds[0]);
            ReportDatabaseDashboard(nestedIds[0], ParallelSearchDashboardUpdateKind.DatabaseFinished, dbName,
                $"Finished {dbName} (no transient hits)", DashboardDatabaseFinishedProgress);
            ReportProgress(new(100, $"Finished {dbName} (no transient hits)", nestedIds));
            return;
        }

        // Create the output folder lazily — only databases with transient hits get one.
        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        Status($"Writing results for {dbName}...", nestedIds);
        ReportDatabaseDashboard(nestedIds[0], ParallelSearchDashboardUpdateKind.DatabaseProgress, dbName,
            $"Writing results for {dbName}...", DashboardDatabaseWritingProgress);

        // Output is limited to confident matches (q-value <= OutputQValueThreshold). The filtering is row-level:
        // every file below contains only matches at or below 5% FDR.
        var confidentTransientPsms = FilterToConfident(context.TransientPsms, peptideLevel: false);
        string transientPsmFile = Path.Combine(outputFolder,
            $"{dbName}_All{GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        await WritePsmsToTsvAsync(confidentTransientPsms, transientPsmFile, SearchParameters.ModsToWriteSelection, false);
        FinishedWritingFile(transientPsmFile, nestedIds);

        if (writeAllResults)
        {
            string psmFile = Path.Combine(outputFolder,
                $"All{GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
            await WritePsmsToTsvAsync(FilterToConfident(context.AllPsms, peptideLevel: false), psmFile, SearchParameters.ModsToWriteSelection, false);
            FinishedWritingFile(psmFile, nestedIds);
        }

        var confidentTransientPeptides = FilterToConfident(context.TransientPeptides, peptideLevel: true);
        if (confidentTransientPeptides is { Count: > 0 })
        {
            string transientPeptideFile = Path.Combine(outputFolder,
            $"{dbName}_All{GlobalVariables.AnalyteType}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
            await WritePsmsToTsvAsync(confidentTransientPeptides, transientPeptideFile, SearchParameters.ModsToWriteSelection, true);
            FinishedWritingFile(transientPeptideFile, nestedIds);
        }

        if (writeAllResults)
        {
            string peptideFile = Path.Combine(outputFolder,
                $"All{GlobalVariables.AnalyteType}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
            await WritePsmsToTsvAsync(FilterToConfident(context.AllPeptides, peptideLevel: true), peptideFile, SearchParameters.ModsToWriteSelection, true);
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

        // Deal with custom reduced database writing.
        // Always use the family-aware gate (combined q-value + a minimum number of evidence families).
        // The old test-ratio gate required passing >=50% of ALL tests, which even a perfect spike-in
        // (SARS-CoV-2 passed 31/78) can't clear — many sub-tests structurally cannot fire for a small
        // genome (NullEvidence / Undefined / BelowEligibilityThreshold), so nothing was ever written.
        var statsByDatabase = SelectDatabasesForWritingByFamily();

         Task[] dbWritingTasks = new Task[3];
         if (statsByDatabase.Count > 0)
         {
              Log($"Found {statsByDatabase.Count} significant databases passing cutoff (family-aware, >={MinFamiliesForSignificance}/7 families)", [taskId]);

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

    /// <summary>Minimum number (of 7) of independent evidence families a database must pass to be written
    /// as a confidently-detected organism.
    /// NOTE on tuning: on the SARS virus spike-in, SARS-CoV-2 passes 7/7 and SARS-CoV 5/7, while a tail of
    /// phage databases that floor out at the combined-q value only reach 4/7. So a bar of 4 admits that phage
    /// tail (more sensitive, noisier) and a bar of 5 isolates the genuine detections. Left at 4 by request;
    /// raise to 5 if the phage tail proves to be false positives.</summary>
    private const int MinFamiliesForSignificance = 4;

    private Dictionary<DbForTask, List<StatisticalTestResult>> SelectDatabasesForWritingByFamily()
    {
        double qValueThreshold = CommonParameters.QValueThreshold;
        int minFamilyPasses = MinFamiliesForSignificance;
        // Resolve the source DbForTask for a database tag. In merged-index mode TransientDatabases holds only the
        // single merged .msl, so there is no per-organism DbForTask — synthesize one keyed by the db tag.
        DbForTask ResolveDb(string dbTag) =>
            ParallelSearchParameters.TransientDatabases.FirstOrDefault(db => Path.GetFileNameWithoutExtension(db.FileName) == dbTag)
            ?? new DbForTask(dbTag, false);

        return _resultsManager!.StatisticalTestResultList
            .GroupBy(p => p.DatabaseName)
            .Where(g => QualifiesAsDetectedOrganism(g, minFamilyPasses, qValueThreshold))
            .ToDictionary(g => ResolveDb(g.Key), g => g.OrderBy(t => t.ToString()).ToList());
    }

    /// <summary>
    /// The family-aware detection predicate (extracted for testing): a database qualifies as a confidently
    /// detected organism when at least <paramref name="minFamilyPasses"/> independent evidence families are
    /// significant AND the overall combined q-value is at or below <paramref name="qValueThreshold"/>. Both
    /// quantities are computed DIRECTLY from the test results — the same source StatisticalAnalysis_Results.csv
    /// uses — rather than from the metrics-summary fields, which are not reliably populated for this writer
    /// (and were silently selecting nothing, even for SARS-CoV-2 at 7/7).
    /// </summary>
    internal static bool QualifiesAsDetectedOrganism(
        IEnumerable<StatisticalTestResult> dbResults, int minFamilyPasses, double qValueThreshold,
        double significanceAlpha = 0.05, string overallCombinedMetric = "All")
    {
        var results = dbResults as ICollection<StatisticalTestResult> ?? dbResults.ToList();
        int passedFamilies = results
            .Where(r => !r.IsCombinedResult && r.EvidenceFamily.HasValue && r.IsSignificant(significanceAlpha))
            .Select(r => r.EvidenceFamily!.Value)
            .Distinct()
            .Count();
        double combinedQ = results
            .Where(r => r.IsCombinedResult && r.MetricName == overallCombinedMetric)
            .Select(r => r.QValue)
            .DefaultIfEmpty(double.NaN)
            .First();
        return passedFamilies >= minFamilyPasses
            && !double.IsNaN(combinedQ)
            && combinedQ <= qValueThreshold;
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
