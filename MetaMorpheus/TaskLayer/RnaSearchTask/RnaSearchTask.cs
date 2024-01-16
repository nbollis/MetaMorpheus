using System.Collections.Generic;
using System.IO;
using System.Linq;
using EngineLayer;
using MassSpectrometry;
using Transcriptomics;

namespace TaskLayer
{
    public class RnaSearchTask : MetaMorpheusTask
    {
        public RnaSearchTask() : base(MyTask.RnaSearch)
        {
            CommonParameters = new CommonParameters();
            RnaSearchParameters = new RnaSearchParameters();
        }

        public RnaSearchParameters RnaSearchParameters { get; set; }

        protected override MyTaskResults RunSpecific(string OutputFolder, List<DbForTask> dbFilenameList,
            List<string> currentRawFileList, string taskId,
            FileSpecificParameters[] fileSettingsList)
        {
            LoadRnaModifications(taskId, out var variableModifications, out var fixedModifications,
                out var localizeableModificationTypes);
            List<RNA> rnas = LoadRNA(taskId, dbFilenameList, true, RnaSearchParameters.DecoyType,
                localizeableModificationTypes, CommonParameters, RnaSearchParameters);

            // TODO: write prose settings


            // start search
            MyTaskResults = new MyTaskResults(this);
            List<OligoSpectralMatch> allOsms = new List<OligoSpectralMatch>();
            MyFileManager myFileManager = new MyFileManager(RnaSearchParameters.DisposeOfFileWhenDone);

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
                    combinedParams.PrecursorMassTolerance, RnaSearchParameters.MassDiffAcceptorType,
                    RnaSearchParameters.CustomMdac);

                var thisId = new List<string> { taskId, "Individual Spectra Files", origDataFile };
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
                    massDiffAcceptor, RnaSearchParameters.DigestionParams, variableModifications,
                    fixedModifications, FileSpecificParameters, thisId);
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

            RnaPostSearchAnalysisParameters parameters = new()
            {
                SearchTaskResults = MyTaskResults,
                SearchTaskId = taskId,
                SearchParameters = RnaSearchParameters,
                RnaList = rnas,
                AllOsms = allOsms,
                FixedModifications = fixedModifications,
                VariableModifications = variableModifications,
                CurrentRawFileList = currentRawFileList,
                DatabaseFilenameList = dbFilenameList,
                FileSettingsList = fileSettingsList,
                NumMs2SpectraPerFile = numMs2SpectraPerFile,
                OutputFolder = OutputFolder,
                IndividualResultsOutputFolder = Path.Combine(OutputFolder, "Individual Spectra Files"),
                MyFileManager = myFileManager
            };
            RnaPostSearchAnalysisTask postSearchAnalysis = new()
            {
                Parameters = parameters,
                FileSpecificParameters = this.FileSpecificParameters,
                CommonParameters = this.CommonParameters,
                MassDiffAcceptorNumNotches = SearchTask.GetNumNotches(RnaSearchParameters.MassDiffAcceptorType, RnaSearchParameters.CustomMdac)
            };


            return postSearchAnalysis.Run();
        }
    }
}
