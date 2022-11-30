﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using FlashLFQ;
using IO.MzML;
using MassSpectrometry;
using MzLibSpectralAveraging;
using Nett;
using UsefulProteomicsDatabases;

namespace TaskLayer
{
    public class SpectralAveragingTask : MetaMorpheusTask
    {
        public MzLibSpectralAveragingOptions Options { get; private set; }
        public const string AveragingSuffix = "-averaged";

        public SpectralAveragingTask(MzLibSpectralAveragingOptions options) : base(MyTask.Average)
        {
            Options = options;
        }

        /// <summary>
        /// Constructor should only be used when reading in toml files
        /// </summary>
        public SpectralAveragingTask() : base(MyTask.Average)
        {

        }

        protected override MyTaskResults RunSpecific(string OutputFolder, List<DbForTask> dbFilenameList, List<string> currentRawFileList, string taskId,
            FileSpecificParameters[] fileSettingsList)
        {

            // write prose settings
            ProseCreatedWhileRunning.Append("The following averaging settings were used: ");
            ProseCreatedWhileRunning.Append($"file averaging type = {Options.SpectraFileProcessingType}; ");
            ProseCreatedWhileRunning.Append($"rejection type = {Options.RejectionType}; ");
            ProseCreatedWhileRunning.Append($"weighting type = {Options.WeightingType}; ");
            ProseCreatedWhileRunning.Append($"perform normalization = {Options.PerformNormalization}; ");
            ProseCreatedWhileRunning.Append($"percentile = {Options.Percentile}; ");
            ProseCreatedWhileRunning.Append($"minimum sigma value = {Options.MinSigmaValue}; ");
            ProseCreatedWhileRunning.Append($"maximum sigma value = {Options.MaxSigmaValue}; ");
            ProseCreatedWhileRunning.Append($"bin size = {Options.BinSize}; ");
            ProseCreatedWhileRunning.Append($"number of scans to average = {(Options.SpectraFileProcessingType == SpectraFileProcessingType.AverageAll ? "All" : Options.NumberOfScansToAverage)}; " );
            ProseCreatedWhileRunning.Append($"scan overlap = {Options.ScanOverlap}; ");
            ProseCreatedWhileRunning.Append($"file output type = {Options.OutputType}; ");

            //Start Averaging Task
            Status("Averaging...", new List<string>() { taskId });
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

                // get filename stuff
                var originalUnaveragedFilepath = currentRawFileList[spectraFileIndex];
                var originalUnaveragedFilepathWithoutExtenstion = Path.GetFileNameWithoutExtension(originalUnaveragedFilepath);
                var averagedFilepath = Path.Combine(OutputFolder, originalUnaveragedFilepathWithoutExtenstion + AveragingSuffix + ".mzML");

                // mark file as in progress
                StartingDataFile(originalUnaveragedFilepath, new List<string>() {taskId, "Individual Spectra Files", originalUnaveragedFilepath});

                // load the file
                Status("Loading spectra file...", new List<string> { taskId, "Individual Spectra Files" });
                MsDataFile myMsdataFile = myFileManager.LoadFile(originalUnaveragedFilepath, CommonParameters);
                List<MsDataScan> scanList = myMsdataFile.GetAllScansList();

                // Average the spectra
                Status("Averaging spectra file...", new List<string> { taskId, "Individual Spectra Files" });
                var averagedScans = SpectraFileProcessing.ProcessSpectra(scanList, Options);
                if (averagedScans == null || !averagedScans.Any())
                {
                    Warn($"Averaging Failure! Could not average spectra for file {originalUnaveragedFilepathWithoutExtenstion}");
                    unsuccessfulyAveragedFilePaths.Add(Path.GetFileNameWithoutExtension(currentRawFileList[spectraFileIndex]));
                }

                // Output the spectra
                Status("Writing spectra file...", new List<string> { taskId, "Individual Spectra Files" });
                SourceFile sourceFile = SpectraFileHandler.GetSourceFile(originalUnaveragedFilepath);
                MsDataFile dataFile = new(averagedScans, sourceFile);
                MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(dataFile, averagedFilepath, true);

                myFileManager.DoneWithFile(originalUnaveragedFilepath);

                // carry over file-specific parameters from the uncalibrated file to the calibrated one
                var fileSpecificParams = new FileSpecificParameters();
                if (fileSettingsList[spectraFileIndex] != null)
                {
                    fileSpecificParams = fileSettingsList[spectraFileIndex].Clone();
                }

                // write toml settings for the calibrated file
                var newTomlFileName = Path.Combine(OutputFolder, originalUnaveragedFilepathWithoutExtenstion + AveragingSuffix + ".toml");
                Toml.WriteFile(fileSpecificParams, newTomlFileName, tomlConfig);
                FinishedWritingFile(newTomlFileName, new List<string> { taskId, "Individual Spectra Files", originalUnaveragedFilepathWithoutExtenstion });

                // finished averaging this file
                FinishedWritingFile(averagedFilepath, new List<string> { taskId, "Individual Spectra Files", originalUnaveragedFilepathWithoutExtenstion });
                MyTaskResults.NewSpectra.Add(averagedFilepath);
                MyTaskResults.NewFileSpecificTomls.Add(newTomlFileName);
                FinishedDataFile(originalUnaveragedFilepath, new List<string> { taskId, "Individual Spectra Files", originalUnaveragedFilepath });
                ReportProgress(new ProgressEventArgs(100, "Done!", new List<string> { taskId, "Individual Spectra Files", originalUnaveragedFilepathWithoutExtenstion }));
            }

            // re-write experimental design (if it has been defined) with new calibrated file names
            string assumedPathToExperDesign = Directory.GetParent(currentRawFileList.First()).FullName;
            assumedPathToExperDesign = Path.Combine(assumedPathToExperDesign, GlobalVariables.ExperimentalDesignFileName);
            if (File.Exists(assumedPathToExperDesign))
            {
                WriteNewExperimentalDesignFile(assumedPathToExperDesign, OutputFolder, currentRawFileList, unsuccessfulyAveragedFilePaths);
            }

            // finished calibrating all files for the task
            ReportProgress(new ProgressEventArgs(100, "Done!", new List<string> { taskId, "Individual Spectra Files" }));

            return MyTaskResults;
        }

        private static void WriteNewExperimentalDesignFile(string pathToOldExperDesign, string outputFolder, List<string> originalUnaveragedFileNamesWithExtension,
            List<string> unsuccessfullyAveragedFilePaths)
        {
            var oldExperDesign = ExperimentalDesign.ReadExperimentalDesign(pathToOldExperDesign, originalUnaveragedFileNamesWithExtension, out var errors);

            if (errors.Any())
            {
                foreach (var error in errors)
                {
                    Warn(error);
                }

                return;
            }

            var newExperDesign = new List<SpectraFileInfo>();

            foreach (var unaveragedSpectraFile in oldExperDesign)
            {
                var originalUnaveragedFilePath = unaveragedSpectraFile.FullFilePathWithExtension;
                var originalUnaveragedFilenameWithoutExtension = GlobalVariables.GetFilenameWithoutExtension(originalUnaveragedFilePath);
                string averagedFilePath = Path.Combine(outputFolder, originalUnaveragedFilenameWithoutExtension + AveragingSuffix + ".mzML");

                var averagedSpectraFile = new SpectraFileInfo(averagedFilePath,
                    unaveragedSpectraFile.Condition, unaveragedSpectraFile.BiologicalReplicate, unaveragedSpectraFile.TechnicalReplicate, unaveragedSpectraFile.Fraction);

                if (unsuccessfullyAveragedFilePaths.Contains(unaveragedSpectraFile.FullFilePathWithExtension))
                {
                    newExperDesign.Add(unaveragedSpectraFile);
                }
                else
                {
                    newExperDesign.Add(averagedSpectraFile);
                }
            }

            ExperimentalDesign.WriteExperimentalDesignToFile(newExperDesign);
        }
    }
}
