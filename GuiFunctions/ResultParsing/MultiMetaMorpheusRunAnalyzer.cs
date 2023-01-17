using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using TaskLayer;

namespace GuiFunctions
{
    public class MultiMetaMorpheusRunAnalyzer
    {
        #region Private Properties



        #endregion

        #region Public Properties

        public List<MetaMorpheusRun> Runs { protected get; set; }

        #region Calculated Results

        public Dictionary<int, int> ChimericPsmIdsPerMs2Scan
        {
            get
            {
                var chimeraDict = new Dictionary<int, int>();
                var searchTasks = GetAllTasksOfTypeT(MyTask.Search).Select(p => (SearchTaskResult)p);
                var maxChimeraPerMs2 = searchTasks.Select(p => p.MaxPsmChimerasFromOneSpectra).Max();

                for (int i = 0; i < maxChimeraPerMs2; i++)
                {
                    foreach (var task in searchTasks)
                    {
                        if (!task.ChimericPsmIdsPerMs2Scan.ContainsKey(i)) continue;
                        if (!chimeraDict.TryAdd(i, task.ChimericPsmIdsPerMs2Scan[i]))
                        {
                            chimeraDict[i] += task.ChimericPsmIdsPerMs2Scan[i];
                        }
                    }
                }
                return chimeraDict;
            }
        }

        public Dictionary<int, int> ChimericProteoformIdsPerMs2Scan
        {
            get
            {
                var chimeraDict = new Dictionary<int, int>();
                var searchTasks = GetAllTasksOfTypeT(MyTask.Search).Select(p => (SearchTaskResult)p);
                var maxChimeraPerMs2 = searchTasks.Select(p => p.MaxProteoformChimerasFromOneSpectra).Max();

                for (int i = 0; i < maxChimeraPerMs2; i++)
                {
                    foreach (var task in searchTasks)
                    {
                        if (!task.ChimericProteoformIdsPerMs2Scan.ContainsKey(i)) continue;
                        if (!chimeraDict.TryAdd(i, task.ChimericProteoformIdsPerMs2Scan[i]))
                        {
                            chimeraDict[i] += task.ChimericProteoformIdsPerMs2Scan[i];
                        }
                    }
                }
                return chimeraDict;
            }
        }

        public Dictionary<string, int> AmbiguityPsmCountDictionary;
        public Dictionary<string, int> AmbiguityProteoformCountDictionary;

        #endregion

        #endregion

        #region Constructor

        public MultiMetaMorpheusRunAnalyzer()
        {
            Runs = new();
        }

        #endregion

        #region Processing Methods

        #region SearchResultProcessing

        public void PerformAllSearchResultProcessing()
        {
            PerformIndividualSearchResultProcessing();
            CompareSearchTasks();
        }

        public void PerformIndividualSearchResultProcessing()
        {
            PerformChimeraProcessing();
            PerformAmbiguityProcessing();
        }

        public void PerformChimeraProcessing()
        {
            foreach (SearchTaskResult searchTask in GetAllTasksOfTypeT(MyTask.Search))
            {
                searchTask.PerformChimeraProcessing();
            }
        }

        public void PerformAmbiguityProcessing()
        {
            foreach (SearchTaskResult searchTask in GetAllTasksOfTypeT(MyTask.Search))
            {
                searchTask.PerformAmbiguityProcessing();
            }
        }

        public void CompareSearchTasks()
        {
            var searchTasks = GetAllTasksOfTypeT(MyTask.Search).Select(p => (SearchTaskResult)p).ToList();
            foreach (var outerSearchTask in searchTasks)
            {
                IEnumerable<PsmFromTsv> distinctProteoforms = outerSearchTask.AllFilteredProteoforms;
                IEnumerable<PsmFromTsv> distinctProteins = outerSearchTask.AllFilteredProteoforms;
                IEnumerable<PsmFromTsv> distinctPsms = null;
                if (ResultAnalysisVariables.CalculateForPsms)
                {
                    distinctPsms = outerSearchTask.AllFilteredPsms;
                }

                foreach (var innerSearch in searchTasks.Where(p => p.Name != outerSearchTask.Name))
                {
                    distinctProteoforms =
                        distinctProteoforms.ExceptBy(innerSearch.AllFilteredProteoforms.Select(p => p.FullSequence), p => p.FullSequence);
                    distinctProteins =
                        distinctProteins.ExceptBy(innerSearch.AllFilteredProteoforms.Select(p => p.ProteinAccession), p => p.ProteinAccession);
                    distinctPsms = distinctPsms?.ExceptBy(innerSearch.AllFilteredPsms.Select(p => p.FullSequence), p => p.FullSequence);
                }

                outerSearchTask.ComparativeSearchResults =
                    new ComparativeSearchResults(distinctPsms, distinctProteins, distinctProteoforms);
            }
        }

        #endregion

        #endregion

        #region Mutator and Helper Methods

        public void AddRun(MetaMorpheusRun run)
        {
            Runs.Add(run);
        }

        public void AddRuns(IEnumerable<MetaMorpheusRun> runs)
        {
            Runs.AddRange(runs);
        }

        public IEnumerable<TaskResults> GetAllTasksOfTypeT(MyTask taskType)
        {
            Type type = taskType.GetTaskType();
            var tasks = Runs.SelectMany(p => p.TaskResults
                .Where(m => m.Key == taskType)
                .Select(n => n.Value));

            return tasks;
        }

        #endregion

        #region Output Methods

        public void ExportAsTsv(string outPath)
        {
            Runs.ExportAsTsv(outPath);
        }

        #endregion
    }
}
