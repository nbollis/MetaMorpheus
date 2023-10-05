using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nett;
using TaskLayer;

namespace MetaMorpheusGUI
{
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
                string outPath = Path.Combine(SearchDirectory, name);
                if (!overWriteIfDoneAlready && Directory.Exists(outPath))
                {
                    var files = Directory.GetFiles(outPath);
                    var directories = Directory.GetDirectories(outPath);
                    if (files.Any(p => p.Contains("allResults.txt")) 
                        && directories.Any(p =>
                            p.Contains("Task Settings")
                            && p.Contains("Task1-SearchTask")))
                        continue;
                }

                RunIndividualSearch(database, dataFilePaths.ToList(), TomlPath, outPath);
            }
        }

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
    }
}
