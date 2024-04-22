using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Easy.Common.Extensions;
using EngineLayer;
using NUnit.Framework;
using pepXML.Generated;
using Proteomics;
using Proteomics.ProteolyticDigestion;
using Readers;
using Test.AveragingPaper;
using Test.ChimeraPaper.ResultFiles;
using UsefulProteomicsDatabases;

namespace Test.ChimeraPaper
{
    internal class BURunner
    {
        internal static string DirectoryPath = @"B:\Users\Nic\Chimeras\Mann_11cell_analysis";
        internal static bool RunOnAll = true;

        [Test]
        public static void RunAllParsing()
        {
            var datasets = Directory.GetDirectories(DirectoryPath)
                .Where(p => !p.Contains("Figures") && RunOnAll || p.Contains("Hela"))
                .Select(datasetDirectory => new CellLineResults(datasetDirectory)).ToList();
            var allResults = new AllResults(DirectoryPath, datasets);
            foreach (CellLineResults cellLine in allResults)
            {
                foreach (var result in cellLine)
                {
                    result.Override = true;
                    result.IndividualFileComparison();
                    result.GetBulkResultCountComparisonFile();
                    result.CountChimericPsms();
                    if (result is MetaMorpheusResult mm)
                        mm.CountChimericPeptides();
                    result.Override = false;
                }

                cellLine.Override = true;
                cellLine.IndividualFileComparison();
                cellLine.GetBulkResultCountComparisonFile();
                cellLine.CountChimericPsms();
                cellLine.CountChimericPeptides();
                cellLine.Override = false;
            }

            allResults.Override = true;
            allResults.GetBulkResultCountComparisonFile();
            allResults.IndividualFileComparison();
            allResults.CountChimericPsms();
            allResults.CountChimericPeptides();
            allResults.Override = false;
        }

        [Test]
        public static void GenerateAllFigures()
        {
            var datasets = Directory.GetDirectories(DirectoryPath)
                .Where(p => !p.Contains("Figures") && RunOnAll || p.Contains("Hela"))
                .Select(datasetDirectory => new CellLineResults(datasetDirectory)).ToList();
            var allResults = new AllResults(DirectoryPath, datasets);
            foreach (CellLineResults cellLine in allResults)
            {
                cellLine.PlotIndividualFileResults();
                cellLine.PlotCellLineRetentionTimePredictions();
                cellLine.PlotCellLineSpectralSimilarity();
            }


            allResults.PlotInternalMMComparison();
            allResults.PlotBulkResultComparison();
            allResults.PlotStackedIndividualFileComparison();
            allResults.PlotStackedSpectralSimilarity();
            allResults.PlotAggregatedSpectralSimilarity();
        }

        [Test]
        public static void GenerateSpecificFigure()
        {
            var datasets = Directory.GetDirectories(DirectoryPath)
                .Where(p => !p.Contains("Figures") && RunOnAll || p.Contains("Hela"))
                .Select(datasetDirectory => new CellLineResults(datasetDirectory)).ToList();
            var allResults = new AllResults(DirectoryPath, datasets);

            allResults.PlotBulkResultRetentionTimePredictions();
            //allResults.PlotStackedSpectralSimilarity();
            //allResults.PlotAggregatedSpectralSimilarity();
            foreach (CellLineResults cellLine in allResults)
            {
                //cellLine.PlotCellLineSpectralSimilarity();
            }

        }

        [Test]
        public static void GetConversionDictionary()
        {
            var allResults = new AllResults(DirectoryPath);
            var toPull = allResults.SelectMany(p => p.Where(m => m.Condition.Equals("MsFragger")))
                .Select(p => Path.Combine(p.DirectoryPath, "fragpipe-files.fp-manifest"));

            var sb = new StringBuilder();
            foreach (var manifest in allResults.SelectMany(p => p.Where(m => m.Condition.Equals("MsFragger")))
                         .Select(p => Path.Combine(p.DirectoryPath, "fragpipe-files.fp-manifest")))
            {
                var lines = File.ReadAllLines(manifest);
                foreach (var line in lines)
                {
                    var splits = line.Split('\t');
                    var path = splits[0].Split('\\').Last();
                    var specific = $"{splits[1]}_{splits[2]}";
                    sb.AppendLine($"{{\"{path}\",\"{specific}\"}},");
                }
            }

            var result = sb.ToString();
        }


        [Test]
        public static void FengchaoOutputForPlots()
        {
            
            string mmPath = @"B:\Users\Nic\Chimeras\MSV000090552\metamorpheus";
            string mmPath2 = @"B:\Users\Nic\Chimeras\MSV000090552\metamorpheus_GPTMD_fasta";
            string mmPath3 = @"B:\Users\Nic\Chimeras\MSV000090552\metamorpheus_GPTMD_xml";
            string mmPath4 = @"B:\Users\Nic\Chimeras\MSV000090552\metamorpheus_DefaultGPTMD";
            string fraggerPath = @"B:\Users\Nic\Chimeras\MSV000090552\fragpipe";
            string ddaPlusPath = @"B:\Users\Nic\Chimeras\MSV000090552\fragpipe_ddaplus";

            var mmResults = new MetaMorpheusResult(mmPath);
            var mmResults2 = new MetaMorpheusResult(mmPath2);
            var mmResults3 = new MetaMorpheusResult(mmPath3);
            var mmResults4 = new MetaMorpheusResult(mmPath4);

            var fraggerResults = new MsFraggerResult(fraggerPath);
            var ddaPlusResults = new MsFraggerResult(ddaPlusPath);
            List<BulkResult> results = new List<BulkResult> { mmResults, /*mmResults2, mmResults3, mmResults4,*/ fraggerResults, ddaPlusResults };
            var cellLine = new CellLineResults(@"B:\Users\Nic\Chimeras\MSV000090552", results);
            cellLine.PlotIndividualFileResults();
            
            
            string outPath = @"B:\Users\Nic\Chimeras\MSV000090552\comparison.csv";
            cellLine.FileComparisonDifferentTypes(outPath);



            //var datasets = Directory.GetDirectories(DirectoryPath)
            //    .Where(p => RunOnAll || p.Contains("A549"))
            //    .Select(datasetDirectory => new CellLineResults(datasetDirectory)).ToList();
            //string outPath = Path.Combine(datasets.First().DirectoryPath, "Comparison.csv");
            //datasets.First().FileComparisonDifferentTypes(outPath);







        }

        private static ulong GetFactorial(ulong n)
        {
            if (n == 0)
            {
                return 1;
            }
            return n * GetFactorial(n - 1);
        }

        private static double GetFactorial(double n)
        {
            if (n == 0)
            {
                return 0;
            }
            return Math.Log(n) + GetFactorial(n - 1);
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
