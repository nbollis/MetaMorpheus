using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using MassSpectrometry;

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

        protected override MyTaskResults RunSpecific(string OutputFolder, List<DbForTask> dbFilenameList, List<string> currentRawFileList, string taskId,
            FileSpecificParameters[] fileSettingsList)
        {

            // TODO: Load modifications

            // TODO: write prose settings

            // start search
            var myTaskResult = new MyTaskResults(this);
            List<OligoSpectralMatch> allOsms = new List<OligoSpectralMatch>();
            MyFileManager myFileManager = new MyFileManager(RnaSearchParameters.DisposeOfFileWhenDone);

            //TODO: file specific parameters


            Status("Searching files...", taskId);
            Status("Searching files...", new List<string> { taskId, "Individual Spectra Files" });

            Dictionary<string, int[]> numMs2SpectraPerFile = new Dictionary<string, int[]>();
            for (int spectraFileIndex = 0; spectraFileIndex < currentRawFileList.Count; spectraFileIndex++)
            {
                // variable setup
                string origDataFile = currentRawFileList[spectraFileIndex];

                StartingDataFile(origDataFile, new List<string> { taskId, "Individual Spectra Files", origDataFile });

                CommonParameters combinedParams = SetAllFileSpecificCommonParams(CommonParameters, fileSettingsList[spectraFileIndex]);

                MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(
                    combinedParams.PrecursorMassTolerance, RnaSearchParameters.MassDiffAcceptorType,
                    RnaSearchParameters.CustomMdac);

                var thisId = new List<string> { taskId, "Individual Spectra Files", origDataFile };
                Status("Loading spectra file...", thisId);
                MsDataFile myMsDataFile = myFileManager.LoadFile(origDataFile, combinedParams);
                Status("Getting ms2 scans...", thisId);

                Ms2ScanWithSpecificMass[] arrayOfMs2ScansSortedByMass = GetMs2Scans(myMsDataFile, origDataFile, combinedParams)
                        .OrderBy(b => b.PrecursorMass)
                        .ToArray();

                numMs2SpectraPerFile.Add(Path.GetFileNameWithoutExtension(origDataFile),
                    new int[]
                    {
                        myMsDataFile.GetAllScansList().Count(p => p.MsnOrder == 2), arrayOfMs2ScansSortedByMass.Length
                    });
                myFileManager.DoneWithFile(origDataFile);
                PeptideSpectralMatch[] fileSpecificPsms = new PeptideSpectralMatch[arrayOfMs2ScansSortedByMass.Length];

                // actually do the search
                for (int currentPartition = 0; currentPartition < combinedParams.TotalPartitions; currentPartition++)
                {

                }
            }






            throw new NotImplementedException();
        }
    }
}
