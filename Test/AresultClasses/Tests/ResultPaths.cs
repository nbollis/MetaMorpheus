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
        public static string OutDirectory = @"D:\Projects\SpectralAveraging\PaperTestOutputs\JurkatTopDown_DeconvolutionAnalysis";

        // jurkat - old
        //public static string TopFDCalibDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\TopFD\Calib";
        //public static string TopFDAverageCalibDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\TopFD\CalibAveraged";
        ////public static string FlashDeconvCalibNoCentroidDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\FlashDeconNoCentroid\Calib";
        ////public static string FlashDeconvAverageCalibNoCentroidDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\FlashDeconNoCentroid\CalibAveraged";
        //public static string FlashDeconvCalibCentroidDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\FLASHDecon\Calib";
        //public static string FlashDeconvAveragedCalibCentroidDirectory = @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\FLASHDecon\CalibAveraged";
        //public static string CalibAveragedNoRejectionDirectory =
        //    @"D:\Projects\SpectralAveraging\PaperTestOutputs\JurkatTopDown_DeconvolutionAnalysis\Rep2CalibAverageNoRejection";

        public static string TopFDCalibDirectory = @"D:\Projects\SpectralAveraging\PaperTestOutputs\JurkatTopDown_DeconvolutionAnalysis\Rep2CalibCentroided\TopFD";
        public static string TopFDAverageCalibDirectory = @"D:\Projects\SpectralAveraging\PaperTestOutputs\JurkatTopDown_DeconvolutionAnalysis\Rep2CalibAverageCentroid\TopFD";
        public static string TopFdNoRejectionDirectory =
            @"D:\Projects\SpectralAveraging\PaperTestOutputs\JurkatTopDown_DeconvolutionAnalysis\Rep2CalibAverageNoRejection\TopFD";

        public static string FlashDeconvCalibCentroidDirectory = @"D:\Projects\SpectralAveraging\PaperTestOutputs\JurkatTopDown_DeconvolutionAnalysis\Rep2CalibCentroided\FlashDeconv";
        public static string FlashDeconvAveragedCalibCentroidDirectory = @"D:\Projects\SpectralAveraging\PaperTestOutputs\JurkatTopDown_DeconvolutionAnalysis\Rep2CalibAverageCentroid\FlashDeconv";
        public static string FlashDeconvNoRejectionDirectory =
            @"D:\Projects\SpectralAveraging\PaperTestOutputs\JurkatTopDown_DeconvolutionAnalysis\Rep2CalibAverageNoRejection\FlashDeconv";

        

        public static Dictionary<DeconSoftware, (string Control, string Averaged, string NoRejection)> JurkatDirectoryDictionary { get; set; }

        static ResultPaths()
        {
            JurkatDirectoryDictionary = new Dictionary<DeconSoftware, (string Calib, string CalibAveraged, string NoRejection)>
            {
                { DeconSoftware.FLASHDeconv, (FlashDeconvCalibCentroidDirectory, FlashDeconvAveragedCalibCentroidDirectory, FlashDeconvNoRejectionDirectory) },
                { DeconSoftware.TopFD, (TopFDCalibDirectory, TopFDAverageCalibDirectory, TopFdNoRejectionDirectory) }
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
