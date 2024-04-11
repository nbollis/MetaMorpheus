using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Easy.Common.Extensions;
using EngineLayer;
using NUnit.Framework;
using Proteomics;
using Proteomics.ProteolyticDigestion;
using Readers;
using Test.AveragingPaper;
using Test.ChimeraPaper.ResultFiles;
using UsefulProteomicsDatabases;

namespace Test.ChimeraPaper
{
    internal class Runner
    {
        internal static string DirectoryPath = @"B:\Users\Nic\Chimeras\Mann_11cell_analysis";
        internal static bool RunOnAll = true;

        [Test]
        public void PerformOperations()
        {
            var datasets = Directory.GetDirectories(DirectoryPath)
                .Where(p => RunOnAll || p.Contains("A549"))
                .Select(datasetDirectory => new Dataset(datasetDirectory)).ToList();
            
     
            

            // perform operations
            foreach (var dataset in datasets)
            {
                dataset.CreateRetentionTimePredictionReadyFile();
                dataset.AppendChronologerPrediction();
                dataset.CreateFraggerIndividualFileOutput();
                dataset.CountMetaMorpheusChimericPsms();
                dataset.CountMetaMorpheusChimericPsms(true);
                dataset.CombineMsFraggerPSMResults();
                dataset.CombineDDAPlusPSMFraggerResults();
                dataset.CombineMsFraggerPeptideResults();
                dataset.CombineMsFraggerPeptideResults(true);
                dataset.CountMsFraggerChimericPsms();
            }

            //datasets.MergeAllResultComparisons();
            //DatasetOperations.MergeMMResultsForInternalComparison(datasets);
            //DatasetOperations.MergeChimeraCountingData(datasets);
        }

        [Test]
        public static void RemoveSelectedFileType()
        {
            string toRemove = FileIdentifiers.ChimeraCountingFile;
            var files = Directory.GetFiles(DirectoryPath, "*.csv", SearchOption.AllDirectories).Where(p => p.Contains(toRemove)).ToList();
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }

        [Test]
        public static void TryNewStuffs()
        {
            
            var datasets = Directory.GetDirectories(DirectoryPath)
                .Where(p => RunOnAll || p.Contains("A549"))
                .Select(datasetDirectory => new CellLineResults(datasetDirectory)).ToList();

            var allResults = new AllResults(DirectoryPath);
            allResults.IndividualFileComparison();
            allResults.GetBulkResultCountComparisonFile();
            allResults.CountChimericPeptides();
            allResults.CountChimericPsms();

        }





        [Test]
        public void TESTNAME()
        {
            string inputDir = @"B:\Users\Nic\Chimeras\TopDown_Analysis\Jurkat\Searches\MsPathFinderT";
            string outputDir = inputDir;
            string databasePath =
                @"B:\Users\Nic\Chimeras\TopDown_Analysis\Jurkat\uniprotkb_human_proteome_AND_reviewed_t_2024_03_25.fasta";
            string modFilePath = @"B:\Users\Nic\Chimeras\TopDown_Analysis\Jurkat\InformedProteomicsMods.txt";

            var inputFiles = Directory.GetFiles(inputDir, "*.pbf").ToList();


            List<string> outputsList = new List<string>();
            foreach (var inputFile in inputFiles)
            {
                string cmdLineText =
                    $"-s {inputFile} -o {outputDir} -d {databasePath} -mod {modFilePath} " +
                    $"-maxCharge 60 -tda 1 -IncludeDecoys True -n 4 -ic 1 -maxThreads 12 ";
                try
                {
                    var proc = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "MSPathFinderT.exe",
                            Arguments = $"{cmdLineText}",
                            UseShellExecute = true,
                            CreateNoWindow = true,
                            WorkingDirectory = @"INSERT INFORMED-PROTEOMICS DIRECTORY HERE"
                        }
                    };
                    proc.Start();
                    proc.WaitForExit();
                    outputsList.Add(inputFile + ":  " + "Success");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    outputsList.Add(inputFile + ":  " + e.Message);
                }
            }
            using var sw = new StreamWriter(Path.Combine(outputDir, "output.txt"));
            foreach (var output in outputsList)
            {
                sw.WriteLine(output);
            }
        }

    }
}
