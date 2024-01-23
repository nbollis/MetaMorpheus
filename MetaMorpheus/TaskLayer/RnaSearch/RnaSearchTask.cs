using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using Transcriptomics.Digestion;

namespace TaskLayer
{
    public class RnaSearchTask : MetaMorpheusTask
    {
        public RnaSearchTask() : base(MyTask.RnaSearch)
        {
            CommonParameters = new CommonParameters(digestionParams: new RnaDigestionParams());
            SearchParameters = new RnaSearchParameters();
        }

        public RnaSearchParameters SearchParameters { get; set; }

        protected override MyTaskResults RunSpecific(string OutputFolder, List<DbForTask> dbFilenameList, List<string> currentRawFileList, string taskId,
            FileSpecificParameters[] fileSettingsList)
        {
            throw new NotImplementedException();
        }
    }
}
