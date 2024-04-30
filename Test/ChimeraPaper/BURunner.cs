using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Easy.Common.Extensions;
using EngineLayer;
using MassSpectrometry;
using NUnit.Framework;
using pepXML.Generated;
using Plotly.NET.ImageExport;
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
                    if (result is not MetaMorpheusResult)
                        continue;
                    result.Override = true;
                    result.IndividualFileComparison();
                    result.GetBulkResultCountComparisonFile();
                    result.CountChimericPsms();
                    if (result is MetaMorpheusResult mm)
                    {
                        mm.CountChimericPeptides();
                        mm.GetChimeraBreakdownFile();
                    }
                    result.Override = false;
                }

                cellLine.Override = true;
                cellLine.IndividualFileComparison();
                cellLine.GetBulkResultCountComparisonFile();
                cellLine.CountChimericPsms();
                cellLine.CountChimericPeptides();
                cellLine.GetChimeraBreakdownFile();
                cellLine.Override = false;
            }

            allResults.Override = true;
            allResults.GetBulkResultCountComparisonFile();
            allResults.IndividualFileComparison();
            allResults.CountChimericPsms();
            allResults.CountChimericPeptides();
            allResults.GetChimeraBreakdownFile();
            allResults.Override = false;
        }

        [Test]
        public static void RunSpecificparser()
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
                    if (result is MetaMorpheusResult mm)
                        mm.GetChimeraBreakdownFile();
                    result.Override = false;
                }

                cellLine.Override = true;
                cellLine.GetChimeraBreakdownFile();
                cellLine.Override = false;
                cellLine.PlotCellLineChimeraBreakdown();
                cellLine.PlotCellLineChimeraBreakdown_TargetDecoy();
                
            }

            allResults.Override = true;
            allResults.GetChimeraBreakdownFile();
            allResults.Override = false;
            allResults.PlotBulkResultChimeraBreakDown();
            allResults.PlotBulkResultChimeraBreakDown_TargetDecoy();
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
                cellLine.PlotCellLineChimeraBreakdown();
                //cellLine.PlotCellLineRetentionTimePredictions();
                //cellLine.PlotCellLineSpectralSimilarity();
                cellLine.PlotCellLineChimeraBreakdown();
                cellLine.PlotCellLineChimeraBreakdown_TargetDecoy();
            }


            allResults.PlotInternalMMComparison();
            allResults.PlotBulkResultComparison();
            allResults.PlotStackedIndividualFileComparison();
            allResults.PlotBulkResultChimeraBreakDown();
            //allResults.PlotStackedSpectralSimilarity();
            //allResults.PlotAggregatedSpectralSimilarity();
            allResults.PlotBulkResultChimeraBreakDown();
            allResults.PlotBulkResultChimeraBreakDown_TargetDecoy();
        }

        [Test]
        public static void GenerateSpecificFigure()
        {
            var datasets = Directory.GetDirectories(DirectoryPath)
                .Where(p => !p.Contains("Figures") && RunOnAll || p.Contains("Hela"))
                .Select(datasetDirectory => new CellLineResults(datasetDirectory)).ToList();
            var allResults = new AllResults(DirectoryPath, datasets);

            //allResults.PlotBulkResultRetentionTimePredictions();
            //allResults.PlotStackedSpectralSimilarity();
            //allResults.PlotAggregatedSpectralSimilarity();
            foreach (CellLineResults cellLine in allResults)
            {
                //cellLine.PlotCellLineChimeraBreakdown();
                //cellLine.PlotIndividualFileResults();
                //cellLine.PlotCellLineSpectralSimilarity();
            }
            //allResults.PlotBulkResultComparison();
            //allResults.PlotStackedIndividualFileComparison();
            allResults.PlotBulkResultChimeraBreakDown();
        }

        [Test]
        public static void OvernightRunner()
        {
            var bottomUpResults = new AllResults(DirectoryPath);
            var topDownResults = new AllResults(TDRunner.DirectoryPath);

            foreach (var cellLine in bottomUpResults)
            {
                foreach (var result in cellLine)
                {
                    result.Override = true;
                    if (result is MetaMorpheusResult { Condition: "MetaMorpheusWithLibrary" } mm)
                    {
                        mm.GetChimeraBreakdownFile();
                    }
                    result.Override = false;

                }

                cellLine.Override = true;
                cellLine.GetChimeraBreakdownFile();
                cellLine.Override = false;

                cellLine.PlotCellLineChimeraBreakdown();
                cellLine.PlotCellLineChimeraBreakdown_TargetDecoy();
            }

            bottomUpResults.Override = true;
            bottomUpResults.GetChimeraBreakdownFile();
            bottomUpResults.Override = false;
            bottomUpResults.PlotBulkResultChimeraBreakDown();
            bottomUpResults.PlotBulkResultChimeraBreakDown_TargetDecoy();


            foreach (var cellLine in topDownResults)
            {
                foreach (var result in cellLine)
                {
                    result.Override = true;
                    if (result is MetaMorpheusResult { Condition: "MetaMorpheus" } mm)
                    {
                        mm.GetChimeraBreakdownFile();
                    }
                    else if (result is MsPathFinderTResults mspt)
                    {

                    }
                    else if (result is ProsightPDResult pspd)
                    {

                    }

                    result.Override = false;
                }

                cellLine.Override = true;
                cellLine.GetChimeraBreakdownFile();
                cellLine.Override = false;

                cellLine.PlotCellLineChimeraBreakdown();
                cellLine.PlotCellLineChimeraBreakdown_TargetDecoy();
            }

            topDownResults.Override = true;
            topDownResults.GetChimeraBreakdownFile();
            topDownResults.Override = false;
            topDownResults.PlotBulkResultChimeraBreakDown();
            topDownResults.PlotBulkResultChimeraBreakDown_TargetDecoy();
        }


        [Test]
        public static void TryPrecursorIsolationAsParent()
        {
            string outpath = @"B:\Users\Nic\Chimeras\Mann_11cell_analysis\Hela\SearchResults\MetaMorpheusWithLibrary\Hela_MetaMorpheusWithLibrary_PrecursorIsolation_ChimeraBreakdownComparison.csv";
            ChimeraBreakdownFile file;
            if (!File.Exists(outpath))
            {
                var datasets = Directory.GetDirectories(DirectoryPath)
                    .Where(p => p.Contains("Hela"))
                    .Select(datasetDirectory => new CellLineResults(datasetDirectory)).ToList();
                var specificResult = datasets.First().First(p => p.Condition.Equals("MetaMorpheusWithLibrary"));
                string helaDataPath = @"B:\RawSpectraFiles\Mann_11cell_lines\Hela\CalibratedAveraged";


                List<ChimeraBreakdownRecord> chimeraBreakDownRecords = new();
                foreach (var fileGroup in PsmTsvReader.ReadTsv(specificResult._psmPath, out _)
                             .Where(p => p.PEP_QValue <= 0.01 && p.DecoyContamTarget == "T")
                             .GroupBy(p => p.FileNameWithoutExtension))
                {
                    var dataFilePath = Directory.GetFiles(helaDataPath).FirstOrDefault(p => p.Contains(fileGroup.Key));
                    if (dataFilePath == null)
                        throw new Exception();
                    MsDataFile dataFile = MsDataFileReader.GetDataFile(dataFilePath);
                    dataFile.InitiateDynamicConnection();
                    foreach (var chimeraGroup in fileGroup.GroupBy(p => p, CustomComparer<PsmFromTsv>.ChimeraComparer)
                                 .Select(p => p.ToArray()))
                    {
                        var record = new ChimeraBreakdownRecord()
                        {
                            Dataset = specificResult.DatasetName,
                            FileName = chimeraGroup.First().FileNameWithoutExtension.Replace("-calib", "")
                                .Replace("-averaged", ""),
                            Condition = specificResult.Condition,
                            Ms2ScanNumber = chimeraGroup.First().Ms2ScanNumber,
                            Type = ChimeraBreakdownType.Psm,
                            IdsPerSpectra = chimeraGroup.Length,
                        };

                        PsmFromTsv parent = null;
                        if (chimeraGroup.Length != 1)
                        {
                            var ms2Scan =
                                dataFile.GetOneBasedScanFromDynamicConnection(chimeraGroup.First().Ms2ScanNumber);
                            var isolationMz = ms2Scan.IsolationMz;
                            if (isolationMz == null)
                            {
                                Debugger.Break();
                                record.Parent--;
                                continue;
                            }

                            foreach (var chimericPsm in chimeraGroup
                                         .OrderBy(p => Math.Abs(p.PrecursorMz - (double)isolationMz))
                                         .ThenByDescending(p => p.Score))
                                if (parent is null)
                                    parent = chimericPsm;
                                else if (parent.BaseSeq == chimericPsm.BaseSeq)
                                    record.UniqueForms++;
                                else
                                    record.UniqueProteins++;
                        }

                        chimeraBreakDownRecords.Add(record);
                    }

                    dataFile.CloseDynamicConnection();
                }

                foreach (var fileGroup in PsmTsvReader.ReadTsv(specificResult._peptidePath, out _)
                             .Where(p => p.PEP_QValue <= 0.01 && p.DecoyContamTarget == "T")
                             .GroupBy(p => p.FileNameWithoutExtension))
                {
                    var dataFilePath = Directory.GetFiles(helaDataPath).FirstOrDefault(p => p.Contains(fileGroup.Key));
                    if (dataFilePath == null)
                        throw new Exception();
                    MsDataFile dataFile = MsDataFileReader.GetDataFile(dataFilePath);
                    dataFile.InitiateDynamicConnection();
                    foreach (var chimeraGroup in fileGroup.GroupBy(p => p, CustomComparer<PsmFromTsv>.ChimeraComparer)
                                 .Select(p => p.ToArray()))
                    {
                        var record = new ChimeraBreakdownRecord()
                        {
                            Dataset = specificResult.DatasetName,
                            FileName = chimeraGroup.First().FileNameWithoutExtension.Replace("-calib", "")
                                .Replace("-averaged", ""),
                            Condition = specificResult.Condition,
                            Ms2ScanNumber = chimeraGroup.First().Ms2ScanNumber,
                            Type = ChimeraBreakdownType.Psm,
                            IdsPerSpectra = chimeraGroup.Length,
                        };

                        PsmFromTsv parent = null;
                        if (chimeraGroup.Length != 1)
                        {
                            var ms2Scan =
                                dataFile.GetOneBasedScanFromDynamicConnection(chimeraGroup.First().Ms2ScanNumber);
                            var isolationMz = ms2Scan.IsolationMz;
                            if (isolationMz == null)
                            {
                                Debugger.Break();
                                continue;
                            }

                            foreach (var chimericPsm in chimeraGroup
                                         .OrderBy(p => Math.Abs(p.PrecursorMz - (double)isolationMz))
                                         .ThenByDescending(p => p.Score))
                                if (parent is null)
                                    parent = chimericPsm;
                                else if (parent.BaseSeq == chimericPsm.BaseSeq)
                                    record.UniqueForms++;
                                else
                                    record.UniqueProteins++;
                        }

                        chimeraBreakDownRecords.Add(record);
                    }

                    dataFile.CloseDynamicConnection();
                }

                file = new ChimeraBreakdownFile(outpath) { Results = chimeraBreakDownRecords };
                file.WriteResults(outpath);

            }
            else
            {
                file = new ChimeraBreakdownFile(outpath);
                file.LoadResults();
            }

            var psmChart =
                file.Results.GetChimeraBreakDownStackedColumn(ChimeraBreakdownType.Psm, false, out int width);
            var psmChartOutpath = @"B:\Users\Nic\Chimeras\Mann_11cell_analysis\Hela\SearchResults\MetaMorpheusWithLibrary\Hela_MetaMorpheusWithLibrary_PrecursorIsolation_ChimeraBreakdownComparison_PSM";
            psmChart.SavePNG(psmChartOutpath, null, width, 600);

            var peptideChart =
                file.Results.GetChimeraBreakDownStackedColumn(ChimeraBreakdownType.Peptide, false, out width);
            var peptideChartOutpath = @"B:\Users\Nic\Chimeras\Mann_11cell_analysis\Hela\SearchResults\MetaMorpheusWithLibrary\Hela_MetaMorpheusWithLibrary_PrecursorIsolation_ChimeraBreakdownComparison_Peptide";
            peptideChart.SavePNG(peptideChartOutpath, null, width, 600);

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





    }
}
