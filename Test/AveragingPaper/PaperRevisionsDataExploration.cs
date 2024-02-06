using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using EngineLayer;
using GuiFunctions;
using GuiFunctions.MetaDraw.SpectrumMatch;
using MassSpectrometry;
using MzLibUtil;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using OxyPlot;
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

        public static string NoRejectionPsmsPath = 
            @"C:\Users\Nic\OneDrive - UW-Madison\AUSTIN V CARR - AUSTIN V CARR's files\SpectralAveragingPaper\Supplemental Information\MM Output Bulk Jurkat\SI Workbook 5_PrSMs_CalibAveragedNoRejection.psmtsv";

        public static string NoRejectionProteoformsPath =
            @"C:\Users\Nic\OneDrive - UW-Madison\AUSTIN V CARR - AUSTIN V CARR's files\SpectralAveragingPaper\Supplemental Information\MM Output Bulk Jurkat\SI Workbook 6_Proteoforms_CalibAveragedNoRejection.psmtsv";

        private static List<PsmFromTsv> calibPsms;
        public static List<PsmFromTsv> CalibPsms => calibPsms ??= PsmTsvReader.ReadTsv(CalibPsmsPath, out _).Where(p => p.QValue <= 0.01).ToList();

        private static List<PsmFromTsv> calibProteoforms;
        public static List<PsmFromTsv> CalibProteoforms => calibProteoforms ??= PsmTsvReader.ReadTsv(CalibProteoformsPath, out _);

        private static List<PsmFromTsv> averagedPsms;
        public static List<PsmFromTsv> AveragedPsms => averagedPsms ??= PsmTsvReader.ReadTsv(AveragedPsmsPath, out _).Where(p => p.QValue <= 0.01).ToList();

        private static List<PsmFromTsv> averagedProteoforms;
        public static List<PsmFromTsv> AveragedProteoforms => averagedProteoforms ??= PsmTsvReader.ReadTsv(AveragedProteoformsPath, out _);

        private static List<PsmFromTsv> noRejectionPsms;
        public static List<PsmFromTsv> NoRejectionPsms => noRejectionPsms ??= PsmTsvReader.ReadTsv(NoRejectionPsmsPath, out _).Where(p => p.QValue <= 0.01).ToList();

        private static List<PsmFromTsv> noRejectionProteoforms;
        public static List<PsmFromTsv> NoRejectionProteoforms => noRejectionProteoforms ??= PsmTsvReader.ReadTsv(NoRejectionProteoformsPath, out _);

        

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
        public static void ChimeraCountingOutput()
        {
            var results = CalibPsms.GroupBy(p => p, ChimeraComparer)
                .GroupBy(m => m.Count())
                .Select(group => ("Calibrated", group.Key, group.Count()))
                .ToList();

            results.AddRange(AveragedPsms.GroupBy(p => p, ChimeraComparer)
                .GroupBy(m => m.Count())
                .Select(group => ("Averaged", group.Key, group.Count())));

            results.AddRange(NoRejectionPsms.GroupBy(p => p, ChimeraComparer)
                .GroupBy(m => m.Count())
                .Select(group => ("No Rejection", group.Key, group.Count())));

            var outPath = @"C:\Users\Nic\OneDrive - UW-Madison\AveragingPaper\ReReResubmissionBundle\SI4\chimeraCounting.csv";
            using (var sw = new StreamWriter(File.Create(outPath)))
            {
                sw.WriteLine("Condition,Count,Number of Groups");
                foreach (var result in results)
                {
                    sw.WriteLine($"{result.Item1},{result.Item2},{result.Item3}");
                }
            }

        }
       

        [Test]
        [Apartment(ApartmentState.STA)]
        public static void ChimeraValidationForReRevisions()
        {
            List<string> dataFilePaths = new()
            {
                //@"B:\Users\Nic\ScanAveraging\ResvisionsSearches\AvgSig5\Task1-AveragingTask\02-17-20_jurkat_td_rep2_fract2-calib-averaged.mzML",
                //@"B:\Users\Nic\ScanAveraging\ResvisionsSearches\AvgSig5\Task1-AveragingTask\02-17-20_jurkat_td_rep2_fract3-calib-averaged.mzML",
                //@"B:\Users\Nic\ScanAveraging\ResvisionsSearches\AvgSig5\Task1-AveragingTask\02-17-20_jurkat_td_rep2_fract4-calib-averaged.mzML",
                @"D:\Projects\SpectralAveraging\PaperTestOutputs\Searches\Refined\Sigma5AllBellsAndWhistles\Task2-AveragingTask\02-18-20_jurkat_td_rep2_fract6-calib-averaged.mzML"
            };
            var dataFiles = dataFilePaths.ToDictionary(Path.GetFileNameWithoutExtension ,MsDataFileReader.GetDataFile);
            foreach (var keyValuePair in dataFiles)
            {
                keyValuePair.Value.InitiateDynamicConnection();
            }
            var chimeraGroups = AveragedPsms
                .Where(p => p.QValue <= 0.01 
                            && dataFilePaths.Any(m => m.Contains(p.FileNameWithoutExtension)))
                //.GroupBy(p => p, ChimeraComparer)
                //.Where(p => p.Count() > 1)
                //.OrderByDescending(p => p.Count())
                //.ToDictionary(p =>
                //        (p.Key.PrecursorScanNum, p.Key.Ms2ScanNumber, p.Key.FileNameWithoutExtension),
                //    p => p.OrderByDescending(m => m.QValue)
                //                                     .ThenByDescending(m => m.Score)
                                                     .ToList();
            var temp = new ChimeraAnalysisTabViewModel(chimeraGroups, dataFiles);
            var temp2 = temp.ChimeraGroupViewModels.First().PrecursorIonsByColor;
        }





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

            //var noRejectionPrecursor = NoRejectionPsms.GroupBy(tsv => tsv, ChimeraComparer)
            //    .ToDictionary(p => p.Key, p => p.ToList());

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

                //var noRej = noRejectionPrecursor.FirstOrDefault(p => p.Key.Ms2ScanNumber == scanNum && p.Key.FileNameWithoutExtension == dataFile);

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
            if (true)
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

                    foreach (var toWrites in temp.Values)
                    {
                        foreach (var thing in toWrites)
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
        private static string _FractPattern = @"fract(\d+)";
        // 779, 771, 657.5, 658.3.5
        [Test]
        [Apartment(ApartmentState.STA)]
        public static void ChimeraExampleDirectPlotting()
        {
            double[][] data = new double[][]
            {
                new [] { 769, 771, 657.5, 658.5 },
                new double[] { 769, 773, 809, 810 },
                new double[] {769, 772, 619, 620},
                new double[] {769, 775, 0, 0},
                new double[] {769, 776, 0, 0},
                new double[] {769, 777, 0, 0},
                new double[] {2534, 2536, 0, 0},
                new double[] {2534, 2542, 0, 0},
            };

            var selectedData = data[1];
            int scanNumber = (int)selectedData[0];
            int ms2ScanNumber = (int)selectedData[1];
            string outDir = @"C:\Users\Nic\OneDrive - UW-Madison\AveragingPaper\ReReResubmissionBundle\SI4";
            int exportWidth = 400;
            int exportHeight = 300;

            // zoomed
            bool doZoomed = true;
            double zoomedMin = selectedData[2];
            double zoomedMax = selectedData[3];
            int zoomedExportWidth = 100;
            int zoomedExportHeight = 100;
            var zoomedRange = new DoubleRange(zoomedMin, zoomedMax);

            var calibFilePath =
                @"D:\Projects\SpectralAveraging\PaperTestOutputs\JurkatTopDown_DeconvolutionAnalysis\Rep2Calib\02-18-20_jurkat_td_rep2_fract7-calib.mzML";
            var avgNoRejectionFilePath =
                @"D:\Projects\SpectralAveraging\PaperTestOutputs\JurkatTopDown_DeconvolutionAnalysis\Rep2CalibAverageNoRejection\id_02-18-20_jurkat_td_rep2_fract7-calib-averaged.mzML";
            var averagedFilepath =
                @"D:\Projects\SpectralAveraging\PaperTestOutputs\JurkatTopDown_DeconvolutionAnalysis\Rep2CalibAveraged\02-18-20_jurkat_td_rep2_fract7-calib-averaged.mzML";

            var calibFile = new Mzml(calibFilePath);
            var calibOut = Path.Combine(outDir, $"{scanNumber}_{ms2ScanNumber}_Calib.png");
            var calibColor = OxyColor.FromRgb(51, 255, 51);
            var title = $"Fraction 7 - Calibrated Only - Scan {ms2ScanNumber} - Isolation Window";
            calibFile.InitiateDynamicConnection();
            var calibScan = calibFile.GetOneBasedScanFromDynamicConnection(scanNumber);
            var range = calibFile.GetOneBasedScanFromDynamicConnection(ms2ScanNumber).IsolationRange;
            calibFile.CloseDynamicConnection();
            var calibPlot = new IsolationWindowPlot(new PlotView(), calibScan, range, calibColor, title);
            calibPlot.ExportToPng(calibOut, exportWidth, exportHeight);
            if (doZoomed)
            {
                var zoomedCalibPlot = new IsolationWindowPlot(new PlotView(), calibScan, zoomedRange, calibColor, title, true);
                var zoomedCalibOut = Path.Combine(outDir, $"{scanNumber}_{ms2ScanNumber}_Calib_Zoomed.png");
                zoomedCalibPlot.ExportToPng(zoomedCalibOut, zoomedExportWidth, zoomedExportHeight);
            }


            var averagedFile = MsDataFileReader.GetDataFile(averagedFilepath);
            var avgOut = Path.Combine(outDir, $"{scanNumber}_{ms2ScanNumber}_Averaged.png");
            var avgColor = OxyColor.FromRgb(102, 0, 153);
            title = $"Fraction 7 - Averaged With Rejection - Scan {ms2ScanNumber} - Isolation Window";
            averagedFile.InitiateDynamicConnection();
            var averagedScan = averagedFile.GetOneBasedScanFromDynamicConnection(scanNumber);
            range = averagedFile.GetOneBasedScanFromDynamicConnection(ms2ScanNumber).IsolationRange;
            averagedFile.CloseDynamicConnection();
            var avgPlot = new IsolationWindowPlot(new PlotView(), averagedScan, range, avgColor, title);
            avgPlot.ExportToPng(avgOut, exportWidth, exportHeight);
            if (doZoomed)
            {
                var zoomedAvgPlot = new IsolationWindowPlot(new PlotView(), averagedScan, zoomedRange, avgColor, title, true);
                var zoomedAvgOut = Path.Combine(outDir, $"{scanNumber}_{ms2ScanNumber}_Averaged_Zoomed.png");
                zoomedAvgPlot.ExportToPng(zoomedAvgOut, zoomedExportWidth, zoomedExportHeight);
            }


            var avgNoRejectionFile = MsDataFileReader.GetDataFile(avgNoRejectionFilePath);
            var avgNoRejectionOut = Path.Combine(outDir, $"{scanNumber}_{ms2ScanNumber}_AveragedNoRejection.png");
            var avgNoRejectionColor = OxyColor.FromRgb(51, 153, 255);
            title = $"Fraction 7 - Averaged No Rejection - Scan {ms2ScanNumber} - Isolation Window";
            avgNoRejectionFile.InitiateDynamicConnection();
            var avgNoRejectionScan = avgNoRejectionFile.GetOneBasedScanFromDynamicConnection(scanNumber);
            range = avgNoRejectionFile.GetOneBasedScanFromDynamicConnection(ms2ScanNumber).IsolationRange;
            avgNoRejectionFile.CloseDynamicConnection();
            var avgNoRejectionPlot = new IsolationWindowPlot(new PlotView(), avgNoRejectionScan, range, avgNoRejectionColor, title);
            avgNoRejectionPlot.ExportToPng(avgNoRejectionOut, exportWidth, exportHeight);
            if (doZoomed)
            {
                var zoomedAvgNoRejectionPlot = new IsolationWindowPlot(new PlotView(), avgNoRejectionScan, zoomedRange, avgNoRejectionColor, title, true);
                var zoomedAvgNoRejectionOut = Path.Combine(outDir, $"{scanNumber}_{ms2ScanNumber}_AveragedNoRejection_Zoomed.png");
                zoomedAvgNoRejectionPlot.ExportToPng(zoomedAvgNoRejectionOut, zoomedExportWidth, zoomedExportHeight);
            }
            







            //var temp = new MirrorSpectrum(new PlotView(), calibScan, averagedScan, range);
            
            //string outPath = @"C:\Users\Nic\Downloads\810.png";
            //temp.ExportToPng(outPath);

        }


        private void GetScanPlot(MsDataScan scan, DoubleRange range)
        {

        }


        // Parsing full dataset  \/


        [Test]
        public static void ChimeraAnalysis3()
        {
            bool outputChimeraCounting = false;
            string directoryPath = @"D:\Projects\SpectralAveraging\PaperTestOutputs\Searches\Refined\Sigma5AllBellsAndWhistles"; // TODO: remove Sigma5AllBellsAndWhistles
            
            var allPsms = directoryPath.GetAllPsmsWithinScanRange(1000, 2000).ToList();
            
            // group psms by DataFile after removing calib and averaging suffix, then by ms1 scan number and dataset, then by ms2 scan number and order by score
            var groupedPsms = allPsms
                .GroupBy(p => p.FileNameWithoutExtension.Replace("-calib", "").Replace("-averaged", ""), p => p, (key, g) => new { DataFile = key, Psms = g })
                .ToDictionary(p => p.DataFile, p => p.Psms
                                   .GroupBy(p => p.PrecursorScanNum, p => p, (key, g) => new { Ms1ScanNum = key, Psms = g })
                                   .ToDictionary(p => p.Ms1ScanNum, p => p.Psms
                                                          .GroupBy(p => p.Dataset, p => p, (key, g) => new { Dataset = key, Psms = g })
                                                          .ToDictionary(p => p.Dataset, p => p.Psms
                                                                                     .GroupBy(p => p.Ms2ScanNumber, p => p, (key, g) => new { Ms2ScanNum = key, Psms = g })
                                                                                     .ToDictionary(p => p.Ms2ScanNum, p => p.Psms
                                                                                                                    .OrderByDescending(p => p.Score)
                                                                                                                    .ToList()))));
            // File Name
            //  Ms1 Ms2Scan Number
            //      Dataset (condition)
            //          Ms2 Ms2Scan Number



            if (outputChimeraCounting)
            {
                // collect the number of psms based upon the ms2 scan number and datafile and get ready for export in csv format with the column headers being the datafile, ms2 scan number, dataset, and psm count
                var toWrite = new List<string[]>();
                foreach (var dataFile in groupedPsms)
                {
                    foreach (var ms1 in dataFile.Value)
                    {
                        foreach (var dataset in ms1.Value)
                        {
                            foreach (var ms2 in dataset.Value)
                            {
                                toWrite.Add(new string[]
                                    { dataFile.Key, ms2.Key.ToString(), dataset.Key, ms2.Value.Count.ToString() });
                            }
                        }
                    }
                }

                // write to new csv file in same directory as the input directory
                string outPath = Path.Combine(directoryPath, "ChimeraCountingAnalysis.csv");
                using (var sw = new StreamWriter(File.Create(outPath)))
                {
                    var header = new string[]
                    {
                        "DataFile",
                        "MS2 Scan Number",
                        "Dataset",
                        "PSM Count"
                    };
                    sw.WriteLine(string.Join(',', header));
                    foreach (var line in toWrite)
                        sw.WriteLine(string.Join(',', line));
                }
            }

            // TODO: Figure out how to parse this data to get the information I want out of it

            Dictionary<string, Dictionary<int, Dictionary<string, Dictionary<int, List<PsmFromTsv>>>>> trimmed = new();
            foreach (var dataFile in groupedPsms)
            {
                Dictionary<int, Dictionary<string, Dictionary<int, List<PsmFromTsv>>>> ms1SetsDictionary = new();
                foreach (var ms1Set in dataFile.Value.OrderBy(p => p.Key))
                {
                    // if any of the datasets have at least 3 psms, from the same ms1, that share a base sequence but different precursor charges
                    bool retain = false;
                    Dictionary<string, Dictionary<int, List<PsmFromTsv>>> ugh = new();
                    foreach (var sequenceChargeByMs1 in ms1Set.Value.Select(dataset => dataset.Value.Select(p =>
                                 p.Value.Select(m => (m.BaseSeq, m.PrecursorCharge)).ToArray()).ToArray()))
                    {
                        foreach (var sequenceChargeByDataset in sequenceChargeByMs1)
                        {
                            if (sequenceChargeByDataset
                                .Select(sequenceCharge => 
                                    sequenceChargeByMs1.Count(p => 
                                        p.Any(m => m.BaseSeq == sequenceCharge.BaseSeq 
                                                   && m.PrecursorCharge != sequenceCharge.PrecursorCharge)))
                                .Any(count => count >= 3))
                            {
                                retain = true;
                                break;
                            }
                        }

                        if (retain)
                        {
                          

                        }
                    }
                }
            }

        }



        //public static Dictionary<string, string> AllDataFiles = new Dictionary<string, string>()
        //{
        //    // calib
        //    {"02-17-20_jurkat_td_rep2_fract2-calib", @"D:\Projects\SpectralAveraging\PaperTestOutputs\Searches\Refined\Sigma5AllBellsAndWhistles\Task1-CalibrateTask\02-17-20_jurkat_td_rep2_fract2-calib.mzML"},
        //    {"02-17-20_jurkat_td_rep2_fract3-calib", @"D:\Projects\SpectralAveraging\PaperTestOutputs\Searches\Refined\Sigma5AllBellsAndWhistles\Task1-CalibrateTask\02-17-20_jurkat_td_rep2_fract3-calib.mzML"},
        //    {"02-17-20_jurkat_td_rep2_fract4-calib", @"D:\Projects\SpectralAveraging\PaperTestOutputs\Searches\Refined\Sigma5AllBellsAndWhistles\Task1-CalibrateTask\02-17-20_jurkat_td_rep2_fract4-calib.mzML"},
        //    {"02-18-20_jurkat_td_rep2_fract5-calib", @"D:\Projects\SpectralAveraging\PaperTestOutputs\Searches\Refined\Sigma5AllBellsAndWhistles\Task1-CalibrateTask\02-18-20_jurkat_td_rep2_fract5-calib.mzML"},
        //    {"02-18-20_jurkat_td_rep2_fract6-calib", @"D:\Projects\SpectralAveraging\PaperTestOutputs\Searches\Refined\Sigma5AllBellsAndWhistles\Task1-CalibrateTask\02-18-20_jurkat_td_rep2_fract6-calib.mzML"},
        //    {"02-18-20_jurkat_td_rep2_fract7-calib", @"D:\Projects\SpectralAveraging\PaperTestOutputs\Searches\Refined\Sigma5AllBellsAndWhistles\Task1-CalibrateTask\02-18-20_jurkat_td_rep2_fract7-calib.mzML"},
        //    {"02-18-20_jurkat_td_rep2_fract8-calib", @"D:\Projects\SpectralAveraging\PaperTestOutputs\Searches\Refined\Sigma5AllBellsAndWhistles\Task1-CalibrateTask\02-18-20_jurkat_td_rep2_fract8-calib.mzML"},
        //    {"02-18-20_jurkat_td_rep2_fract9-calib", @"D:\Projects\SpectralAveraging\PaperTestOutputs\Searches\Refined\Sigma5AllBellsAndWhistles\Task1-CalibrateTask\02-18-20_jurkat_td_rep2_fract9-calib.mzML"},
        //    {"02-18-20_jurkat_td_rep2_fract10-calib", @"D:\Projects\SpectralAveraging\PaperTestOutputs\Searches\Refined\Sigma5AllBellsAndWhistles\Task1-CalibrateTask\02-18-20_jurkat_td_rep2_fract10-calib.mzML"},
        //    {"", @},

        //    // no rejection
        //    {"", @"D:\Projects\SpectralAveraging\PaperTestOutputs\Searches\Refined\NoRejection5AllBellsAndWhistles\Task2-AveragingTask\02-17-20_jurkat_td_rep2_fract2-calib-averaged.mzML"},
        //    {"", @"D:\Projects\SpectralAveraging\PaperTestOutputs\Searches\Refined\NoRejection5AllBellsAndWhistles\Task2-AveragingTask\02-17-20_jurkat_td_rep2_fract3-calib-averaged.mzML"},
        //    {"", @"D:\Projects\SpectralAveraging\PaperTestOutputs\Searches\Refined\NoRejection5AllBellsAndWhistles\Task2-AveragingTask\02-17-20_jurkat_td_rep2_fract4-calib-averaged.mzML"},
        //    {"", @"D:\Projects\SpectralAveraging\PaperTestOutputs\Searches\Refined\NoRejection5AllBellsAndWhistles\Task2-AveragingTask\02-18-20_jurkat_td_rep2_fract5-calib-averaged.mzML"},
        //    {"", @"D:\Projects\SpectralAveraging\PaperTestOutputs\Searches\Refined\NoRejection5AllBellsAndWhistles\Task2-AveragingTask\02-18-20_jurkat_td_rep2_fract5-calib-averaged.mzML"},
        //    {"", @"D:\Projects\SpectralAveraging\PaperTestOutputs\Searches\Refined\NoRejection5AllBellsAndWhistles\Task2-AveragingTask\02-18-20_jurkat_td_rep2_fract5-calib-averaged.mzML"},
        //    {"", @"D:\Projects\SpectralAveraging\PaperTestOutputs\Searches\Refined\NoRejection5AllBellsAndWhistles\Task2-AveragingTask\02-18-20_jurkat_td_rep2_fract5-calib-averaged.mzML"},
        //    {"", @"D:\Projects\SpectralAveraging\PaperTestOutputs\Searches\Refined\NoRejection5AllBellsAndWhistles\Task2-AveragingTask\02-18-20_jurkat_td_rep2_fract5-calib-averaged.mzML"},
        //    {"", @"D:\Projects\SpectralAveraging\PaperTestOutputs\Searches\Refined\NoRejection5AllBellsAndWhistles\Task2-AveragingTask\02-18-20_jurkat_td_rep2_fract5-calib-averaged.mzML"},


        //    // sigma

        //    // averaged sigma
        //};
    }

    public static class ClassExtensions
    {
      

        

        public static IEnumerable<PsmFromTsv> GetAllPsmsWithinScanRange(this string directoryPath, int minScan, int maxScan)
        {
            var files = Directory.GetFiles(directoryPath, "*AllPSMs.psmtsv", SearchOption.AllDirectories)
                .Where(p => !p.Contains("NoBellsAndWhi"));
            foreach (var file in files)
            {
                var path = Path.GetDirectoryName(file)!.Split('\\')[^2];
                var psms = PsmTsvReader.ReadTsv(file, out _);
                foreach (var psm in psms.Where(psm => psm.PrecursorScanNum >= minScan && psm.PrecursorScanNum <= maxScan))
                {
                    psm.Dataset = path;
                    yield return psm;
                }
            }
        }
    }
}
