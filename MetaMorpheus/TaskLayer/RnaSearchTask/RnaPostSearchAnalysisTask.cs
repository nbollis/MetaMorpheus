using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using MassSpectrometry;
using Transcriptomics;

namespace TaskLayer
{
    public class RnaPostSearchAnalysisTask : MetaMorpheusTask
    {
        public RnaPostSearchAnalysisParameters Parameters { get; set; }
        internal IEnumerable<IGrouping<string, OligoSpectralMatch>> OsmsGroupedByFileName { get; private set; }
        internal List<OligoSpectralMatch> _filteredOsms;

        public RnaPostSearchAnalysisTask() : base(MyTask.RnaSearch)
        {
        }


        public MyTaskResults Run()
        {
            // Stop loop if canceled
            if (GlobalVariables.StopLoops) { return Parameters.SearchTaskResults; }

            Parameters.AllPsms = Parameters.AllPsms.Where(psm => psm != null).ToList();
            Parameters.AllPsms.ForEach(psm => psm.ResolveAllAmbiguities());
            Parameters.AllPsms = Parameters.AllPsms.OrderByDescending(b => b.Score)
                .ThenBy(b => b.OligoMonoisotopicMass.HasValue ? Math.Abs(b.ScanPrecursorMass - b.OligoMonoisotopicMass.Value) : double.MaxValue)
                .GroupBy(b => (b.FullFilePath, b.ScanNumber, b.OligoMonoisotopicMass)).Select(b => b.First()).ToList();

            // TODO: FDR
            //FilterAllOsms();

            // TODO: more analysis

            WriteOsmResults();


            return Parameters.SearchTaskResults;
        }


        protected override MyTaskResults RunSpecific(string OutputFolder, List<DbForTask> dbFilenameList, List<string> currentRawFileList, string taskId,
            FileSpecificParameters[] fileSettingsList)
        {
            throw new NotImplementedException();
        }

        private void WriteOsmResults()
        {
            Status("Writing OSM results...", Parameters.SearchTaskId);

            string writtenFile = Path.Combine(Parameters.OutputFolder, "AllOSMs.osmtsv");
            WriteOsmsToTsv(Parameters.AllPsms, writtenFile, null);
        }

        private void FilterAllOsms()
        {
            throw new NotImplementedException();
        }
    }

    public class RnaPostSearchAnalysisParameters
    {
        public MyTaskResults SearchTaskResults { get; set; }
        public string SearchTaskId { get; set; }
        public RnaSearchParameters SearchParameters { get; set; }
        public List<RNA> RnaList { get; set; }
        public List<Modification> VariableModifications { get; set; }
        public List<Modification> FixedModifications { get; set; }
        HashSet<RnaDigestionParams> ListOfDigestionParams { get; set; }
        public List<OligoSpectralMatch> AllPsms { get; set; }

        public string OutputFolder { get; set; }
        public string IndividualResultsOutputFolder { get; set; }
        public FileSpecificParameters[] FileSettingsList { get; set; }
        public Dictionary<string, int[]> NumMs2SpectraPerFile { get; set; }
        public MyFileManager MyFileManager { get; set; }
        public List<DbForTask> DatabaseFilenameList { get; set; }
        public List<string> CurrentRawFileList { get; set; }
    }
}
