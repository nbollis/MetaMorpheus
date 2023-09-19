using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using EngineLayer;
using GuiFunctions;
using MzLibUtil;
using NUnit.Framework;
using OxyPlot.Wpf;
using Readers;

namespace Test.AveragingPaper
{
    [TestFixture]
    public class PaperRevisionsDataExploration
    {
        public static string CalibPsmsPath =
            @"C:\Users\Nic\OneDrive - UW-Madison\AUSTIN V CARR - AUSTIN V CARR's files\SpectralAveragingPaper\Supplemental Information\MM Output Bulk Jurkat\SI Workbook 1_PrSMs_CalibOnly.psmtsv";

        public static string CalibProteoformsPath =
            @"C:\Users\Nic\OneDrive - UW-Madison\AUSTIN V CARR - AUSTIN V CARR's files\SpectralAveragingPaper\Supplemental Information\MM Output Bulk Jurkat\SI Workbook 2_Proteoforms_CalibOnly.psmtsv";

        public static string AveragedPsmsPath =
            @"C:\Users\Nic\OneDrive - UW-Madison\AUSTIN V CARR - AUSTIN V CARR's files\SpectralAveragingPaper\Supplemental Information\MM Output Bulk Jurkat\SI Workbook 3_PrSMs_CalibAveraged.psmtsv";

        public static string AveragedProteoformsPath =
            @"C:\Users\Nic\OneDrive - UW-Madison\AUSTIN V CARR - AUSTIN V CARR's files\SpectralAveragingPaper\Supplemental Information\MM Output Bulk Jurkat\SI Workbook 4_Proteoforms_CalibAveraged.psmtsv";

        private static List<PsmFromTsv> calibPsms;
        public static List<PsmFromTsv> CalibPsms => calibPsms ??= PsmTsvReader.ReadTsv(CalibPsmsPath, out _).Where(p => p.QValue <= 0.01).ToList();

        private static List<PsmFromTsv> calibProteoforms;
        public static List<PsmFromTsv> CalibProteoforms => calibProteoforms ??= PsmTsvReader.ReadTsv(CalibProteoformsPath, out _);

        private static List<PsmFromTsv> averagedPsms;
        public static List<PsmFromTsv> AveragedPsms => averagedPsms ??= PsmTsvReader.ReadTsv(AveragedPsmsPath, out _).Where(p => p.QValue <= 0.01).ToList();

        private static List<PsmFromTsv> averagedProteoforms;
        public static List<PsmFromTsv> AveragedProteoforms => averagedProteoforms ??= PsmTsvReader.ReadTsv(AveragedProteoformsPath, out _);


        public static Func<PsmFromTsv, object>[] Ms1Selector =
        {
            psm => psm.PrecursorScanNum,
            psm => psm.FileNameWithoutExtension.Replace("-averaged", "")
        };

        public static CustomComparer<PsmFromTsv> Ms1Comparer = new CustomComparer<PsmFromTsv>(Ms1Selector);



        public static Func<PsmFromTsv, object>[] ChimeraSelector =
        {
            psm => psm.PrecursorScanNum,
            psm => psm.Ms2ScanNumber,
            psm => psm.FileNameWithoutExtension.Replace("-averaged", "")
        };

        public static CustomComparer<PsmFromTsv> ChimeraComparer = new CustomComparer<PsmFromTsv>(ChimeraSelector);


        [Test]
        public static void FindThoseThatAreIdentical()
        {
            Func<PsmFromTsv, object>[] bigSelector =
            {
                psm => psm.BaseSeq,
                psm => psm.PrecursorScanNum,
                psm => psm.Ms2ScanNumber,
                psm => psm.FileNameWithoutExtension.Replace("-averaged", "")
            };
            var bigComparer = new CustomComparer<PsmFromTsv>(bigSelector);
            var bigIntersect = AveragedPsms.Intersect(CalibPsms, bigComparer).ToList();

            int count = 0;
            int same = 0;
            int different = 0;
            int sameFull = 0;
            int differentFull = 0;
            foreach (var averagedPsm in bigIntersect)
            {
                count++;
                var temp = CalibPsms
                    .Where(p => p.PrecursorScanNum == averagedPsm.PrecursorScanNum &&
                                p.Ms2ScanNumber == averagedPsm.Ms2ScanNumber &&
                                p.FileNameWithoutExtension == averagedPsm.FileNameWithoutExtension.Replace("-averaged", "") &&
                                p.BaseSeq == averagedPsm.BaseSeq)
                    .ToList();
                if (temp.Any())
                {
                    same++;
                    var full = temp.Where(p => p.FullSequence == averagedPsm.FullSequence);
                    if (full.Any())
                        sameFull++;
                    else
                        differentFull++;
                }
                else
                    different++;
            }
        }

        /// <summary>
        /// Grouped by Ms1 Number, Ms2 Number, and fraction
        /// Find those that are the same by ms2 and fraction between cali and averaged
        /// 
        /// </summary>
        [Test]
        public static void ChimeraAnalysis()
        {
            
            var calibPrecursor = CalibPsms.GroupBy(tsv => tsv, ChimeraComparer)
                .ToDictionary(p => p.Key, p => p.ToList());

            var averagedPrecursor = AveragedPsms.GroupBy(tsv => tsv, ChimeraComparer)
                .ToDictionary(p => p.Key, p => p.ToList());

            Dictionary<(int ScanNum, int PrecursorScanNum, string DataFile), (List<PsmFromTsv> Calib, List<PsmFromTsv> Averaged)> GroupedDictionary =
                new Dictionary<(int, int, string), (List<PsmFromTsv> Calib, List<PsmFromTsv> Averaged)>();

            foreach (var avg in averagedPrecursor)
            {
                var scanNum = avg.Key.Ms2ScanNumber;
                var precursorScanNum = avg.Key.PrecursorScanNum;
                var dataFile = avg.Key.FileNameWithoutExtension.Replace("-averaged", "");
                var calib = calibPrecursor.FirstOrDefault(p => p.Key.Ms2ScanNumber == scanNum && p.Key.FileNameWithoutExtension == dataFile);
                if ( calib.Value is not null && calib.Value.Any() && avg.Value.Any()) 
                    GroupedDictionary.Add((scanNum, precursorScanNum, dataFile), (calib.Value, avg.Value));
            }

            // look at
            if (false)
            {
                // where averaged has more than calibration for the same scan
                // order by averaged psm score, then cali score, then count of ID's
                var differenceDictionary = GroupedDictionary.Where(p => p.Value.Calib.Count < p.Value.Averaged.Count)
                    .OrderByDescending(p => p.Value.Averaged.Average(m => m.Score))
                    .ThenByDescending(p => p.Value.Calib.Average(m => m.Score))
                    .ThenByDescending(p => p.Value.Averaged.Count + p.Value.Calib.Count)
                    .ToDictionary(p => p.Key, p => p.Value);
            }

            // write results to file
            if (false)
            {
                // group by ms1 scan number and order by count
                var temp = GroupedDictionary.GroupBy(p => p.Value.Averaged.First(), Ms1Comparer)
                    .OrderByDescending(p => p.Count())
                    .ToDictionary(
                        p => p.First().Key, 
                        p => p
                            .Select(m => m)
                            .ToDictionary(n => n.Key, n => n.Value));
                var toWrite = temp.First().Value;

                string outpath = @"D:\Projects\SpectralAveraging\PaperTestOutputs\Revisions_ChimeraAnalysis.csv";
                using (var sw = new StreamWriter(File.Create(outpath)))
                {
                    var header = new string[]
                    {
                        "Fraction",
                        "Condition",
                        "MS1 Scan Number",
                        "MS2 Scan Number",
                        "Precursor Charge",
                        "Precursor m/z",
                        "Precursor Mass",
                        "Accession",
                        "Full Sequence",
                        "Base Sequence",

                    };
                    sw.WriteLine(string.Join(',', header));

                    foreach (var thing in toWrite)
                    {
                        foreach (var val in thing.Value.Calib)
                        {
                            sw.WriteLine
                            (
                                $"{val.FileNameWithoutExtension},Calibrated,{val.PrecursorScanNum},{val.Ms2ScanNumber}," +
                                $"{val.PrecursorCharge},{val.PrecursorMz},{val.PrecursorMass}," +
                                $"{val.ProteinAccession},{val.FullSequence},{val.BaseSeq}"
                            );
                        }
                        foreach (var val in thing.Value.Averaged)
                        {
                            sw.WriteLine
                            (
                                $"{val.FileNameWithoutExtension},Averaged,{val.PrecursorScanNum},{val.Ms2ScanNumber}," +
                                $"{val.PrecursorCharge},{val.PrecursorMz},{val.PrecursorMass}," +
                                $"{val.ProteinAccession},{val.FullSequence},{val.BaseSeq}"
                            );
                        }

                    }

                }
                
            }

            // plot the results
            if (true)
            {
                var temp = GroupedDictionary.GroupBy(p => p.Value.Averaged.First(), Ms1Comparer)
                    .OrderByDescending(p => p.Count())
                    .ToDictionary(
                        p => p.First().Key,
                        p => p
                            .Select(m => m)
                            .ToDictionary(n => n.Key, n => n.Value));
                var toWrite = temp.First().Value;
            }

            
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public static void ChimeraExampleDirectPlotting()
        {
            var calibFilePath =
                @"D:\Projects\SpectralAveraging\PaperTestOutputs\JurkatTopDown_DeconvolutionAnalysis\Rep2Calib\02-18-20_jurkat_td_rep2_fract7-calib.mzML";
            var averagedFilepath =
                @"D:\Projects\SpectralAveraging\PaperTestOutputs\JurkatTopDown_DeconvolutionAnalysis\Rep2CalibAveraged\02-18-20_jurkat_td_rep2_fract7-calib-averaged.mzML";
            var calibFile = new Mzml(calibFilePath);
            var averagedFile = MsDataFileReader.GetDataFile(averagedFilepath);

            calibFile.InitiateDynamicConnection();
            var calibScan = calibFile.GetOneBasedScanFromDynamicConnection(769);
            calibFile.CloseDynamicConnection();

            averagedFile.InitiateDynamicConnection();
            var averagedScan = averagedFile.GetOneBasedScanFromDynamicConnection(769);
            averagedFile.CloseDynamicConnection();

            double isolationValue = 810.8206;
            double isolationMin = isolationValue - 2;
            double isolationMax = isolationValue + 2;

            var range = new DoubleRange(isolationMin, isolationMax);
            var temp = new MirrorSpectrum(new PlotView(), calibScan, averagedScan, range);
            
            string outPath = @"C:\Users\Nic\Downloads\810.png";
            temp.ExportToPng(outPath);

        }

    }
}
