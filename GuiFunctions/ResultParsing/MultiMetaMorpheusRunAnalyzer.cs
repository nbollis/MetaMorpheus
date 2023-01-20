using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using Easy.Common.Extensions;
using EngineLayer;
using Proteomics.ProteolyticDigestion;
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
                var searchTasks = GetAllTasksOfTypeT<SearchTaskResult>();
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
                var searchTasks = GetAllTasksOfTypeT<SearchTaskResult>();
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
        public Dictionary<string, TdBuMatchComparison> TdBuMatchComparisons;

        #endregion

        #endregion

        #region Constructor

        public MultiMetaMorpheusRunAnalyzer()
        {
            Runs = new();
            TdBuMatchComparisons = new();
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
            foreach (SearchTaskResult searchTask in GetAllTasksOfTypeT<SearchTaskResult>())
            {
                searchTask.PerformChimeraProcessing();
            }
        }

        public void PerformAmbiguityProcessing()
        {
            foreach (SearchTaskResult searchTask in GetAllTasksOfTypeT<SearchTaskResult>())
            {
                searchTask.PerformAmbiguityProcessing();
            }
        }

        public void CompareSearchTasks()
        {
            var searchTasks = GetAllTasksOfTypeT<SearchTaskResult>().ToList();
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

        public void CompareTdBu()
        {
            if (Runs.Count != 2)
                throw new ArgumentException("Cannot compare more than 2 runs with this method");
            SearchTaskResult tdResult;
            SearchTaskResult buResult;
            try
            {
                tdResult = GetAllTasksOfTypeT<SearchTaskResult>().First(p => p.Protease == "top-down");
                buResult = GetAllTasksOfTypeT<SearchTaskResult>().First(p => p.Protease != "top-down");
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Cannot find top-down and bottom up search results");
            }


            TdBuMatchComparisons.Add("AllProteoforms",
                GetTdBuMatchComparison(tdResult.AllProteoforms, buResult.AllProteoforms, "AllProteoforms"));
            TdBuMatchComparisons.Add("AllFilteredProteoforms",
                GetTdBuMatchComparison(tdResult.AllFilteredProteoforms, buResult.AllFilteredProteoforms, "AllFilteredProteoforms"));

            if (!ResultAnalysisVariables.CalculateForPsms) return;
            TdBuMatchComparisons.Add("AllPsms",
                GetTdBuMatchComparison(tdResult.AllPsms, buResult.AllPsms, "AllPsms"));
            TdBuMatchComparisons.Add("AllFilteredPsms",
                GetTdBuMatchComparison(tdResult.AllFilteredPsms, buResult.AllFilteredPsms, "AllFilteredPsms"));
        }

        /// <summary>
        /// To be utilized on https://sankeymatic.com/build/
        /// </summary>
        /// <returns></returns>
        public (string Proteoforms, string? Psms) TdBuComparisonToSankeyScripts()
        {
            if (!TdBuMatchComparisons.Any())
                CompareTdBu();

            string proteoforms = GetSankeyFromTwoTdBuComparisons(TdBuMatchComparisons["AllProteoforms"],
                TdBuMatchComparisons["AllFilteredProteoforms"], "Proteoforms");
            string psms = null;

            if (ResultAnalysisVariables.CalculateForPsms)
                psms = GetSankeyFromTwoTdBuComparisons(TdBuMatchComparisons["AllPsms"],
                    TdBuMatchComparisons["AllFilteredPsms"], "Psms");

            return (proteoforms, psms);
        }

        internal string GetSankeyFromTwoTdBuComparisons(TdBuMatchComparison allComparison,
            TdBuMatchComparison filteredComparison, string type)
        {
            string type2 = type.Equals("Proteoforms") ? "Peptides" : type;

            List<SankeyLine> lines = new List<SankeyLine>();
            lines.Add(new($"Top-Down {type}", $"Filtered Top-Down {type}", filteredComparison.TdCount));
            lines.Add(new($"Top-Down {type}", $"Q-Value > 0.01", allComparison.TdCount - filteredComparison.TdCount));
            lines.Add(new($"Bottom-Up {type2}", $"Filtered Bottom-Up {type2}", filteredComparison.BuCount));
            lines.Add(new($"Bottom-Up {type2}", $"Q-Value > 0.01", allComparison.BuCount - filteredComparison.BuCount));

            lines.Add(new($"Filtered Top-Down {type}", $"Unambiguous Top-Down {type}", filteredComparison.TdUnAmbiguousCount));
            lines.Add(new($"Filtered Top-Down {type}", $"Ambiguous Identifications", filteredComparison.TdCount - filteredComparison.TdUnAmbiguousCount));
            lines.Add(new($"Filtered Bottom-Up {type2}", $"Unambiguous Bottom-Up {type2}", filteredComparison.BuUnAmbiguousCount));
            lines.Add(new($"Filtered Bottom-Up {type2}", $"Ambiguous Identifications", filteredComparison.BuCount - filteredComparison.BuUnAmbiguousCount));

            var sum = filteredComparison.BuHitsInTopDown + filteredComparison.TdHitsInBottomUp;
            lines.Add(new($"Unambiguous Top-Down {type}", $"Accession In Both Searches",
                (int)(filteredComparison.BuHitsInTopDown * filteredComparison.TdHitsInBottomUp / sum)));
            lines.Add(new($"Unambiguous Top-Down {type}", $"Accession In One Search",
                filteredComparison.TdUnAmbiguousCount - filteredComparison.TdHitsInBottomUp));
            lines.Add(new($"Unambiguous Bottom-Up {type2}", $"Accession In Both Searches",
                (int)(filteredComparison.BuHitsInTopDown * filteredComparison.BuHitsInTopDown / sum )));
            lines.Add(new($"Unambiguous Bottom-Up {type2}", $"Accession In One Search", 
                filteredComparison.BuUnAmbiguousCount - filteredComparison.BuHitsInTopDown));

            lines.Add(new($"Accession In Both Searches", $"Matched Location and Modification", filteredComparison.MatchModAndLocation));
            lines.Add(new($"Accession In Both Searches", $"Matched Location Only", filteredComparison.MatchLocationOnly));
            lines.Add(new($"Accession In Both Searches", $"Matched Modification Only", filteredComparison.MatchModOnly));
            lines.Add(new($"Accession In Both Searches", $"Matched Nothing", filteredComparison.MatchNothing));

            var sb = new StringBuilder();
            lines.ForEach(p => sb.AppendLine(p.ToString()));

            return sb.ToString();
        }

        #endregion

        #endregion

        #region Accessors

        public void AddRun(MetaMorpheusRun run)
        {
            Runs.Add(run);
        }

        public void AddRuns(IEnumerable<MetaMorpheusRun> runs)
        {
            Runs.AddRange(runs);
        }

        public IEnumerable<T> GetAllTasksOfTypeT<T>() where T : TaskResults
        {
            var tasks = Runs.SelectMany(p => p.TaskResults
                .Where(m => m.Value is T)
                .Select(n => (T)n.Value));

            return tasks;
        }

        #endregion

        #region internal helpers

        internal TdBuMatchComparison GetTdBuMatchComparison(List<PsmFromTsv> tdPsms, List<PsmFromTsv> buPsms, string name)
        {
            // get unambiguous and group by accession
            var unambiguousTd = tdPsms.Where(p => p.AmbiguityLevel == "1").ToList();
            var unambiguousBu = buPsms.Where(p => p.AmbiguityLevel == "1").ToList();
            var buGroupedByAccessionWithTd = unambiguousBu.GroupJoin(unambiguousTd,
                bu => bu.ProteinAccession,
                td => td.ProteinAccession,
                (bu, tdGroups) => new
                {
                    BuPsm = (bu.FullSequence, bu.StartAndEndResiduesInProtein),
                    TdGroups = tdGroups.Select(p => (p.FullSequence, p.StartAndEndResiduesInProtein)).ToList()
                }).Where(p => p.TdGroups.Count > 0).ToList();

            var matchModAndLocation = 0;
            var matchLocationOnly = 0;
            var matchModOnly = 0;
            var matchNothing = 0;

            foreach (var group in buGroupedByAccessionWithTd)
            {
                var buMods = PsmFromTsv.ParseModifications(group.BuPsm.FullSequence);
                bool matchMod = false;
                bool matchLoc = false;
                int index = 0;
                while (index < group.TdGroups.Count & (!matchLoc && !matchMod))
                {
                    if (group.TdGroups[index].FullSequence.Contains(group.BuPsm.FullSequence))
                    {
                        matchLoc = true;
                        matchMod = true;
                        break;
                    }

                    var tdSequenceInRange = GetFullSequenceWithinAminoAcidRange(group.TdGroups[index].FullSequence,
                        group.TdGroups[index].StartAndEndResiduesInProtein, group.BuPsm.StartAndEndResiduesInProtein);
                    if (tdSequenceInRange.IsNullOrEmpty())
                    {
                        index++;
                        continue;
                    }

                    var tdMods = PsmFromTsv.ParseModifications(tdSequenceInRange);
                    var tdModsUnTrimmed = PsmFromTsv.ParseModifications(group.TdGroups[index].FullSequence);

                    if (buMods.Select(p => p.Key).SequenceEqual(tdMods.Select(p => p.Key)))
                        matchLoc = true;
                    if (buMods.SelectMany(p => p.Value).All(m => tdModsUnTrimmed.SelectMany(p => p.Value).Contains(m)))
                        matchMod = true;

                    index++;
                } 

                if (matchMod && matchLoc)
                    matchModAndLocation++;
                else if (matchMod)
                    matchModOnly++;
                else if (matchLoc)
                    matchLocationOnly++;
                else
                    matchNothing++;
            }

            var unambigTdInBuCount = unambiguousTd.GroupJoin(unambiguousBu,
                td => td.ProteinAccession,
                bu => bu.ProteinAccession,
                (bu, tdGroups) => new
                {
                    BuPsm = (bu.FullSequence, bu.StartAndEndResiduesInProtein),
                    TdGroups = tdGroups.Select(p => (p.FullSequence, p.StartAndEndResiduesInProtein)).ToList()
                }).Count(p => p.TdGroups.Count > 0);

            return new TdBuMatchComparison(name, tdPsms.Count, buPsms.Count, unambiguousTd.Count,
                unambiguousBu.Count(), unambigTdInBuCount, buGroupedByAccessionWithTd.Count(), matchModAndLocation, matchLocationOnly,
                matchModOnly, matchNothing);
        }

        internal string GetFullSequenceWithinAminoAcidRange(string fullSequence, string startAndEndOriginal, string startAndEndNew)
        {
            var newSplits = startAndEndNew.Substring(1, startAndEndNew.Length - 2).Split(" to ");
            (int Start, int End) newStartAndEndResidues = (int.Parse(newSplits[0]), int.Parse(newSplits[1]));

            var oldSplits = startAndEndOriginal.Substring(1, startAndEndOriginal.Length - 2).Split(" to ");
            (int Start, int End) originalStartAndEndResidues = (int.Parse(oldSplits[0]), int.Parse(oldSplits[1]));

            // if new indexes are outside that of old indexes
            if (originalStartAndEndResidues.End < newStartAndEndResidues.End ||
                originalStartAndEndResidues.Start > newStartAndEndResidues.Start)
                return "";

            var startIndex = newStartAndEndResidues.Start - originalStartAndEndResidues.Start;
            var endIndex = newStartAndEndResidues.End - originalStartAndEndResidues.Start + 1;
            var length = endIndex - startIndex;

            var pepWithSetMods = new PeptideWithSetModifications(fullSequence, GlobalVariables.AllModsKnownDictionary);
            var fullSeq = PeptideWithSetModifications.GetBaseSequenceFromFullSequence(fullSequence).Substring(startIndex, length);
            var modDictionary = pepWithSetMods.AllModsOneIsNterminus
                .Where(p => p.Key - 1 >= startIndex && p.Key - 1 < endIndex)
                .OrderByDescending(p => p.Key);

            foreach (var mod in modDictionary)
            {
                fullSeq = fullSeq.Insert(mod.Key - 1 - startIndex,
                    $"[{mod.Value.ModificationType}:{mod.Value.IdWithMotif}]");
            }
            return fullSeq;
        }

        #endregion

        #region Output Methods

        public void ExportAsTsv(string outPath)
        {
            Runs.ExportAsTsv(outPath);
        }

        public void ExportBuTdComparisonToTsv(string outPath)
        {
            TdBuMatchComparisons.Select(p => (ITsv)p.Value).ExportAsTsv(outPath);
        }

        #endregion
    }
}
