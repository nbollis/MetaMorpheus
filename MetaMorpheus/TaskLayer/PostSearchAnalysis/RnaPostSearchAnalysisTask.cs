using System;
using System.Collections.Generic;
using System.Linq;
using EngineLayer;

namespace TaskLayer
{
    public class RnaPostSearchAnalysisTask : PostSearchAnalysisTaskParent
    {
        public new RnaPostSearchAnalysisParameters Parameters
        {
            get => (RnaPostSearchAnalysisParameters)base.Parameters;
            set => base.Parameters = value;
        }

        internal int MassDiffAcceptorNumNotches;

        public RnaPostSearchAnalysisTask() : base(MyTask.RnaSearch)
        {
            SpectralMatchMoniker = "OSM";
        }

        protected override MyTaskResults RunSpecific(string OutputFolder, List<DbForTask> dbFilenameList, List<string> currentRawFileList, string taskId,
            FileSpecificParameters[] fileSettingsList)
        {
            MyTaskResults = new MyTaskResults(this);
            return null;
        }

        public MyTaskResults Run()
        {
            // Stop loop if canceled
            if (GlobalVariables.StopLoops) { return Parameters.SearchTaskResults; }

            Parameters.AllSpectralMatches = Parameters.AllSpectralMatches.Where(psm => psm != null).ToList();
            Parameters.AllSpectralMatches.ForEach(psm => psm.ResolveAllAmbiguities());
            Parameters.AllSpectralMatches = Parameters.AllSpectralMatches.OrderByDescending(b => b.Score)
                .ThenBy(b => b.BioPolymerWithSetModsMonoisotopicMass.HasValue ? Math.Abs(b.ScanPrecursorMass - b.BioPolymerWithSetModsMonoisotopicMass.Value) : double.MaxValue)
                .GroupBy(b => (b.FullFilePath, b.ScanNumber, b.BioPolymerWithSetModsMonoisotopicMass)).Select(b => b.First()).ToList();

            CalculateSpectralMatchFdr();
            FilterAllSpectralMatches();
            DoMassDifferenceLocalizationAnalysis();

            //ProteinAnalysis();
            //QuantificationAnalysis();

            ReportProgress(new ProgressEventArgs(100, "Done!", new List<string> { Parameters.SearchTaskId, "Individual Spectra Files" }));


            HistogramAnalysis();

            WriteSpectralMatchResults();
            WritePeptideResults();
            CompressIndividualFileResults();

            return Parameters.SearchTaskResults;
        }


        #region Writing

        /// <summary>
        /// Writes PSMs to a .psmtsv file. If multiplex labeling was used (e.g., TMT), the intensities of the diagnostic ions are
        /// included, with each ion being reported in a separate column.
        /// </summary>
        /// <param name="psms">PSMs to be written</param>
        /// <param name="filePath">Full file path, up to and including the filename and extensioh. </param>
        internal override void WritePsmsToTsv(IEnumerable<SpectralMatch> psms, string filePath)
        {
            WritePsmsToTsv(psms, filePath, Parameters.SearchParameters.ModsToWriteSelection);
        }


        #endregion

    }
}
