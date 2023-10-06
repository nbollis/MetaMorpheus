using Nett;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using pepXML.Generated;
using TaskLayer;
using TopDownProteomics.IO.PsiMod;
using Readers;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using IgnoreAttribute = CsvHelper.Configuration.Attributes.IgnoreAttribute;
using EngineLayer.FdrAnalysis;

namespace Test
{
    [TestFixture]
    internal class ATargetDecoyRunner
    {

        



        [Test]
        public static void RunThatShit()
        {
            // foreach database in DatabaseDirectory that does not contain the word "NoDecoy"
            // search all data in SearchDirectory 
            // with toml @ toml path
            // output in Search Directory with the species and decoy type taken from Database File Name
            MultiRunner.RunAll(false);

            // foreach PSM file found within the search directory
            // create a target decoy histogram and output it in the search results directory
            MultiRunner.ParseAllPsmsFilesDataAndCreateCsvsOfTargetDecoyByScore(false);

            // Combine all Histograms to a master sheet and output in SearhDirectory
            MultiRunner.CombineAllTargetDecoyResultFiles();
        }

        [Test]
        public static void RunDataParser()
        {
            MultiRunner.ParseAllPsmsFilesDataAndCreateCsvsOfTargetDecoyByScore(true);
        }

        [Test]
        public static void CombineRecords()
        {
            MultiRunner.CombineAllTargetDecoyResultFiles();
        }
    }

    /// <summary>
    /// To use this class, switch out the base directory for each computer it is used on.
    /// The search output location, database paths, and ms file paths are set by relative paths from the base directory
    /// Base directory should be Bison\Users\Nic\SharedWithMe\targetBias
    /// </summary>
    public static class MultiRunner
    {
        public static string BaseDirectory => @"B:\Users\Nic\SharedWithMe\targetBias";
        public static string SearchDirectory => Path.Combine(BaseDirectory, @"SearchResults\MetaMorpheus");
        public static string DatabaseDirectory => Path.Combine(BaseDirectory, @"FASTA_Files\Standardized");
        public static string TomlPath = Path.Combine(BaseDirectory, "TopDownSearch.toml");

        /// <summary>
        /// Takes all databases in database directory, and searches them with all 11 raw files, choosing its output name based upon database name, and outputting into search directory
        /// </summary>
        /// <param name="overWriteIfDoneAlready"></param>
        public static void RunAll(bool overWriteIfDoneAlready = false)
        {
            // parse database
            string[] databasePaths = Directory.GetFiles(DatabaseDirectory, "*.fasta")
                .Where(p => !p.Contains("NoDecoy"))
                .ToArray();
            List<string> dataFilePaths = Directory.GetFiles(BaseDirectory, "*.raw").ToList();

            foreach (var database in databasePaths)
            {
                var name = Path.GetFileNameWithoutExtension(database).Split('_').First();
                var condition = Path.GetFileNameWithoutExtension(database).Split('_')[2];
                string outPath = Path.Combine(SearchDirectory, $"{name}_{condition}");
                if (!overWriteIfDoneAlready && Directory.Exists(outPath))
                {
                    var files = Directory.GetFiles(outPath);
                    var directories = Directory.GetDirectories(outPath);
                    if (files.Any(p => p.EndsWith("allResults.txt"))
                        && directories.Any(p => p.EndsWith("Task Settings"))
                        && directories.Any(p => p.EndsWith("Task1-SearchTask"))
                        && Directory.Exists(Path.Combine(outPath, "Task1-SearchTask"))
                        && Directory.GetFiles(Path.Combine(outPath, "Task1-SearchTask"))
                            .Any(p => p.Contains("AllPSMs.psmtsv")))
                        continue;
                    else
                        Directory.Delete(outPath, true);
                }

                RunIndividualSearch(database, dataFilePaths.ToList(), TomlPath, outPath);
            }
        }

        /// <summary>
        /// takes database from location, and searches it with all 11 raw files, choosing its output name based upon database name, and outputting into search directory
        /// </summary>
        /// <param name="databasePath"></param>
        /// <param name="dataFilePaths"></param>
        /// <param name="searchToml"></param>
        /// <param name="outputPath"></param>
        public static void RunIndividualSearch(string databasePath, List<string> dataFilePaths, string searchToml, string outputPath)
        {

            SearchTask searchTask = Toml.ReadFile<SearchTask>(searchToml, MetaMorpheusTask.tomlConfig);

            var taskList = new List<(string, MetaMorpheusTask)>()
            {
                ("Task1-SearchTask", searchTask),
            };
            var dbList = new List<DbForTask>()
            {
                new DbForTask(databasePath, false),
            };

            EverythingRunnerEngine engine = new(taskList, dataFilePaths.ToList(), dbList, outputPath);
            engine.Run();
        }

        /// <summary>
        ///  Finds all Psm files in search directory and creates a histogram of target and decoy's by their integer score in csv format
        /// </summary>
        /// <param name="overWriteIfDoneAlready"></param>
        public static void ParseAllPsmsFilesDataAndCreateCsvsOfTargetDecoyByScore(bool overWriteIfDoneAlready = false)
        {
            foreach (var psmFile in Directory.GetFiles(SearchDirectory, "*AllPSMs.psmtsv", SearchOption.AllDirectories))
            {
                if (!overWriteIfDoneAlready)
                {
                    var splits = psmFile.Split("\\");
                    var name = splits[^3];
                    var outpath = Path.Combine(string.Join("\\", splits[..^2]), $"{name}_TDAnalysis.csv");
                    if (File.Exists(outpath))
                        continue;
                }
                ParseSinglePsmsFileDataAndCreateCsvOfTargetDecoyByScore(psmFile);
            }
        }

        /// <summary>
        /// Reads in a single psms file and creates a histogram of target and decoy's by their integer score in csv format
        /// </summary>
        /// <param name="psmPath"></param>
        public static void ParseSinglePsmsFileDataAndCreateCsvOfTargetDecoyByScore(string psmPath)
        {
            var splits = psmPath.Split("\\");
            var name = splits[^3];
            var organism = name.Split('_')[0];
            var condition = name.Split('_')[1];
            var outpath = Path.Combine(string.Join("\\", splits[..^2]), $"{name}_TDAnalysis.csv");

            var allPsms = PsmTsvReader.ReadTsv(psmPath, out _);
            var decoyScoreHistogram = allPsms.Where(p =>
                    p.ProteinAccession.StartsWith("DECOY", StringComparison.InvariantCultureIgnoreCase))
                .GroupBy(p => (int)p.Score)
                .ToDictionary(p => p.Key, p => p.Count());
            var targetScoreHistogram = allPsms.Where(p =>
                    !p.ProteinAccession.StartsWith("DECOY", StringComparison.InvariantCultureIgnoreCase))
                .GroupBy(p => (int)p.Score)
                .ToDictionary(p => p.Key, p => p.Count());

            var keys = decoyScoreHistogram.Keys.Union(targetScoreHistogram.Keys)
                .OrderByDescending(p => p)
                .ToArray();

            TargetDecoyResultFile tdFile = new(outpath);
            for (int i = keys.Min(); i < keys.Max() + 1; i++)
            {
                if (!targetScoreHistogram.TryGetValue(i, out int targetCount))
                    targetCount = 0;
                if (!decoyScoreHistogram.TryGetValue(i, out int decoyCount))
                    decoyCount = 0;

                if (i == keys.Min())
                    tdFile.Results = new List<TargetDecoyResult>()
                        { new TargetDecoyResult(organism, condition, i, targetCount, decoyCount) };
                else
                    tdFile.Results.Add(new TargetDecoyResult(organism, condition, i, targetCount, decoyCount));
            }

            tdFile.WriteResults(outpath);
        }

        /// <summary>
        /// Searches for all TDHistograms and combines into one output
        /// </summary>
        public static void CombineAllTargetDecoyResultFiles()
        {
            var combinedPath = Path.Combine(SearchDirectory, "Combined_TdAnalysis.csv");
            var combinedFile = new TargetDecoyResultFile(combinedPath);
            List<TargetDecoyResult> allResults = new();
            foreach (var tdFile in Directory.GetFiles(SearchDirectory, "*TDAnalysis.csv", SearchOption.AllDirectories))
            {
                allResults.AddRange(new TargetDecoyResultFile(tdFile).Results);
            }

            combinedFile.Results = allResults;
            combinedFile.WriteResults(combinedPath);
        }
    }


    public class TargetDecoyResultFile : ResultFile<TargetDecoyResult>, IResultFile
    {
        public override void LoadResults()
        {
            using var csv = new CsvReader(new StreamReader(FilePath), TargetDecoyResult.CsvConfiguration);
            Results = csv.GetRecords<TargetDecoyResult>().ToList();
        }

        public override void WriteResults(string outputPath)
        {
            using var csv = new CsvWriter(new StreamWriter(File.Create(outputPath)), CultureInfo.InvariantCulture);
            csv.WriteHeader<TargetDecoyResult>();
            
            foreach (var result in Results)
            {
                csv.NextRecord();
                csv.WriteRecord(result);
            }
        }

        public override SupportedFileType FileType { get; }
        public override Software Software { get; set; }

        public TargetDecoyResultFile(string csvPath) : base (csvPath, Software.Unspecified)
        {
            
        }
    }
    
    public class TargetDecoyResult 
    {
        [Ignore]
        public static CsvConfiguration CsvConfiguration => new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Encoding = Encoding.UTF8,
            HasHeaderRecord = true,
            Delimiter = ","
        };

        [Name("Organism")]
        public string Organism { get; set; }
        [Name("DecoyType")]
        public string DecoyType { get; set; }
        [Name("Score")]
        public int Score { get; set; }
        [Name("TargetCount")]
        public int TargetCount { get; set; }
        [Name("DecoyCount")]
        public int DecoyCount { get; set; }

     
        public TargetDecoyResult(string organism, string decoyType, int score, int targetCount, int decoyCount)
        {
            Organism = organism;
            DecoyType = decoyType;
            Score = score;
            TargetCount = targetCount;
            DecoyCount = decoyCount;
        }

        public TargetDecoyResult()
        {
        }
    }
}
