using CsvHelper;
using GuiFunctions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Test.AveragingPaper.DeconResults;

namespace Test
{
    public static class ResultPaths
    {
        public static string OutDirectory = @"C:\Users\Nic\OneDrive - UW-Madison\AUSTIN V CARR - AUSTIN V CARR's files\SpectralAveragingPaper\ResultsData\Deconvolution";

        // jurkat
        public static string TopFDCalibDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\TopFD\Calib";
        public static string TopFDAverageCalibDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\TopFD\CalibAveraged";
        //public static string FlashDeconvCalibNoCentroidDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\FlashDeconNoCentroid\Calib";
        //public static string FlashDeconvAverageCalibNoCentroidDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\FlashDeconNoCentroid\CalibAveraged";
        public static string FlashDeconvCalibCentroidDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\FLASHDecon\Calib";
        public static string FlashDeconvAveragedCalibCentroidDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\FLASHDecon\CalibAveraged";

        // myo
        //public static string MyoTopFDControlDirectory = @"B:\Users\Nic\ScanAveraging\LVSMyolbast\TopFD\Centroided";
        //public static string MyoTopFDAveragedDirectory = @"B:\Users\Nic\ScanAveraging\LVSMyolbast\TopFD\AveragedCentroided";
        //public static string MyoFlashDeconvControlDirectory = @"B:\Users\Nic\ScanAveraging\LVSMyolbast\FlashDeconv\Centroided";
        //public static string MyoFlashDeconvAveragedDirectory = @"B:\Users\Nic\ScanAveraging\LVSMyolbast\FlashDeconv\AveragedCentroided";
        //public static string MyoFlashDeconvNoCentroidControlDirectory = @"B:\Users\Nic\ScanAveraging\LVSMyolbast\FlashDeconv\AveragedCentroided";
        //public static string MyoFlashDeconvNoCentroidAveragedDirectory = @"B:\Users\Nic\ScanAveraging\LVSMyolbast\FlashDeconv\AveragedNoCentroid";

        public static Dictionary<DeconSoftware, (string Control, string Averaged)> JurkatDirectoryDictionary { get; set; }

        static ResultPaths()
        {
            JurkatDirectoryDictionary = new Dictionary<DeconSoftware, (string Calib, string CalibAveraged)>
            {
                { DeconSoftware.FLASHDeconv, (FlashDeconvCalibCentroidDirectory, FlashDeconvAveragedCalibCentroidDirectory) },
                { DeconSoftware.TopFD, (TopFDCalibDirectory, TopFDAverageCalibDirectory) }
            };
        }

        public static string[] GetMs1FeatureFiles(string directoryPath)
        {
            return Directory.GetFiles(directoryPath).Where(p => p.EndsWith("ms1.feature")).OrderBy(p => p).ToArray();
        }

        

        public static string[] GetTsvFiles(string directoryPath)
        {
            return Directory.GetFiles(directoryPath).Where(p => p.EndsWith(".tsv") && !p.Contains("_ms1")).OrderBy(p => p).ToArray();
        }

        public static string[] GetMs1TsvFiles(string directoryPath)
        {
            return Directory.GetFiles(directoryPath).Where(p => p.EndsWith(".tsv") && p.Contains("_ms1")).OrderBy(p => p).ToArray();
        }

        public static string[] GetMzRTFiles(string directoryPath)
        {
            var files = new List<string>();

            foreach (var directory in Directory.GetDirectories(directoryPath).Where(p => p.EndsWith("_file")))
            {
                files.Add(Directory.GetFiles(directory).First(p => p.EndsWith(".mzrt.csv")));
            }
            return files.ToArray();
        }

        
    }
}
