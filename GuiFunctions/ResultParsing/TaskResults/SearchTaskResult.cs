using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using EngineLayer;
using MassSpectrometry;
using MzLibSpectralAveraging;
using Proteomics;
using TaskLayer;
using UsefulProteomicsDatabases;

namespace GuiFunctions
{
    public class SearchTaskResult : TaskResults, ITsv
    {
        #region Private Properties

        private string mzIDPath;
        private string proteinGroupsPath;
        private string proteoformsPath;
        private string psmsPath;
        private string percolatorPath;

        private List<PsmFromTsv> allPsms;
        private List<PsmFromTsv> allProteoforms;
        private List<PsmFromTsv> allProteinGroups;
        private List<PsmFromTsv> allFilteredPsms;
        private List<PsmFromTsv> allFilteredProteoforms;
        private List<PsmFromTsv> allFilteredProteinGroups;

        private Dictionary<string, List<PsmFromTsv>> psmsByFileDictionary;
        private Dictionary<string, List<PsmFromTsv>> proteoformsByFileDictionary;
        private Dictionary<string, List<PsmFromTsv>> proteinGroupsByFileDictionary;

        #endregion


        #region Public Properties

        public string Protease { get; set; }
        public List<PsmFromTsv> AllPsms
        {
            get
            {
                if (allPsms.Any()) return allPsms;
                allPsms = PsmTsvReader.ReadTsv(psmsPath, out List<string> warnings);
                return allPsms;
            }
        }

        public List<PsmFromTsv> AllProteoforms
        {
            get
            {
                if (allProteoforms.Any()) return allProteoforms;
                allProteoforms = PsmTsvReader.ReadTsv(proteoformsPath, out List<string> warnings);
                return allProteoforms;
            }
        }

        public List<PsmFromTsv> AllProteinGroups
        {
            get
            {
                if (allProteinGroups.Any()) return allProteinGroups;
                allProteinGroups = PsmTsvReader.ReadTsv(proteinGroupsPath, out List<string> warnings);
                return allProteinGroups;
            }
        }

        public List<PsmFromTsv> AllFilteredPsms
        {
            get
            {
                if (allFilteredPsms.Any()) return allFilteredPsms;
                allFilteredPsms = AllPsms.Where(p => p.PassesFilter())
                    .ToList();
                return allFilteredPsms;
            }
        }

        public List<PsmFromTsv> AllFilteredProteoforms
        {
            get
            {
                if (allFilteredProteoforms.Any()) return allFilteredProteoforms;
                allFilteredProteoforms = AllProteoforms.Where(p => p.PassesFilter())
                    .ToList();
                return allFilteredProteoforms;
            }
        }

        public List<PsmFromTsv> AllFilteredProteinGroups
        {
            get
            {
                if (allFilteredProteinGroups.Any()) return allFilteredProteinGroups;
                allFilteredProteinGroups = AllProteinGroups.Where(p =>
                        (!ResultAnalysisVariables.FilterByQ || p.QValue <= ResultAnalysisVariables.QValueFilter))
                    .ToList();
                return allFilteredProteinGroups;
            }
        }

        public Dictionary<string, List<PsmFromTsv>> PsmsByFileDictionaryFiltered
        {
            get
            {
                if (psmsByFileDictionary.Any()) return psmsByFileDictionary;
                psmsByFileDictionary = new Dictionary<string, List<PsmFromTsv>>();
                foreach (var spec in inputSpectraPaths)
                {
                    var fileName = Path.GetFileNameWithoutExtension(spec);
                    var psmsFromFile = AllFilteredPsms.Where(
                            p => p.FileNameWithoutExtension == fileName)
                        .ToList();
                    psmsByFileDictionary.TryAdd(fileName, psmsFromFile);
                }

                return psmsByFileDictionary;
            }
        }

        public Dictionary<string, List<PsmFromTsv>> ProteoformsByFileDictionaryFiltered
        {
            get
            {
                if (proteoformsByFileDictionary.Any()) return proteoformsByFileDictionary;
                proteoformsByFileDictionary = new Dictionary<string, List<PsmFromTsv>>();
                foreach (var spec in inputSpectraPaths)
                {
                    var fileName = Path.GetFileNameWithoutExtension(spec);
                    var psmsFromFile = AllFilteredProteoforms.Where(
                        p => p.FileNameWithoutExtension == fileName)
                        .ToList();
                    proteoformsByFileDictionary.TryAdd(fileName, psmsFromFile);
                }

                return proteoformsByFileDictionary;
            }
        }

        public Dictionary<string, List<PsmFromTsv>> ProteinGroupsByFileDictionaryFiltered
        {
            get
            {
                if (proteinGroupsByFileDictionary.Any()) return proteinGroupsByFileDictionary;
                proteinGroupsByFileDictionary = new Dictionary<string, List<PsmFromTsv>>();
                foreach (var spec in inputSpectraPaths)
                {
                    var fileName = Path.GetFileNameWithoutExtension(spec);
                    var psmsFromFile = AllFilteredProteinGroups.Where(
                            p => p.FileNameWithoutExtension == fileName)
                        .ToList();
                    proteinGroupsByFileDictionary.TryAdd(fileName, psmsFromFile);
                }

                return proteinGroupsByFileDictionary;
            }
        }

        #region Calculation Results

        public int MaxProteoformChimerasFromOneSpectra = 0;
        public int MaxPsmChimerasFromOneSpectra = 0;
        public SortedDictionary<int, int> ChimericPsmIdsPerMs2Scan;
        public SortedDictionary<int, int> ChimericProteoformIdsPerMs2Scan;
        public SortedDictionary<string, int> AmbiguityPsmCountDictionary;
        public SortedDictionary<string, int> AmbiguityProteoformCountDictionary;

        public ComparativeSearchResults? ComparativeSearchResults { get; set; }



        #endregion

        #endregion

        #region Constructor

        public SearchTaskResult(string taskDirectory, string name) : base(taskDirectory, MyTask.Search, name)
        {
            allProteinGroups = new();
            allPsms = new();
            allProteoforms = new();
            allFilteredPsms = new();
            allFilteredProteinGroups = new();
            allFilteredProteoforms = new();
            psmsByFileDictionary = new();
            proteinGroupsByFileDictionary = new();
            proteoformsByFileDictionary = new();
            ChimericProteoformIdsPerMs2Scan = new();
            ChimericPsmIdsPerMs2Scan = new();
            AmbiguityProteoformCountDictionary = new();
            AmbiguityPsmCountDictionary = new();

            var files = Directory.GetFiles(taskDirectory);
            Protease = TaskToml.FirstOrDefault(p => p.Contains("Protease"))?.Split("=")[1].Replace("\"", "").Trim();
            mzIDPath = files.FirstOrDefault(p => p.Contains(".mzID"));
            percolatorPath = files.FirstOrDefault(p => p.Contains("Percolator"));

            psmsPath = files.First(p => p.Contains("AllPSMs"));
            if (Protease is "top-down")
            {
                proteinGroupsPath = files.FirstOrDefault(p => p.Contains("AllProteinGroups"));
                proteoformsPath = files.First(p => p.Contains("AllProteoforms"));
            }
            else
            {
                proteinGroupsPath = files.FirstOrDefault(p => p.Contains("AllProteinGroups") || p.Contains("AllQuantifiedProteinGroups"));
                proteoformsPath = files.First(p =>  p.Contains("AllPeptides"));
            }
        }

        #endregion

        #region Processing Methods

        public void PerformAllProcessing()
        {
            PerformChimeraProcessing();
            PerformAmbiguityProcessing();
        }

        // TODO: Make this store values for each file
        public void PerformChimeraProcessing()
        {
            SortedDictionary<int, int> chimeraInfo = new();
            foreach (var proteoformsInOneFile in ProteoformsByFileDictionaryFiltered)
            {
                var groupedProteoforms = proteoformsInOneFile.Value.GroupBy(p => p.Ms2ScanNumber);
                foreach (var group in groupedProteoforms)
                {
                    if (!chimeraInfo.TryAdd(group.Count(), 1))
                    {
                        chimeraInfo[group.Count()]++;
                    }
                }

                if (chimeraInfo.Last().Key > MaxProteoformChimerasFromOneSpectra)
                    MaxProteoformChimerasFromOneSpectra = chimeraInfo.Last().Key;
            }
            ChimericProteoformIdsPerMs2Scan = chimeraInfo;

            if (!ResultAnalysisVariables.CalculateForPsms) return;
            chimeraInfo = new();
            foreach (var psmsInOneFile in PsmsByFileDictionaryFiltered)
            {
                var groupedProteoforms = psmsInOneFile.Value.GroupBy(p => p.Ms2ScanNumber);
                foreach (var group in groupedProteoforms)
                {
                    if (!chimeraInfo.TryAdd(group.Count(), 1))
                    {
                        chimeraInfo[group.Count()]++;
                    }
                }

                if (chimeraInfo.Last().Key > MaxPsmChimerasFromOneSpectra)
                    MaxPsmChimerasFromOneSpectra = chimeraInfo.Last().Key;
            }
            ChimericPsmIdsPerMs2Scan = chimeraInfo;
        }

        // TODO: make this store values for each file
        public void PerformAmbiguityProcessing()
        {
            SortedDictionary<string, int> ambiguityInfo = new();
            foreach (var proteoformsInOneFile in ProteoformsByFileDictionaryFiltered)
            {
                foreach (var level in ambiguityLevels)
                {
                    var ambigCount = AllFilteredProteoforms.Count(p => p.AmbiguityLevel == level);
                    ambiguityInfo.TryAdd(level, ambigCount);
                }
            }
            AmbiguityProteoformCountDictionary = ambiguityInfo;

            if (!ResultAnalysisVariables.CalculateForPsms) return;
            ambiguityInfo = new();
            foreach (var psmsInOneFile in PsmsByFileDictionaryFiltered)
            {
                foreach (var level in ambiguityLevels)
                {
                    var ambigCount = AllFilteredPsms.Count(p => p.AmbiguityLevel == level);
                    ambiguityInfo.TryAdd(level, ambigCount);
                }
            }

            AmbiguityPsmCountDictionary = ambiguityInfo;
        }

        #endregion

        #region ITsv Members

        public string TabSeparatedHeader
        {
            get
            {
                var sb = new StringBuilder();

                // base information
                sb.Append("MM Run Name" + "\t");
                sb.Append("1% Psms" + "\t");
                sb.Append("1% Proteoforms" + "\t");
                sb.Append("1% Protein Groups" + "\t");
                sb.Append("Psms" + "\t");
                sb.Append("Proteoforms" + "\t");
                sb.Append("ProteinGroups" + "\t");

                // specific processing
                // comparing results
                if (ComparativeSearchResults.HasValue)
                {
                    sb.Append("Distinct Psms" + '\t');
                    sb.Append("Distinct Proteins" + '\t');
                    sb.Append("Distinct Proteoforms" + '\t');
                }

                // chimeras
                foreach (var chimericIdCount in ChimericProteoformIdsPerMs2Scan)
                {
                    sb.Append($"{chimericIdCount.Key} Proteoforms per Ms2\t");
                }
                foreach (var chimericIdCount in ChimericPsmIdsPerMs2Scan)
                {
                    sb.Append($"{chimericIdCount.Key} Psms per Ms2\t");
                }

                // ambiguity
                foreach (var ambigIdCount in AmbiguityProteoformCountDictionary)
                {
                    sb.Append($"Proteoform Ambig {ambigIdCount.Key}\t");
                }
                foreach (var ambigIdCount in AmbiguityPsmCountDictionary)
                {
                    sb.Append($"Psm Ambig {ambigIdCount.Key}\t");
                }

                var tsvString = sb.ToString().TrimEnd('\t');
                return tsvString;
            }
        }

        public string ToTsvString()
        {
            var sb = new StringBuilder();

            // base information
            sb.Append(Name + "\t");
            sb.Append(AllFilteredPsms.Count + "\t");
            sb.Append(AllFilteredProteoforms.Count + "\t");
            sb.Append(AllFilteredProteinGroups.Count + "\t");
            sb.Append(AllPsms.Count + "\t");
            sb.Append(AllProteoforms.Count + "\t");
            sb.Append(AllProteinGroups.Count + "\t");

            // specific processing
            // comparing results
            if (ComparativeSearchResults.HasValue)
            {
                sb.Append(ComparativeSearchResults.Value.DistinctFilteredPsms.Count.ToString() + '\t');
                sb.Append(ComparativeSearchResults.Value.DistinctFilteredProteins.Count.ToString() + '\t');
                sb.Append(ComparativeSearchResults.Value.DistinctFilteredProteoforms.Count.ToString() + '\t');
            }

            // chimeric
            foreach (var chimericIdCount in ChimericProteoformIdsPerMs2Scan)
            {
                sb.Append($"{chimericIdCount.Value}\t");
            }
            foreach (var chimericIdCount in ChimericPsmIdsPerMs2Scan)
            {
                sb.Append($"{chimericIdCount.Value}\t");
            }

            // ambiguity
            foreach (var ambigIdCount in AmbiguityProteoformCountDictionary)
            {
                sb.Append($"{ambigIdCount.Value}\t");
            }
            foreach (var ambigIdCount in AmbiguityPsmCountDictionary)
            {
                sb.Append($"{ambigIdCount.Value}\t");
            }

            var tsvString = sb.ToString().TrimEnd('\t');
            return tsvString;
        }
        
        #endregion
    }
}






