using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EngineLayer;
using EngineLayer.DatabaseLoading;
using EngineLayer.FragmentTypeDetection;
using MassSpectrometry;
using Omics;
using Omics.Fragmentation;
using Omics.Modifications;

namespace TaskLayer.FragmentTypeDetection;

public class FragmentTypeDetectionTask : SearchTask
{
    public FragmentTypeDetectionTask() : base(MyTask.FragmentDetection)
    {
        SearchParameters = new FragmentationDetectionParameters()
        {
            SearchType = SearchType.Classic,
        };
        CommonParameters = new(taskDescriptor: "FragmentTypeDetectionTask");
    }

    public override SearchParameters SearchParameters
    {
        get => FragmentationDetectionParameters;
        set
        {
            if (value is FragmentationDetectionParameters fp)
                FragmentationDetectionParameters = fp;
            else
            {
                FragmentationDetectionParameters = new FragmentationDetectionParameters(value);
            }
        }
    }

    public FragmentationDetectionParameters FragmentationDetectionParameters { get; set; } = new();

    protected override MyTaskResults RunSpecific(
        string OutputFolder, 
        List<DbForTask> dbFilenameList, 
        List<string> currentRawFileList, 
        string taskId,
        FileSpecificParameters[] fileSettingsList)
    {
        // Initialize task
        MyTaskResults = new FragmentTypeDetectionResult(this);
        MyFileManager myFileManager = new MyFileManager(SearchParameters.DisposeOfFileWhenDone);

        // Load modifications and database
        LoadModifications(taskId, out var variableModifications, out var fixedModifications, out var localizeableModificationTypes);
        
        var dbLoader = new DatabaseLoadingEngine(
            CommonParameters, 
            this.FileSpecificParameters, 
            [taskId], 
            dbFilenameList, 
            taskId, 
            SearchParameters.DecoyType, 
            SearchParameters.SearchTarget, 
            localizeableModificationTypes);
        
        var proteinLoadingTask = dbLoader.RunAsync();
        
        // Write prose settings
        WriteProse(fixedModifications, variableModifications);

        // Get all fragment types to test
        var allFragmentTypes = FragmentationDetectionParameters.IonsToSearchFor.Count == 0 
            ? GetFragmentTypesToTest() 
            : FragmentationDetectionParameters.IonsToSearchFor;

        // Storage for results from both searches
        List<SpectralMatch> allPsmsFromFirstSearch = new List<SpectralMatch>();
        List<SpectralMatch> allPsmsFromSecondSearch = new List<SpectralMatch>();

        Status("Running comprehensive fragment type detection...", new List<string> { taskId });

        // Ensure proteins are loaded
        proteinLoadingTask.Wait();
        var bioPolymerList = (proteinLoadingTask.Result as DatabaseLoadingEngineResults)!.BioPolymers;

        // First Search: Run comprehensive search with ALL fragment types across all files
        allPsmsFromFirstSearch = RunSearchAcrossAllFiles(
            currentRawFileList, 
            fileSettingsList, 
            myFileManager, 
            bioPolymerList,
            variableModifications, 
            fixedModifications, 
            allFragmentTypes, 
            taskId, 
            "comprehensive search with all fragment types");

        // Analyze fragment type performance
        Status("Analyzing fragment type performance...", new List<string> { taskId });
        var analysisEngine = new FragmentTypeAnalysisEngine(
            allPsmsFromFirstSearch, 
            allFragmentTypes, 
            CommonParameters, 
            FileSpecificParameters, 
            new List<string> { taskId });
        
        var analysisResults = analysisEngine.Run() as FragmentTypeAnalysisEngineResults;

        // Write first search results
        WriteFirstSearchResults(OutputFolder, analysisResults);

        // Determine optimal fragment types
        var optimalFragmentTypes = DetermineOptimalFragmentTypes(analysisResults);
        Status($"Optimal fragment types identified: {string.Join(", ", optimalFragmentTypes)}", new List<string> { taskId });

        // Second Search: Confirmatory search with optimal fragment types
        allPsmsFromSecondSearch = RunSearchAcrossAllFiles(
            currentRawFileList, 
            fileSettingsList, 
            myFileManager, 
            bioPolymerList,
            variableModifications, 
            fixedModifications, 
            optimalFragmentTypes, 
            taskId, 
            "confirmatory search with optimal fragment types");

        //// Compare results
        //var comparisonResult = CompareSearchResults(allPsmsFromFirstSearch, allPsmsFromSecondSearch);

        //// Write final results
        //WriteFinalResults(OutputFolder, analysisResults, comparisonResult, optimalFragmentTypes);

        Status("Fragment type detection complete!", new List<string> { taskId });

        return MyTaskResults;
    }

    #region Search Execution

    /// <summary>
    /// Run search across all data files with specified fragment types
    /// </summary>
    private List<SpectralMatch> RunSearchAcrossAllFiles(
        List<string> currentRawFileList,
        FileSpecificParameters[] fileSettingsList,
        MyFileManager myFileManager,
        List<IBioPolymer> bioPolymerList,
        List<Modification> variableModifications,
        List<Modification> fixedModifications,
        List<ProductType> fragmentTypes,
        string taskId,
        string searchDescription)
    {
        List<SpectralMatch> allPsms = new List<SpectralMatch>();
        int completedFiles = 0;

        Status($"Running {searchDescription}...", new List<string> { taskId });

        for (int spectraFileIndex = 0; spectraFileIndex < currentRawFileList.Count; spectraFileIndex++)
        {
            if (GlobalVariables.StopLoops) { break; }

            var origDataFile = currentRawFileList[spectraFileIndex];
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(origDataFile);
            var thisId = new List<string> { taskId, "Individual Spectra Files", origDataFile };

            // Mark file as in-progress
            StartingDataFile(origDataFile, thisId);
            NewCollection(Path.GetFileName(origDataFile), thisId);

            // Load file and get scans
            CommonParameters combinedParams = SetAllFileSpecificCommonParams(CommonParameters, fileSettingsList[spectraFileIndex]);
            
            Status("Loading spectra file...", thisId);
            MsDataFile myMsDataFile = myFileManager.LoadFile(origDataFile, combinedParams);
            
            Status("Getting ms2 scans...", thisId);
            Ms2ScanWithSpecificMass[] arrayOfMs2ScansSortedByMass = GetMs2Scans(myMsDataFile, origDataFile, combinedParams)
                .OrderBy(b => b.PrecursorMass)
                .ToArray();
            
            myFileManager.DoneWithFile(origDataFile);

            // Create search mode
            MassDiffAcceptor searchMode = GetMassDiffAcceptor(
                combinedParams.PrecursorMassTolerance, 
                SearchParameters.MassDiffAcceptorType, 
                SearchParameters.CustomMdac);

            // Run search using the engine
            Status($"Searching {fileNameWithoutExtension}...", thisId);
            
            var searchEngine = new FragmentTypeDetectionEngine(
                arrayOfMs2ScansSortedByMass,
                variableModifications,
                fixedModifications,
                bioPolymerList,
                searchMode,
                fragmentTypes,
                fileNameWithoutExtension,
                combinedParams,
                this.FileSpecificParameters,
                thisId);

            var searchResults = searchEngine.Run() as FragmentTypeDetectionEngineResults;
            allPsms.AddRange(searchResults.AllPsms);

            // Mark file as complete
            completedFiles++;
            FinishedDataFile(origDataFile, thisId);
            ReportProgress(new ProgressEventArgs(
                completedFiles / currentRawFileList.Count, 
                $"Searching ({completedFiles}/{currentRawFileList.Count})...", 
                new List<string> { taskId, "Individual Spectra Files" }));
        }

        ReportProgress(new ProgressEventArgs(100, "Search complete!", new List<string> { taskId, "Individual Spectra Files" }));
        
        return allPsms;
    }

    #endregion

    #region Analysis Methods

    /// <summary>
    /// Get all fragment types to test based on the analyte type
    /// </summary>
    public static List<ProductType> GetFragmentTypesToTest(AnalyteType? type = null)
    {
        type ??= GlobalVariables.AnalyteType;

        switch (type)
        {
            case AnalyteType.Oligo:
                return new List<ProductType>
                {
                    ProductType.a, ProductType.aBaseLoss, ProductType.aWaterLoss,
                    ProductType.b, ProductType.bBaseLoss, ProductType.bWaterLoss,
                    ProductType.c, ProductType.cBaseLoss, ProductType.cWaterLoss,
                    ProductType.d, ProductType.dBaseLoss, ProductType.dWaterLoss,
                    ProductType.w, ProductType.wBaseLoss, ProductType.wWaterLoss,
                    ProductType.x, ProductType.xBaseLoss, ProductType.xWaterLoss,
                    ProductType.y, ProductType.yBaseLoss, ProductType.yWaterLoss,
                    ProductType.z, ProductType.zBaseLoss, ProductType.zWaterLoss
                };

            case AnalyteType.Peptide:
            case AnalyteType.Proteoform:
                return new List<ProductType>
                {
                    ProductType.a, ProductType.b, ProductType.c,
                    ProductType.x, ProductType.y, ProductType.z,
                    ProductType.aDegree, ProductType.aStar, 
                    ProductType.bAmmoniaLoss, ProductType.bWaterLoss,
                    ProductType.yAmmoniaLoss, ProductType.yWaterLoss, 
                    ProductType.zDot, ProductType.zPlusOne
                };

            default:
                throw new NotImplementedException("Fragment type detection not implemented for this analyte type.");
        }
    }

    /// <summary>
    /// Determine the optimal fragment types to use based on the analysis
    /// </summary>
    private List<ProductType> DetermineOptimalFragmentTypes(FragmentTypeAnalysisEngineResults analysisResult)
    {
        // Keep fragment types that appear in >10% of PSMs
        var optimalTypes = analysisResult.FragmentTypeStatistics
            .Where(kvp => kvp.Value.PercentOfPsmsWithMatches > 10.0)
            .OrderByDescending(kvp => kvp.Value.PercentOfPsmsWithMatches)
            .Select(kvp => kvp.Key)
            .ToList();
        
        // Ensure we have at least some fragment types
        if (optimalTypes.Count == 0)
        {
            // Fall back to top 5 most common fragment types
            optimalTypes = analysisResult.FragmentTypeStatistics
                .OrderByDescending(kvp => kvp.Value.PsmsWithMatches)
                .Take(5)
                .Select(kvp => kvp.Key)
                .ToList();
        }
        
        return optimalTypes;
    }


    #endregion

    #region Output Methods

    private void WriteProse(List<Modification> fixedModifications, List<Modification> variableModifications)
    {
        ProseCreatedWhileRunning.Append("The following fragment type detection settings were used: ");
        ProseCreatedWhileRunning.Append($"{GlobalVariables.AnalyteType.GetDigestionAgentLabel()} = " + CommonParameters.DigestionParams.DigestionAgent + "; ");
        ProseCreatedWhileRunning.Append("maximum missed cleavages = " + CommonParameters.DigestionParams.MaxMissedCleavages + "; ");
        ProseCreatedWhileRunning.Append($"minimum {GlobalVariables.AnalyteType.GetUniqueFormLabel().ToLower()} length = " + CommonParameters.DigestionParams.MinLength + "; ");
        ProseCreatedWhileRunning.Append(CommonParameters.DigestionParams.MaxLength == int.MaxValue ?
            $"maximum {GlobalVariables.AnalyteType.GetUniqueFormLabel().ToLower()} length = unspecified; " :
            $"maximum {GlobalVariables.AnalyteType.GetUniqueFormLabel().ToLower()} length = " + CommonParameters.DigestionParams.MaxLength + "; ");
        if (CommonParameters.DigestionParams is Proteomics.ProteolyticDigestion.DigestionParams digestionParams)
            ProseCreatedWhileRunning.Append("initiator methionine behavior = " + digestionParams.InitiatorMethionineBehavior + "; ");
        ProseCreatedWhileRunning.Append("fixed modifications = " + string.Join(", ", fixedModifications.Select(m => m.IdWithMotif)) + "; ");
        ProseCreatedWhileRunning.Append("variable modifications = " + string.Join(", ", variableModifications.Select(m => m.IdWithMotif)) + "; ");
        ProseCreatedWhileRunning.Append($"max mods per {GlobalVariables.AnalyteType.GetUniqueFormLabel().ToLower()} = " + CommonParameters.DigestionParams.MaxMods + "; ");
        ProseCreatedWhileRunning.Append("max modification isoforms = " + CommonParameters.DigestionParams.MaxModificationIsoforms + "; ");
        ProseCreatedWhileRunning.Append("precursor mass tolerance = " + CommonParameters.PrecursorMassTolerance + "; ");
        ProseCreatedWhileRunning.Append("product mass tolerance = " + CommonParameters.ProductMassTolerance + ". ");
    }

    private void WriteFirstSearchResults(string outputFolder, FragmentTypeAnalysisEngineResults analysisResult)
    {
        string resultsFile = Path.Combine(outputFolder, "FragmentTypeDetection_ComprehensiveSearch.txt");

        using (StreamWriter writer = new StreamWriter(resultsFile))
        {
            writer.WriteLine("Fragment Type Detection - Comprehensive Search Results");
            writer.WriteLine("=======================================================");
            writer.WriteLine();
            writer.WriteLine($"Total PSMs: {analysisResult.TotalPsms}");
            writer.WriteLine($"PSMs at 1% FDR: {analysisResult.PsmsAt1PercentFdr}");
            writer.WriteLine($"Average Score: {analysisResult.AverageScore:F2}");
            writer.WriteLine();
            writer.WriteLine("Individual Fragment Type Performance:");
            writer.WriteLine("=====================================");
            writer.WriteLine();
            writer.WriteLine("Fragment Type\tPSMs with Matches\t% of PSMs\tAvg Matches When Present");

            foreach (var stat in analysisResult.FragmentTypeStatistics.OrderByDescending(kvp => kvp.Value.PercentOfPsmsWithMatches))
            {
                writer.WriteLine($"{stat.Key}\t{stat.Value.PsmsWithMatches}\t{stat.Value.PercentOfPsmsWithMatches:F2}%\t{stat.Value.AverageMatchesWhenPresent:F2}");
            }
        }

        FinishedWritingFile(resultsFile, new List<string> { "Fragment Type Detection" });

        var tsvPath = Path.Combine(outputFolder, "FragmentTypeDetection_FragmentTypeStatistics.tsv");
        new FragmentTypeStatisticsResultFile(tsvPath) 
        { 
            Results = analysisResult.FragmentTypeStatistics.Values.ToList() 
        }.WriteResults(tsvPath);
        FinishedWritingFile(tsvPath, new List<string> { "Fragment Type Detection" });
    }

    private void WriteFinalResults(
        string outputFolder,
        FragmentTypeAnalysisEngineResults analysisResult,
        SearchComparisonResult comparisonResult, 
        List<ProductType> optimalFragmentTypes)
    {
        string resultsFile = Path.Combine(outputFolder, "FragmentTypeDetection_FinalResults.txt");

        using (StreamWriter writer = new StreamWriter(resultsFile))
        {
            writer.WriteLine("Fragment Type Detection - Final Results");
            writer.WriteLine("=======================================");
            writer.WriteLine();
            writer.WriteLine("Optimal Fragment Types Identified:");
            writer.WriteLine(string.Join(", ", optimalFragmentTypes));
            writer.WriteLine();
            writer.WriteLine("Search Comparison:");
            writer.WriteLine("==================");
            writer.WriteLine();
            writer.WriteLine($"Comprehensive Search (all fragment types):");
            writer.WriteLine($"  PSMs at 1% FDR: {comparisonResult.FirstSearchPsmsAt1PercentFdr}");
            writer.WriteLine($"  Average Score: {comparisonResult.FirstSearchAverageScore:F2}");
            writer.WriteLine();
            writer.WriteLine($"Confirmatory Search (optimal fragment types only):");
            writer.WriteLine($"  PSMs at 1% FDR: {comparisonResult.SecondSearchPsmsAt1PercentFdr}");
            writer.WriteLine($"  Average Score: {comparisonResult.SecondSearchAverageScore:F2}");
            writer.WriteLine();
            writer.WriteLine($"Improvement: {comparisonResult.ImprovementInPsmCount} PSMs ({comparisonResult.PercentImprovement:F2}%)");
            writer.WriteLine();
            writer.WriteLine("Recommendation:");
            writer.WriteLine("===============");
            if (comparisonResult.ImprovementInPsmCount >= 0)
            {
                writer.WriteLine($"Using only the identified optimal fragment types maintains or improves search results.");
                writer.WriteLine($"Recommended fragment types for future searches: {string.Join(", ", optimalFragmentTypes)}");
            }
            else
            {
                writer.WriteLine($"Using reduced fragment types resulted in {Math.Abs(comparisonResult.ImprovementInPsmCount)} fewer PSMs.");
                writer.WriteLine($"Consider using the full set of fragment types or adjusting selection criteria.");
            }
        }

        FinishedWritingFile(resultsFile, new List<string> { "Fragment Type Detection" });
    }

    #endregion
}

public class FragmentTypeDetectionResult : MyTaskResults
{
    internal FragmentTypeDetectionResult(MetaMorpheusTask s) : base(s)
    {
    }
}
