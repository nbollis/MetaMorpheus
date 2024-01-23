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
    public class RnaPostAnalysisSearchTask : MetaMorpheusTask
    {
        public RnaPostAnalysisSearchTask() : base(MyTask.RnaSearch)
        {

        }

        protected override MyTaskResults RunSpecific(string OutputFolder, List<DbForTask> dbFilenameList, List<string> currentRawFileList, string taskId,
            FileSpecificParameters[] fileSettingsList)
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
        HashSet<IDigestionParams> ListOfDigestionParams { get; set; }
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
