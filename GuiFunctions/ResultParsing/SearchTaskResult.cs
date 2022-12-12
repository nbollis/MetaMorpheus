using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public class SearchTaskResult : TaskResults
    {
        private string mzIDPath;
        private string proteinGroupsPath;
        private string proteoformsPath;
        private string psmsPath;
        private string percolatorPath;

        private List<PsmFromTsv> allPsms;
        private List<PsmFromTsv> allProteoforms;
        private List<PsmFromTsv> allProteinGroups;

        public SearchResultAnalyzer Analyzer { get; }
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


        public SearchTaskResult(string taskDirectory) : base(taskDirectory, MyTask.Search)
        {
            allProteinGroups = new();
            allPsms = new();
            allProteoforms = new();
            var files = Directory.GetFiles(taskDirectory);
            mzIDPath = files.FirstOrDefault(p => p.Contains(".mzID"));
            proteinGroupsPath = files.FirstOrDefault(p => p.Contains("AllProteinGroups"));
            proteoformsPath = files.First(p => p.Contains("AllProteoforms") || p.Contains("AllPeptides"));
            psmsPath = files.First(p => p.Contains("AllPSMs"));
            percolatorPath = files.FirstOrDefault(p => p.Contains("Percolator"));
            Analyzer = new(inputSpectraPaths.ToList(), proteoformsPath, psmsPath);
        }
    }

    public class GptmdTaskResult : TaskResults
    {
        private string customDatabasePath;
        private string candidatesPath;
        private List<PsmFromTsv> gptmdCandidates;
        private List<Protein> databaseProteins;
        public List<PsmFromTsv> GptmdCandidates
        {
            get
            {
                if (gptmdCandidates.Any()) return gptmdCandidates;
                gptmdCandidates = PsmTsvReader.ReadTsv(candidatesPath, out List<string> warnings);
                return gptmdCandidates;
            }
        }

        public List<Protein> DatabaseProteins
        {
            get
            {
                if (databaseProteins.Any()) return databaseProteins;
                string theExtension = Path.GetExtension(customDatabasePath).ToLowerInvariant();
                if (theExtension.Equals(".fasta") || theExtension.Equals(".fa"))
                {

                    databaseProteins = ProteinDbLoader.LoadProteinFasta(customDatabasePath, true, DecoyType.None, false, out List<string> errors,
                        ProteinDbLoader.UniprotAccessionRegex, ProteinDbLoader.UniprotFullNameRegex, ProteinDbLoader.UniprotFullNameRegex, ProteinDbLoader.UniprotGeneNameRegex,
                        ProteinDbLoader.UniprotOrganismRegex, 1);
                }
                else
                {
                    Dictionary<string, Modification> outStuff = new();
                    databaseProteins = ProteinDbLoader.LoadProteinXML(customDatabasePath, true, DecoyType.None, GlobalVariables.AllModsKnown,
                        false, new List<string>(), out outStuff, 1, 4, 1);
                }
                return databaseProteins;
            }
        }

        public GptmdTaskResult(string taskDirectory) : base(taskDirectory, MyTask.Gptmd)
        {
            var files = Directory.GetFiles(taskDirectory);
            customDatabasePath = files.First(p => p.Contains("custom"));
            candidatesPath = files.First(p => p.Contains("GPTMD_Candidates"));
            gptmdCandidates = new();
            databaseProteins = new();
        }
    }

    public class CalibrationTaskResult : TaskResults
    {
        private string[] outputSpectraPaths;
        private string[] outputTomlPaths;
        private Dictionary<string, List<MsDataScan>> outputSpectra;

        public Dictionary<string, List<MsDataScan>> OutputSpectra
        {
            get
            {
                if (outputSpectra.Any()) return outputSpectra;
                foreach (var path in outputSpectraPaths)
                {
                    var fileName = Path.GetFileName(path);
                    var scans = SpectraFileHandler.LoadAllScansFromFile(path);
                    outputSpectra.Add(fileName, scans);
                }
                return outputSpectra;
            }
        }

        public CalibrationTaskResult(string taskDirectory) : base(taskDirectory, MyTask.Calibrate)
        {
            this.outputSpectra = new();
            var files = Directory.GetFiles(taskDirectory);
            outputSpectraPaths = files.Where(p => p.Contains(".mzML")).OrderBy(p => p).ToArray();
            outputTomlPaths = files.Where(p => p.Contains(".toml")).OrderBy(p => p).ToArray();
        }
    }

    public class AveragingTaskResult : TaskResults
    {
        private string[] outputSpectraPaths;
        private string[] outputTomlPaths;
        private Dictionary<string, List<MsDataScan>> outputSpectra;

        public Dictionary<string, List<MsDataScan>> OutputSpectra
        {
            get
            {
                if (outputSpectra.Any()) return outputSpectra;
                foreach (var path in outputSpectraPaths)
                {
                    var fileName = Path.GetFileName(path);
                    var scans = SpectraFileHandler.LoadAllScansFromFile(path);
                    outputSpectra.Add(fileName, scans);
                }
                return outputSpectra;
            }
        }

        public AveragingTaskResult(string taskDirectory) : base(taskDirectory, MyTask.Averaging)
        {
            this.outputSpectra = new();
            var files = Directory.GetFiles(taskDirectory);
            outputSpectraPaths = files.Where(p => p.Contains(".mzML")).OrderBy(p => p).ToArray();
            outputTomlPaths = files.Where(p => p.Contains(".toml")).OrderBy(p => p).ToArray();
        }
    }
}
