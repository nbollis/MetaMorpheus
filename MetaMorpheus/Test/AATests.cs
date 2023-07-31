using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Easy.Common.Interfaces;
using EngineLayer;
using EngineLayer.HistogramAnalysis;
using MathNet.Numerics.Statistics;
using NUnit.Framework;
using OxyPlot.Series;

namespace Test
{
    [TestFixture]
    public static class AATests
    {

        public static string SearchDirectoryPath = @"D:\Projects\Top Down MetaMorpheus\NB_Replicate_1.0.2\WithoutLibrary\Search";
        public static string SearchDirectoryPath12 = @"D:\Projects\Top Down MetaMorpheus\NB_Replicate_1.0.2\WithoutLibrary\Search_12ppmPrecursorTolerance";

        public static string CaliDirectorPath =
            @"D:\Projects\Top Down MetaMorpheus\NB_Replicate_1.0.2\WithoutLibrary\CaliSearch";
        public static string CaliAvgDirectorPath =
            @"D:\Projects\Top Down MetaMorpheus\NB_Replicate_1.0.2\WithoutLibrary\CaliAvg_Search";

        public static string VariableDirectoryPath =
            @"D:\Projects\Top Down MetaMorpheus\NB_Replicate_1.0.2\WithoutLibrary\CaliAvg_VarPhoAce_Search";

        public static string VariableInternalDirectoryPath =
            @"D:\Projects\Top Down MetaMorpheus\NB_Replicate_1.0.2\WithoutLibrary\CaliAvg_VarPhoAce_SearchWithInternal";

         public static string PhoAceGPTMDDirectoryPath =
            @"D:\Projects\Top Down MetaMorpheus\NB_Replicate_1.0.2\WithoutLibrary\CaliAvg_PhoAceGPTMD_Search";

         public static string PhoAceGPTMDInternalDirectoryPath =
            @"D:\Projects\Top Down MetaMorpheus\NB_Replicate_1.0.2\WithoutLibrary\CaliAvg_PhoAceGPTMD_SearchWithInternal";

         public static string BigGPTMDDirectoryPath =
            @"D:\Projects\Top Down MetaMorpheus\NB_Replicate_1.0.2\WithoutLibrary\CaliAvg_BioMetArtGPTMD_Search";

         public static string BigGPTMDInternalDirectoryPath =
            @"D:\Projects\Top Down MetaMorpheus\NB_Replicate_1.0.2\WithoutLibrary\CaliAvg_BioMetArtGPTMD_SearchWithInternal";


        #region Helpers

        private static string GetPsmsPath(string directoryPath)
        {
            var directories = Directory.GetDirectories(directoryPath);
            var searchTask = directories.First(p => p.Contains("SearchTask"));
            var files = Directory.GetFiles(searchTask);
            return files.First(p => p.Contains("AllPSMs.psmtsv"));
        }
        private static string GetProteoformsPath(string directoryPath)
        {
            var directories = Directory.GetDirectories(directoryPath);
            var searchTask = directories.First(p => p.Contains("SearchTask"));
            var files = Directory.GetFiles(searchTask);
            return files.First(p => p.Contains("AllProteoforms.psmtsv"));
        }

        private static List<PsmFromTsv> GetFilteredPsms(List<PsmFromTsv> unfilteredPsms)
        {
            return unfilteredPsms.Where(p =>
                p.DecoyContamTarget == "T" &&
                //p.Notch == "0" &&
                !p.MassDiffPpm.Contains("|") &&
                (p.FileNameWithoutExtension.Contains("FXN5")
                 || p.FileNameWithoutExtension.Contains("FXN6")
                 || p.FileNameWithoutExtension.Contains("FXN7")
                 || p.FileNameWithoutExtension.Contains("FXN8")) &&
                p.QValue <= 0.01)
                .ToList();
        }

        private static List<double>  GetAllFragmentIonPpmErrors(List<PsmFromTsv> psms)
        {
            return psms.SelectMany(p => p.MatchedIons.Select(m => m.MassErrorPpm))
                .ToList();
        }

        #endregion

        [Test]
        public static void GetHistogramInfo()
        {
            // data loading
            var search = PsmTsvReader.ReadTsv(GetPsmsPath(BigGPTMDInternalDirectoryPath), out List<string> warnings)
                .Where(p => p.QValue <= 0.01).ToList();

            var temp = search.GroupBy(p => p.AmbiguityLevel);

            string outpath =
                    @"D:\Projects\Top Down MetaMorpheus\Writing and Important Documents\AmbiguityPaper\biometartGptmdInternal.csv";


            var results = temp.ToDictionary(p => p.Key, p => p.Count());

            using (var sw = new StreamWriter(File.Create(outpath)))
            {
                foreach (var value in temp)
                {
                    sw.WriteLine($"{value.Key},{value.Count()}");
                }
            }

        }


        [Test]
        public static void PrecursorHist()
        {
            double binSize = 0.1;
            double min = -20;
            double max = 20;


            var filteredSearch12 =
                GetFilteredPsms(PsmTsvReader.ReadTsv(GetPsmsPath(SearchDirectoryPath12), out List<string> warnings));
            var searchGram = new Histogram(filteredSearch12.Select(p => double.Parse(p.MassDiffPpm)), binSize, min, max, "Search");
            var filteredCalibSearch =
                GetFilteredPsms(PsmTsvReader.ReadTsv(GetPsmsPath(CaliDirectorPath), out warnings));
            var calibGram = new Histogram(filteredCalibSearch.Select(p => double.Parse(p.MassDiffPpm)), binSize, min, max, "Calibrated");
            var filteredCalibAvgSearch =
                GetFilteredPsms(PsmTsvReader.ReadTsv(GetPsmsPath(CaliAvgDirectorPath), out warnings));
            var avgGram = new Histogram(filteredCalibAvgSearch.Select(p => double.Parse(p.MassDiffPpm)), binSize, min, max, "Calibrated-Averaged");

            string outpath = @"D:\Projects\Top Down MetaMorpheus\NB_Replicate_1.0.2\WithoutLibrary\Grams\Targets58FDR.csv";
            new List<Histogram> { searchGram, calibGram, avgGram }.ExportMultipleGrams(outpath);
        }

        [Test]
        public static void FragmentIonHist()
        {
            double binSize = 0.1;
            double min = -20;
            double max = 20;


            var filteredSearch12 =
                GetFilteredPsms(PsmTsvReader.ReadTsv(GetPsmsPath(SearchDirectoryPath12), out List<string> warnings));
            var searchGram = new Histogram(GetAllFragmentIonPpmErrors(filteredSearch12), binSize, min, max, "Search");
            var filteredCalibSearch =
                GetFilteredPsms(PsmTsvReader.ReadTsv(GetPsmsPath(CaliDirectorPath), out warnings));
            var calibGram = new Histogram(GetAllFragmentIonPpmErrors(filteredCalibSearch), binSize, min, max, "Calibrated");
            var filteredCalibAvgSearch =
                GetFilteredPsms(PsmTsvReader.ReadTsv(GetPsmsPath(CaliAvgDirectorPath), out warnings));
            var avgGram = new Histogram(GetAllFragmentIonPpmErrors(filteredCalibAvgSearch), binSize, min, max, "Calibrated-Averaged");

            string outpath = @"D:\Projects\Top Down MetaMorpheus\NB_Replicate_1.0.2\WithoutLibrary\Grams\Frag_Targets58FDR.csv";
            new List<Histogram> { searchGram, calibGram, avgGram }.ExportMultipleGrams(outpath);
        }

        [Test]
        public static void TestTimeProfiler()
        {
            TimeProfiler.MarkTime("start");
            Thread.Sleep(3000);
            TimeProfiler.MarkTime("start");
            TimeProfiler.MarkTime("start");


            TimeProfiler.MarkTime("end");
            TimeProfiler.ExportProfiling(@"C:\Users\Nic\Downloads\test");
            TimeProfiler.ExportProfiling(@"C:\Users\Nic\Downloads\test.csv");
        }
    }
}
