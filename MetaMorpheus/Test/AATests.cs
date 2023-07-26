using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using MathNet.Numerics.Statistics;
using NUnit.Framework;

namespace Test
{
    [TestFixture]
    public static class AATests
    {

        public static string SearchDirectoryPath = @"D:\Projects\Top Down MetaMorpheus\NB_Replicate_1.0.2\WithoutLibrary\Search";

        public static string CaliDirectorPath =
            @"D:\Projects\Top Down MetaMorpheus\NB_Replicate_1.0.2\WithoutLibrary\CaliSearch";

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

        public record struct HistogramBin(double min, double max, int count);
        private static List<HistogramBin> GenerateHistogram(double binWidth, List<double> values)
        {
            var min = values.Min();
            var max = values.Max();
            List<HistogramBin> bins = new();
            for (double i = min-binWidth; i < max + binWidth; i+=binWidth)
            {
                HistogramBin bin = new(i, i + binWidth, 0);
                var count = values.Count(p => p >= bin.min && p < bin.max);
                bin.count = count;
                bins.Add(bin);
            }

            return bins;
        }

        public static void ExportHistogram(this List<HistogramBin> histogramBinLines, string outPath)
        {
            using (var sw = new StreamWriter(File.Create(outPath)))
            {
                sw.WriteLine("min,max,avg,count");
                histogramBinLines.ForEach(p => sw.WriteLine($"{p.min},{p.max},{(p.min + p.max) / 2},{p.count}"));
            }
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

        
    }
}
