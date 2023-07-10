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
    public class DeconvolutionTask : MetaMorpheusTask
    {
        public DeconvolutionParameters DeconvolutionParameters { get; set; }
        public const string DeconvolutionSuffix = "-decon";

        public DeconvolutionTask(MyTask taskType, DeconvolutionParameters deconParameters) : base(taskType)
        {
            DeconvolutionParameters = deconParameters;
        }

        /// <summary>
        /// Constructor should only be used when reading in toml files
        /// </summary>
        public DeconvolutionTask() : base(MyTask.Deconvolution)
        {

        }

        protected override MyTaskResults RunSpecific(string OutputFolder, List<DbForTask> dbFilenameList, List<string> currentRawFileList, string taskId,
            FileSpecificParameters[] fileSettingsList)
        {
            // Initial Task Setup
            Status("Deconvoluting...", new List<string>() { taskId });
            var myFileManager = new MyFileManager(true);
            List<string> unsuccessfulyAveragedFilePaths = new();
            MyTaskResults = new MyTaskResults(this)
            {
                NewSpectra = new List<string>(),
                NewFileSpecificTomls = new List<string>(),
            };

            for (int spectraFileIndex = 0; spectraFileIndex < currentRawFileList.Count; spectraFileIndex++)
            {
                if (GlobalVariables.StopLoops) { break; }

                // general setup
                ReportProgress(new ProgressEventArgs((int)((spectraFileIndex / (double)currentRawFileList.Count) * 100),
                    $"Deconvoluting File {spectraFileIndex + 1}/{currentRawFileList.Count} ",
                    new List<string> { taskId, "Individual Spectra Files" }));
                var originalFilePath = currentRawFileList[spectraFileIndex];
                var originalFilePathWithoutExtension = Path.GetFileNameWithoutExtension(originalFilePath);
                var outputFilePath = Path.Combine(OutputFolder, originalFilePathWithoutExtension);
                Deconvoluter precursorDeconvoluter = new(DeconvolutionType.ClassicDeconvolution,
                    new ClassicDeconvolutionParameters(1, CommonParameters.DeconvolutionMaxAssumedChargeState,
                        CommonParameters.DeconvolutionMassTolerance.Value, CommonParameters.DeconvolutionIntensityRatio));

                // Mark file as in progress
                StartingDataFile(originalFilePath, new List<string>() { taskId, "Individual Spectra Files", originalFilePathWithoutExtension });

                // Load the file
                Status("Loading spectra file...", new List<string> { taskId, "Individual Spectra Files", originalFilePathWithoutExtension });
                MsDataFile myMsdataFile = myFileManager.LoadFile(originalFilePath, CommonParameters);

                // Perform Deconvolution
                foreach (var msDataScan in myMsdataFile.GetMS1Scans())
                {
                    // deconvolute each MS1Scan and add to new generic data file


                    // if msalign, also deconvolute each ms2 scan
                    if (true) // msAlign output
                    {

                    }

                    if (true) // mzmL output{
                    {

                    }



                }

                // Output the File


            }
            

            throw new NotImplementedException();
        }
    }
}
