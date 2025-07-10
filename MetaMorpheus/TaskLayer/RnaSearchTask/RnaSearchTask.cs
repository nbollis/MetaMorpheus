using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using EngineLayer.ClassicSearch;
using MassSpectrometry;
using MzLibUtil;
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
                digestionParams: new RnaDigestionParams("RNase T1")
                {
                    MaxMissedCleavages = 3,
                    MaxMods = 4,
                    MaxModificationIsoforms = 4096
                },
                listOfModsVariable: new List<(string, string)>()
                {
                    ("Digestion Termini", "Cyclic Phosphate on X")
                },
                listOfModsFixed: new List<(string, string)>(),
                precursorMassTolerance: new PpmTolerance(15),
                scoreCutoff: 5,
                qValueThreshold: 0.05,
                addCompIons: false, 
                deconvolutionMaxAssumedChargeState: -1,
                precursorDeconParams: new ClassicDeconvolutionParameters(-20, -1, 4, 3, Polarity.Negative, new OxyriboAveragine()),
                productDeconParams: new ClassicDeconvolutionParameters(-12, -1, 4, 3, Polarity.Negative, new OxyriboAveragine())

            );
            SearchParameters = new RnaSearchParameters()
            {
                CustomMdac = "Na:3:21.981943,K:2:37.955882;3",
                MassDiffAcceptorType = MassDiffAcceptorType.Adduct,
            };

        }

        public RnaSearchParameters SearchParameters { get; set; }

        protected override MyTaskResults RunSpecific(string OutputFolder, List<DbForTask> dbFilenameList, List<string> currentRawFileList, string taskId,
            FileSpecificParameters[] fileSettingsList)
        {
            LoadModifications(taskId, out var variableModifications, out var fixedModifications,
                out var localizeableModificationTypes);
            List<IBioPolymer> rnas = LoadBioPolymers(taskId, dbFilenameList, true, SearchParameters.DecoyType,
                localizeableModificationTypes, CommonParameters);

            // TODO: write prose settings


            // start search
            MyTaskResults = new MyTaskResults(this);
            List<SpectralMatch> allOsms = new List<SpectralMatch>();
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
                SpectralMatch[] fileSpecificOsms = new SpectralMatch[arrayOfMs2ScansSortedByMass.Length];

                // actually do the search
                Status("Starting Search...", thisId);
                //var engine = new RnaSearchEngine(fileSpecificOsms, rnas, arrayOfMs2ScansSortedByMass, combinedParams,
                //    massDiffAcceptor, variableModifications, fixedModifications, FileSpecificParameters, thisId);\
                var engine = new ClassicSearchEngine(fileSpecificOsms, arrayOfMs2ScansSortedByMass, variableModifications, fixedModifications, null, null, null, rnas, massDiffAcceptor, combinedParams, FileSpecificParameters, null, thisId, false);
                engine.Run();

                lock (osmLock)
                {
                    allOsms.AddRange(fileSpecificOsms.Where(p => p != null));
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
                BioPolymerList = rnas.ToList(),
                NumNotches = numNotches,
                AllSpectralMatches = allOsms.ToList(),
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
