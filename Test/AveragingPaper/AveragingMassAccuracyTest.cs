﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.Providers.LinearAlgebra;
using System.Windows.Media.Media3D;
using NUnit.Framework;
using OxyPlot;
using SpectralAveraging;
using GuiFunctions;
using MassSpectrometry;
using Nett;
using Plotly.NET;
using Plotly.NET.CSharp;
using Plotly.NET.LayoutObjects;
using Chart = Plotly.NET.CSharp.Chart;
using Proteomics.RetentionTimePrediction;
using TaskLayer;
using GenericChartExtensions = Plotly.NET.CSharp.GenericChartExtensions;
using System.Windows.Controls;
using System.Xml.Serialization;
using Easy.Common.Extensions;
using Microsoft.FSharp.Core;
using Plotly.NET.TraceObjects;
using Proteomics;
using Readers;
using ThermoFisher.CommonCore.Data.Business;
using TopDownProteomics;
using Mzml = IO.MzML.Mzml;
using Range = System.Range;
using ThermoRawFileReader = IO.ThermoRawFileReader.ThermoRawFileReader;

namespace Test.AveragingPaper
{
    [TestFixture]
    public static class AveragingMassAccuracyTest
    {
        public const string SearchTomlPath =
            @"D:\Projects\SpectralAveraging\PaperMassAccuracyTest\Task1-SearchTaskconfig.toml";
        public const string StandardsDatabase =
            @"D:\Projects\Top Down MetaMorpheus\ChimeraValidation\Database Construction\customProtStandardDB8.xml";

        public const string hghPath =
            @"D:\Projects\SpectralAveraging\PaperMassAccuracyTest\Hgh\221110_HGHOnly_50IW.raw";

        public static double[] hghMzs = new[]
            { 2213.42, 2012.29, 1844.69, 1702.87, 1581.38, 1475.95, 1383.77, 1302.43, 1230.13, 1165.44, 1107.21 };

        public static int[] hghCharges = new[]
            { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };

        public static (int Charge, double Mz)[] hghChargeAndMz;

        public static List<SpectralAveragingParameters> AllParameters;

        [OneTimeSetUp]
        public static void OneTimeSetup()
        {
            // set up charge mz
            List<(int Charge, double Mz)> chargeMz = new List<(int Charge, double Mz)>();
            for (int i = 0; i < hghMzs.Length; i++)
            {
                int charge = hghCharges[i];
                double mz = hghMzs[i];
                chargeMz.Add((charge, mz));
            }
            hghChargeAndMz = chargeMz.ToArray();

            AllParameters = new();
            // set up all parameters
            var binSizes = new[]
                { 0.01 };
            //{ 1, 0.1, 0.01, 0.001 };
            var scansToAverage = new[]
                { 5, 10 };
            //{ 3, 5, 10, 15, 20 };
            var outlier = new[]
            {
                OutlierRejectionType.AveragedSigmaClipping,
                OutlierRejectionType.SigmaClipping,
                OutlierRejectionType.WinsorizedSigmaClipping,
                //OutlierRejectionType.MinMaxClipping,
                //OutlierRejectionType.NoRejection
            };
            var sigmas = new[]
                { 0.1, 0.2, 1.1, 1.2, 1.5, 2.1, 2.2, 2.5, 3, 3.1, 3.2, 3.5 };
            //{ 0.5, 1.0, 1.5, 2.0, 2.5, 3.0 };
            //var percentiles = new[] 
            //    { 0.5, 0.6, 0.7, 0.8, 0.9 };

            foreach (var binSize in binSizes)
            {
                foreach (var scanCount in scansToAverage)
                {
                    foreach (var rejectionType in outlier)
                    {
                        switch (rejectionType)
                        {
                            case OutlierRejectionType.NoRejection:
                            case OutlierRejectionType.MinMaxClipping:
                            case OutlierRejectionType.BelowThresholdRejection:
                                AllParameters.Add(new SpectralAveragingParameters()
                                {
                                    BinSize = binSize,
                                    NumberOfScansToAverage = scanCount,
                                    ScanOverlap = scanCount - 1,
                                    NormalizationType = NormalizationType.RelativeToTics,
                                    SpectralWeightingType = SpectraWeightingType.WeightEvenly,
                                    OutlierRejectionType = rejectionType,
                                    OutputType = OutputType.MzML,
                                    SpectraFileAveragingType = SpectraFileAveragingType.AverageDdaScansWithOverlap,
                                    SpectralAveragingType = SpectralAveragingType.MzBinning,
                                    MaxThreadsToUsePerFile = 10,
                                });
                                break;
                            case OutlierRejectionType.PercentileClipping:
                            //AllParameters.AddRange(percentiles.Select(percentile => new SpectralAveragingParameters()
                            //{
                            //    BinSize = binSize,
                            //    NumberOfScansToAverage = scanCount,
                            //    ScanOverlap = scanCount - 1,
                            //    NormalizationType = NormalizationType.RelativeToTics,
                            //    SpectralWeightingType = SpectraWeightingType.WeightEvenly,
                            //    OutlierRejectionType = rejectionType,
                            //    OutputType = OutputType.MzML,
                            //    SpectraFileAveragingType = SpectraFileAveragingType.AverageDdaScansWithOverlap,
                            //    SpectralAveragingType = SpectralAveragingType.MzBinning,
                            //    MaxThreadsToUsePerFile = 10,
                            //    Percentile = percentile,
                            //}));
                            //break;
                            case OutlierRejectionType.SigmaClipping:
                            case OutlierRejectionType.WinsorizedSigmaClipping:
                            case OutlierRejectionType.AveragedSigmaClipping:
                                AllParameters.AddRange(from outerSigma in sigmas
                                                       from innerSigma in sigmas
                                                       let minSigma = outerSigma
                                                       let maxSigma = innerSigma
                                                       select new SpectralAveragingParameters()
                                                       {
                                                           BinSize = binSize,
                                                           NumberOfScansToAverage = scanCount,
                                                           ScanOverlap = scanCount - 1,
                                                           NormalizationType = NormalizationType.RelativeToTics,
                                                           SpectralWeightingType = SpectraWeightingType.WeightEvenly,
                                                           OutlierRejectionType = rejectionType,
                                                           OutputType = OutputType.MzML,
                                                           SpectraFileAveragingType = SpectraFileAveragingType.AverageDdaScansWithOverlap,
                                                           SpectralAveragingType = SpectralAveragingType.MzBinning,
                                                           MaxThreadsToUsePerFile = 10,
                                                           MinSigmaValue = minSigma,
                                                           MaxSigmaValue = maxSigma,
                                                       });
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }

                }
            }
        }


        [Test]
        public static void MainRunner()
        {
            // TODO: Turn on override and match all
            AveragingMassAccuracyTestRunner accuracyTest = new(hghPath, hghChargeAndMz, AllParameters,
                overwrite: false, ppmTolerance: 10, relativeIntensityCutoff: 5);
            //accuracyTest.SetUpOutputDirectories();
            accuracyTest.AverageSpectra();
            accuracyTest.MatchAll();
            accuracyTest.SearchData(StandardsDatabase, SearchTomlPath);

            string mainDirectory = @"D:\Projects\SpectralAveraging\PaperMassAccuracyTest\Hgh";
            var outputs = Directory.GetDirectories(mainDirectory);

            List<FileMassAccuracyResults> results = new();
            foreach (var output in GetCompleted(outputs))
            {
                // load
                var files = Directory.GetFiles(output);
                var parameters = Toml.ReadFile<SpectralAveragingParameters>(files.First(p => p.Contains(".toml")),
                    MetaMorpheusTask.tomlConfig);
                var scanResults =
                    ScanMassAccuracyResults.LoadResultsListFromTsv(files.First(p => p.Contains("ScanMass")));
                var fileResults = new FileMassAccuracyResults(scanResults.ToList(), parameters);

                if (fileResults.PsmCount == 0)
                {
                    var count = File.ReadAllLines(files.First(p => p.EndsWith(".txt")))
                        .First(m => m.Contains("All target PSMS"))
                        .Split(":")[1]
                        .Trim();
                    fileResults.PsmCount = int.Parse(count);
                }

                results.Add(fileResults);
            }

            string fileOutPath = @"D:\Projects\SpectralAveraging\PaperMassAccuracyTest\HghAllFileResultsExpandedSigmas.tsv";
            Enumerable.Select(results, p => (ITsv)p).ExportAsTsv(fileOutPath);
        }




        [Test]
        public static void AverageLisaStuff()
        {
            string lisaDirectory =
                @"B:\Users\Whitworth\Raw Mass Spec Data\Mass Spec Data 2023\LN\032223_SC_PEPPI_soni\2023-03-22 SC PEPPI Soni";
            var files = Directory.GetFiles(lisaDirectory);

            string averagedSigmaDirectory = Path.Combine(lisaDirectory, "AveragedSigmaClipping");
            Directory.CreateDirectory(averagedSigmaDirectory);
            SpectralAveragingParameters parameters = new SpectralAveragingParameters()
            {
                SpectraFileAveragingType = SpectraFileAveragingType.AverageDdaScansWithOverlap,
                SpectralAveragingType = SpectralAveragingType.MzBinning,
                OutlierRejectionType = OutlierRejectionType.AveragedSigmaClipping,
                NormalizationType = NormalizationType.AbsoluteToTic,
                SpectralWeightingType = SpectraWeightingType.TicValue,
                NumberOfScansToAverage = 5,
                BinSize = 0.01,
                MinSigmaValue = 1,
                MaxSigmaValue = 3,
                ScanOverlap = 4,
                MaxThreadsToUsePerFile = 10,
            };

            foreach (var file in files)
            {
                var sourceFile = MsDataFileReader.GetDataFile(file).GetSourceFile();
                var scans = ThermoRawFileReader.LoadAllStaticData(file).GetAllScansList();
                var averagedScans = SpectraFileAveraging.AverageSpectraFile(scans, parameters);

                MsDataFile dataFile = new GenericMsDataFile(averagedScans, sourceFile);
                var outputPath = Path.Combine(averagedSigmaDirectory,
                    Path.GetFileNameWithoutExtension(file) + "-averaged.mzML");
                MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(dataFile, outputPath, true);
            }


        }


        #region Checkpoint Tests

        [Test]
        public static void TestParameterConstructionIsDistinct()
        {
            var distinct = AllParameters.Distinct();
            Assert.That(AllParameters.Count(), Is.EqualTo(distinct.Count()));
        }

        [Test]
        public static void TestRunnerDictionaryConstruction()
        {
            AveragingMassAccuracyTestRunner accuracyTest = new(hghPath, hghChargeAndMz, AllParameters);
            var distinctStrings = accuracyTest.allParameters.Keys.Distinct();
            Assert.That(distinctStrings.Count(), Is.EqualTo(AllParameters.Count));
        }

        [Test]
        public static void TestBasicTomlReadWrite()
        {
            var repParam = AllParameters.First();
            string dummyOutPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "averagingTestToml.toml");

            string toml = Toml.WriteString(repParam, MetaMorpheusTask.tomlConfig);
            Toml.WriteFile(repParam, dummyOutPath, MetaMorpheusTask.tomlConfig);
            Assert.That(File.Exists(dummyOutPath));
            Assert.That(toml, Is.EqualTo(File.ReadAllText(dummyOutPath)));

            var readInParams = Toml.ReadFile<SpectralAveragingParameters>(dummyOutPath, MetaMorpheusTask.tomlConfig);
            Assert.That(readInParams.OutlierRejectionType, Is.EqualTo(OutlierRejectionType.AveragedSigmaClipping));
            Assert.That(readInParams.SpectralWeightingType, Is.EqualTo(SpectraWeightingType.WeightEvenly));
            Assert.That(readInParams.NormalizationType, Is.EqualTo(NormalizationType.NoNormalization));
            Assert.That(readInParams.SpectralAveragingType, Is.EqualTo(SpectralAveragingType.MzBinning));
            Assert.That(readInParams.SpectraFileAveragingType, Is.EqualTo(SpectraFileAveragingType.AverageDdaScansWithOverlap));
            Assert.That(readInParams.OutputType, Is.EqualTo(OutputType.MzML));
            Assert.That(readInParams.Percentile, Is.EqualTo(0.1));
            Assert.That(readInParams.MinSigmaValue, Is.EqualTo(0.5));
            Assert.That(readInParams.MaxSigmaValue, Is.EqualTo(0.5));
            Assert.That(readInParams.BinSize, Is.EqualTo(1.0));
            Assert.That(readInParams.NumberOfScansToAverage, Is.EqualTo(3));
            Assert.That(readInParams.ScanOverlap, Is.EqualTo(2));
            Assert.That(readInParams.MaxThreadsToUsePerFile, Is.EqualTo(10));

            File.Delete(dummyOutPath);
            Assert.That(!File.Exists(dummyOutPath));
        }

        [Test]
        public static void TestOutputDirectoryCreation()
        {
            string testDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, @"DatabaseTests");
            string testPath = Path.Combine(testDirectory, "sliced_b6.mzML");
            List<SpectralAveragingParameters> paramsToTest = new()
            {
                new SpectralAveragingParameters() { BinSize = 1 },
                new SpectralAveragingParameters() { BinSize = 2 },
                new SpectralAveragingParameters() { BinSize = 3 },
            };
            AveragingMassAccuracyTestRunner accuracyTest = new(testPath, hghChargeAndMz, paramsToTest);
            Assert.That(accuracyTest.mainOutDirectory, Is.EqualTo(testDirectory));

            accuracyTest.SetUpOutputDirectories();

            var directories = Directory.GetDirectories(testDirectory);
            Assert.That(paramsToTest.Count, Is.EqualTo(directories.Length));
            foreach (var directory in directories)
            {
                Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
                Directory.Delete(directory, true);
            }
            Assert.That(Directory.GetDirectories(testDirectory).Length, Is.EqualTo(0));
        }

        [Test]
        public static void TestAveragingWithUpFrontDirectoryCreation()
        {
            string testDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, @"DatabaseTests");
            string testPath = Path.Combine(testDirectory, "sliced_b6.mzML");
            List<SpectralAveragingParameters> paramsToTest = new()
            {
                new SpectralAveragingParameters() { BinSize = 1 },
                new SpectralAveragingParameters() { BinSize = 2 },
                new SpectralAveragingParameters() { BinSize = 3 },
            };
            AveragingMassAccuracyTestRunner accuracyTest = new(testPath, hghChargeAndMz, paramsToTest);
            Assert.That(accuracyTest.mainOutDirectory, Is.EqualTo(testDirectory));

            accuracyTest.SetUpOutputDirectories();
            accuracyTest.AverageSpectra();

            var directories = Directory.GetDirectories(testDirectory);
            Assert.That(paramsToTest.Count, Is.EqualTo(directories.Length));
            foreach (var directory in directories)
            {
                var files = Directory.GetFiles(directory);

                Assert.That(files.Length, Is.EqualTo(2));
                Assert.That(files.Any(p => p.EndsWith(".mzML")));
                Assert.That(files.Any(p => p.EndsWith(".toml")));

                Directory.Delete(directory, true);
            }
            Assert.That(Directory.GetDirectories(testDirectory).Length, Is.EqualTo(0));
        }

        [Test]
        public static void TestAveragingWithOutUpFrontDirectoryCreation()
        {
            string testDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, @"DatabaseTests");
            string testPath = Path.Combine(testDirectory, "sliced_b6.mzML");
            List<SpectralAveragingParameters> paramsToTest = new()
            {
                new SpectralAveragingParameters() { BinSize = 1 },
                new SpectralAveragingParameters() { BinSize = 2 },
                new SpectralAveragingParameters() { BinSize = 3 },
            };
            AveragingMassAccuracyTestRunner accuracyTest = new(testPath, hghChargeAndMz, paramsToTest);
            Assert.That(accuracyTest.mainOutDirectory, Is.EqualTo(testDirectory));

            accuracyTest.AverageSpectra();

            var directories = Directory.GetDirectories(testDirectory);
            Assert.That(paramsToTest.Count, Is.EqualTo(directories.Length));
            foreach (var directory in directories)
            {
                var files = Directory.GetFiles(directory);

                Assert.That(files.Length, Is.EqualTo(2));
                Assert.That(files.Any(p => p.EndsWith(".mzML")));
                Assert.That(files.Any(p => p.EndsWith(".toml")));

                Directory.Delete(directory, true);
            }
            Assert.That(Directory.GetDirectories(testDirectory).Length, Is.EqualTo(0));

        }

        [Test]
        public static void TestAveragingWithOutUpFrontDirectoryCreationWithoutReWriting()
        {
            string testDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, @"DatabaseTests");
            string testPath = Path.Combine(testDirectory, "sliced_b6.mzML");
            List<SpectralAveragingParameters> paramsToTest = new()
            {
                new SpectralAveragingParameters() { BinSize = 1 },
                new SpectralAveragingParameters() { BinSize = 2 },
                new SpectralAveragingParameters() { BinSize = 3 },
            };
            AveragingMassAccuracyTestRunner accuracyTest = new(testPath, hghChargeAndMz, paramsToTest);
            Assert.That(accuracyTest.mainOutDirectory, Is.EqualTo(testDirectory));

            accuracyTest.AverageSpectra();

            var directories = Directory.GetDirectories(testDirectory);
            Assert.That(paramsToTest.Count, Is.EqualTo(directories.Length));
            foreach (var directory in directories)
            {
                var files = Directory.GetFiles(directory);

                Assert.That(files.Length, Is.EqualTo(2));
                Assert.That(files.Any(p => p.EndsWith(".mzML")));
                Assert.That(files.Any(p => p.EndsWith(".toml")));
            }

            accuracyTest.AverageSpectra();

            directories = Directory.GetDirectories(testDirectory);
            Assert.That(paramsToTest.Count, Is.EqualTo(directories.Length));
            foreach (var directory in directories)
            {
                var files = Directory.GetFiles(directory);

                Assert.That(files.Length, Is.EqualTo(2));
                Assert.That(files.Any(p => p.EndsWith(".mzML")));
                Assert.That(files.Any(p => p.EndsWith(".toml")));

                Directory.Delete(directory, true);
            }
            Assert.That(Directory.GetDirectories(testDirectory).Length, Is.EqualTo(0));
        }



        #endregion

        [Test]
        public static void GetAmountDone()
        {
            string mainDirectory = @"D:\Projects\SpectralAveraging\PaperMassAccuracyTest\Hgh";
            var outputs = Directory.GetDirectories(mainDirectory);

            int averaged = 0;
            int toml = 0;
            int scored = 0;
            int searched = 0;
            int total = outputs.Length;
            foreach (var output in outputs)
            {
                if (Directory.GetFiles(output).Any(p => p.Contains(".mzML")))
                    averaged++;
                if (Directory.GetFiles(output).Any(p => p.Contains(".toml")))
                    toml++;
                if (Directory.GetFiles(output).Count(p => p.Contains(".tsv")) == 2)
                    scored++;
                if (Directory.GetFiles(output).Count(p => p.Contains(".txt")) == 1)
                    searched++;
            }

            // 230328 3:20 pm 4343/9280 averaged
            // 230328 3:42 pm 4371/9280 averaged
            // 230329 10:23 am 5912/9280 averaged
            // 230329 11:31 am 5995/9280 averaged
            // 230329 2:41 pm 6118/9280 averaged
            // 230330 1:14 pm 6708/9280 averaged
            // 230330 3:23 pm 6749/9280 averaged
            // 230331 10:52 am 8106/9280 averaged
            // 230331 3:14 pm 8307/9280 averaged
            // 230401 10:00 am 8461/9280 averaged
            // 230401 2:20 pm 8591/9280 averaged
            // 230401 4:25 pm 8661/9280 averaged
            // 230402 9:54 am 8857/9280 averaged
        }

        [Test]
        public static void GenerateFinalFile()
        {
            string mainDirectory = @"D:\Projects\SpectralAveraging\PaperMassAccuracyTest\Hgh";
            var outputs = Directory.GetDirectories(mainDirectory);

            List<FileMassAccuracyResults> results = new();
            foreach (var output in GetCompleted(outputs))
            {
                // load
                var files = Directory.GetFiles(output);
                var parameters = Toml.ReadFile<SpectralAveragingParameters>(files.First(p => p.Contains(".toml")),
                    MetaMorpheusTask.tomlConfig);
                var scanResults =
                    ScanMassAccuracyResults.LoadResultsListFromTsv(files.First(p => p.Contains("ScanMass")));
                var fileResults = new FileMassAccuracyResults(scanResults.ToList(), parameters);

                if (fileResults.PsmCount == 0)
                {
                    var count = File.ReadAllLines(files.First(p => p.EndsWith(".txt")))
                        .First(m => m.Contains("All target PSMS"))
                        .Split(":")[1]
                        .Trim();
                    fileResults.PsmCount = int.Parse(count);
                }

                results.Add(fileResults);
            }

            string fileOutPath = @"D:\Projects\SpectralAveraging\PaperMassAccuracyTest\HghAllFileResultsExpandedSigmas.tsv";
            Enumerable.Select(results, p => (ITsv)p).ExportAsTsv(fileOutPath);
        }

        [Test]
        public static void TestIdk()
        {
            string originalScanResultsPath = @"D:\Projects\SpectralAveraging\PaperMassAccuracyTest\Hgh\0riginal\OriginalSpectraScanMassAccuracyResults.tsv";
            var originalScanResults = ScanMassAccuracyResults.LoadResultsListFromTsv(originalScanResultsPath).ToList();
            var originalFileResults = new FileMassAccuracyResults(originalScanResults, null);
            var count = File.ReadAllLines(@"D:\Projects\SpectralAveraging\PaperMassAccuracyTest\Hgh\0riginal\allResults.txt")
                .First(m => m.Contains("All target PSM"))
                .Split(":")[1]
                .Trim();
            originalFileResults.PsmCount = int.Parse(count);

            string fileTsvPath = @"D:\Projects\SpectralAveraging\PaperMassAccuracyTest\HghAllFileResultsExpandedSigmas.tsv";
            var fileResults = FileMassAccuracyResults.LoadResultFromTsv(fileTsvPath)
                .Where(p =>
                    Math.Abs(p.Parameters.BinSize - 0.01) < 0.00001
                    //&& p.Parameters.NumberOfScansToAverage == 10)
                    && p.Parameters.SpectralWeightingType == SpectraWeightingType.WeightEvenly
                    && p.Parameters.NormalizationType == NormalizationType.RelativeToTics
                ).ToList();

            var analyzer = new AveragingMassAccuracyTestAnalyzer(fileResults, originalFileResults);
            List<GenericChart.GenericChart> charts = new();

            //box and whisker
            //foreach (var groupingType in Enum.GetValues<GroupingType>())
            //{
            //    if (groupingType == GroupingType.Normalization || groupingType == GroupingType.Weighting) continue;
            //    string title = $" Weighted Envenly, Normalized to Tic, Bin Size = 0.01 by {groupingType}";
            //    var resultTypes = Enum.GetValues<ResultType>();
            //    var grid = analyzer.CompoundBoxAndWhisker(resultTypes.ToList(), groupingType, title);
            //    GenericChartExtensions.Show(grid);

            //}

            // Heat Map Sigma Values
            analyzer.HeatMapSigmaValues(ResultType.PsmCount);
            var compoundHeatmap = analyzer.CompoundHeatMapSigmaValues(Enum.GetValues<ResultType>().ToList());
            GenericChartExtensions.Show(compoundHeatmap);

        }

        [Test]
        public static void GetSigmaHeatmapValues()
        {

            string originalScanResultsPath = @"D:\Projects\SpectralAveraging\PaperMassAccuracyTest\Hgh\0riginal\OriginalSpectraScanMassAccuracyResults.tsv";
            var originalScanResults = ScanMassAccuracyResults.LoadResultsListFromTsv(originalScanResultsPath).ToList();
            var originalFileResults = new FileMassAccuracyResults(originalScanResults, null);
            var count = File.ReadAllLines(@"D:\Projects\SpectralAveraging\PaperMassAccuracyTest\Hgh\0riginal\allResults.txt")
                .First(m => m.Contains("All target PSM"))
                .Split(":")[1]
                .Trim();
            originalFileResults.PsmCount = int.Parse(count);

            string fileTsvPath = @"D:\Projects\SpectralAveraging\PaperMassAccuracyTest\HghAllFileResultsExpandedSigmas.tsv";
            var fileResults = FileMassAccuracyResults.LoadResultFromTsv(fileTsvPath)
                .Where(p =>
                    Math.Abs(p.Parameters.BinSize - 0.01) < 0.00001
                    //&& p.Parameters.NumberOfScansToAverage == 10)
                    && p.Parameters.SpectralWeightingType == SpectraWeightingType.WeightEvenly
                    && p.Parameters.NormalizationType == NormalizationType.RelativeToTics
                ).ToList();

            var analyzer = new AveragingMassAccuracyTestAnalyzer(fileResults, originalFileResults);
            var sigmaResults = analyzer.GetHeatmapSigmaValues(OutlierRejectionType.AveragedSigmaClipping);

            string outPath = Path.Combine(ResultPaths.OutDirectory, "AveragedSigmaHeatmapValues.csv");
            using (var sw = new StreamWriter(File.Create(outPath)))
            {
                sw.WriteLine("Max Sigma,Min Sigma,Ppm Error,Mz Found,Deconvoluted Features");
                foreach (var result in sigmaResults)
                {
                    sw.WriteLine($"{result.MaxSigma},{result.MinSigma},{result.PpmError},{result.MzFound},{result.DeconPeaks}");
                }
            }

        }



        [Test]
        public static void TestOutputs()
        {
            string mainDirectory = @"D:\Projects\SpectralAveraging\PaperMassAccuracyTest\Hgh";
            var outputs = Directory.GetDirectories(mainDirectory);
            var relevant = outputs
                .Where(p => Directory.GetFiles(p)
                    .Any(m => m.Contains(".mzML")));
            var top3 = relevant.Take(3).ToList();

            List<SpectralAveragingParameters> parameters = new();
            foreach (var folder in top3)
            {
                var toml = Directory.GetFiles(folder).First(p => p.Contains(".toml"));
                var param = Toml.ReadFile<SpectralAveragingParameters>(toml);
                parameters.Add(param);
            }

            AveragingMassAccuracyTestRunner runner = new(hghPath, hghChargeAndMz, parameters);
            runner.MatchAll();
        }



        private static IEnumerable<string> GetCompleted(string[] potential)
        {
            foreach (var directory in potential)
            {
                var files = Directory.GetFiles(directory);
                if (files.Count(p => p.EndsWith(".tsv")) == 2 &&
                    files.Count(p => p.EndsWith(".txt")) == 1 &&
                    files.Count(p => p.EndsWith(".toml")) == 1)
                    yield return directory;
            }
        }



        private static List<MzPeak> GetPeaks(string specPath, double minMz, double maxMz)
        {
            return Mzml.LoadAllStaticData(specPath).GetMS1Scans()
                .SelectMany(p => p.MassSpectrum
                    .Extract(minMz, maxMz))
                .OrderBy(p => p.Mz).ToList();
        }

        private static IEnumerable<MzPeak> GetPeaksRelativeIntensityByRegion(string specPath, double minMz, double maxMz)
        {
            List<MsDataScan> ms1Scans = new();
            try
            {
                ms1Scans = Mzml.LoadAllStaticData(specPath).GetMS1Scans().ToList();
            }
            catch
            {
                ms1Scans = ThermoRawFileReader.LoadAllStaticData(specPath).GetMS1Scans().ToList();
            }



            foreach (var scan in ms1Scans)
            {
                var peaks = scan.MassSpectrum.Extract(minMz, maxMz).ToList();
                if (!peaks.Any()) continue;

                var maxIntensityPeak = peaks.Max(p => p.Intensity);
                var maxIntensityScan = scan.MassSpectrum.YArray.Max();
                foreach (var peak in peaks)
                {
                    yield return new MzPeak(peak.Mz, peak.Intensity / maxIntensityPeak);
                }
            }
        }

        private static IEnumerable<MzPeak> GetPeaksRelativeIntensity(List<MsDataScan> scans, double minMz, double maxMz)
        {
            foreach (var scan in scans)
            {
                var peaks = scan.MassSpectrum.Extract(minMz, maxMz).ToList();
                if (!peaks.Any()) continue;

                var maxIntensityPeak = peaks.Max(p => p.Intensity);
                var maxIntensityScan = scan.MassSpectrum.YArray.Max();
                foreach (var peak in peaks)
                {
                    yield return new MzPeak(peak.Mz, peak.Intensity / maxIntensityScan);
                }
            }
        }


        [Test]
        public static void SetUpInputsForMzInHeatMap()
        {
            string controlSpecPath = @"B:\Users\Nic\ChimeraValidation\SingleStandards\221110_HGHOnly_50IW.mzML";
            string averagedSpecPath =
                @"D:\Projects\SpectralAveraging\TestOutputs\PaperMassAccuracyTest\Hgh\10Scans_0.01_RelativeToTics_WeightEvenly_AveragedSigmaClipping_0.5_3.2\221110_HGHOnly_50IW-averaged.mzML";

            List<MsDataScan> controlScans = Mzml.LoadAllStaticData(controlSpecPath).GetMS1Scans().ToList();
            List<MsDataScan> averagedScans = Mzml.LoadAllStaticData(averagedSpecPath).GetMS1Scans().ToList();

            // heuristics
            int xSquares = 100;
            int ySquares = 100;

            // 2213.42, 2012.29, 1844.69, 1702.87, 1581.38, 1475.95, 1383.77, 1302.43, 1230.13, 2213.42, 1107.21
            (double minMz, double maxMz)[] mzRanges = new[]
            {
                (1106.71, 1107.71),
                (1229.7, 1230.7),
                (1301.93, 1302.93),
                (1383.07, 1384.47),
                (1475.45, 1476.45),
                (1580.88, 1581.88),
                (1702.17, 1703.57),
                (2011.29, 2013.29),
                (2211.92, 2214.92),
            };

            List<GenericChart.GenericChart> charts = new();
            for (var i = 0; i < mzRanges.Length; i++)
            {
                var range = mzRanges[i];
                var control =
                    CreateMzIntHeatmapSingle(controlScans, xSquares, ySquares, range.minMz, range.maxMz, "Control")
                        .WithAxisAnchor(0, i);
                var averaged = CreateMzIntHeatmapSingle(averagedScans, xSquares, ySquares, range.minMz, range.maxMz, "Averaged")
                    .WithAxisAnchor(1, i);

                charts.Add(control);
                charts.Add(averaged);
                //GenericChartExtensions.Show(chart);
            }

            var bigCombined = Chart.Grid(charts, 9, 2)
                .WithTitle("Relative To Scan - Absolute")
                .WithSize(600, 2200);
            GenericChartExtensions.Show(bigCombined);
        }


        [Test]
        public static void OutputMzIntHeatmapValues()
        {

            string controlSpecPath = @"B:\Users\Nic\ChimeraValidation\SingleStandards\221110_HGHOnly_50IW.mzML";
            string averagedSpecPath =
                @"D:\Projects\SpectralAveraging\TestOutputs\PaperMassAccuracyTest\Hgh\10Scans_0.01_RelativeToTics_WeightEvenly_AveragedSigmaClipping_0.5_3.2\221110_HGHOnly_50IW-averaged.mzML";

            List<MsDataScan> controlScans = Mzml.LoadAllStaticData(controlSpecPath).GetMS1Scans().ToList();
            List<MsDataScan> averagedScans = Mzml.LoadAllStaticData(averagedSpecPath).GetMS1Scans().ToList();

            // heuristics
            int xSquares = 100;
            int ySquares = 100;

            // 2213.42, 2012.29, 1844.69, 1702.87, 1581.38, 1475.95, 1383.77, 1302.43, 1230.13, 2213.42, 1107.21
            (double minMz, double maxMz, bool log)[] mzRanges = new[]
            {
                (1106.71, 1107.71, false),
                (1301.93, 1302.93, false),
                (1580.88, 1581.88, false),
                (2011.29, 2013.29, false),
                (2211.92, 2214.92, true),
                (1106.71, 1107.71, true)
            };

            for (int i = 0; i < mzRanges.Length; i++)
            {
                var controlPeaks = GetPeaksRelativeIntensity(controlScans, mzRanges[i].minMz, mzRanges[i].maxMz).ToList();
                var averagedPeaks = GetPeaksRelativeIntensity(averagedScans, mzRanges[i].minMz, mzRanges[i].maxMz).ToList();

                // calculated values for graph
                double mzLength = mzRanges[i].maxMz - mzRanges[i].minMz;
                double xSize = mzLength / xSquares;
                double maxIntensity = controlPeaks.Max(p => p.Intensity) > averagedPeaks.Max(p => p.Intensity)
                    ? controlPeaks.Max(p => p.Intensity) : averagedPeaks.Max(p => p.Intensity);
                double ySize = maxIntensity / ySquares;


                // bin into defined bin size
                var zControl = new double[ySquares][];
                var zAveraged = new double[ySquares][];
                var x = new double[xSquares];
                var y = new double[ySquares];

                var outlist = new List<(string Group, double Mz, double Intensity, double Count)>();
                int index = 0;
                for (double d = 0; d < maxIntensity; d += ySize)
                {
                    if (index >= y.Length) continue;
                    y[index] = d;
                    zControl[index] = new double[xSquares];
                    zAveraged[index] = new double[xSquares];

                    var innerIndex = 0;
                    for (double j = mzRanges[i].minMz; Math.Round(j, 4) < mzRanges[i].maxMz; j += xSize)
                    {
                        if (innerIndex >= y.Length) continue;

                        x[innerIndex] = j;
                        var zControlCount = controlPeaks.Count(
                            p => p.Mz >= j && p.Mz < j + xSize &&
                                 p.Intensity >= d && p.Intensity < d + ySize);
                        var zAveragedCount = averagedPeaks.Count(
                            p => p.Mz >= j && p.Mz < j + xSize &&
                                 p.Intensity >= d && p.Intensity < d + ySize);

                        if (mzRanges[i].log)
                        {
                            zControl[index][innerIndex] = zControlCount == 0 ? 0 : Math.Log(zControlCount, 2);
                            zAveraged[index][innerIndex] = zAveragedCount == 0 ? 0 : Math.Log(zAveragedCount, 2);
                        }
                        else
                        {
                            zControl[index][innerIndex] = zControlCount;
                            zAveraged[index][innerIndex] = zAveragedCount;
                        }

                        outlist.Add(new("Control", x[innerIndex], y[index], zControl[index][innerIndex]));
                        outlist.Add(new("Averaged", x[innerIndex], y[index], zAveraged[index][innerIndex]));
                        innerIndex++;
                    }
                    index++;
                }
                string outDirectory = @"D:\Projects\SpectralAveraging\TestOutputs\PaperIntensityHeatmaps";
                string outPath = Path.Combine(outDirectory, $"MzIntensityHeatmap_ByScan_log={mzRanges[i].log}_{mzRanges[i].minMz}-{mzRanges[i].maxMz}.csv");

                using (var sw = new StreamWriter(File.Create(outPath)))
                {
                    sw.WriteLine("Group,Mz,Intensity,Count");
                    foreach (var peak in outlist.Where(p => p.Intensity != 0))
                    {
                        sw.WriteLine($"{peak.Group},{peak.Mz},{peak.Intensity},{peak.Count}");
                    }
                }
            }
        }



        public static GenericChart.GenericChart CreateMzIntHeatmapSingle(List<MsDataScan> spec, int xSquares, int ySquares,
            double minToExtract, double maxToExtract, string yLabel)
        {

            var peaks = GetPeaksRelativeIntensity(spec, minToExtract, maxToExtract).ToList();

            // calculated values for graph
            double mzLength = maxToExtract - minToExtract;
            double xSize = mzLength / xSquares;
            double maxIntensity = peaks.Max(p => p.Intensity);
            double ySize = maxIntensity / ySquares;


            // bin into defined bin size
            var z = new double[ySquares][];
            var x = new double[xSquares];
            var y = new double[ySquares];

            int index = 0;
            for (double i = 0; i < maxIntensity; i += ySize)
            {
                if (index >= y.Length) continue;
                y[index] = i;
                z[index] = new double[xSquares];
                int innerIndex = 0;
                for (double j = minToExtract; Math.Round(j, 4) < maxToExtract; j += xSize)
                {

                    var zCount = peaks.Count(
                        p => p.Mz >= j && p.Mz < j + xSize &&
                             p.Intensity >= i && p.Intensity < i + ySize);
                    x[innerIndex] = j;
                    z[index][innerIndex] = zCount /*== 0 ? 0 : Math.Log(zCount, 2)*/;
                    innerIndex++;
                }
                index++;
            }

            var zMin = 0;
            var zMax = z.SelectMany(p => p).Max();

            // create heatmaps
            z[0][0] = zMax;
            var heatmap = Chart.Contour<double, double, double, string>(
                    z, "Control", X: x, Y: y, ColorScale: GetColorScale())
                .WithTitle("Control")
                .WithXAxisStyle<double, double, string>(TitleText: new Optional<string>(yLabel, true))
                .WithZAxisStyle(Title.init("Peak Count"),
                    FSharpOption<Tuple<IConvertible, IConvertible>>.Some(
                        new Tuple<IConvertible, IConvertible>(zMin, zMax)));
            return heatmap;
        }



        [Test]
        public static GenericChart.GenericChart CreateMzIntHeatmap(List<MsDataScan> controlSpec, List<MsDataScan> averagedSpec, int xSquares, int ySquares, double minToExtract, double maxToExtract, int ColumnNumber)
        {

            // get all peaks within range
            var controlPeaks = GetPeaksRelativeIntensity(controlSpec, minToExtract, maxToExtract).ToList();
            var averagedPeaks = GetPeaksRelativeIntensity(averagedSpec, minToExtract, maxToExtract).ToList();

            // calculated values for graph
            double mzLength = maxToExtract - minToExtract;
            double xSize = mzLength / xSquares;
            double maxIntensity = controlPeaks.Max(p => p.Intensity) > averagedPeaks.Max(p => p.Intensity)
                ? controlPeaks.Max(p => p.Intensity) : averagedPeaks.Max(p => p.Intensity);
            double ySize = maxIntensity / ySquares;

            // bin into defined bin size
            var zControl = new double[ySquares][];
            var zAveraged = new double[ySquares][];
            var x = new double[xSquares];
            var y = new double[ySquares];

            int index = 0;
            for (double i = 0; i < maxIntensity; i += ySize)
            {
                y[index] = i;
                zControl[index] = new double[xSquares];
                zAveraged[index] = new double[xSquares];
                int innerIndex = 0;
                for (double j = minToExtract; Math.Round(j, 4) < maxToExtract; j += xSize)
                {
                    var zControlCount = controlPeaks.Count(
                        p => p.Mz >= j && p.Mz < j + xSize &&
                             p.Intensity >= i && p.Intensity < i + ySize);
                    var zAveragedCount = averagedPeaks.Count(
                        p => p.Mz >= j && p.Mz < j + xSize &&
                             p.Intensity >= i && p.Intensity < i + ySize);
                    x[innerIndex] = j;
                    zControl[index][innerIndex] = zControlCount /*== 0 ? 0 : Math.Log(zControlCount, 2)*/;
                    zAveraged[index][innerIndex] = zAveragedCount /*== 0 ? 0 : Math.Log(zAveragedCount, 2)*/;
                    innerIndex++;
                }
                index++;
            }

            // output each individual peak
            if (false)
            {
                string outDirectory = @"D:\Projects\SpectralAveraging\PaperIntensityHeatmaps";
                string averagedOut = Path.Combine(outDirectory, "averagedByScan.csv");
                string controlOut = Path.Combine(outDirectory, "controlByScan.csv");

                using (var sw = new StreamWriter(File.Create(averagedOut)))
                {
                    sw.WriteLine("Mz,Intensity");
                    foreach (var peak in averagedPeaks)
                        sw.WriteLine($"{peak.Mz},{peak.Intensity / maxIntensity}");
                }


                using (var sw = new StreamWriter(File.Create(controlOut)))
                {
                    sw.WriteLine("Mz,Intensity");
                    foreach (var peak in controlPeaks)
                        sw.WriteLine($"{peak.Mz},{peak.Intensity / maxIntensity}");
                }
            }

            // output after binned
            if (false)
            {
                string outDirectory = @"D:\Projects\SpectralAveraging\PaperIntensityHeatmaps";
                string averagedOut = Path.Combine(outDirectory, "averagedPeaksRelIntensityByScanNoZeroInt250.csv");
                string controlOut = Path.Combine(outDirectory, "controlpeaksRelIntensityByScanNoZeroInt250.csv");

                var outList = new List<(double Mz, double Intensity, double Count)>();
                index = 0;
                for (double i = 0; i < maxIntensity; i += ySize)
                {
                    int innerIndex = 0;
                    for (double j = minToExtract; Math.Round(j, 4) < maxToExtract; j += xSize)
                    {

                        outList.Add(new(x[innerIndex], y[index], zAveraged[index][innerIndex]));
                        innerIndex++;
                    }
                    index++;
                }
                using (var sw = new StreamWriter(File.Create(averagedOut)))
                {
                    sw.WriteLine("Mz,Intensity,Count");
                    foreach (var peak in outList.Where(p => p.Intensity != 0))
                    {
                        sw.WriteLine($"{peak.Mz},{peak.Intensity},{peak.Count}");
                    }
                }

                outList.Clear();
                index = 0;
                for (double i = 0; i < maxIntensity; i += ySize)
                {
                    int innerIndex = 0;
                    for (double j = minToExtract; Math.Round(j, 4) < maxToExtract; j += xSize)
                    {

                        outList.Add(new(x[innerIndex], y[index], zControl[index][innerIndex]));
                        innerIndex++;
                    }
                    index++;
                }
                using (var sw = new StreamWriter(File.Create(controlOut)))
                {
                    sw.WriteLine("Mz,Intensity,Count");
                    foreach (var peak in outList.Where(p => p.Intensity != 0))
                    {
                        sw.WriteLine($"{peak.Mz},{peak.Intensity},{peak.Count}");
                    }
                }
            }


            var zMin = 0;
            var zMax = zAveraged.SelectMany(p => p).Max();

            // create heatmaps
            zControl[0][0] = zMax;
            var controlHeatmap = Chart.Contour<double, double, double, string>(
                    zControl, "Control", X: x, Y: y, ColorScale: GetColorScale())
                .WithTitle("Control")
                .WithYAxisStyle<double, double, string>(TitleText: new Optional<string>("Control", true),
                    Anchor: new Optional<StyleParam.LinearAxisId>(StyleParam.LinearAxisId.NewX(ColumnNumber), true))
                .WithXAxisStyle<double, double, string>(Anchor: new Optional<StyleParam.LinearAxisId>(StyleParam.LinearAxisId.NewY(0), true))
                .WithZAxisStyle(Title.init("Peak Count"),
                    FSharpOption<Tuple<IConvertible, IConvertible>>.Some(
                        new Tuple<IConvertible, IConvertible>(zMin, zMax)));


            var averagedHeatmap = Chart.Contour<double, double, double, string>(
                    zAveraged, "Averaged", X: x, Y: y, ColorScale: GetColorScale())
                .WithTitle("Averaged")
                .WithYAxisStyle<double, double, string>(TitleText: new Optional<string>("Averaged", true),
                    Anchor: new Optional<StyleParam.LinearAxisId>(StyleParam.LinearAxisId.NewX(ColumnNumber), true))
                .WithXAxisStyle<double, double, string>(Anchor: new Optional<StyleParam.LinearAxisId>(StyleParam.LinearAxisId.NewY(1), true))
                .WithZAxisStyle(Title.init("Peak Count"),
                    FSharpOption<Tuple<IConvertible, IConvertible>>.Some(new Tuple<IConvertible, IConvertible>(zMin, zMax)));

            var combined = Chart.Grid(new List<GenericChart.GenericChart>() { controlHeatmap, averagedHeatmap }, 2, 1)
                .WithTitle($"Contour {minToExtract}-{maxToExtract} : xBins={xSquares} yBins ={ySquares}")
                .WithSize(600, 1200)
                .WithZAxisStyle(Title.init("Peak Count"),
                    FSharpOption<Tuple<IConvertible, IConvertible>>.Some(new Tuple<IConvertible, IConvertible>(zMin, zMax)));


            return combined;
            //GenericChartExtensions.Show(combined);
            //GenericChartExtensions.Show(controlHeatmap);
            //GenericChartExtensions.Show(averagedHeatmap);
        }

        private static StyleParam.Colorscale GetColorScale()
        {
            List<string> colorStrings = new()
            {
                "#092b8d",
                "#031a99",
                "#241993",
                "#53199b",
                "#9923a7",
                "#ae1a69",
                "#c11b40",
                "#da1d02",
                "#df2d05",
                "#dd5807",
                "#d88503",
                "#f1ad02",
                "#f5ca07",
                "#feea08",
                "#daed05",
                "#93c805",
                "#5abc05",
                "#39ab03"
            };

            var colors = colorStrings.Select(p => Color.fromHex(p)).ToArray();
            var colorTuple = new List<Tuple<double, Color>>();
            for (var index = 0; index < colors.Length; index++)
            {
                var color = colors[index];
                colorTuple.Add(new Tuple<double, Color>(index / ((double)colors.Length - 1), color));
            }

            var scale = StyleParam.Colorscale.NewCustom(colorTuple);
            return scale;
        }

        /// <summary>
        /// Creates The bottom component of figure one, showing the effect of averaging
        /// 5 unaveraged spectra zoomed in in a single peak, then one averaged spectra
        /// </summary>
        [Test]
        public static void CreateFigure1()
        {
            string fiveSpecPath = @"B:\Users\Nic\ChimeraValidation\SingleStandards\221110_HGHOnly_50IW.mzML";
            string averagedSpecPath =
                @"D:\Projects\SpectralAveraging\TestOutputs\PaperMassAccuracyTest\Hgh\10Scans_0.01_RelativeToTics_WeightEvenly_AveragedSigmaClipping_0.5_3.2\221110_HGHOnly_50IW-averaged.mzML";

            var fiveSpecFile = MsDataFileReader.GetDataFile(fiveSpecPath);
            fiveSpecFile.InitiateDynamicConnection();
            List<MsDataScan> fiveSpec = new List<MsDataScan>();
            for (int i = 1075; i < 1100; i++)
            {
                var scan = fiveSpecFile.GetOneBasedScanFromDynamicConnection(i);
                if (scan.MsnOrder == 1)
                    fiveSpec.Add(scan);
            }
            fiveSpecFile.CloseDynamicConnection();
            

            var averagedSpecFile = MsDataFileReader.GetDataFile(averagedSpecPath);
            averagedSpecFile.InitiateDynamicConnection();
            MsDataScan averagedSpec = averagedSpecFile.GetOneBasedScanFromDynamicConnection(1087);
            averagedSpecFile.CloseDynamicConnection();
            
            double minToExtract = 1229.7;
            double maxToExtract = 1230.6;
            var averagedPeaks = averagedSpec.MassSpectrum.Extract(minToExtract, maxToExtract).ToList();
            Dictionary<int, List<MzPeak>> peakDictionary = fiveSpec.ToDictionary(p => p.OneBasedScanNumber,
                p => p.MassSpectrum.Extract(minToExtract, maxToExtract).ToList());

            if (true)
            {
                averagedPeaks = new();
                foreach (var peak in averagedSpec?.MassSpectrum.Extract(minToExtract, maxToExtract))
                {
                    averagedPeaks.Add(new MzPeak(peak.Mz - 0.005, 0));
                    averagedPeaks.Add(peak);
                    averagedPeaks.Add(new MzPeak(peak.Mz + 0.005, 0));
                }
                //averagedPeaks.Add(new MzPeak(1230.2, 40000000));

                peakDictionary = new();
                foreach (var originalSpectrum in fiveSpec)
                {
                    List<MzPeak> newPeaks = new();
                    foreach (var peak in originalSpectrum.MassSpectrum.Extract(minToExtract, maxToExtract))
                    {
                        newPeaks.Add(new MzPeak(peak.Mz - 0.005, 0));
                        newPeaks.Add(peak);
                        newPeaks.Add(new MzPeak(peak.Mz + 0.005, 0));
                    }
                    //newPeaks.Add(new MzPeak(1230.2, 40000000));
                    peakDictionary.Add(originalSpectrum.OneBasedScanNumber, newPeaks);
                }
            }


            var tempChart = Chart.Column<double, double, string>(
                Enumerable.Select(averagedPeaks, p => p.Intensity),
                new Optional<IEnumerable<double>>(Enumerable.Select(averagedPeaks, p => p.Mz), true),
                Width: 0.005,
                MarkerColor: new Optional<Color>(Color.fromHex("#3084C0"), true)
                )
                .WithXAxisStyle(title: Title.init("m/z"))
                .WithYAxisStyle(title: Title.init("Intensity"))
                .WithTitle("Averaged Spectrum");
            //GenericChartExtensions.Show(tempChart);

            Queue<string> colorQueue = new();
            colorQueue.Enqueue("#fc0303");
            colorQueue.Enqueue("#5efc03");
            colorQueue.Enqueue("#03fcf8");
            colorQueue.Enqueue("#a103fc");
            colorQueue.Enqueue("#fc03d3");

            var individualCharts = new List<GenericChart.GenericChart>();
            foreach (var peaks in peakDictionary)
            {
                var temp = Chart.Column<double, double, string>
                    (
                        Enumerable.Select(peaks.Value, p => p.Intensity),
                        new Optional<IEnumerable<double>>(Enumerable.Select(peaks.Value, p => p.Mz), true),
                        Width: 0.01,
                        MarkerColor: new Optional<Color>(Color.fromHex("#3084C0"), true)
                    )
                    //.WithXAxisStyle(title: Title.init("m/z"))
                    //.WithYAxisStyle(title: Title.init("Intensity"))
                    .WithTitle($"Scan {peaks.Key}")
                    .WithSize(300, 200);

                individualCharts.Add(temp);
            }

            var top = Chart.Grid(
                new List<GenericChart.GenericChart>()
                    { individualCharts[0], individualCharts[1], individualCharts[2], individualCharts[3], individualCharts[4], tempChart}, 2, 3)
                .WithSize(900, 600)
                .WithXAxisStyle(title: Title.init("m/z"))
                .WithYAxisStyle(title: Title.init("Intensity"));
            GenericChartExtensions.Show(top);

            var bottom = Chart.Grid(
                new List<GenericChart.GenericChart>()
                    { individualCharts[3], individualCharts[4] }, 1, 2)
                .WithSize(600, 300)
                .WithXAxisStyle(title: Title.init("m/z", X: 300))
                .WithYAxisStyle(title: Title.init("Intensity"));
            GenericChartExtensions.Show(bottom);
        }

        [Test]
        public static void SortPeaksForFig1Plots()
        {
            string fiveSpecPath = @"B:\Users\Nic\ChimeraValidation\SingleStandards\221110_HGHOnly_50IW.mzML";
            string averagedSpecPath =
                @"D:\Projects\SpectralAveraging\TestOutputs\PaperMassAccuracyTest\Hgh\10Scans_0.01_RelativeToTics_WeightEvenly_AveragedSigmaClipping_0.5_3.2\221110_HGHOnly_50IW-averaged.mzML";

            var controlScans = Mzml.LoadAllStaticData(fiveSpecPath).GetMS1Scans().ToArray();
            var averageScans = Mzml.LoadAllStaticData(averagedSpecPath).GetMS1Scans().ToArray();

            double minToExtract = 1475.25;
            double maxToExtract = 1476.75;

            int[] charge18OneBasedScanNumbers = new[] { 967, 739, 679, 463 };

            for (int i = 50; i < averageScans.Length; i++)
            {
                var averagedScan = averageScans[i];

                if (averagedScan.OneBasedScanNumber == 1141)
                {
                    var selectedControlScans = controlScans[(i - 2)..(i + 3)];
                    GetFig1TentativePlots(minToExtract, maxToExtract, averagedScan, selectedControlScans.ToList());
                }
            }
        }

        public static void GetFig1TentativePlots(double minToExtract, double maxToExtract, MsDataScan averagedSpec, List<MsDataScan> fiveSpec)
        {
            List<MzPeak> averagedPeaks = new();
            Dictionary<int, List<MzPeak>> peakDictionary = fiveSpec.ToDictionary(p => p.OneBasedScanNumber,
                p => p.MassSpectrum.Extract(minToExtract, maxToExtract).ToList());

            bool addZeroValues = true;
            bool showNonRejected = true;
            bool showAveraged = true;
            bool showIndividual = true;

            // add 0 intensity values to either side of each peak to make plot work okay
            if (addZeroValues)
            {

                foreach (var peak in averagedSpec.MassSpectrum.Extract(minToExtract, maxToExtract))
                {
                    averagedPeaks.Add(new MzPeak(peak.Mz - 0.005, 0));
                    averagedPeaks.Add(new MzPeak(peak.Mz, peak.Intensity / averagedSpec.MassSpectrum.YofPeakWithHighestY ?? throw new NullReferenceException()));
                    averagedPeaks.Add(new MzPeak(peak.Mz + 0.005, 0));
                }
                //averagedPeaks.Add(new MzPeak(1230.2, 40000000));

                peakDictionary = new();
                foreach (var originalSpectrum in fiveSpec)
                {
                    List<MzPeak> newPeaks = new();
                    var maxInt = originalSpectrum.MassSpectrum.YofPeakWithHighestY ?? throw new NullReferenceException();
                    foreach (var peak in originalSpectrum.MassSpectrum.Extract(minToExtract, maxToExtract))
                    {
                        newPeaks.Add(new MzPeak(peak.Mz - 0.005, 0));
                        newPeaks.Add(new MzPeak(peak.Mz, peak.Intensity / maxInt));
                        newPeaks.Add(new MzPeak(peak.Mz + 0.005, 0));
                    }
                    //newPeaks.Add(new MzPeak(1230.2, 40000000));
                    peakDictionary.Add(originalSpectrum.OneBasedScanNumber, newPeaks);
                }
            }

            if (showNonRejected)
            {
                string path =
                    @"C:\Users\Nic\OneDrive - UW-Madison\AUSTIN V CARR - AUSTIN V CARR's files\SpectralAveragingPaper\FigureData\Figure 1\peakdata.csv";
                var lines = File.ReadAllLines(path);
                var mzs = lines[0].Split(',').Select(p => double.Parse(p));
                var intensities = lines[1].Split(',').Select(p => double.Parse(p));
                var noRejection = Chart.Column<double, double, string>(
                        intensities,
                        mzs,
                        Width: 0.02,
                        MarkerColor: new Optional<Color>(Color.fromHex("#333333"), true))
                    .WithXAxis(LinearAxis.init<string, string, string, string, string, string>(
                        Title: Title.init("m/z"),
                        AxisType: StyleParam.AxisType.Linear, 
                        NTicks: 20, 
                        TickAngle: 45, 
                        TickFormat: "0.1f",
                        TickFont: Font.init(Size: 14)))
                    .WithYAxis(LinearAxis.init<string, string, string, string, string, string>(
                        Title: Title.init("Intensity"),
                        AxisType: StyleParam.AxisType.Linear))
                    .WithTitle($"Averaged Spectrum No Rejection {averagedSpec.OneBasedScanNumber}")
                    .WithLayout(Layout.init<string>(
                        PaperBGColor: new FSharpOption<Color>(Color.fromHex("#ffffff")),
                        PlotBGColor: new FSharpOption<Color>(Color.fromHex("#ffffff"))));
                GenericChartExtensions.Show(noRejection);
            }

            if (showAveraged)
            {
                var averaged = Chart.Column<double, double, string>(
                        Enumerable.Select(averagedPeaks, p => p.Intensity),
                        new Optional<IEnumerable<double>>(Enumerable.Select(averagedPeaks, p => p.Mz), true),
                        Width: 0.02,
                        MarkerColor: new Optional<Color>(Color.fromHex("#333333"), true)
                    )
                    .WithXAxis(LinearAxis.init<string, string, string, string, string, string>(
                        Title: Title.init("m/z"),
                        AxisType: StyleParam.AxisType.Linear,
                        NTicks: 20,
                        TickAngle: 45,
                        TickFormat: "0.1f",
                        TickFont: Font.init(Size: 14)))
                    .WithYAxis(LinearAxis.init<string, string, string, string, string, string>(
                        Title: Title.init("Intensity"),
                        AxisType: StyleParam.AxisType.Linear, 
                        TickLen: 10))
                    .WithTitle($"Averaged Spectrum {averagedSpec.OneBasedScanNumber}")
                    .WithLayout(Layout.init<string>(PaperBGColor: new FSharpOption<Color>(Color.fromARGB(0, 0, 0, 0)),
                        PlotBGColor: new FSharpOption<Color>(Color.fromARGB(0, 0, 0, 0))));
                GenericChartExtensions.Show(averaged);
            }


            if (showIndividual)
            {
                var individualCharts = new List<GenericChart.GenericChart>();
                foreach (var peaks in peakDictionary)
                {
                    var temp = Chart.Column<double, double, string>
                        (
                            Enumerable.Select(peaks.Value, p => p.Intensity),
                            new Optional<IEnumerable<double>>(Enumerable.Select(peaks.Value, p => p.Mz), true),
                            Width: 0.02,
                            MarkerColor: new Optional<Color>(Color.fromHex("#333333"), true)
                        )
                        .WithXAxis(LinearAxis.init<string, string, string, string, string, string>(Title: Title.init("m/z"),
                            AxisType: StyleParam.AxisType.Linear, NTicks: 10, TickAngle: 45, TickFormat: "0.1f"))
                        //.WithYAxis(LinearAxis.init<string, string, string, string, string, string>(Title: Title.init("Intensity"),
                        //    AxisType: StyleParam.AxisType.Linear))
                        .WithTitle($"Scan {peaks.Key}")
                        .WithSize(300, 300)
                        .WithLayout(Layout.init<string>(PaperBGColor: new FSharpOption<Color>(Color.fromARGB(0, 0, 0, 0)),
                            PlotBGColor: new FSharpOption<Color>(Color.fromARGB(0, 0, 0, 0))));

                    individualCharts.Add(temp);
                }

                var top = Chart.Grid(
                    new List<GenericChart.GenericChart>()
                        { individualCharts[0], individualCharts[1], individualCharts[2], /*individualCharts[3], individualCharts[4], tempChart*/}, 1, 3)
                    .WithSize(900, 300)
                    .WithXAxisStyle(title: Title.init("m/z"))
                    .WithYAxisStyle(title: Title.init("Intensity"));
                GenericChartExtensions.Show(top);

                var bottom = Chart.Grid(
                    new List<GenericChart.GenericChart>()
                        { individualCharts[3], individualCharts[4] }, 1, 2)
                    .WithSize(600, 300)
                    .WithXAxisStyle(title: Title.init("m/z", X: 300))
                    .WithYAxisStyle(title: Title.init("Intensity"));
                GenericChartExtensions.Show(bottom);
            }
     

            
        }

        [Test]
        public static void GenerateFig1CentralPlots()
        {
            var customTickValues = new[] { "Scan 1", "Scan 2", "Scan 3", "Scan 4", "Scan 5", "Averaged"};
            var xValues = new[] { 1.0, 2, 3, 4, 5, 6 };
            var intensities1460 = new double[] { .6518884, 0.285763, .315319, .737388, .575323, .6337349 }; //002ad5
            var intensities14756_2 = new double[] { .0613598, .1658609, .113819, .2181364, .374033, .2577063 }; //24d500
            var intensities14764_2 = new double[] { .1999923, .2011029, .1598912, .1405509, .3290449, .183104 }; //8e00d5
            var intensities14769 = new double[] { .6118561, .5617021, .5803082, .7076075, .5242278, .571939 };

            var layout = Layout.init<string>(PaperBGColor: new FSharpOption<Color>(Color.fromARGB(0, 0, 0, 0)),
                PlotBGColor: new FSharpOption<Color>(Color.fromARGB(0, 0, 0, 0)));
            var xAxis = LinearAxis.init<double, double, double, string, string, string>(
                AxisType: StyleParam.AxisType.Linear,
                NTicks: 6,
                TickAngle: 45,
                TickText: customTickValues,
                TickVals: xValues);

            var plot14756_2 = Chart.Column<double, double, string>
                (intensities14756_2, xValues, Width: 0.2,
                    MarkerColor: new Optional<Color>(Color.fromHex("#24d500"), true))
                .WithXAxis(xAxis)
                .WithSize(300, 300)
                .WithLayout(layout);

            var plot1460 = Chart.Column<double, double, string>
                (intensities1460,
                    xValues, 
                    Width: 0.2,
                    MarkerColor: new Optional<Color>(Color.fromHex("#002ad5"), true))
                .WithXAxis(xAxis)
                .WithSize(300, 300)
                .WithLayout(layout);

            var plot14764_2 = Chart.Column<double, double, string>
                (intensities14764_2, xValues, Width: 0.2,
                    MarkerColor: new Optional<Color>(Color.fromHex("#8e00d5"), true))
                .WithXAxis(xAxis)
                //.WithYAxis(yAxis)
                .WithSize(300, 300)
                .WithLayout(layout);

            var final = Chart.Grid(
                    new List<GenericChart.GenericChart>() { plot14756_2, plot1460, plot14764_2 },
                    1,
                    3)
                .WithSize(900, 350)
                .WithXAxisStyle(title: Title.init("m/z"))
                .WithYAxisStyle(title: Title.init("Intensity"));
            GenericChartExtensions.Show(final);
        }
    }
}