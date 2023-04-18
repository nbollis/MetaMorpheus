using System;
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
using IO.MzML;
using IO.ThermoRawFileReader;
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
using ThermoFisher.CommonCore.Data.Business;
using TopDownProteomics;
using Range = System.Range;

namespace Test
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
                chargeMz.Add((charge,mz));
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
                var sourceFile = SpectraFileHandler.GetSourceFile(file);
                var scans = ThermoRawFileReader.LoadAllStaticData(file).GetAllScansList();
                var averagedScans = SpectraFileAveraging.AverageSpectraFile(scans, parameters);

                MsDataFile dataFile = new(averagedScans, sourceFile);
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
            string originalScanResultsPath = @"D:\Projects\SpectralAveraging\PaperMassAccuracyTest\Hgh\0riginal\ScanMassAccuracyResults.tsv";
            var originalScanResults = ScanMassAccuracyResults.LoadResultsListFromTsv(originalScanResultsPath).ToList();
            var originalFileResults = new FileMassAccuracyResults(originalScanResults, null);
            var count = File.ReadAllLines(@"D:\Projects\SpectralAveraging\PaperMassAccuracyTest\Hgh\0riginal\allResults.txt")
                .First(m => m.Contains("All target PSM"))
                .Split(":")[1]
                .Trim();
            originalFileResults.PsmCount = int.Parse(count);

            string fileTsvPath = @"D:\Projects\SpectralAveraging\PaperMassAccuracyTest\HghAllFileResults.tsv";
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


       

        [Test]
        public static void CreateFigure1()
        {
            string fiveSpecPath = @"B:\Users\Nic\ChimeraValidation\SingleStandards\221110_HGHOnly_50IW.mzML";
            string averagedSpecPath =
                @"D:\Projects\SpectralAveraging\PaperMassAccuracyTest\Hgh\10Scans_0.01_RelativeToTics_WeightEvenly_AveragedSigmaClipping_0.5_3.2\221110_HGHOnly_50IW-averaged.mzML";

            var fiveSpec = Mzml.LoadAllStaticData(fiveSpecPath).GetMS1Scans()
                .Where(p => p.OneBasedScanNumber is >= 1075 and <= 1099);
            var averagedSpec = Mzml.LoadAllStaticData(averagedSpecPath).GetMS1Scans()
                .First(p => p.OneBasedScanNumber >= 1087);

            double minToExtract = 1229.7;
            double maxToExtract = 1230.6;
            var averagedPeaks = averagedSpec.MassSpectrum.Extract(minToExtract, maxToExtract).ToList();
            Dictionary<int, List<MzPeak>> peakDictionary = fiveSpec.ToDictionary(p => p.OneBasedScanNumber,
                p => p.MassSpectrum.Extract(minToExtract, maxToExtract).ToList());

            if (true)
            {
                averagedPeaks = new();
                foreach (var peak in averagedSpec.MassSpectrum.Extract(minToExtract, maxToExtract))
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
            GenericChartExtensions.Show(tempChart);

            Queue<string> colorQueue = new();
            colorQueue.Enqueue("#fc0303");
            colorQueue.Enqueue("#5efc03");
            colorQueue.Enqueue("#03fcf8");
            colorQueue.Enqueue("#a103fc");
            colorQueue.Enqueue("#fc03d3");

            var individualCharts = new List<GenericChart.GenericChart>();
            foreach (var peaks in peakDictionary)
            {
                var temp = Chart.Column<double,double,string>
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
                    { individualCharts[0], individualCharts[1], individualCharts[2], }, 1, 3)
                .WithSize(900, 300)
                .WithXAxisStyle(title: Title.init("m/z"))
                .WithYAxisStyle(title: Title.init("Intensity"));
            GenericChartExtensions.Show(top);

            var bottom = Chart.Grid(
                new List<GenericChart.GenericChart>()
                    { individualCharts[3], individualCharts[4] }, 1, 2)
                .WithSize(600, 300)
                .WithXAxisStyle(title: Title.init("m/z", X:300))
                .WithYAxisStyle(title: Title.init("Intensity"));
            GenericChartExtensions.Show(bottom);




            //var x = new List<double>();
            //var y = new List<double>();
            //var z = new List<double>();

            //peakDictionary.ForEach(p => x.AddRange(Enumerable.Repeat((double)p.Key, p.Value.Count)));
            //peakDictionary.ForEach(p => y.AddRange(p.Value.Select(m => m.Mz)));
            //peakDictionary.ForEach(p => z.AddRange(p.Value.Select(m => m.Intensity)));

            //var threeDChart = Chart.Line3D<double, double, double, string>
            //(
            //    x, y, z,
            //    ShowMarkers:false

            //);

            //GenericChartExtensions.Show(threeDChart);

        }
    }
}
