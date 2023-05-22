using Easy.Common.Extensions;
using iText.Kernel.Geom;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GuiFunctions;
using Path = System.IO.Path;

namespace Test.AveragingPaper
{
    [TestFixture]
    public static class DeconResults
    {

        public enum DeconSoftware
        {
            TopFD,
            FLASHDeconv,
            FLASHDeconvNoCentroid,
        }
        internal record struct DeconComparison(DeconSoftware DeconSoftware, string FileName, int Calib, int CalibAveraged);



        private static void ExportDeconResults(string path, List<DeconComparison> comparisons)
        {
            using (StreamWriter sw = new StreamWriter(File.Create(path)))
            {
                sw.WriteLine("Software,File,Calib,Calib-Averaged");
                foreach (var comparison in comparisons)
                {
                    sw.WriteLine($"{comparison.DeconSoftware},{comparison.FileName},{comparison.Calib},{comparison.CalibAveraged}");
                }
            }
        }

        private static int GetFeatureCountFromFile(string filePath)
        {
            if (!filePath.EndsWith(".feature")) throw new ArgumentException();
            return File.ReadAllLines(filePath).Length - 1;
        }

        private static string[] GetFeatureFiles(string directoryPath)
        {
            return Directory.GetFiles(directoryPath).Where(p => p.EndsWith("ms1.feature")).OrderBy(p => p).ToArray();
        }


        private const string OutDirectory = @"C:\Users\Nic\OneDrive - UW-Madison\AUSTIN V CARR - AUSTIN V CARR's files\SpectralAveragingPaper\ResultsData\Deconvolution";

        // jurkat
        private const string TopFDCalibDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\TopFD\Calib";
        private const string TopFDAverageCalibDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\TopFD\CalibAveraged";
        private const string FlashDeconvCalibNoCentroidDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\FlashDeconNoCentroid\Calib";
        private const string FlashDeconvAverageCalibNoCentroidDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\FlashDeconNoCentroid\CalibAveraged";
        private const string FlashDeconvCalibCentroidDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\FLASHDecon\Calib";
        private const string FlashDeconvAveragedCalibCentroidDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\FLASHDecon\CalibAveraged";

        // myo
        private const string MyoTopFDControlDirectory = @"B:\Users\Nic\ScanAveraging\LVSMyolbast\TopFD\Centroided";
        private const string MyoTopFDAveragedDirectory = @"B:\Users\Nic\ScanAveraging\LVSMyolbast\TopFD\AveragedCentroided";
        private const string MyoFlashDeconvControlDirectory = @"B:\Users\Nic\ScanAveraging\LVSMyolbast\FlashDeconv\Centroided";
        private const string MyoFlashDeconvAveragedDirectory = @"B:\Users\Nic\ScanAveraging\LVSMyolbast\FlashDeconv\AveragedCentroided";
        private const string MyoFlashDeconvNoCentroidControlDirectory = @"B:\Users\Nic\ScanAveraging\LVSMyolbast\FlashDeconv\AveragedCentroided";
        private const string MyoFlashDeconvNoCentroidAveragedDirectory = @"B:\Users\Nic\ScanAveraging\LVSMyolbast\FlashDeconv\AveragedNoCentroid";
        
        private static Dictionary<DeconSoftware, (string Calib, string CalibAveraged)> JurkatDirectoryDictionary { get; set; }
        private static Dictionary<DeconSoftware, (string Calib, string CalibAveraged)> MyoDirectoryDictionary { get; set; }

        [OneTimeSetUp]
        public static void OneTimeSetUp()
        {
            JurkatDirectoryDictionary = new Dictionary<DeconSoftware, (string Calib, string CalibAveraged)>
            {
                
                { DeconSoftware.FLASHDeconv, (FlashDeconvCalibCentroidDirectory, FlashDeconvAveragedCalibCentroidDirectory) },
                { DeconSoftware.TopFD, (TopFDCalibDirectory, TopFDAverageCalibDirectory) }
            };

            MyoDirectoryDictionary = new Dictionary<DeconSoftware, (string Calib, string CalibAveraged)>
            {
                { DeconSoftware.TopFD, (MyoTopFDControlDirectory, MyoTopFDAveragedDirectory) },
                { DeconSoftware.FLASHDeconv, (MyoFlashDeconvControlDirectory, MyoFlashDeconvAveragedDirectory) },
                { DeconSoftware.FLASHDeconvNoCentroid, (MyoFlashDeconvNoCentroidControlDirectory, MyoFlashDeconvNoCentroidAveragedDirectory) }
            };
        }


        [Test]
        public static void ParseDeconvolutedDirectoriesAndScoreFeatures()
        {

            List<Ms1FeatureFile> processedFeatureFiles = new();
            // Jurkat
            //foreach (var deconSoftware in JurkatDirectoryDictionary)
            //{
            //    // get files
            //    var calibFiles = GetFeatureFiles(deconSoftware.Value.Calib);
            //    var averagedFiles = GetFeatureFiles(deconSoftware.Value.CalibAveraged);

            //    for (int i = 0; i < calibFiles.Length; i++)
            //    {
            //        processedFeatureFiles.Add(new Ms1FeatureFile(calibFiles[i], deconSoftware.Key, "Jurkat", "Control"));
            //        processedFeatureFiles.Add(new Ms1FeatureFile(averagedFiles[i], deconSoftware.Key, "Jurkat", "Averaged"));
            //    }
            //}
            //string outPath = Path.Combine(OutDirectory, "JurkatFeatureScoringByFlashMethod.tsv");

            // Myo
            foreach (var deconSoftware in MyoDirectoryDictionary)
            {
                // get files
                var calibFiles = GetFeatureFiles(deconSoftware.Value.Calib);
                var averagedFiles = GetFeatureFiles(deconSoftware.Value.CalibAveraged);

                for (int i = 0; i < calibFiles.Length; i++)
                {
                    processedFeatureFiles.Add(new Ms1FeatureFile(calibFiles[i], deconSoftware.Key, "Myo", "Control"));
                    processedFeatureFiles.Add(new Ms1FeatureFile(averagedFiles[i], deconSoftware.Key, "Myo", "Averaged"));
                }
            }
            string outPath = Path.Combine(OutDirectory, "MyoFeatureScoringByFlashMethod.tsv");


            processedFeatureFiles.Select(p => (ITsv)p).ExportAsTsv(outPath);
        }



        [Test]
        public static void ParseDeconvolutionDirectoriesToGetFeatureCount()
        {
            List<DeconComparison> results = new();
            foreach (var deconSoftware in JurkatDirectoryDictionary)
            {
                // get files
                var calibFiles = GetFeatureFiles(deconSoftware.Value.Calib);
                var averagedFiles = GetFeatureFiles(deconSoftware.Value.CalibAveraged);

                for (int i = 0; i < calibFiles.Length; i++)
                {
                    int calibResults = GetFeatureCountFromFile(calibFiles[i]);
                    int averagedResults = GetFeatureCountFromFile(averagedFiles[i]);

                    string name = Path.GetFileNameWithoutExtension(averagedFiles[i])
                        .Replace("id_02-17-20_", "")
                        .Replace("id_02-18-20_", "")
                        .Replace("-calib-averaged-centroided_file", "");

                    results.Add(new DeconComparison(deconSoftware.Key, name, calibResults, averagedResults));
                }
            }

            string outpath = Path.Combine(OutDirectory, "FlashNoCentroid_TopFD_Ms1Feature_Results.csv");
            ExportDeconResults(outpath, results);

        }




        [Test]
        public static void OriginalTopFDComparison()
        {
            string resultPath = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\Centroided";
            //string resultPath = @"B:\Users\Nic\ScanAveraging\KHBFraction7\TopFDOutputs";
            var directories = Directory.GetDirectories(resultPath);
            var fileDirectories = directories.Where(p => p.Contains("_file"));

            List<DeconComparison> comparisons = new();
            foreach (var averagedDirectory in fileDirectories.Where(p => p.Contains("-averaged-")))
            {
                string calibDirectory = averagedDirectory.Replace("-averaged", "");
                string name = Path.GetFileNameWithoutExtension(averagedDirectory)
                    .Replace("id_02-17-20_", "")
                    .Replace("id_02-18-20_", "")
                    .Replace("-calib-averaged-centroided_file", "");

                var averagedLines =
                    File.ReadAllLines(Directory.GetFiles(averagedDirectory).First(p => p.EndsWith(".csv")));
                var calibLines =
                    File.ReadAllLines(Directory.GetFiles(calibDirectory).First(p => p.EndsWith(".csv")));

                var averagedCount = averagedLines.Length - 1;
                var calibCount = calibLines.Length - 1;

                DeconComparison comparison = new(DeconSoftware.TopFD, name, calibCount, averagedCount);
                comparisons.Add(comparison);
            }

            string outPath = Path.Combine(resultPath, "FeatureAnalysis.csv");

        }

    }
}
