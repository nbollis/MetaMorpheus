using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nett;
using NUnit.Framework;
using TaskLayer;

namespace Test
{
    public static class DavidTabbRunner
    {
        [Test]
        public static void Run()
        {
            foreach (var dataset in DavidTabbData.GetTabbDataSets())
            {
                List<string> filesForSearch;

                // process spectra
                if (dataset.DataFilePaths.Count != dataset.AveragedDataFileNames.Count)
                {
                    try
                    {
                        var calibTask = Toml.ReadFile<CalibrationTask>(dataset.CalibrationTomlPath, MetaMorpheusTask.tomlConfig);
                        var avgTask = Toml.ReadFile<SpectralAveragingTask>(DavidTabbData.AveragingTomlPath, MetaMorpheusTask.tomlConfig);

                        var runner = new EverythingRunnerEngine(
                            new List<(string, MetaMorpheusTask)>()
                            {
                                ("Calibration", calibTask),
                                ("SpectralAveraging", avgTask)
                            },
                            dataset.DataFilePaths,
                            dataset.DbForTask,
                            dataset.ProcessingOutputDirectory);
                        runner.Run();

                        filesForSearch = runner.CurrentRawDataFilenameList;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        filesForSearch = null;
                    }
                }
                else
                    filesForSearch = dataset.AveragedDataFileNames;
                

                // search
                foreach (var searchType in dataset.SearchTomlAndOutputDirectories)
                {
                    if (filesForSearch == null) continue;

                    // just search
                    try
                    {
                        if (Directory.Exists(searchType.Key) &&
                            Directory.GetFiles(searchType.Key, "allResults.txt").Any())
                            throw new Exception("Already Searched");


                        var searchTask = Toml.ReadFile<SearchTask>(searchType.Value, MetaMorpheusTask.tomlConfig);
                        var searchRunner = new EverythingRunnerEngine(
                            new List<(string, MetaMorpheusTask)>()
                            {
                                ("Search", searchTask)
                            },
                            filesForSearch,
                            dataset.DbForTask,
                            searchType.Key);
                        searchRunner.Run();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                    

                    // gptmd then search
                    try
                    {
                        string outputPath = searchType.Key + "_GPTMD";
                        if (Directory.Exists(outputPath) &&
                            Directory.GetFiles(outputPath, "allResults.txt").Any())
                            continue;

                        var searchTask = Toml.ReadFile<SearchTask>(searchType.Value, MetaMorpheusTask.tomlConfig);
                        var gptmdTask = Toml.ReadFile<GptmdTask>(dataset.GptmdTomlPath, MetaMorpheusTask.tomlConfig);
                        var searchRunner = new EverythingRunnerEngine(
                            new List<(string, MetaMorpheusTask)>()
                            {
                                ("GPTMD", gptmdTask),
                                ("Search", searchTask)
                            },
                            filesForSearch,
                            dataset.DbForTask,
                            outputPath);
                        searchRunner.Run();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }

                    // TODO: gptmd then no variable mods search
                }
            }
        }
    }
}