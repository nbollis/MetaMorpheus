#nullable enable
using EngineLayer;
using EngineLayer.ClassicSearch;
using EngineLayer.DatabaseLoading;
using EngineLayer.FdrAnalysis;
using EngineLayer.SpectrumMatch;
using EngineLayer.Util;
using Nett;
using Omics;
using Omics.Modifications;
using Omics.SpectrumMatch;
using Proteomics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using EngineLayer.ParallelSearch.FdrAlignment;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Analysis.Collectors;
using TaskLayer.ParallelSearch.Statistics;
using TaskLayer.ParallelSearch.Util;
using ProteinGroup = EngineLayer.ProteinGroup;

namespace TaskLayer.ParallelSearch;
public class ParallelSearchTask : SearchTask
{
     private readonly object _progressLock = new object();
     private TransientDatabaseResultsManager? _resultsManager;

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
    [TomlIgnore] private List<CachedProteinGroup> CachedBaselineProteinGroups { get; set; } = [];
    [TomlIgnore] private readonly PsmSpectralMatchFdrAlignmentService _psmFdrAlignmentService = new();
    [TomlIgnore] private readonly PeptideSpectralMatchFdrAlignmentService _peptideFdrAlignmentService = new();
    [TomlIgnore] private readonly ProteinGroupFdrAlignmentService _proteinGroupFdrAlignmentService = new();
    [TomlIgnore] private readonly Dictionary<string, List<int>> _baselinePeptideKeyToScanIndexes =
        new(StringComparer.Ordinal);
    [TomlIgnore] public int TotalDatabases => ParallelSearchParameters.TransientDatabases.Count;
    [TomlIgnore] public int TotalMs2Scans => AllSortedMs2Scans.Length;
    [TomlIgnore] public List<DbForTask> PersistentDatabases { get; private set; } = [];

    #endregion

    protected override MyTaskResults RunSpecific(string outputFolder,
        List<DbForTask> dbFilenameList, List<string> currentRawFileList,
        string taskId, FileSpecificParameters[] fileSettingsList)
    {
        MyTaskResults = new MyTaskResults(this);
        PersistentDatabases = dbFilenameList;

        // Initialize unified results manager
        _resultsManager = CreateResultsManager(outputFolder, ParallelSearchParameters.DoParsimony, ParallelSearchParameters.DeNovoMappingDataFilePath);
        
        // Check cache status early for fast-path optimization
        var allDatabaseNames = ParallelSearchParameters.TransientDatabases
            .Select(db => Path.GetFileNameWithoutExtension(db.FilePath))
            .ToList();
         
         var cacheSummary = _resultsManager!.GetCacheSummary(allDatabaseNames);
         Status(cacheSummary.ToString(), taskId);

         // Fast path: If all databases are cached and not overwriting, skip to finalization
         if (cacheSummary.DatabasesNeedingProcessing == 0 && !ParallelSearchParameters.OverwriteTransientSearchOutputs)
         {
             Status("All databases cached, skipping search phase and proceeding to finalization...", taskId);
             goto Finalization;
         }

         // Initialize all necessary data structures including base search
         Initialize(taskId, dbFilenameList, currentRawFileList, fileSettingsList, outputFolder);

        Status($"Starting search of {TotalDatabases} transient databases...", taskId);

        // Determine optimal thread allocation
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
                 ProcessTransientDatabase(transientDbPath, outputFolder, taskId);
             });

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


         Status("Running statistical analysis on all results...", taskId);
         _resultsManager!.RunStatisticalAnalysis();

         Status("Writing Final Results...", taskId);
         WriteFinalOutputs(outputFolder, taskId, currentRawFileList.Count);

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
         PerformSearch(BaseBioPolymers, BaseSearchPsms, [taskId], out _);
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

            var baselinePsmsForParsimony = FilteredPsms.Filter(baselinePsms,
                commonParams: CommonParameters,
                includeDecoys: true,
                includeContaminants: true,
                includeAmbiguous: false,
                includeHighQValuePsms: true);

            ProteinParsimonyResults baselineParsimonyResults = (ProteinParsimonyResults)new ProteinParsimonyEngine(
                baselinePsmsForParsimony, SearchParameters.ModPeptidesAreDifferent,
                CommonParameters, FileSpecificParameters, [taskId]).Run();

              CachedBaselineProteinGroups = baselineParsimonyResults.ProteinGroups
                  .Select(p => new CachedProteinGroup(p))
                  .ToList();

             if (CachedBaselineProteinGroups.Count > 0)
             {
                // TOTHINK: Do we need to copy here? 
                 var baselineProteinGroups = CachedBaselineProteinGroups
                     .Select(group => group.CreateRuntimeCopy())
                     .ToList();

                 ProteinScoringAndFdrResults baselineProteinScoringResults = (ProteinScoringAndFdrResults)new ProteinScoringAndFdrEngine(
                     baselineProteinGroups,
                     baselinePsms,
                     SearchParameters.NoOneHitWonders,
                     SearchParameters.ModPeptidesAreDifferent,
                     true,
                     CommonParameters,
                     FileSpecificParameters,
                     [taskId]).Run();

                 _proteinGroupFdrAlignmentService.BuildBaselineCache(baselineProteinScoringResults.SortedAndScoredProteinGroups);
             }
             else
             {
                 _proteinGroupFdrAlignmentService.BuildBaselineCache([]);
             }
         }

         // Write prose for base settings
         ProseCreatedWhileRunning.Append($"Base database contained {BaseBioPolymers.Count(p => !p.IsDecoy)} non-decoy protein entries. ");
         ProseCreatedWhileRunning.Append($"Searching {ParallelSearchParameters.TransientDatabases.Count} transient databases against {currentRawFileList.Count} spectra files. ");
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
                 var singleFileStopwatch = Stopwatch.StartNew();
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

         // Check if we should skip or overwrite this database
         bool shouldProcess = !_resultsManager!.HasCachedResults(dbName) || 
                            ParallelSearchParameters.OverwriteTransientSearchOutputs;

         if (!shouldProcess)
         {
             ReportProgress(new(100, $"Skipping {dbName} - results already exist in cache", nestedIds));
             UpdateProgress(TotalDatabases, taskId);
             return;
         }

         // Handle overwrite scenario
         if (ParallelSearchParameters.OverwriteTransientSearchOutputs && _resultsManager.HasCachedResults(dbName))
         {
             Status($"Overwriting existing results for {dbName}...", nestedIds);
             if (Directory.Exists(dbOutputFolder))
             {
                 Directory.Delete(dbOutputFolder, true);
             }
         }

         if (!Directory.Exists(dbOutputFolder))
             Directory.CreateDirectory(dbOutputFolder);

         Status($"Loading transient database {dbName}...", nestedIds);

         // Load transient database
         var transientProteins = LoadTransientDatabase(transientDb, nestedIds, taskId);

         if (GlobalVariables.StopLoops)
         {
             return;
         }

         // Create HashSet of transient protein accessions for later filtering
         var transientProteinAccessions = new HashSet<string>(
             transientProteins.Select(p => p.Accession));

         // Calculate transient peptide count
         Status($"Calculating peptide count for {dbName}...", nestedIds);
         int transientPeptideCount = CalculateTransientPeptideCount(transientProteins);

         Status($"Searching {dbName} ({transientProteins.Count} transient proteins)...", nestedIds);

         // Reuse baseline PSMs with copy-on-write in peptide/proteoform mode.
         SpectralMatch[] psmArray = BaseSearchPsms.ToArray();
         PerformSearch(transientProteins, psmArray, nestedIds, out HashSet<int> updatedPsmIndexes, useCopyOnWrite: true);

         Status($"Performing post-search analysis for {dbName}...", nestedIds);

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
             transientDb,
             updatedPsmIndexes
         ).GetAwaiter().GetResult();

         // Cleanup transient proteins to free memory
         transientProteins.Clear();

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
     private void PerformSearch(List<IBioPolymer> proteinsToSearch, SpectralMatch[] spectralMatchArray, List<string> nestedIds, out HashSet<int> updatedPsmIndexes, bool useCopyOnWrite = false)
     {
         var massDiffAcceptor = GetMassDiffAcceptor(
             CommonParameters.PrecursorMassTolerance,
             SearchParameters.MassDiffAcceptorType,
             SearchParameters.CustomMdac);

         // Run the classic search engine
          var searchEngine = new StreamlinedClassicSearchEngine(
              spectralMatchArray, AllSortedMs2Scans, VariableModifications,
              FixedModifications, proteinsToSearch, massDiffAcceptor, CommonParameters,
              FileSpecificParameters, nestedIds, copyOnWriteEnabled: useCopyOnWrite);

         var results = searchEngine.Run();
         updatedPsmIndexes = (results as StreamLinedClassicSearchEngineResults)!.UpdatedSpectralMatchIndexes;
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
        DbForTask transientDatabase,
        HashSet<int> updatedPsmIndexes)
    {
        List<SpectralMatch> psmList = allPsms.Where(p => p != null)
            .OrderByDescending(p => p).ToList();

        var transientProteinSet = new HashSet<IBioPolymer>(transientProteins, ReferenceComparer<IBioPolymer>.Instance);

        #region FDR and Parsiomony

        Status($"Performing FDR Analysis for {dbName}...", nestedIds);

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

            var transientPsmsForParsimony = BuildParsimonyNeighborhoodPsms(
                allPsms,
                updatedPsmIndexes,
                transientProteinSet,
                nestedIds);

            List<ProteinGroup> transientParsimonyGroups;
            if (transientPsmsForParsimony.Count > 0)
            {
                ProteinParsimonyResults proteinAnalysisResults = (ProteinParsimonyResults)await new ProteinParsimonyEngine(
                    transientPsmsForParsimony.ToList(), SearchParameters.ModPeptidesAreDifferent,
                    CommonParameters, FileSpecificParameters, nestedIds).RunAsync();
                transientParsimonyGroups =
                    FilterProteinGroupsToTransientProteins(proteinAnalysisResults.ProteinGroups, transientProteinSet).ToList();
            }
            else
            {
                transientParsimonyGroups = [];
            }

            var combinedProteinGroups = CachedBaselineProteinGroups
                .Select(group => group.CreateRuntimeCopy())
                .Concat(transientParsimonyGroups)
                .ToList();

            if (combinedProteinGroups.Count > 0)
            {
                ProteinScoringAndFdrResults proteinScoringAndFdrResults =
                    (ProteinScoringAndFdrResults)await new ProteinScoringAndFdrEngine(
                        combinedProteinGroups, psmList,
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

        #region Writing

        Status($"Writing results for {dbName}...", nestedIds);

        bool writeAllResults = !ParallelSearchParameters.WriteTransientResultsOnly;

        // Write transient PSMs to file
        string transientPsmFile = Path.Combine(outputFolder,
            $"{dbName}_All{GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        await WritePsmsToTsvAsync(transientPsms, transientPsmFile, SearchParameters.ModsToWriteSelection, false);
        FinishedWritingFile(transientPsmFile, nestedIds);

        // Write all PSMs to file
        if (writeAllResults)
        {
            string psmFile = Path.Combine(outputFolder,
                $"All{GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
            await WritePsmsToTsvAsync(psmList, psmFile, SearchParameters.ModsToWriteSelection, false);
            FinishedWritingFile(psmFile, nestedIds);
        }

        // Write transient peptides to file
        string transientPeptideFile = Path.Combine(outputFolder,
            $"{dbName}_All{GlobalVariables.AnalyteType}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        await WritePsmsToTsvAsync(transientPeptides, transientPeptideFile, SearchParameters.ModsToWriteSelection, true);
        FinishedWritingFile(transientPeptideFile, nestedIds);

        // Write all peptides to file
        if (writeAllResults)
        {
            string peptideFile = Path.Combine(outputFolder,
                $"All{GlobalVariables.AnalyteType}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
            await WritePsmsToTsvAsync(allPeptides, peptideFile, SearchParameters.ModsToWriteSelection, true);
            FinishedWritingFile(peptideFile, nestedIds);
        }

        List<ProteinGroup>? transientProteinGroups = null;
        if (proteinGroups is not null)
        {
            proteinGroups.ForEach(x => x.GetIdentifiedPeptidesOutput(SearchParameters.SilacLabels));

            // Count protein groups that contain at least one transient database protein
            transientProteinGroups =
                FilterProteinGroupsToTransientDatabaseOnly(proteinGroups, transientProteinAccessions).ToList();

            _ = _proteinGroupFdrAlignmentService.ApplyBaseline(transientProteinGroups);

            if (transientProteinGroups.Any())
            {
                string transientProteinFile = Path.Combine(outputFolder,
                    $"{dbName}_All{GlobalVariables.AnalyteType.GetBioPolymerLabel()}Groups.tsv");
                await WriteProteinGroupsToTsvAsync(transientProteinGroups, transientProteinFile);
                FinishedWritingFile(transientProteinFile, nestedIds);
            }

            // Write protein groups to file
            if (writeAllResults)
            {
                string proteinFile = Path.Combine(outputFolder,
                    $"All{GlobalVariables.AnalyteType.GetBioPolymerLabel()}Groups.tsv");
                await WriteProteinGroupsToTsvAsync(proteinGroups, proteinFile);
                FinishedWritingFile(proteinFile, nestedIds);
            }
        }

        if (SearchParameters.WriteSpectralLibrary)
        {
            // Write spectral library
            string spectralLibraryPath = Path.Combine(outputFolder, $"AllPeptidesAnd_{dbName}_SpectralLibrary.msp");
            await WriteSpectralLibraryAsync(psmList, spectralLibraryPath);
            FinishedWritingFile(spectralLibraryPath, nestedIds);
        }

        if (ParallelSearchParameters.WriteTransientSpectralLibrary)
        {
            string spectralLibraryPath = Path.Combine(outputFolder, $"{dbName}_SpectralLibrary.msp");
            await WriteSpectralLibraryAsync(transientPsms, spectralLibraryPath);
            FinishedWritingFile(spectralLibraryPath, nestedIds);
        }

        #endregion

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
        var aggregatedResult = _resultsManager!.ProcessDatabase(
            analysisContext,
            forceRecompute: ParallelSearchParameters.OverwriteTransientSearchOutputs
        );

        // Write individual results.txt for this database
        await WriteIndividualDatabaseResultsTextAsync(aggregatedResult, outputFolder, nestedIds);

        #endregion

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

         var writtenFiles = _resultsManager!.WriteAllResults(outputFolder);
         foreach (var writtenFile in writtenFiles)
             FinishedWritingFile(writtenFile, [taskId]); 

         // Write global summary text file
         WriteGlobalResultsText(_resultsManager.TransientDatabaseMetricsDictionary, outputFolder, taskId, numFiles);

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
    /// Filters protein groups to only include those where all proteins are from the transient database
    /// </summary>
    private IEnumerable<ProteinGroup> FilterProteinGroupsToTransientDatabaseOnly(List<ProteinGroup> proteinGroups,
        HashSet<string> transientProteinAccessions)
    {
        return proteinGroups
            .Where(pg => pg.Proteins.Any(p => transientProteinAccessions.Contains(p.Accession)));
    }

    private static IEnumerable<ProteinGroup> FilterProteinGroupsToTransientProteins(List<ProteinGroup> proteinGroups,
        HashSet<IBioPolymer> transientProteins)
    {
        return proteinGroups.Where(pg => pg.Proteins.Any(transientProteins.Contains));
    }

    private List<SpectralMatch> BuildParsimonyNeighborhoodPsms(
        SpectralMatch[] allPsms,
        HashSet<int> updatedPsmIndexes,
        HashSet<IBioPolymer> transientProteins,
        List<string> nestedIds)
    {
        List<SpectralMatch> neighborhoodPsms = new(updatedPsmIndexes.Count * 2);
        HashSet<int> includedScanIndexes = new();
        HashSet<string> neighborhoodPeptideKeys = new(StringComparer.Ordinal);

        foreach (int scanIndex in updatedPsmIndexes.OrderBy(p => p))
        {
            if (scanIndex < 0 || scanIndex >= allPsms.Length)
                continue;

            var psm = allPsms[scanIndex];
            if (psm is null)
                continue;

            SpectralMatch? originalBaselinePsm = BaseSearchPsms[scanIndex];

            foreach (var hypothesis in psm.BestMatchingBioPolymersWithSetMods)
            {
                // Hypothesis is from baseline search that was replaced entirely, so add its original hypotheses to the neighborhood
                if (!transientProteins.Contains(hypothesis.SpecificBioPolymer.Parent) && originalBaselinePsm is not null)
                    foreach (var originalHypothesis in originalBaselinePsm.BestMatchingBioPolymersWithSetMods)
                            neighborhoodPeptideKeys.Add(GetParsimonyPeptideKey(originalHypothesis.SpecificBioPolymer));
                else
                    neighborhoodPeptideKeys.Add(GetParsimonyPeptideKey(hypothesis.SpecificBioPolymer));
            }

            neighborhoodPsms.Add(psm);
            includedScanIndexes.Add(scanIndex);
        }

        foreach (string peptideKey in neighborhoodPeptideKeys)
        {
            if (!_baselinePeptideKeyToScanIndexes.TryGetValue(peptideKey, out var baselineScanIndexes))
                continue;

            foreach (int scanIndex in baselineScanIndexes)
            {
                if (!includedScanIndexes.Add(scanIndex))
                    continue;

                var baselinePsm = BaseSearchPsms[scanIndex];
                if (baselinePsm is null)
                    continue;

                var clonedBaselinePsm = ClonePsmForParsimony(baselinePsm);
                if (clonedBaselinePsm is null)
                    continue;

                neighborhoodPsms.Add(clonedBaselinePsm);
            }
        }

        Log($"Parsimony neighborhood for {string.Join(" > ", nestedIds)}: {neighborhoodPsms.Count} PSMs ({updatedPsmIndexes.Count} updated scans, {neighborhoodPeptideKeys.Count} transient peptide keys)", nestedIds);

        return neighborhoodPsms;
    }

    private static SpectralMatch? ClonePsmForParsimony(SpectralMatch source)
    {
        var bestMatches = source.BestMatchingBioPolymersWithSetMods.ToList();
        if (bestMatches.Count == 0)
            return null;

        SpectralMatch? clone = source switch
        {
            PeptideSpectralMatch peptidePsm => peptidePsm.Clone(bestMatches),
            _ => null
        };

        if (clone is null)
            return null;

        clone.PsmFdrInfo = source.PsmFdrInfo?.Clone() ?? new FdrInfo();
        clone.PeptideFdrInfo = source.PeptideFdrInfo?.Clone() ?? new FdrInfo();
        clone.ResolveAllAmbiguities();

        return clone;
    }

    private string GetParsimonyPeptideKey(IBioPolymerWithSetMods peptide)
    {
        string sequence = SearchParameters.ModPeptidesAreDifferent ? peptide.FullSequence : peptide.BaseSequence;
        return $"{peptide.DigestionParams.DigestionAgent}|{sequence}";
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
                string peptideKey = GetParsimonyPeptideKey(hypothesis.SpecificBioPolymer);
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

    private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        public static ReferenceComparer<T> Instance { get; } = new();

        public bool Equals(T? x, T? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(T obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
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
}
