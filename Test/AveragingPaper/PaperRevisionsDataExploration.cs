using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using EngineLayer;
using NUnit.Framework;

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

        [Test]
        public static void ChimeraAnalysis()
        {
            
            var calibPrecursor = CalibPsms.GroupBy(tsv => tsv, ChimeraComparer)
                .ToDictionary(p => p.Key, p => p.ToList());
            //var calibChimeraGroups = calibPrecursor.GroupBy(p => p.Value.Count)
            //    .OrderBy(p => p.Key)
            //    .ToDictionary(p => p.Key, p => p.ToList());
            //var calibCounts = calibChimeraGroups.Select(p => (p.Key, p.Value.Count)).ToList();

            var averagedPrecursor = AveragedPsms.GroupBy(tsv => tsv, ChimeraComparer)
                .ToDictionary(p => p.Key, p => p.ToList());
            //var averagedChimeraGroups = averagedPrecursor.GroupBy(p => p.Value.Count)
            //    .OrderBy(p => p.Key)
            //    .ToDictionary(p => p.Key, p => p.ToList());
            //var averagedCounts = averagedChimeraGroups.Select(p => (p.Key, p.Value.Count)).ToList();

            Dictionary<(int ScanNum, string DataFile), (List<PsmFromTsv> Calib, List<PsmFromTsv> Averaged)> GroupedDictionary =
                new Dictionary<(int, string), (List<PsmFromTsv> Calib, List<PsmFromTsv> Averaged)>();

            foreach (var avg in averagedPrecursor)
            {
                var scanNum = avg.Key.Ms2ScanNumber;
                var dataFile = avg.Key.FileNameWithoutExtension.Replace("-averaged", "");
                var calib = calibPrecursor.FirstOrDefault(p => p.Key.Ms2ScanNumber == scanNum && p.Key.FileNameWithoutExtension == dataFile);
                if ( calib.Value is not null && calib.Value.Any() && avg.Value.Any()) 
                    GroupedDictionary.Add((scanNum, dataFile), (calib.Value, avg.Value));
            }

            var differenceDictionary = GroupedDictionary.Where(p => p.Value.Calib.Count < p.Value.Averaged.Count)
                .OrderByDescending(p => p.Value.Averaged.Average(m => m.Score))
                .ThenByDescending(p => p.Value.Calib.Average(m => m.Score))
                .ThenByDescending(p => p.Value.Averaged.Count + p.Value.Calib.Count)
                .ToDictionary(p => p.Key, p => p.Value);
        }




       
    }
}
