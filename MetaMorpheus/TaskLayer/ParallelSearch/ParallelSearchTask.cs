#nullable enable
using Chromatography.RetentionTimeCalibration;
using EngineLayer;
using EngineLayer.DatabaseLoading;
using EngineLayer.FdrAnalysis;
using EngineLayer.Indexing;
using EngineLayer.ParallelSearch;
using EngineLayer.ParallelSearch.FdrAlignment;
using EngineLayer.SpectrumMatch;
using EngineLayer.Util;
using MassSpectrometry;
using Nett;
using Omics;
using Omics.Modifications;
using Proteomics.ProteolyticDigestion;
using Omics.SpectrumMatch;
using Readers.DeNovoMapping;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Analysis.Collectors;
using TaskLayer.ParallelSearch.Evaluation;
using TaskLayer.ParallelSearch.Twins;
using TaskLayer.ParallelSearch.Util;
using UsefulProteomicsDatabases;

namespace TaskLayer.ParallelSearch;
public class ParallelSearchTask : SearchTask
{
    private readonly object _progressLock = new object();
    private readonly object _countLock = new object();
    private TransientDatabaseResultsManager? _resultsManager;

    // The de novo mapper, built in the background from when the base database is loaded, and the collector that maps each
    // searched database with it
    private Task<DeNovoMapper>? _deNovoMapperTask;
    private DeNovoMappingCollector? _deNovoCollector;
    private Channel<(TransientDatabaseContext Context, TransientDatabaseMetrics Metrics)>? _completedDatabaseWriteChannel;
    private Task? _completedDatabaseWriterTask;
    private int _cachedAtStart;
    private int _initialFinishedCount;
    private int _completedThisRun;

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
                ParallelSearchParameters = new ParallelSearchParameters(value);
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

    /// <summary>
    /// Arrays the length of <see cref="BaseSearchPsms"/>, each handed to one transient search at a time and returned when
    /// its database is done. A new copy per database lands on the large object heap (one reference per scan), which only
    /// a gen2 collection frees; reusing them keeps the arrays to one per search running at once.
    /// </summary>
    [TomlIgnore] private readonly ConcurrentBag<SpectralMatch[]> _transientPsmArrays = new();
    [TomlIgnore] private PerFileTolerances? _perFileTolerances; // null when every file uses the task's tolerances


    // Optimization caches for FDR alignment and parsimony
    [TomlIgnore] private BaselineScoreLookup<FdrInfo> _baselinePsmFdr = new([]);
    [TomlIgnore] private BaselineScoreLookup<FdrInfo> _baselinePeptideFdr = new([]);
    // per-notch notch q-value curves; empty (and unused) when the acceptor has one notch
    [TomlIgnore] private IReadOnlyDictionary<int, BaselineScoreLookup<NotchFdr>> _baselinePsmNotchFdr = new Dictionary<int, BaselineScoreLookup<NotchFdr>>();
    [TomlIgnore] private IReadOnlyDictionary<int, BaselineScoreLookup<NotchFdr>> _baselinePeptideNotchFdr = new Dictionary<int, BaselineScoreLookup<NotchFdr>>();
    [TomlIgnore] private ProteinGroupFdrLookup _baselineProteinGroupFdr = new([], FilterType.QValue);
    [TomlIgnore] private List<CachedProteinGroup> CachedBaselineProteinGroups { get; set; } = [];
    // the baseline's run-wide PSM and peptide counts, updated per database instead of re-sorting every PSM; null under
    // PEP filtering, whose cutoff needs the full lists
    [TomlIgnore] private BaselineMatchCounts? _baselineMatchCounts;
    // the classic search's per-scan structures (masses, sorted index, locks), built once for AllSortedMs2Scans and shared
    // by every search instead of rebuilt per database
    [TomlIgnore] private EngineLayer.ClassicSearch.ClassicSearchScanIndex? _scanIndex;
    [TomlIgnore] private readonly Dictionary<string, List<int>> _baselinePeptideKeyToScanIndexes = new(StringComparer.Ordinal);
    [TomlIgnore] private readonly Dictionary<IBioPolymer, List<int>> _baselineProteinToScanIndexes = new(ReferenceEqualityComparer.Instance);
    [TomlIgnore] private Dictionary<string, IrtCalibrationModel> _retentionTimeCalibrations = new();

    // Every database this run searches: the transient databases, then their twins when UseTwinEntrapment is on
    [TomlIgnore] private List<DbForTask> _databasesToSearch = [];
    [TomlIgnore] private TwinEntrapment.TwinSettings? _twinSettings; // set when UseTwinEntrapment is on

    // Properties to track progress and results across databases
    [TomlIgnore] public int TotalDatabases => _databasesToSearch.Count;
    [TomlIgnore] public int TotalMs2Scans => AllSortedMs2Scans.Length;
    [TomlIgnore] public int PersistentDatabasePeptideCount { get; set; } = -1; // populated during initialization for use in analysis

    #endregion

    /// <summary> Rough memory for one database searched at once: its copy of the baseline PSM array, its per-scan lock and any PSMs it changes. </summary>
    private const int TransientBytesPerScan = 128;

    protected override MyTaskResults RunSpecific(string outputFolder,
        List<DbForTask> dbFilenameList, List<string> currentRawFileList,
        string taskId, FileSpecificParameters[] fileSettingsList)
    {
        ValidateSettings(fileSettingsList, currentRawFileList);

        MyTaskResults = new MyTaskResults(this);
        PersistentDatabases = dbFilenameList;
        _databasesToSearch = GetDatabasesToSearch(outputFolder, dbFilenameList);

        if (currentRawFileList.All(p => p.EndsWith(".mgf", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".msAlign", StringComparison.OrdinalIgnoreCase)))
        {
            CommonParameters.DoPrecursorDeconvolution = false;
            CommonParameters.UseProvidedPrecursorInfo = true;
        }

        // Initialize unified results manager
        _deNovoMapperTask = null;
        _resultsManager = CreateResultsManager(outputFolder);
        if (_resultsManager.UnreadableSummaryMessage is { } unreadableSummaryMessage)
        {
            Warn(unreadableSummaryMessage);
        }

        // Check cache status early for fast-path optimization
        var allDatabaseNames = _databasesToSearch
            .Select(db => Path.GetFileNameWithoutExtension(db.FilePath))
            .ToList();
         
        var cacheSummary = _resultsManager!.GetCacheSummary(allDatabaseNames);
        Status(cacheSummary.ToString(), taskId);
        InitializeProgressCounts(cacheSummary);

        // Fast path: If all databases are cached and not overwriting, skip to finalization
        if (cacheSummary.DatabasesNeedingProcessing == 0 && !ParallelSearchParameters.OverwriteTransientSearchOutputs)
        {
            Status("All databases cached, skipping search phase and proceeding to finalization...", taskId);
            goto Finalization;
        }

        // Initialize all necessary data structures including base search
        Initialize(taskId, dbFilenameList, currentRawFileList, fileSettingsList, outputFolder);
        if (ParallelSearchParameters.UseTwinEntrapment)
        {
            EnsureTwinDatabases(taskId);
        }
        InitializeCompletedDatabaseWriter();

        Status($"Starting search of {TotalDatabases} transient databases...", taskId);

        // The user's MaxThreadsToUsePerFile is one budget shared by every database searched at once; MaxSearchesInParallel caps how many.
        // Each worker gets its own copy of the parameters, so the task's own CommonParameters are never changed.
        int databasesToProcess = Math.Max(1, _databasesToSearch.Count(db =>
            ParallelSearchParameters.OverwriteTransientSearchOutputs
            || !_resultsManager!.HasCachedResults(Path.GetFileNameWithoutExtension(db.FilePath))));
        long bytesPerDatabase = (long)AllSortedMs2Scans.Length * TransientBytesPerScan;
        // A transient database's work is mostly single-threaded (loading, FDR alignment, parsimony, metrics, writing); only its
        // search uses more than one thread, and for a viral or bacterial proteome against the baseline's scans that is short.
        // So the budget goes to more databases at once, down to one thread each, not to more threads per database.
        FileParallelismPlan plan = FileParallelism.Decide(databasesToProcess, CommonParameters.MaxThreadsToUsePerFile,
            IndexPartitioning.AvailableBytes(), bytesPerDatabase, ParallelSearchParameters.MaxSearchesInParallel,
            minimumThreadsPerFile: 1);
        ProseCreatedWhileRunning.Append($"transient databases searched in parallel = {plan.FilesInParallel} of {databasesToProcess} " +
            $"(limited by {plan.LimitedBy}), each with {plan.ThreadsPerFile} threads; ");
        CommonParameters transientParameters = CommonParameters.CloneWithNewMaxThreadsToUsePerFile(plan.ThreadsPerFile);

        // Loop through each transient database
        PhaseTimings.StartLoop();
        try
        {
            var databases = _databasesToSearch;
            FileParallelism.ForEachFile(databases.Count, plan.FilesInParallel,
                index => ProcessTransientDatabase(databases[index], outputFolder, taskId, transientParameters));
        }
        finally
        {
            CompleteCompletedDatabaseWriter();
            PhaseTimings.EndLoopAndWrite(plan.FilesInParallel, plan.ThreadsPerFile);
        }

          Finalization:

        if (ParallelSearchParameters.DeNovoMzTabPaths is { Count: > 0 })
        {
            // each searched database was mapped during its search; map the rows that were not, and write the mapping summary
            FinishDeNovoMapping(outputFolder, dbFilenameList, currentRawFileList, taskId);
        }
        // If we have a de novo results path that has not been collected into our results yet, collect it before writing the summary.
        else if (ParallelSearchParameters.UseDeNovoMapping && _resultsManager.TransientDatabaseMetricsDictionary.All(p => p.Value.TotalPredictions == 0))
        {
            string deNovoMappingDataFilePath = ParallelSearchParameters.DeNovoMappingDataFilePath!;

            var collector = new DeNovoMappingCollector(deNovoMappingDataFilePath);

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

        // Rewrite the summary so it holds every database, including rows carried over from an earlier run
        Status("Writing summary...", taskId);
        _resultsManager!.PersistAnalysisCache();
        FinishedWritingFile(_resultsManager.SummaryFilePath, [taskId]);

        if (ParallelSearchParameters.WriteTransientSpectralLibrary)
        {
            Status("Combining the transient spectral libraries...", taskId);
            string combinedLibraryPath = Path.Combine(outputFolder, TransientSpectralLibrary.CombinedFileName);
            if (TransientSpectralLibrary.Combine(outputFolder, combinedLibraryPath) is { } combined)
            {
                FinishedWritingFile(combinedLibraryPath, [taskId]);
                Status($"Combined library: {combined.OutputEntryCount} peptides from {combined.SourceEntryCounts.Count} databases.", taskId);
            }
        }

        RunDatabaseEvaluations(outputFolder, taskId);

        MyTaskResults.AddTaskSummaryText(
            $"Searched {TotalDatabases} transient databases against {currentRawFileList.Count} spectra files.");

        ReportProgress(new(100, "Parallel search task complete!", [taskId]));
        return MyTaskResults;
    }

    #region Initialization

    /// <summary>
    /// Rejects settings the parallel search cannot honour, before anything is loaded or written.
    /// The transient search engine skips XCorr scoring for speed, and the
    /// summary cache and per-database output folders are keyed by database file name.
    /// </summary>
    private void ValidateSettings(FileSpecificParameters?[]? fileSettingsList, List<string>? currentRawFileList)
    {
        var transientDatabases = ParallelSearchParameters.TransientDatabases;
        if (transientDatabases == null || transientDatabases.Count == 0)
        {
            throw new MetaMorpheusException("Parallel search needs at least one transient database. "
                + "Add transient databases to the task before running it.");
        }

        var duplicateNames = transientDatabases
            .GroupBy(db => Path.GetFileNameWithoutExtension(db.FilePath), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} ({string.Join(", ", g.Select(db => db.FilePath))})")
            .ToList();
        if (duplicateNames.Count > 0)
        {
            throw new MetaMorpheusException("Parallel search needs every transient database to have a different file name, "
                + "because results are stored by name. These names are used more than once: "
                + string.Join("; ", duplicateNames));
        }

        if (ParallelSearchParameters.UseTwinEntrapment)
        {
            var twinConflicts = TwinEntrapment.FindNameConflicts(
                transientDatabases.Select(db => Path.GetFileNameWithoutExtension(db.FilePath)));
            if (twinConflicts.Count > 0)
            {
                throw new MetaMorpheusException($"With twin entrapment on, each transient database's twin is named <name>{TwinEntrapment.TwinSuffix}, "
                    + "so no transient database may already have a name like that. Rename these databases or turn off twin entrapment: "
                    + string.Join("; ", twinConflicts));
            }
        }

        if (ParallelSearchParameters.HasDeNovoMappingDataFile && ParallelSearchParameters.DeNovoMzTabPaths is { Count: > 0 })
        {
            throw new MetaMorpheusException("Give de novo predictions either as a mapping summary (DeNovoMappingDataFilePath) "
                + "or as mzTab files (DeNovoMzTabPaths), not both.");
        }

        var missingDeNovoFiles = new[] { ParallelSearchParameters.DeNovoMappingDataFilePath }
            .Where(_ => ParallelSearchParameters.HasDeNovoMappingDataFile).OfType<string>()
            .Concat(ParallelSearchParameters.DeNovoMzTabPaths ?? [])
            .Concat(ParallelSearchParameters.DeNovoSpectraFilePaths ?? [])
            .Where(path => !File.Exists(path))
            .ToList();
        if (missingDeNovoFiles.Count > 0)
        {
            throw new MetaMorpheusException("These de novo mapping files cannot be found: " + string.Join("; ", missingDeNovoFiles)
                + ". Fix the paths or remove them from the task, and run again.");
        }

        if (GlobalVariables.AnalyteType == AnalyteType.Oligo)
        {
            throw new MetaMorpheusException("Parallel search does not support RNA searches yet.");
        }

        ValidateDissociationType(CommonParameters.DissociationType, "the task settings");
        if (fileSettingsList != null)
        {
            for (int i = 0; i < fileSettingsList.Length; i++)
            {
                var fileSettings = fileSettingsList[i];
                if (fileSettings is null)
                {
                    continue;
                }

                if (fileSettings.DissociationType is { } fileDissociationType)
                {
                    ValidateDissociationType(fileDissociationType, "a file-specific settings file");
                }

                // the transient engine digests and fragments with the task's settings for every file; only the
                // tolerances and deconvolution are applied per file
                var unhonoured = FileSettingsTheSearchCannotHonour(fileSettings);
                if (unhonoured.Count > 0)
                {
                    string fileName = currentRawFileList != null && i < currentRawFileList.Count
                        ? Path.GetFileName(currentRawFileList[i])
                        : $"spectra file {i + 1}";
                    throw new MetaMorpheusException($"Parallel search uses the task's digestion and fragmentation settings for every file, "
                        + $"but the file-specific settings for {fileName} change {string.Join(", ", unhonoured)}. "
                        + "Remove these from the file-specific settings, or set them on the task, and run again.");
                }
            }
        }

        if (SearchParameters.SearchType != SearchType.Classic)
        {
            throw new MetaMorpheusException($"Parallel search only runs a classic search, but the search type is set to {SearchParameters.SearchType}. "
                + "Set the search type to Classic and run again.");
        }

        if (SearchParameters.MassDiffAcceptorType.IsMostAbundant()
            || CommonParameters.PrecursorMassMatchMode == PrecursorMassMatchMode.MostAbundant)
        {
            throw new MetaMorpheusException("Parallel search does not support most-abundant precursor matching. "
                + "Choose a monoisotopic precursor mass tolerance and run again.");
        }
    }

    /// <summary>
    /// File-specific settings that differ from the task's and that the transient search would silently ignore,
    /// each described as "setting (file value, task value)".
    /// </summary>
    private List<string> FileSettingsTheSearchCannotHonour(FileSpecificParameters fileSettings)
    {
        var taskDigestion = CommonParameters.DigestionParams;
        var unhonoured = new List<string>();
        void Check<T>(string name, T? fileValue, T taskValue) where T : struct
        {
            if (fileValue.HasValue && !EqualityComparer<T>.Default.Equals(fileValue.Value, taskValue))
            {
                unhonoured.Add($"{name} ({fileValue.Value} for the file, {taskValue} for the task)");
            }
        }

        Check(nameof(TaskLayer.FileSpecificParameters.DissociationType), fileSettings.DissociationType, CommonParameters.DissociationType);
        if (fileSettings.DigestionAgent != null && fileSettings.DigestionAgent.Name != taskDigestion.DigestionAgent.Name)
        {
            unhonoured.Add($"{nameof(TaskLayer.FileSpecificParameters.DigestionAgent)} ({fileSettings.DigestionAgent.Name} for the file, "
                + $"{taskDigestion.DigestionAgent.Name} for the task)");
        }
        Check(nameof(TaskLayer.FileSpecificParameters.MinPeptideLength), fileSettings.MinPeptideLength, taskDigestion.MinLength);
        Check(nameof(TaskLayer.FileSpecificParameters.MaxPeptideLength), fileSettings.MaxPeptideLength, taskDigestion.MaxLength);
        Check(nameof(TaskLayer.FileSpecificParameters.MaxMissedCleavages), fileSettings.MaxMissedCleavages, taskDigestion.MaxMissedCleavages);
        Check(nameof(TaskLayer.FileSpecificParameters.MaxModsForPeptide), fileSettings.MaxModsForPeptide, taskDigestion.MaxMods);
        if (fileSettings.CustomIons != null && CommonParameters.DissociationType == DissociationType.Custom
            && !fileSettings.CustomIons.ToHashSet().SetEquals(CommonParameters.CustomIons ?? []))
        {
            unhonoured.Add(nameof(TaskLayer.FileSpecificParameters.CustomIons));
        }

        return unhonoured;
    }

    private static void ValidateDissociationType(DissociationType dissociationType, string source)
    {
        if (dissociationType == DissociationType.Autodetect)
        {
            throw new MetaMorpheusException($"Parallel search does not support autodetecting the dissociation type (set in {source}). "
                + "Choose a dissociation type and run again.");
        }

        if (dissociationType == DissociationType.LowCID)
        {
            throw new MetaMorpheusException($"Parallel search does not support Low CID dissociation (set in {source}), "
                + "because it does not score with XCorr. Choose another dissociation type and run again.");
        }
    }

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

        // The de novo mapper needs only the predictions, the spectra Casanovo read and the host, so build it while searching
        StartDeNovoMapper(dbFilenameList, currentRawFileList, taskId);

        // 3. Load all spectra files once and store in memory
        Status("Loading spectra files...", taskId);
        ConcurrentDictionary<string, Ms2ScanWithSpecificMass[]> loadedSpectraByFile = new();
        int totalMs2Scans = LoadSpectraFiles(currentRawFileList, fileSettingsList, MyFileManager,
            loadedSpectraByFile, taskId);
        AllSortedMs2Scans = loadedSpectraByFile
            .SelectMany(p => p.Value)
            .OrderBy(b => b.PrecursorMass)
            .ToArray();

        // Calibrated per-file tolerances: each scan carries its file's index so the one merged search can honour them
        _perFileTolerances = PerFileTolerances.Create(AllSortedMs2Scans, FileSpecificParameters, CommonParameters, out string? toleranceWarning);
        if (toleranceWarning != null)
            Warn(toleranceWarning);

        // 4. Perform base database search once and store results
        Status("Performing base database search...", taskId);
        BaseSearchPsms = new SpectralMatch[AllSortedMs2Scans.Length];
        PerformSearch(BaseBioPolymers, BaseSearchPsms, [taskId], out _, out int persistentDatabasePeptideCount);
        PersistentDatabasePeptideCount = persistentDatabasePeptideCount;
        Status($"Base search complete. Found {BaseSearchPsms.Count(p => p != null)} PSMs.", taskId);

        // 5. Run baseline FDR once and cache score->q-value mapping for transient alignment
        var baselinePsms = BaseSearchPsms.Where(p => p != null)
            .OrderByDescending(p => p).ToList();

        // every transient match takes its FDR from the baseline's, so without baseline matches there is nothing to align to
        if (baselinePsms.Count == 0)
        {
            throw new MetaMorpheusException("The base database search found no matches, so no transient database can be scored. "
                + "Check that the base databases and the spectra files belong together.");
        }

        int numNotches = GetMassDiffAcceptor(CommonParameters.PrecursorMassTolerance,
            SearchParameters.MassDiffAcceptorType, SearchParameters.CustomMdac).NumNotches;
        var baselineFdrEngine = new FdrAnalysisEngine(
            baselinePsms, numNotches, CommonParameters,
            FileSpecificParameters, [taskId], "PSM", false, outputFolder);
        baselineFdrEngine.Run();

        // With more than one notch (e.g. OneMM), a match whose tied hypotheses sit at different notches has notch
        // q-values only on its hypotheses; give it its best notch's, and keep one notch q-value curve per notch for transient matches
        if (numNotches > 1)
        {
            BaselineFdr.UseBestNotch(baselinePsms);
            _baselinePsmNotchFdr = BaselineFdr.CreateNotchLookups(baselinePsms, peptideLevel: false);
            _baselinePeptideNotchFdr = BaselineFdr.CreateNotchLookups(baselinePsms, peptideLevel: true);
        }

        _baselinePsmFdr = BaselineFdr.CreatePsmLookup(baselinePsms);
        _baselinePeptideFdr = BaselineFdr.CreatePeptideLookup(baselinePsms);
        BuildBaselineScanIndexLookups();

        // Map Chronologer's % acetonitrile onto each file's minutes once, so every database's RT errors share one scale
        Status("Calibrating predicted retention times...", taskId);
        try
        {
            _retentionTimeCalibrations = RetentionTimeCollector.FitRetentionTimeCalibrations(baselinePsms,
                Math.Min(CommonParameters.QValueThreshold, CommonParameters.PepQValueThreshold),
                CommonParameters.MaxThreadsToUsePerFile, Warn);
        }
        catch (Exception e)
        {
            // e.g. TorchSharp's native libraries are missing; a missing predictor costs the RT columns, not the search
            Warn($"Retention time errors are not reported: Chronologer could not run ({e.GetBaseException().Message})");
        }

        if (SearchParameters.DoParsimony)
        {
            Status("Preparing baseline parsimony cache...", taskId);

            var baselinePsmsForParsimony = FilteredPsms.Filter(baselinePsms,
                commonParams: CommonParameters,
                includeDecoys: true,
                includeContaminants: true,
                includeAmbiguous: false,
                includeHighQValuePsms: true);

            // every baseline PSM is trimmed to the parsimonious proteins, as in a normal search, not only those parsimony used
            ProteinParsimonyResults baselineParsimonyResults = (ProteinParsimonyResults)new ProteinParsimonyEngine(
                baselinePsmsForParsimony, baselinePsms, SearchParameters.ModPeptidesAreDifferent,
                CommonParameters, FileSpecificParameters, [taskId]).Run();

            if (baselineParsimonyResults.ProteinGroups.Count > 0)
            {
                var baselineProteinScoringEngine = new ProteinScoringAndFdrEngine(
                        baselineParsimonyResults.ProteinGroups,
                        baselinePsms,
                        psmsAlreadyFiltered: true, // score every baseline PSM; transient FDR is aligned to it
                        SearchParameters.NoOneHitWonders,
                        SearchParameters.ModPeptidesAreDifferent,
                        true,
                        CommonParameters,
                        FileSpecificParameters,
                        [taskId]);
                var baselineProteinScoringResults = (ProteinScoringAndFdrResults)baselineProteinScoringEngine.Run();

                CachedBaselineProteinGroups = baselineProteinScoringResults.SortedAndScoredProteinGroups
                    .Select(p => new CachedProteinGroup(p))
                    .ToList();

                _baselineProteinGroupFdr = BaselineFdr.CreateProteinGroupLookup(baselineProteinScoringResults
                    .SortedAndScoredProteinGroups, baselineProteinScoringEngine.FilterType);
            }
            else
            {
                CachedBaselineProteinGroups = [];
                _baselineProteinGroupFdr = new([], FilterType.QValue);
            }
        }

        _baselineMatchCounts = ConfidenceCutoff.PepFilteringRequested(CommonParameters)
            ? null
            : new BaselineMatchCounts(BaseSearchPsms,
                ConfidenceCutoff.For([], CommonParameters, peptideLevel: false),
                ConfidenceCutoff.For([], CommonParameters, peptideLevel: true));

        // Write Psms and Peptides from base search
        string psmFile = Path.Combine(outputFolder,
            $"Base{GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        var psmTask = WritePsmsToTsvAsync(BaseSearchPsms.Where(p => p != null), psmFile, false)
            .ContinueWith(_ => FinishedWritingFile(psmFile, [taskId]));

        var allPeptides = BaseSearchPsms.Where(p => p != null).CollapseToPeptides(true).OrderByDescending(p => p).ToList();
        string peptideFile = Path.Combine(outputFolder,
            $"Base{GlobalVariables.AnalyteType}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        var peptideTask = WritePsmsToTsvAsync(allPeptides, peptideFile, true)
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

        // The user's thread budget is shared by the files loaded at once; copies are made here, on one thread.
        FileParallelismPlan plan = FileParallelism.Decide(currentRawFileList.Count, CommonParameters.MaxThreadsToUsePerFile,
            0, 0, ParallelSearchParameters.MaxSearchesInParallel);
        var paramsPerFile = currentRawFileList
            .Select((rawFile, i) => SetAllFileSpecificCommonParams(CommonParameters, fileSettingsList[i], rawFile)
                .CloneWithNewMaxThreadsToUsePerFile(plan.ThreadsPerFile))
            .ToArray();

        FileParallelism.ForEachFile(currentRawFileList.Count, plan.FilesInParallel,
             fileIndex =>
             {
                 string rawFile = currentRawFileList[fileIndex];
                 var fileParams = paramsPerFile[fileIndex];
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
    /// Creates the results manager with the configured metric collectors
    /// </summary>
    private TransientDatabaseResultsManager CreateResultsManager(string outputFolder)
    {
        // Define cache paths
        string analysisCachePath = Path.Combine(outputFolder, TransientDatabaseResultsManager.SummaryResultsFileName);

        // Initialize collectors based on user preferences
        var collectors = new List<IMetricCollector>
        {
            new BasicMetricCollector(),
            new PsmPeptideSearchCollector(ParallelSearchParameters.ForegroundOrganismName),
            new FragmentIonCollector(),
            new RetentionTimeCollector(),
        };

        if (ParallelSearchParameters.DoParsimony)
        {
            collectors.Add(new ProteinGroupCollector(ParallelSearchParameters.ForegroundOrganismName));
        }

        if (ParallelSearchParameters.DeNovoMappingDataFilePath != null && File.Exists(ParallelSearchParameters.DeNovoMappingDataFilePath))
        {
            collectors.Add(new DeNovoMappingCollector(ParallelSearchParameters.DeNovoMappingDataFilePath));
        }
        else if (ParallelSearchParameters.DeNovoMzTabPaths is { Count: > 0 })
        {
            // maps each database's proteins during its search, with the mapper built in the background from the base load on
            _deNovoCollector = new DeNovoMappingCollector(WaitForDeNovoMapper);
            collectors.Add(_deNovoCollector);
        }

        return new TransientDatabaseResultsManager(new MetricAggregator(collectors), analysisCachePath);
    }

    #endregion

    #region Search

    private void ProcessTransientDatabase(DbForTask transientDb, string outputFolder, string taskId, CommonParameters commonParameters)
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
         List<IBioPolymer> transientProteins;
         using (PhaseTimings.Time(dbName, "1_LoadDatabase"))
             transientProteins = LoadTransientDatabase(transientDb, nestedIds, taskId, commonParameters);

         if (GlobalVariables.StopLoops)
         {
             return;
         }

         // Create HashSet of transient bioPolymer accessions for later filtering
         var transientProteinAccessions = new HashSet<string>(
             transientProteins.Select(p => p.Accession));

        Status($"Searching {dbName} ({transientProteins.Count} transient proteins)...", nestedIds);

         // Reuse baseline PSMs with copy-on-write in peptide/proteoform mode: the search starts from the baseline's
         // references and copies a PSM only where it changes it. The array of references is borrowed, and nothing keeps
         // it once the post-search analysis returns (the context holds lists made from it).
         SpectralMatch[] psmArray;
         using (PhaseTimings.Time(dbName, "2_RentBaselineArray"))
             psmArray = RentTransientPsmArray();
         TransientDatabaseContext analysisContext;
         TransientDatabaseMetrics dbResults;
         try
         {
             HashSet<int> updatedPsmIndexes;
             int transientPeptideCount;
             using (PhaseTimings.Time(dbName, "3_Search"))
                 PerformSearch(transientProteins, psmArray, nestedIds, out updatedPsmIndexes, out transientPeptideCount, useCopyOnWrite: true, commonParameters: commonParameters);
             PhaseTimings.Record(dbName, "3_UpdatedScans", updatedPsmIndexes.Count);

             Status($"Performing post-search analysis for {dbName}...", nestedIds);

             int totalProteins = BaseBioPolymers.Count + transientProteins.Count;

             // Process database through unified manager (handles analysis + caching)
             (analysisContext, dbResults) = PerformPostSearchAnalysis(
                 psmArray,
                 dbOutputFolder,
                 nestedIds,
                 dbName,
                 totalProteins,
                 transientProteinAccessions,
                 transientPeptideCount,
                 transientProteins,
                 transientDb,
                 updatedPsmIndexes,
                 commonParameters
             );
         }
         finally
         {
             ReturnTransientPsmArray(psmArray);
         }

         using (PhaseTimings.Time(dbName, "6_WaitForWriter"))
             _completedDatabaseWriteChannel!.Writer.WriteAsync((analysisContext, dbResults)).AsTask().GetAwaiter().GetResult();

        // Cleanup transient proteins to free memory
        transientProteins.Clear();

        ReportProgress(new(100, $"Finished analysis for {dbName}; queued output writing", nestedIds));
    }

    /// <summary>
    /// Populates and returns the spectral match array using classic search engine
    /// </summary>
     private void PerformSearch(List<IBioPolymer> proteinsToSearch, SpectralMatch[] spectralMatchArray, List<string> nestedIds, out HashSet<int> updatedPsmIndexes, out int peptidesSearched, bool useCopyOnWrite = false, CommonParameters? commonParameters = null)
     {
         commonParameters ??= CommonParameters;
         // with per-file tolerances the window is searched at its widest and narrowed per scan inside the engine
         var massDiffAcceptor = GetMassDiffAcceptor(
             _perFileTolerances?.WidestPrecursor ?? CommonParameters.PrecursorMassTolerance,
             SearchParameters.MassDiffAcceptorType,
             SearchParameters.CustomMdac);

         // Run the classic search engine
          var searchEngine = new TransientClassicSearchEngine(
              spectralMatchArray, AllSortedMs2Scans, VariableModifications,
              FixedModifications, proteinsToSearch, massDiffAcceptor, commonParameters,
              FileSpecificParameters, nestedIds, copyOnWriteEnabled: useCopyOnWrite,
              perFileTolerances: _perFileTolerances,
              scanIndex: _scanIndex ??= new EngineLayer.ClassicSearch.ClassicSearchScanIndex(AllSortedMs2Scans, CommonParameters));

         var results = searchEngine.Run();
         updatedPsmIndexes = (results as TransientSearchEngineResults)!.UpdatedSpectralMatchIndexes;
         peptidesSearched = (results as TransientSearchEngineResults)!.PeptidesSearched;
         ReportProgress(new(100, "Finished Classic Search...", nestedIds));
     }

    private (TransientDatabaseContext Context, TransientDatabaseMetrics Metrics) PerformPostSearchAnalysis(
        SpectralMatch[] allPsms,
        string outputFolder,
        List<string> nestedIds,
        string dbName,
        int totalProteins,
        HashSet<string> transientProteinAccessions,
        int transientPeptideCount,
        List<IBioPolymer> transientProteins,
        DbForTask transientDatabase,
        HashSet<int> updatedPsmIndexes,
        CommonParameters commonParameters)
    {
        bool writeAllResults = !ParallelSearchParameters.WriteTransientResultsOnly;

        // Every PSM and peptide of the run, sorted, are needed only to write them all, for the spectral library, or for a
        // PEP cutoff. Otherwise the run-wide counts are the baseline's, updated for the scans this search changed.
        bool needFullLists = writeAllResults || SearchParameters.WriteSpectralLibrary || _baselineMatchCounts is null;
        List<SpectralMatch> psmList = needFullLists
            ? allPsms.Where(p => p != null).OrderByDescending(p => p).ToList()
            : [];

        #region FDR and Parsiomony

        Status($"Performing FDR Analysis for {dbName}...", nestedIds);

        // Disambiguate - modify PSMs in place - Only used for notch disambiguation and parallel search does not use notches. 
        //var disambiguationEngine = new DisambiguationEngine(
        //    psmList, CommonParameters, FileSpecificParameters, nestedIds);
        //await disambiguationEngine.RunAsync();
        //DebugStatus("Disambiguation complete", nestedIds, dbName, postAnalysisStopwatch);

        // Apply FDR Alignment to PSMs
        // Only scans the transient search changed can hold a transient protein, so only those are checked and sorted.
        // Taking them in scan order first keeps ties in the same order as the full sorted list.
        var transientPsms = FilterToTransientDatabaseOnly(
                updatedPsmIndexes.Order().Select(i => allPsms[i]).Where(p => p != null),
                transientProteinAccessions)
            .OrderByDescending(p => p)
            .ToList();
        BaselineFdr.AlignPsms(_baselinePsmFdr, transientPsms, _baselinePsmNotchFdr);

        // Collapse to peptides and apply FDR alignment to peptides
        List<SpectralMatch> allPeptides;
        List<SpectralMatch> affectedPeptides;
        HashSet<string>? affectedSequences = null;
        if (needFullLists)
        {
            allPeptides = psmList.CollapseToPeptides(true).ToList();
            affectedPeptides = allPeptides;
        }
        else
        {
            allPeptides = [];
            affectedPeptides = _baselineMatchCounts!.CollapseAffectedPeptides(allPsms, updatedPsmIndexes, out affectedSequences);
        }
        var transientPeptides = FilterToTransientDatabaseOnly(affectedPeptides, transientProteinAccessions).ToList();
        BaselineFdr.AlignPeptides(_baselinePeptideFdr, transientPeptides, _baselinePeptideNotchFdr);

        // read before parsimony, which trims hypotheses in place and can drop the baseline protein a match ties with
        var baselineTies = BaselineTies.Read(transientPsms, transientProteinAccessions);

        List<ProteinGroup>? proteinGroups = null;
        if (SearchParameters.DoParsimony && transientPsms.Count > 0)
        {
            Status($"Performing parsimony for {dbName}...", nestedIds);

            var transientParsimonyEngine = new TransientProteinParsimonyEngine(
                allPsms,
                BaseSearchPsms,
                updatedPsmIndexes,
                transientProteins,
                _baselinePeptideKeyToScanIndexes,
                _baselineProteinToScanIndexes,
                SearchParameters.ModPeptidesAreDifferent,
                commonParameters,
                FileSpecificParameters,
                nestedIds);

            ProteinParsimonyResults proteinAnalysisResults =
                (ProteinParsimonyResults)transientParsimonyEngine.Run();
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
                        (ProteinScoringAndFdrResults)new TransientProteinScoringAndFdrEngine(
                            baselineProteinGroups,
                            transientParsimonyGroups,
                            transientPsmsForParsimony,
                            _baselineProteinGroupFdr,
                            SearchParameters.NoOneHitWonders, SearchParameters.ModPeptidesAreDifferent,
                            true, commonParameters, FileSpecificParameters, nestedIds).Run();

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
            // parsimony trims each match's proteins in place, so a match whose transient protein lost to a baseline protein
            // is no longer a transient match; the FDR alignment above already holds for those that remain
            transientPsms = FilterToTransientDatabaseOnly(transientPsms, transientProteinAccessions).ToList();
            transientPeptides = FilterToTransientDatabaseOnly(transientPeptides, transientProteinAccessions).ToList();

            // Count bioPolymer groups that contain at least one transient database bioPolymer
            transientProteinGroups =
                FilterProteinGroupsToTransientDatabaseOnly(proteinGroups, transientProteinAccessions)
                .Select(CachedProteinGroup.DeepCopy)
                .ToList();
        }

        #region Result Caching and Analysis

        // read after parsimony, as counting the full lists in the collectors would have
        GlobalMatchCounts? globalCounts = needFullLists
            ? null
            : _baselineMatchCounts!.Count(allPsms, updatedPsmIndexes, affectedPeptides, affectedSequences!,
                ConfidenceCutoff.For([], commonParameters, peptideLevel: false),
                ConfidenceCutoff.For([], commonParameters, peptideLevel: true));

        // Create analysis context
        var analysisContext = new TransientDatabaseContext
        {
            DatabaseName = dbName,
            TwinOf = GetTwinTargetName(dbName),
            TransientDatabase = transientDatabase,
            TransientProteins = transientProteins,
            TransientProteinAccessions = transientProteinAccessions,
            BaselineTies = baselineTies,
            AllPsms = psmList,
            TransientPsms = transientPsms,
            AllPeptides = allPeptides,
            GlobalCounts = globalCounts,
            TransientPeptides = transientPeptides,
            ProteinGroups = proteinGroups,
            TransientProteinGroups = transientProteinGroups,
            RetentionTimeCalibrations = _retentionTimeCalibrations,
            CommonParameters = commonParameters,
            TotalProteins = totalProteins,
            TransientPeptideCount = transientPeptideCount,
            OutputFolder = outputFolder,
            NestedIds = nestedIds
        };

        // Process through unified manager (caches analysis results)
        Status($"Running analysis for {dbName}...", nestedIds);
        var aggregatedResult = _resultsManager!.ProcessDatabase(
            analysisContext,
            forceRecompute: ParallelSearchParameters.OverwriteTransientSearchOutputs
        );

        #endregion

        return (analysisContext, aggregatedResult);
    }

    /// <summary>An array holding the baseline's PSM references, for one transient search. Give it back with <see cref="ReturnTransientPsmArray"/>.</summary>
    private SpectralMatch[] RentTransientPsmArray()
    {
        if (!_transientPsmArrays.TryTake(out SpectralMatch[]? psmArray) || psmArray.Length != BaseSearchPsms.Length)
            return BaseSearchPsms.ToArray();

        Array.Copy(BaseSearchPsms, psmArray, BaseSearchPsms.Length);
        return psmArray;
    }

    /// <summary>
    /// Takes back an array from <see cref="RentTransientPsmArray"/>. It is cleared first, so the PSMs one database's
    /// search copied (and through them its proteins) are not kept alive until the array's next use.
    /// </summary>
    private void ReturnTransientPsmArray(SpectralMatch[] psmArray)
    {
        Array.Clear(psmArray);
        _transientPsmArrays.Add(psmArray);
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
        // the result tables are written compressed; CompressTransientDatabaseOutput still compresses the rest of the folder
        bool compress = ParallelSearchParameters.CompressTransientSearchOutputs;

        Status($"Writing results for {dbName}...", nestedIds);
        var lap = PhaseTimings.StartLap(dbName);

        string transientPsmFile = Path.Combine(outputFolder,
            $"{dbName}_All{GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        await WritePsmsToTsvAsync(context.TransientPsms, transientPsmFile, false, compress);
        FinishedWritingFile(WrittenPath(transientPsmFile, compress), nestedIds);
        lap.Mark("7a_WriteTransientPsms");

        if (writeAllResults)
        {
            string psmFile = Path.Combine(outputFolder,
                $"All{GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
            await WritePsmsToTsvAsync(context.AllPsms, psmFile, false, compress);
            FinishedWritingFile(WrittenPath(psmFile, compress), nestedIds);
        }

        if (context.TransientPeptides is { Count: > 0 })
        {
            string transientPeptideFile = Path.Combine(outputFolder,
            $"{dbName}_All{GlobalVariables.AnalyteType}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
            await WritePsmsToTsvAsync(context.TransientPeptides, transientPeptideFile, true, compress);
            FinishedWritingFile(WrittenPath(transientPeptideFile, compress), nestedIds);
        }
        lap.Mark("7b_WriteTransientPeptides");

        if (writeAllResults)
        {
            string peptideFile = Path.Combine(outputFolder,
                $"All{GlobalVariables.AnalyteType}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
            await WritePsmsToTsvAsync(context.AllPeptides, peptideFile, true, compress);
            FinishedWritingFile(WrittenPath(peptideFile, compress), nestedIds);
        }

        if (context.TransientProteinGroups is { Count: > 0 })
        {
            context.TransientProteinGroups.ForEach(x => x.GetIdentifiedPeptidesOutput(SearchParameters.SilacLabels));
            string transientProteinFile = Path.Combine(outputFolder,
                $"{dbName}_All{GlobalVariables.AnalyteType.GetBioPolymerLabel()}Groups.tsv");
            await WriteProteinGroupsToTsvAsync(context.TransientProteinGroups, transientProteinFile, compress);
            FinishedWritingFile(WrittenPath(transientProteinFile, compress), nestedIds);
        }
        lap.Mark("7c_WriteTransientGroups");

        if (writeAllResults && context.ProteinGroups is { Count: > 0 })
        {
            context.ProteinGroups.ForEach(x => x.GetIdentifiedPeptidesOutput(SearchParameters.SilacLabels));
            string proteinFile = Path.Combine(outputFolder,
                $"All{GlobalVariables.AnalyteType.GetBioPolymerLabel()}Groups.tsv");
            await WriteProteinGroupsToTsvAsync(context.ProteinGroups, proteinFile, compress);
            FinishedWritingFile(WrittenPath(proteinFile, compress), nestedIds);
        }

        if (SearchParameters.WriteSpectralLibrary)
        {
            string spectralLibraryPath = Path.Combine(outputFolder, $"AllPeptidesAnd_{dbName}_SpectralLibrary.msp");
            await WriteSpectralLibraryAsync(context.AllPsms, spectralLibraryPath);
            FinishedWritingFile(spectralLibraryPath, nestedIds);
        }

        if (ParallelSearchParameters.WriteTransientSpectralLibrary)
        {
            string spectralLibraryPath = Path.Combine(outputFolder, dbName + TransientSpectralLibrary.DatabaseFileSuffix);
            var libraryEntries = TransientSpectralLibrary.BuildEntries(context.TransientPsms, dbName,
                context.TransientProteinAccessions, context.PsmCutoff);
            if (TransientSpectralLibrary.WriteDatabaseLibrary(spectralLibraryPath, libraryEntries))
            {
                FinishedWritingFile(spectralLibraryPath, nestedIds);
            }
        }

        lap.Mark("7d_WriteAllAndLibraries");
        await WriteIndividualDatabaseResultsTextAsync(context, metrics);
        lap.Mark("7e_WriteResultsTxt");

        if (ParallelSearchParameters.CompressTransientSearchOutputs)
        {
            Status($"Compressing output for {dbName}...", nestedIds);
            CompressTransientDatabaseOutput(outputFolder);
        }
        lap.Mark("7f_Compress");

        bool appended = _resultsManager!.AppendCheckpoint(metrics);
        lap.Mark("7g_AppendSummaryRow");
        if (!appended)
        {
            Warn($"Metrics for {dbName} could not all be computed, so it is left out of {TransientDatabaseResultsManager.SummaryResultsFileName} "
                + $"and will be searched again on a rerun: {string.Join("; ", metrics.Errors)}");
        }
        MarkDatabaseCompleted();
        UpdateProgress(TotalDatabases, nestedIds[0]);
        ReportProgress(new(100, $"Finished {dbName}", nestedIds));
    }

    private async Task WriteIndividualDatabaseResultsTextAsync(TransientDatabaseContext context, TransientDatabaseMetrics results)
    {
        var resultsPath = Path.Combine(context.OutputFolder, "results.txt");
        // labelled with the cutoffs the counts were made at, which a normal search would also have chosen
        await results.WriteToTextFileAsync(resultsPath, context.PsmCutoff, context.PeptideCutoff,
            context.ProteinGroupQValueThreshold, SearchParameters.DoParsimony);
        FinishedWritingFile(resultsPath, context.NestedIds);
    }

    /// <summary>
    /// The file a per-database writer writes: <paramref name="filePath"/>, or <c>filePath.gz</c> when the folder's outputs are
    /// compressed.
    /// </summary>
    private static string WrittenPath(string filePath, bool compress) => compress ? filePath + ".gz" : filePath;

    /// <summary>
    /// Opens <see cref="WrittenPath"/> for writing as <c>new StreamWriter(filePath)</c> would (UTF-8 without a byte order mark),
    /// through a large file buffer. Compressed, the text goes straight into the .gz that
    /// <see cref="CompressTransientDatabaseOutput"/> would otherwise make from an uncompressed copy: on a busy spinning disk,
    /// writing that copy, reading it back and deleting it were most of a database's writing time. A stale uncompressed file of
    /// the same name is deleted first, or that pass would compress it over the fresh .gz.
    /// </summary>
    private static StreamWriter OpenResultWriter(string filePath, bool compress)
    {
        const int fileBufferBytes = 1 << 20;
        if (!compress)
        {
            return new StreamWriter(new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read, fileBufferBytes), ResultFileEncoding);
        }

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        var file = new FileStream(WrittenPath(filePath, true), FileMode.Create, FileAccess.Write, FileShare.Read, fileBufferBytes);
        return new StreamWriter(new GZipStream(file, CompressionMode.Compress), ResultFileEncoding);
    }

    /// <summary>The encoding <c>new StreamWriter(path)</c> uses.</summary>
    private static readonly UTF8Encoding ResultFileEncoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private async Task WriteProteinGroupsToTsvAsync(List<ProteinGroup> proteinGroups, string filePath, bool compress = false)
    {
        if (proteinGroups != null && proteinGroups.Any())
        {
            double qValueThreshold = Math.Min(CommonParameters.QValueThreshold, CommonParameters.PepQValueThreshold);
            using (StreamWriter output = OpenResultWriter(filePath, compress))
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

    /// <summary>
    /// Writes PSMs or peptides as a normal search does: the WriteDecoys, WriteContaminants and
    /// WriteHighQValuePsms filter from <see cref="PostSearchAnalysisTask"/>, sorted best first, then
    /// <see cref="MetaMorpheusTask.WritePsmsToTsv"/> for the columns (1/K0, collision energy and the
    /// most-abundant mass error when the data has them). Runs off the caller's thread so the baseline's
    /// files can be written at the same time.
    /// </summary>
    private Task WritePsmsToTsvAsync(IEnumerable<SpectralMatch> psms, string filePath, bool writePeptideLevelResults, bool compress = false)
    {
        return Task.Run(() =>
        {
            var psmsToWrite = FilteredPsms.Filter(psms,
                    CommonParameters,
                    includeDecoys: SearchParameters.WriteDecoys,
                    includeContaminants: SearchParameters.WriteContaminants,
                    includeAmbiguous: true,
                    includeHighQValuePsms: SearchParameters.WriteHighQValuePsms,
                    filterAtPeptideLevel: writePeptideLevelResults)
                .OrderByDescending(p => p)
                .ToList();

            using StreamWriter output = OpenResultWriter(filePath, compress);
            WritePsmsToTsv(psmsToWrite, output, SearchParameters.ModsToWriteSelection, writePeptideLevelResults);
        });
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
            // an existing .gz is either what this loop just wrote or an earlier run's output that it has just replaced;
            // CompressFile deletes a .gz it is handed, which would throw away the fresh result
            if (fileToCompress.Extension.Equals(".gz", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // the combined library is built from these, and the .msl format can hold its own zstd compression
            if (fileToCompress.Extension.Equals(".msl", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

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

    #region Twin Entrapment and Database Evaluation

    /// <summary>
    /// The transient databases, followed by their twins when twin entrapment is on. A twin lives beside its target
    /// (in a folder keyed by the generation settings) so later searches reuse it, or in the output folder when the
    /// target's folder is read-only. The list is complete before any twin file exists.
    /// </summary>
    private List<DbForTask> GetDatabasesToSearch(string outputFolder, List<DbForTask> baselineDatabases)
    {
        var databases = ParallelSearchParameters.TransientDatabases.ToList();
        if (ParallelSearchParameters.UseTwinEntrapment)
        {
            _twinSettings = TwinEntrapment.DescribeSettings(CommonParameters.DigestionParams, baselineDatabases);
            databases.AddRange(TwinEntrapment.GetTwinDatabases(ParallelSearchParameters.TransientDatabases, _twinSettings.Key, outputFolder));
        }
        return databases;
    }

    /// <summary>The target a database is the twin of, or empty when it is a real transient database.</summary>
    private string GetTwinTargetName(string databaseName) =>
        ParallelSearchParameters.UseTwinEntrapment && TwinEntrapment.TryGetTargetName(databaseName, out string targetName)
            ? targetName
            : string.Empty;

    /// <summary>Writes any missing twin FASTA. Needs the baseline loaded: no twin peptide may equal a baseline peptide.</summary>
    private void EnsureTwinDatabases(string taskId)
    {
        Status("Building twin entrapment databases...", taskId);
        var twins = _databasesToSearch.Skip(ParallelSearchParameters.TransientDatabases.Count).ToList();
        int built = TwinEntrapmentDatabaseBuilder.EnsureTwins(twins, _twinSettings!,
            BaseBioPolymers, CommonParameters.DigestionParams,
            targetDb => (new DatabaseLoadingEngine(CommonParameters, FileSpecificParameters, [taskId, "Twin entrapment"],
                [targetDb], taskId, DecoyType.None, SearchParameters.SearchTarget, LocalizableModificationTypes)
                .Run() as DatabaseLoadingEngineResults)!.BioPolymers,
            CommonParameters.MaxThreadsToUsePerFile);

        Status($"Twin entrapment databases ready ({built} built, {twins.Count - built} reused).", taskId);
        int inOutputFolder = twins.Count(t => !t.FilePath.StartsWith(
            Path.GetDirectoryName(Path.GetFullPath(t.DerivedFrom[0].FilePath))!, StringComparison.OrdinalIgnoreCase));
        if (inOutputFolder > 0)
        {
            Warn($"{inOutputFolder} twin entrapment database(s) were written to the output folder because their target's folder "
                + "cannot be written to, so later searches will rebuild them.");
        }
        ProseCreatedWhileRunning.Append($"Each transient database was paired with a shuffled entrapment twin (mzLib EntrapmentProteinGenerator, " +
            $"{TwinEntrapment.FoldCount} partner per entry, seed {TwinEntrapment.Seed}), searched as a transient database of its own. ");
    }

    /// <summary>
    /// Starts building the de novo mapper (reading the mzTabs, retention times, host digestion) in the background, so it runs
    /// alongside the searches; each database is mapped with it during its own search. Called once the base database is loaded,
    /// since the host comes from it.
    /// </summary>
    private void StartDeNovoMapper(List<DbForTask> dbFilenameList, List<string> currentRawFileList, string taskId)
    {
        if (ParallelSearchParameters.DeNovoMzTabPaths is not { Count: > 0 } || _deNovoMapperTask != null)
            return;

        Status("Preparing de novo predictions in the background...", taskId);
        var hostProteins = HostProteins(dbFilenameList, taskId);
        var spectraFiles = DeNovoSpectraFiles(currentRawFileList);
        var digestion = (DigestionParams)CommonParameters.DigestionParams;
        var decoyType = SearchParameters.DecoyType;
        int threads = CommonParameters.MaxThreadsToUsePerFile;
        var mzTabs = ParallelSearchParameters.DeNovoMzTabPaths.ToList();
        _deNovoMapperTask = Task.Run(() => DeNovoMapping.CreateMapper(mzTabs, spectraFiles, digestion, decoyType, hostProteins, threads));
    }

    private List<string> DeNovoSpectraFiles(List<string> currentRawFileList) =>
        ParallelSearchParameters.DeNovoSpectraFilePaths is { Count: > 0 } given ? given.ToList() : currentRawFileList.ToList();

    /// <summary>
    /// The de novo mapper, waiting for the background build to finish. The searches only start after it was started.
    /// </summary>
    private DeNovoMapper WaitForDeNovoMapper() =>
        (_deNovoMapperTask ?? throw new InvalidOperationException("The de novo mapper was not started.")).GetAwaiter().GetResult();

    /// <summary>
    /// After the searches: maps, from the database files, the summary rows the searches did not map (rows carried over from a
    /// run without de novo, or databases this run left cached), fills their de novo columns, and writes the mapping summary of
    /// every database mapped in this run. On the all-cached fast path, where nothing was searched, it builds the mapper here.
    /// </summary>
    private void FinishDeNovoMapping(string outputFolder, List<DbForTask> dbFilenameList, List<string> currentRawFileList, string taskId)
    {
        var metricsByDatabase = _resultsManager!.TransientDatabaseMetricsDictionary;
        var results = new List<DeNovoMappingResult>(_deNovoCollector?.MappedResults ?? []);

        // a row mapped with a host has a host protein count (zero or more); -1 means it was never mapped
        var unmapped = _databasesToSearch.Select(db => db.FilePath)
            .Where(path => metricsByDatabase.TryGetValue(Path.GetFileNameWithoutExtension(path), out var m) && m.DeNovoHostProteinCount < 0)
            .ToList();
        if (unmapped.Count > 0)
        {
            Status($"Mapping de novo predictions onto {unmapped.Count} databases not mapped during the searches...", taskId);
            StartDeNovoMapper(dbFilenameList, currentRawFileList, taskId);
            var (mapped, missing) = DeNovoMapping.MapDatabases(WaitForDeNovoMapper(), unmapped);
            foreach (string warning in missing)
                Warn(warning);
            foreach (var result in mapped)
            {
                var metrics = metricsByDatabase[result.DatabaseIdentifier];
                foreach (var (key, value) in DeNovoMappingCollector.ToResults(result))
                    metrics.Results[key] = value;
            }
            results.AddRange(mapped);
        }

        if (_deNovoMapperTask is { IsCompletedSuccessfully: true } built)
        {
            foreach (string warning in built.Result.Warnings)
                Warn(warning);
        }
        if (results.Count == 0)
            return;

        string summaryPath = DeNovoMapping.WriteSummary(results, outputFolder);
        FinishedWritingFile(summaryPath, [taskId]);
        Status($"Mapped de novo predictions onto {results.Count} databases.", taskId);
        ProseCreatedWhileRunning.Append($"Casanovo de novo predictions from {ParallelSearchParameters.DeNovoMzTabPaths.Count} mzTab file(s) " +
            "were mapped onto each transient database with mzLib's DeNovoMapper (I/L equivalent, modifications ignored, " +
            "retention times predicted by Chronologer and calibrated per file). Predictions whose peptide a " +
            $"{ParallelSearchParameters.ForegroundOrganismName} protein of the base database also produces were counted as host-ambiguous " +
            "and left out of the de novo tests. ");
    }

    /// <summary>
    /// The base database's target proteins from the foreground organism, by the rule the PSM and protein-group collectors use
    /// for ambiguity (organism name contains <see cref="ParallelSearchParameters.ForegroundOrganismName"/>). The base
    /// database is loaded here when the searches were skipped because every database was cached.
    /// </summary>
    private List<IBioPolymer> HostProteins(List<DbForTask> dbFilenameList, string taskId)
    {
        var baseProteins = BaseBioPolymers;
        if (baseProteins.Count == 0)
        {
            Status("Loading base database(s) for de novo host ambiguity...", taskId);
            baseProteins = (new DatabaseLoadingEngine(CommonParameters, FileSpecificParameters, [taskId], dbFilenameList, taskId,
                DecoyType.None, SearchParameters.SearchTarget, []).Run() as DatabaseLoadingEngineResults)!.BioPolymers;
        }

        string foreground = ParallelSearchParameters.ForegroundOrganismName;
        var host = baseProteins
            .Where(p => !p.IsDecoy && p.Organism != null && p.Organism.Contains(foreground, StringComparison.Ordinal))
            .ToList();
        if (host.Count == 0)
        {
            Warn($"No base database protein is from the foreground organism ({foreground}), so no de novo prediction is host-ambiguous.");
        }
        return host;
    }

    /// <summary>Runs the per-database evaluators on the summary and writes their files; also runs on the all-cached fast path.</summary>
    private void RunDatabaseEvaluations(string outputFolder, string taskId)
    {
        var written = DatabaseEvaluationRunner.RunAndWrite(DatabaseEvaluatorFactory.Create(ParallelSearchParameters, CommonParameters, Warn),
            _resultsManager!.TransientDatabaseMetricsDictionary.Values,
            _databasesToSearch.Select(db => Path.GetFileNameWithoutExtension(db.FilePath)),
            outputFolder, Warn, message => Status(message, taskId));

        foreach (string path in written)
            FinishedWritingFile(path, [taskId]);
    }

    #endregion

    #region Transient Protein Handling

    private List<IBioPolymer> LoadTransientDatabase(DbForTask transientDbPath,
        List<string> nestedIds, string taskId, CommonParameters commonParameters)
    {
        var transientDbList = new List<DbForTask> { transientDbPath };
        var transientDbLoader = new DatabaseLoadingEngine(commonParameters,
            FileSpecificParameters, nestedIds, transientDbList, taskId,
            SearchParameters.DecoyType, SearchParameters.SearchTarget,
            LocalizableModificationTypes);
        var transientProteins = (transientDbLoader.Run() as DatabaseLoadingEngineResults)!.BioPolymers;
        return transientProteins;
    }

    /// <summary>
    /// Filters spectral matches to only include those that match exclusively to transient database proteins
    /// </summary>
    private IEnumerable<SpectralMatch> FilterToTransientDatabaseOnly(IEnumerable<SpectralMatch> spectralMatches,
        HashSet<string> transientProteinAccessions, bool allowAmbiguous = true)
    {
        foreach (var psm in spectralMatches)
        {
            bool hasTransientProtein = false;

            // whether any (or every) hypothesis is transient does not depend on their order, so the unsorted list is read
            if (allowAmbiguous)
            {
                foreach (var match in psm.HypothesesInAddedOrder)
                {
                    if (transientProteinAccessions.Contains(match.SpecificBioPolymer.Parent.Accession))
                    {
                        hasTransientProtein = true;
                        break;
                    }
                }
            }
            else if (psm.HypothesesInAddedOrder.All(p => transientProteinAccessions.Contains(p.SpecificBioPolymer.Parent.Accession)))
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

    private void BuildBaselineScanIndexLookups()
    {
        _baselinePeptideKeyToScanIndexes.Clear();
        _baselineProteinToScanIndexes.Clear();

        for (int scanIndex = 0; scanIndex < BaseSearchPsms.Length; scanIndex++)
        {
            var psm = BaseSearchPsms[scanIndex];
            if (psm is null)
                continue;

            HashSet<string> keysSeenForScan = new(StringComparer.Ordinal);
            HashSet<IBioPolymer> proteinsSeenForScan = new(ReferenceEqualityComparer.Instance);
            foreach (var hypothesis in psm.BestMatchingBioPolymersWithSetMods)
            {
                IBioPolymer protein = hypothesis.SpecificBioPolymer.Parent;
                if (proteinsSeenForScan.Add(protein))
                {
                    if (!_baselineProteinToScanIndexes.TryGetValue(protein, out var proteinScanIndexes))
                    {
                        proteinScanIndexes = [];
                        _baselineProteinToScanIndexes.Add(protein, proteinScanIndexes);
                    }

                    proteinScanIndexes.Add(scanIndex);
                }

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

    #region Progress
    private void InitializeProgressCounts(CacheSummary cacheSummary)
    {
        lock (_countLock)
        {
            _cachedAtStart = cacheSummary.CachedDatabases;
            _completedThisRun = 0;
            _initialFinishedCount = ParallelSearchParameters.OverwriteTransientSearchOutputs
                ? 0
                : cacheSummary.CachedDatabases;
        }
    }

    private void MarkDatabaseCompleted()
    {
        lock (_countLock)
        {
            _completedThisRun++;
        }
    }

    private (int Finished, int Todo, int Cached) GetProgressCounts()
    {
        lock (_countLock)
        {
            int finished = _initialFinishedCount + _completedThisRun;
            finished = Math.Min(finished, TotalDatabases);
            int todo = Math.Max(0, TotalDatabases - finished);
            return (finished, todo, _cachedAtStart);
        }
    }

    private void UpdateProgress(int totalDatabases, string taskId)
    {
        lock (_progressLock)
        {
            var counts = GetProgressCounts();
            ReportProgress(new ProgressEventArgs(
                totalDatabases == 0 ? 100 : (int)(counts.Finished / (double)totalDatabases * 100),
                $"Completed {counts.Finished}/{totalDatabases} databases",
                new List<string> { taskId }));
        }
    }

    #endregion
}
