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
        internal static bool RunOnAll = false;

        [Test]
        public static void TryNewStuffs()
        {
            
            var datasets = Directory.GetDirectories(DirectoryPath)
                .Where(p => RunOnAll || p.Contains("A549"))
                .Select(datasetDirectory => new CellLineResults(datasetDirectory)).ToList();
            //var allResults = new AllResults(DirectoryPath);
            foreach (var cellLine in datasets)
            {
                foreach (var result in cellLine.Results)
                {
                    if (result.ToString() != "A549_MsFragger")
                        continue;

                    result.Override = true;
                    if (result is MsFraggerResult frag)
                    {
                        frag.CombinePeptideFiles();
                    }

                    _ = result.BaseSeqIndividualFileComparisonFile;
                    result.IndividualFileComparison();
                    result.Override = false;
                }
                cellLine.Override = true;
                cellLine.GetBulkResultCountComparisonFile();
                cellLine.IndividualFileComparison();
                _ = cellLine.BaseSeqIndividualFileComparisonFile;
            }
            
        }

        [Test]
        public static void RunParserOnFengchaoFiles()
        {
            string mmPath = @"B:\Users\Nic\Chimeras\MSV000090552\metamorpheus";
            string fraggerPath = @"B:\Users\Nic\Chimeras\MSV000090552\fragpipe";
            string ddaPlusPath = @"B:\Users\Nic\Chimeras\MSV000090552\fragpipe_ddaplus";

            var mmResults = new MetaMorpheusResult(mmPath);
            var fraggerResults = new MsFraggerResult(fraggerPath);
            var ddaPlusResults = new MsFraggerResult(ddaPlusPath);

            List<BulkResult> results = new List<BulkResult> { mmResults, fraggerResults, ddaPlusResults };
            var cellLine = new CellLineResults(@"B:\Users\Nic\Chimeras\MSV000090552",  results );
            cellLine.Override = true;
            cellLine.IndividualFileComparison();
            cellLine.GetBulkResultCountComparisonFile();
            cellLine.IndividualFileComparisonBaseSeq();

            //var allIndividualFileResults = new List<BulkResultCountComparison>();
            //fraggerResults.IndividualFileComparisonFile.ForEach(p =>
            //{
            //    p.Condition = "Individual";
            //    p.DatasetName = "FragPipe";
            //});
            //allIndividualFileResults.AddRange(fraggerResults.IndividualFileComparisonFile);

            //ddaPlusResults.IndividualFileComparisonFile.ForEach(p =>
            //{
            //    p.Condition = "Individual";
            //    p.DatasetName = "FragPipeDDAPlus";
            //});
            //allIndividualFileResults.AddRange(ddaPlusResults.IndividualFileComparisonFile);

            //mmResults.IndividualFileComparisonFile.ForEach(p =>
            //{
            //    p.Condition = "Combined";
            //    p.DatasetName = "MetaMorpheus";
            //}); 
            //allIndividualFileResults.AddRange(mmResults.IndividualFileComparisonFile);
            //foreach (var result in mmResults.CountIndividualFilesForFengChaoComparison())
            //{
            //    allIndividualFileResults.Add(result);
            //}

            //string outPath = Path.Combine(@"B:\Users\Nic\Chimeras\MSV000090552", "ParserTesting.csv");
            //var file = new BulkResultCountComparisonFile(outPath) { Results = allIndividualFileResults };
            //file.WriteResults(outPath);

            //string resultDir = @"B:\Users\Nic\Chimeras\MSV000090552";
            //var cellLine = new CellLineResults(resultDir);
            //cellLine.CountChimericPsms();
            //cellLine.IndividualFileComparison();
            //cellLine.GetBulkResultCountComparisonFile();
        }

        [Test]
        public void RunFengChaoComparisonOnA549()
        {
            string resultDir = Path.Combine(DirectoryPath, "A549", "SearchResults");
            var desiredResultDirectories = Directory.GetDirectories(resultDir)
                .Where(p => (p.Contains("MsFragger") || p.Contains("IndividualFiles")) && !p.Contains("Reviewd"))
                .ToList();

            var results = new List<BulkResult>();
            foreach (var directory in desiredResultDirectories)
            {
                if (Directory.GetFiles(directory, "*.psmtsv", SearchOption.AllDirectories).Any())
                    results.Add(new MetaMorpheusResult(directory));
                else
                {
                    var res = new MsFraggerResult(directory);
                    _ = res.CombinedPeptideBaseSeq;
                    _ = res.CombinedPeptides;
                    results.Add(res);
                }
            }

            List<BulkResultCountComparison> allIndividualFileResults = new List<BulkResultCountComparison>();
            foreach (var result in results)
            {
                allIndividualFileResults.AddRange(result.BaseSeqIndividualFileComparisonFile);
            }




            string outPath = Path.Combine(DirectoryPath, "A549", $"A549_BaseSequence_{FileIdentifiers.IndividualFileComparison}");
            var file = new BulkResultCountComparisonFile(outPath) { Results = allIndividualFileResults };
            file.WriteResults(outPath);
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
