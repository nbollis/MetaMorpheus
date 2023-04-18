using iText.Kernel.Geom;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Path = System.IO.Path;

namespace Test.MyTests
{
    [TestFixture]
    public static class DeconResults
    {

        internal enum DeconSoftware
        {
            TopFD,
            FLASHDeconv,
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

        private const string TopFDCalibDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\TopFD\Calib";
        private const string TopFDAverageCalibDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\TopFD\CalibAveraged";
        private const string FlashDeconvCalibDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\FlashDeconNoCentroid\Calib";
        private const string FlashDeconvAverageCalibDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\FlashDeconNoCentroid\AveragedCalib";
        private const string OutDirectory = @"C:\Users\Nic\OneDrive - UW-Madison\AUSTIN V CARR - AUSTIN V CARR's files\SpectralAveragingPaper\ResultsData\Deconvolution";
        private static Dictionary<DeconSoftware, (string Calib, string CalibAveraged)> DirectoryDictionary { get; set; }

        [OneTimeSetUp]
        public static void OneTimeSetUp()
        {
            DirectoryDictionary = new Dictionary<DeconSoftware, (string Calib, string CalibAveraged)>();
            DirectoryDictionary.Add(DeconSoftware.TopFD, (TopFDCalibDirectory, TopFDAverageCalibDirectory));
            DirectoryDictionary.Add(DeconSoftware.FLASHDeconv, (FlashDeconvCalibDirectory, FlashDeconvAverageCalibDirectory));
        }


        [Test]
        public static void ParseDeconvolutionDirectories()
        {
            List<DeconComparison> results = new();
            foreach (var deconSoftware in DirectoryDictionary)
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
