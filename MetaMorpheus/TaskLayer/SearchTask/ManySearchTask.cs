#nullable enable
using EngineLayer;
using EngineLayer.ClassicSearch;
using EngineLayer.DatabaseLoading;
using EngineLayer.FdrAnalysis;
using EngineLayer.SpectrumMatch;
using FlashLFQ;
using Omics;
using Omics.Modifications;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ProteinGroup = EngineLayer.ProteinGroup;

namespace TaskLayer;
public class ManySearchTask : SearchTask
{
    public ManySearchTask() : base()
    {
        // Initialize with appropriate defaults
        SearchParameters = new ManySearchParameters();
    }

    public override SearchParameters SearchParameters { get; set; }

    protected override MyTaskResults RunSpecific(string OutputFolder,
        List<DbForTask> dbFilenameList, List<string> currentRawFileList,
        string taskId, FileSpecificParameters[] fileSettingsList)
    {
        var manySearchParameters = (ManySearchParameters)SearchParameters;
        MyTaskResults = new MyTaskResults(this);
        MyFileManager myFileManager = new MyFileManager(SearchParameters.DisposeOfFileWhenDone);

        // 1. Load modifications once
        LoadModifications(taskId, out var variableModifications,
            out var fixedModifications, out var localizableModificationTypes);

        // 2. Load base database(s) once
        var baseDbList = manySearchParameters.BaseDatabase;
        var baseDbLoader = new DatabaseLoadingEngine(CommonParameters,
            FileSpecificParameters, [taskId], baseDbList, taskId,
            SearchParameters.DecoyType, SearchParameters.SearchTarget,
            localizableModificationTypes);
        var baseProteins = (baseDbLoader.Run() as DatabaseLoadingEngineResults).BioPolymers;

        // 3. Load all spectra files once and store in memory
        Dictionary<string, Ms2ScanWithSpecificMass[]> loadedSpectraByFile = new();
        foreach (var rawFile in currentRawFileList)
        {
            var fileParams = SetAllFileSpecificCommonParams(CommonParameters,
                fileSettingsList[currentRawFileList.IndexOf(rawFile)]);
            var msDataFile = myFileManager.LoadFile(rawFile, fileParams);
            var ms2Scans = GetMs2Scans(msDataFile, rawFile, fileParams)
                .OrderBy(b => b.PrecursorMass).ToArray();
            loadedSpectraByFile[rawFile] = ms2Scans;
            myFileManager.DoneWithFile(rawFile);
        }

        // 4. Loop through each transient database
        Parallel.ForEach(manySearchParameters.TransientDatabases,
            new ParallelOptions { MaxDegreeOfParallelism = manySearchParameters.MaxSearchesInParallel },
            transientDbPath =>
        {
            string dbName = Path.GetFileNameWithoutExtension(transientDbPath.FilePath);
            string dbOutputFolder = Path.Combine(OutputFolder, dbName);
            List<string> nestedIds = [taskId, dbName];

            // 4a. Check if output already exists and is complete
            if (Directory.Exists(dbOutputFolder))
            {
                bool isComplete = TransientDbOutputIsFinished(OutputFolder, dbName, nestedIds);

                if (isComplete)
                {
                    if (manySearchParameters.OverwriteTransientSearchOutputs)
                    {
                        Directory.Delete(dbOutputFolder, true);
                        Directory.CreateDirectory(dbOutputFolder);
                    }
                    else
                    {
                        return; // Skip to next transient database
                    }
                }
            }
            else
                Directory.CreateDirectory(dbOutputFolder);

            // 4a. Load transient database
            var transientDbList = new List<DbForTask> { transientDbPath };
            var transientDbLoader = new DatabaseLoadingEngine(CommonParameters,
                FileSpecificParameters, nestedIds, transientDbList, taskId,
                SearchParameters.DecoyType, SearchParameters.SearchTarget,
                localizableModificationTypes);
            var transientProteins = (transientDbLoader.Run() as DatabaseLoadingEngineResults)!.BioPolymers;

            // 4b. Combine base + transient proteins
            var combinedProteins = new List<IBioPolymer>(baseProteins);
            combinedProteins.AddRange(transientProteins);

            // 4c. Search each spectra file with combined database
            List<SpectralMatch> allPsmsForThisDb = new();

            foreach (var rawFile in currentRawFileList)
            {
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
                    FileSpecificParameters, null, nestedIds, // no spectral library for this use case
                    false, false);

                searchEngine.Run();
                allPsmsForThisDb.AddRange(fileSpecificPsms);
            }

            // 4d. Perform post-search analysis for this database
            PerformPostSearchAnalysis(allPsmsForThisDb, dbOutputFolder, nestedIds);

            // 4e. Cleanup transient proteins to free memory
            transientProteins.Clear();
            transientProteins = null;
            //GC.Collect(); // Optional: force garbage collection
        });

        return MyTaskResults;
    }

    private void PerformPostSearchAnalysis(List<SpectralMatch> allPsms, string outputFolder, List<string> nestedIds)
    {
        // Filter PSMs to keep only best per (file, scan, mass)
        allPsms = allPsms.Where(p => p is not null)
            .Select(p => {
                p.ResolveAllAmbiguities(); 
                return p;
            }).OrderByDescending(b => b)
            .GroupBy(b => (b.FullFilePath, b.ScanNumber, b.BioPolymerWithSetModsMonoisotopicMass)).Select(b => b.First()).ToList();

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
            var psmForParsimony = FilteredPsms.Filter(allPsms,
                commonParams: CommonParameters,
                includeDecoys: true,
                includeContaminants: true,
                includeAmbiguous: false,
                includeHighQValuePsms: false);

            ProteinParsimonyResults proteinAnalysisResults = (ProteinParsimonyResults)new ProteinParsimonyEngine(psmForParsimony.FilteredPsmsList, SearchParameters.ModPeptidesAreDifferent, CommonParameters, FileSpecificParameters, nestedIds).Run();

            ProteinScoringAndFdrResults proteinScoringAndFdrResults = (ProteinScoringAndFdrResults)new ProteinScoringAndFdrEngine(proteinAnalysisResults.ProteinGroups, psmForParsimony.FilteredPsmsList,
                SearchParameters.NoOneHitWonders, SearchParameters.ModPeptidesAreDifferent, true, CommonParameters, FileSpecificParameters, nestedIds).Run();
            proteinGroups = proteinScoringAndFdrResults.SortedAndScoredProteinGroups;
        }


        // Filter PSMs for writing to file
        var psmsForPsmResults = FilteredPsms.Filter(allPsms,
            CommonParameters,
            includeDecoys: SearchParameters.WriteDecoys,
            includeContaminants: SearchParameters.WriteContaminants,
            includeAmbiguous: true,
            includeHighQValuePsms: SearchParameters.WriteHighQValuePsms);

        // Write PSMs to file
        string psmFile = Path.Combine(outputFolder, $"All{GlobalVariables.AnalyteType.GetSpectralMatchLabel()}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        WritePsmsToTsv(psmsForPsmResults.OrderByDescending(p => p), psmFile, SearchParameters.ModsToWriteSelection, false);

        // Filter PSMs for peptide results
        var peptidesForPeptideResults = FilteredPsms.Filter(allPsms,
            CommonParameters,
            includeDecoys: SearchParameters.WriteDecoys,
            includeContaminants: SearchParameters.WriteContaminants,
            includeAmbiguous: true,
            includeHighQValuePsms: SearchParameters.WriteHighQValuePsms,
            filterAtPeptideLevel: true);

        // Write peptides to file
        string peptideFile = Path.Combine(outputFolder, $"All{GlobalVariables.AnalyteType}s.{GlobalVariables.AnalyteType.GetSpectralMatchExtension()}");
        WritePsmsToTsv(peptidesForPeptideResults, peptideFile, SearchParameters.ModsToWriteSelection, true);

        if (proteinGroups is not null)
        {
            proteinGroups.ForEach(x => x.GetIdentifiedPeptidesOutput(SearchParameters.SilacLabels));

            // Write protein groups to file
            string proteinFile = Path.Combine(outputFolder, $"All{GlobalVariables.AnalyteType.GetBioPolymerLabel()}Groups.tsv");
            WriteProteinGroupsToTsv(proteinGroups, proteinFile, nestedIds);
            FinishedWritingFile(proteinFile, nestedIds);
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
        return true;
    }
}