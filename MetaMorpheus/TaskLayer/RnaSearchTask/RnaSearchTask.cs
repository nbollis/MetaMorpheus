using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using MassSpectrometry;
using Omics;
using Omics.Digestion;
using Transcriptomics;
using Transcriptomics.Digestion;

namespace TaskLayer
{
    public class RnaSearchTask : MetaMorpheusTask
    {
        public RnaSearchTask() : base(MyTask.RnaSearch)
        {
            CommonParameters = new CommonParameters(
                digestionParams: new RnaDigestionParams("RNase T1"),
                listOfModsVariable: new List<(string, string)>(),
                listOfModsFixed: new List<(string, string)>(),
                deconvolutionMaxAssumedChargeState: -20
            );
            SearchParameters = new RnaSearchParameters()
            {
                CustomMdac = "Custom interval [-5,5]",
                MassDiffAcceptorType = MassDiffAcceptorType.Custom,
            };

        }

        public RnaSearchParameters SearchParameters { get; set; }

        protected override MyTaskResults RunSpecific(string OutputFolder, List<DbForTask> dbFilenameList, List<string> currentRawFileList, string taskId,
            FileSpecificParameters[] fileSettingsList)
        {
            LoadModifications(taskId, out var variableModifications, out var fixedModifications,
                out var localizeableModificationTypes);
            List<RNA> rnas = LoadBioPolymers(taskId, dbFilenameList, true, SearchParameters.DecoyType,
                localizeableModificationTypes, CommonParameters).Cast<RNA>().ToList();

            // TODO: write prose settings


            // start search
            MyTaskResults = new MyTaskResults(this);
            List<OligoSpectralMatch> allOsms = new List<OligoSpectralMatch>();
            MyFileManager myFileManager = new MyFileManager(SearchParameters.DisposeOfFileWhenDone);

            //TODO: file specific parameters


            Status("Searching files...", taskId);
            Status("Searching files...", new List<string> { taskId, "Individual Spectra Files" });

            int completedFiles = 0;
            object osmLock = new object();
            Dictionary<string, int[]> numMs2SpectraPerFile = new Dictionary<string, int[]>();
            for (int spectraFileIndex = 0; spectraFileIndex < currentRawFileList.Count; spectraFileIndex++)
            {
                if (GlobalVariables.StopLoops)
                {
                    break;
                }

                // variable setup
                string origDataFile = currentRawFileList[spectraFileIndex];

                StartingDataFile(origDataFile, new List<string> { taskId, "Individual Spectra Files", origDataFile });

                CommonParameters combinedParams =
                    SetAllFileSpecificCommonParams(CommonParameters, fileSettingsList[spectraFileIndex]);

                MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(
                    combinedParams.PrecursorMassTolerance, SearchParameters.MassDiffAcceptorType,
                    SearchParameters.CustomMdac);

                var thisId = new List<string> { taskId, "Individual Spectra Files", origDataFile };
                NewCollection(Path.GetFileName(origDataFile), thisId);
                Status("Loading spectra file...", thisId);
                MsDataFile myMsDataFile = myFileManager.LoadFile(origDataFile, combinedParams);
                Status("Getting ms2 scans...", thisId);

                Ms2ScanWithSpecificMass[] arrayOfMs2ScansSortedByMass =
                    GetMs2Scans(myMsDataFile, origDataFile, combinedParams)
                        .OrderBy(b => b.PrecursorMass)
                        .ToArray();

                numMs2SpectraPerFile.Add(Path.GetFileNameWithoutExtension(origDataFile),
                    new int[]
                    {
                        myMsDataFile.GetAllScansList().Count(p => p.MsnOrder == 2), arrayOfMs2ScansSortedByMass.Length
                    });
                myFileManager.DoneWithFile(origDataFile);
                OligoSpectralMatch[] fileSpecificOsms = new OligoSpectralMatch[arrayOfMs2ScansSortedByMass.Length];

                // actually do the search
                Status("Starting Search...", thisId);
                var engine = new RnaSearchEngine(fileSpecificOsms, rnas, arrayOfMs2ScansSortedByMass, combinedParams,
                    massDiffAcceptor, variableModifications, fixedModifications, FileSpecificParameters, thisId);
                engine.Run();

                lock (osmLock)
                {
                    allOsms.AddRange(fileSpecificOsms);
                }

                completedFiles++;
                FinishedDataFile(origDataFile, new List<string> { taskId, "Individual Spectra Files", origDataFile });
                ReportProgress(new ProgressEventArgs(completedFiles / currentRawFileList.Count, "Searching...",
                    new List<string> { taskId, "Individual Spectra Files" }));
            }

            ReportProgress(new ProgressEventArgs(100, "Done with all searches!",
                new List<string> { taskId, "Individual Spectra Files" }));

            int numNotches = SearchTask.GetNumNotches(SearchParameters.MassDiffAcceptorType, SearchParameters.CustomMdac);
            PostSearchAnalysisParameters parameters = new()
            {
                SearchTaskResults = MyTaskResults,
                SearchTaskId = taskId,
                SearchParameters = SearchParameters,
                BioPolymerList = rnas.Cast<IBioPolymer>().ToList(),
                NumNotches = numNotches,
                AllSpectralMatches = allOsms.Cast<SpectralMatch>().ToList(),
                FixedModifications = fixedModifications,
                VariableModifications = variableModifications,
                ListOfDigestionParams = new HashSet<IDigestionParams> {CommonParameters.DigestionParams}, // TODO: File specific params
                CurrentRawFileList = currentRawFileList,
                DatabaseFilenameList = dbFilenameList,
                FileSettingsList = fileSettingsList,
                NumMs2SpectraPerFile = numMs2SpectraPerFile,
                OutputFolder = OutputFolder,
                IndividualResultsOutputFolder = Path.Combine(OutputFolder, "Individual Spectra Files"),
                MyFileManager = myFileManager,
                MassDiffAcceptor = SearchTask.GetMassDiffAcceptor(
                    CommonParameters.PrecursorMassTolerance, SearchParameters.MassDiffAcceptorType,
                    SearchParameters.CustomMdac)
            };

            PostSearchAnalysisTask postSearchAnalysis = new()
            {
                Parameters = parameters,
                FileSpecificParameters = this.FileSpecificParameters,
                CommonParameters = this.CommonParameters
            };


            return postSearchAnalysis.Run();
        }
    }
}
