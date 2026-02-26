using EngineLayer;
using EngineLayer.ClassicSearch;
using EngineLayer.DatabaseLoading;
using EngineLayer.FdrAnalysis;
using EngineLayer.FragmentTypeDetection;
using EngineLayer.SpectrumMatch;
using MassSpectrometry;
using Nett;
using Omics;
using Omics.Fragmentation;
using Omics.Modifications;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TaskLayer.FragmentTypeDetection;

public class FragmentTypeDetectionResult : MyTaskResults
{
    internal FragmentTypeDetectionResult(MetaMorpheusTask s) : base(s)
    {
        NewFileSpecificTomls = new List<string>();
    }
}

public class FragmentTypeDetectionTask : SearchTask
{
    public FragmentTypeDetectionTask() : base(MyTask.FragmentDetection)
    {
        SearchParameters = new FragmentationDetectionParameters();
        CommonParameters = new("FragmentTypeDetectionTask", DissociationType.Custom, qValueThreshold: 0.05);
    }

    [TomlIgnore]
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

    private string _taskId;
    private List<ProductType> _typesToEvaluate;
    private List<IBioPolymer> _bioPolymerList;
    private List<Modification> _variableModifications;
    private List<Modification> _fixedModifications;
    private MyFileManager _myFileManager;

    private static readonly int NumRequiredPsms = 16;

    protected override MyTaskResults RunSpecific(
        string OutputFolder, 
        List<DbForTask> dbFilenameList, 
        List<string> currentRawFileList, 
        string taskId,
        FileSpecificParameters[] fileSettingsList)
    {
        Initialize(taskId, dbFilenameList);

        Status("Running comprehensive fragment type detection...", new List<string> { taskId });
        for (int spectraFileIndex = 0; spectraFileIndex < currentRawFileList.Count; spectraFileIndex++)
        {
            if (GlobalVariables.StopLoops) { break; }

            var origDataFile = currentRawFileList[spectraFileIndex];
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(origDataFile);
            var thisId = new List<string> { _taskId, "Individual Spectra Files", fileNameWithoutExtension };

            // Mark file as in-progress
            StartingDataFile(origDataFile, thisId);
            NewCollection(Path.GetFileName(origDataFile), thisId);

            // carry over file-specific parameters if present and update combined params
            FileSpecificParameters fileSpecificParams = fileSettingsList[spectraFileIndex] == null
                ? new()
                : fileSettingsList[spectraFileIndex].Clone();
            CommonParameters combinedParams = SetAllFileSpecificCommonParams(CommonParameters, fileSpecificParams);


            Status("Loading spectra file...", thisId);
            MsDataFile myMsDataFile = _myFileManager.LoadFile(origDataFile, combinedParams);

            Status("Getting ms2 scans...", thisId);
            Ms2ScanWithSpecificMass[] arrayOfMs2ScansSortedByMass = GetMs2Scans(myMsDataFile, origDataFile, combinedParams)
                .OrderBy(b => b.PrecursorMass)
                .ToArray();

            _myFileManager.DoneWithFile(origDataFile);

            var allPsms = SearchAndExtractPsms(arrayOfMs2ScansSortedByMass, 
                combinedParams, 
                origDataFile, 
                _typesToEvaluate);

            var filtered = FilteredPsms.Filter(allPsms, combinedParams, includeDecoys: false, includeContaminants: false, includeAmbiguous: true, includeAmbiguousMods: true);

            if (filtered.TargetPsmsAboveThreshold < NumRequiredPsms)
            {
                Warn($"Fragment Type Detection failure! Could not find enough high-quality {GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s. Required " + NumRequiredPsms + ", saw " + filtered.TargetPsmsAboveThreshold + $" for data file {fileNameWithoutExtension}");
                continue;
            }

            //WriteFirstSearchResults(OutputFolder, allPsms, fileNameWithoutExtension);
            List<ProductType> newSetToTry = FragmentationDetectionParameters.FragmentDetectionStrategy.DetermineOptimalFragmentTypes(allPsms, combinedParams);

            //var secondFileSpecificResults = SearchAndExtractPsms(arrayOfMs2ScansSortedByMass,
            //    combinedParams,
            //    origDataFile,
            //    newSetToTry);

            //if (secondFileSpecificResults.ConfidentPsms < NumRequiredPsms)
            //{
            //    // TODO: Handle this case, should likely never occur

            //    Warn($"Fragment Type Detection failure on second pass! Could not find enough high-quality {GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s. Required " + NumRequiredPsms + ", saw " + secondFileSpecificResults.ConfidentPsms + $" for data file {fileNameWithoutExtension}");
            //    continue;
            //}

            //if (secondFileSpecificResults.ConfidentPsms < allPsms.ConfidentPsms)
            //{
            //    Warn($"Fragment Type Detection failure on second pass! Saw fewer high-quality {GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s than the first pass. First pass: " + allPsms.ConfidentPsms + ", second pass: " + secondFileSpecificResults.ConfidentPsms + $" for data file {fileNameWithoutExtension}");
            //    continue;
            //}

            // Update file-specific parameters
            UpdateFileSpecificToml(fileSpecificParams, 
                newSetToTry, 
                taskId,
                origDataFile);

            FinishedDataFile(origDataFile, thisId);
        }

        Status("Fragment type detection complete!", new List<string> { taskId });

        return MyTaskResults;
    }

    private void Initialize(string taskId, List<DbForTask> dbFilenameList)
    {
        // Initialize task
        MyTaskResults = new FragmentTypeDetectionResult(this);
        _myFileManager = new MyFileManager(SearchParameters.DisposeOfFileWhenDone);
        _taskId = taskId;
        LoadModifications(_taskId, out _variableModifications, out _fixedModifications, out var localizeableModificationTypes);

        var dbLoader = new DatabaseLoadingEngine(
            CommonParameters,
            FileSpecificParameters,
            [taskId],
            dbFilenameList,
            taskId,
            SearchParameters.DecoyType,
            SearchParameters.SearchTarget,
            localizeableModificationTypes);
        var loadingResults = dbLoader.Run() as DatabaseLoadingEngineResults;
        _bioPolymerList = loadingResults!.BioPolymers;

        // Get all fragment types to test
        _typesToEvaluate = FragmentationDetectionParameters.IonsToSearchFor.Count == 0
            ? GetFragmentTypesToTest()
            : FragmentationDetectionParameters.IonsToSearchFor;

        // Write prose settings
        WriteProse(_fixedModifications, _variableModifications);
    }


    #region Search Execution

    private List<SpectralMatch> SearchAndExtractPsms(Ms2ScanWithSpecificMass[] arrayOfMs2ScansSortedByMass, CommonParameters combinedParams, string originalDataFile, List<ProductType> productTypes)
    {
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalDataFile);
        List<string> nestedIDs = [_taskId, "Individual Spectra Files", fileNameWithoutExtension];

        // Create search mode
        MassDiffAcceptor searchMode = GetMassDiffAcceptor(
            combinedParams.PrecursorMassTolerance,
            SearchParameters.MassDiffAcceptorType,
            SearchParameters.CustomMdac);

        // Set up custom ions
        combinedParams.CustomIons.Clear();
        foreach (var fragType in productTypes)
            combinedParams.CustomIons.Add(fragType);
        combinedParams.SetCustomProductTypes();

        // Run search using the engine
        SpectralMatch[] fileSpecificPsms = new SpectralMatch[arrayOfMs2ScansSortedByMass.Length];
        _ = new ClassicSearchEngine(fileSpecificPsms,
            arrayOfMs2ScansSortedByMass, _variableModifications, _fixedModifications, null, null, null, _bioPolymerList, searchMode, combinedParams, FileSpecificParameters, null, nestedIDs, false).Run();

        // Collect PSMs and run file specific FDR analysis
        var psms = fileSpecificPsms.Where(p => p != null).ToList();
        _ = new FdrAnalysisEngine(psms, searchMode.NumNotches, combinedParams, FileSpecificParameters, nestedIDs, "PSM", false).Run();

        return psms;
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

   


    #endregion

    #region Output Methods

    private void UpdateFileSpecificToml(FileSpecificParameters fileParams, List<ProductType> productTypesToUse, string taskId, string originalDataFile)
    {
        string mzFilenameNoExtension = Path.GetFileNameWithoutExtension(originalDataFile);
        string originalDataFileDirectory = Path.GetDirectoryName(originalDataFile) ?? OutputFolder;
        string tomlName = Path.Combine(originalDataFileDirectory, mzFilenameNoExtension + ".toml");

        // Get Dictionary fron analyte type
        Dictionary<DissociationType, List<ProductType>> dissociationTypeDictionary = CommonParameters.DigestionParams.ProductsFromDissociationType();

        // Check to see if the products to use are already in the dictionary with a defined dissociation type
        DissociationType? assignedDissociationType = null;
        foreach (var kvp in dissociationTypeDictionary)
        {
            var dissociationType = kvp.Key;
            var productTypes = kvp.Value;
            if (productTypes.Count != productTypesToUse.Count || productTypes.Except(productTypesToUse).Any()) 
                continue;

            assignedDissociationType = dissociationType;
            break;
        }

        // Update file specific parameters
        if (assignedDissociationType.HasValue && assignedDissociationType.Value != DissociationType.Custom)
            fileParams.DissociationType = assignedDissociationType.Value;
        else
        {
            // Use custom dissociation type
            fileParams.DissociationType = DissociationType.Custom;
            fileParams.CustomIons = new List<ProductType>(productTypesToUse);
        }

        // Write file-specific toml
        Toml.WriteFile(fileParams, tomlName, tomlConfig);
        FinishedWritingFile(tomlName, new List<string> { taskId, "Individual Spectra Files", mzFilenameNoExtension });
        MyTaskResults.NewFileSpecificTomls.Add(tomlName);
    }

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

    //private void WriteFirstSearchResults(string outputFolder, FragmentTypeAnalysisEngineResults analysisResult, string fileNameWithoutExtension)
    //{
    //    string indResultDir = Path.Combine(outputFolder, "Individual File Results");
    //    if (!Directory.Exists(indResultDir))
    //        Directory.CreateDirectory(indResultDir);

    //    string resultsFile = Path.Combine(indResultDir, $"{fileNameWithoutExtension}_InitialSearch.txt");

    //    using (StreamWriter writer = new StreamWriter(resultsFile))
    //    {
    //        writer.WriteLine("Fragment Type Detection - Comprehensive Search Results");
    //        writer.WriteLine("=======================================================");
    //        writer.WriteLine();
    //        writer.WriteLine($"Total PSMs: {analysisResult.TotalPsms}");
    //        writer.WriteLine($"PSMs at 1% FDR: {analysisResult.ConfidentPsms}");
    //        writer.WriteLine($"Average Score: {analysisResult.AverageScore:F2}");
    //        writer.WriteLine();
    //        writer.WriteLine("Individual Fragment Type Performance:");
    //        writer.WriteLine("=====================================");
    //        writer.WriteLine();
    //        writer.WriteLine("Fragment Type\tPSMs with Matches\t% of PSMs\tAvg Matches When Present");

    //        foreach (var stat in analysisResult.FragmentTypeStatistics.OrderByDescending(kvp => kvp.Value.PercentOfPsmsWithMatches))
    //        {
    //            writer.WriteLine($"{stat.Key}\t{stat.Value.PsmsWithMatches}\t{stat.Value.PercentOfPsmsWithMatches:F2}%\t{stat.Value.AverageMatchesWhenPresent:F2}");
    //        }
    //    }

    //    FinishedWritingFile(resultsFile, [_taskId, fileNameWithoutExtension]);

    //    var tsvPath = Path.Combine(indResultDir, $"{fileNameWithoutExtension}_FragmentTypeStatistics.tsv");
    //    new FragmentTypeStatisticsResultFile(tsvPath) 
    //    { 
    //        Results = analysisResult.FragmentTypeStatistics.Values.ToList() 
    //    }.WriteResults(tsvPath);
    //    FinishedWritingFile(tsvPath, [_taskId, fileNameWithoutExtension]);
    //}

    #endregion
}

