using EngineLayer;
using Omics.Modifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Omics.Digestion;
using Transcriptomics.Digestion;
using Transcriptomics;

namespace TaskLayer
{
    public class RnaPostSearchAnalysisTask : MetaMorpheusTask
    {
        public RnaPostSearchAnalysisParameters Parameters { get; set; }
        internal IEnumerable<IGrouping<string, OligoSpectralMatch>> OsmsGroupedByFileName { get; private set; }
        internal List<OligoSpectralMatch> _filteredOsms;

        internal int MassDiffAcceptorNumNotches;
        private bool _pepFilteringNotPerformed;
        private string _filterType;
        private double _filterThreshold;


        public RnaPostSearchAnalysisTask() : base(MyTask.RnaSearch)
        {

        }

        protected override MyTaskResults RunSpecific(string OutputFolder, List<DbForTask> dbFilenameList, List<string> currentRawFileList, string taskId,
            FileSpecificParameters[] fileSettingsList)
        {
            throw new NotImplementedException();
        }

        public MyTaskResults Run()
        {
            // Stop loop if canceled
            if (GlobalVariables.StopLoops) { return Parameters.SearchTaskResults; }

            return null;
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
        public HashSet<IDigestionParams> ListOfDigestionParams { get; set; }
        public List<OligoSpectralMatch> AllOsms { get; set; }

        public string OutputFolder { get; set; }
        public string IndividualResultsOutputFolder { get; set; }
        public FileSpecificParameters[] FileSettingsList { get; set; }
        public Dictionary<string, int[]> NumMs2SpectraPerFile { get; set; }
        public MyFileManager MyFileManager { get; set; }
        public List<DbForTask> DatabaseFilenameList { get; set; }
        public List<string> CurrentRawFileList { get; set; }
    }
}
