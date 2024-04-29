using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Test.ChimeraPaper.ResultFiles;

namespace Test.ChimeraPaper
{
    internal class TDRunner
    {
        internal static string DirectoryPath = @"B:\Users\Nic\Chimeras\TopDown_Analysis";
        internal static bool RunOnAll = false;
        internal static bool Override = false;

        [Test]
        public static void RunAllParsing()
        {
            var datasets = Directory.GetDirectories(DirectoryPath)
                .Where(p => !p.Contains("Figures") && RunOnAll || p.Contains("Jurkat"))
                .Select(datasetDirectory => new CellLineResults(datasetDirectory)).ToList();
            var allResults = new AllResults(DirectoryPath, datasets);
            foreach (var cellLine in allResults)
            {
                foreach (var result in cellLine)
                {
                    if (result is MsPathFinderTResults)
                        result.Override = true;
                    result.IndividualFileComparison();
                    result.GetBulkResultCountComparisonFile();
                    result.CountChimericPsms();
                    if (result is MetaMorpheusResult mm)
                        mm.CountChimericPeptides();
                    result.Override = false;
                }

                cellLine.Override = true;
                cellLine.IndividualFileComparison();
                cellLine.GetBulkResultCountComparisonFile();
                cellLine.CountChimericPsms();
                cellLine.CountChimericPeptides();
                cellLine.Override = false;
            }

            allResults.Override = true;
            allResults.IndividualFileComparison();
            allResults.GetBulkResultCountComparisonFile();
            allResults.CountChimericPsms();
            allResults.CountChimericPeptides();
        }

        [Test]
        public static void RunAllPlots()
        {
            var datasets = Directory.GetDirectories(DirectoryPath)
                .Where(p => !p.Contains("Figures") && RunOnAll || p.Contains("Jurkat"))
                .Select(datasetDirectory => new CellLineResults(datasetDirectory)).ToList();
            var allResults = new AllResults(DirectoryPath, datasets);
            foreach (var cellLine in allResults)
            {
                cellLine.Override = true;
                cellLine.PlotIndividualFileResults();
            }

            allResults.Override = true;
            allResults.PlotInternalMMComparison();
            allResults.PlotBulkResultComparison();
            allResults.PlotStackedIndividualFileComparison();
        }

        [Test]
        public static void FigureOutMsPathFinderT()
        {
            var allResults = new AllResults(DirectoryPath);

            foreach (var cellLine in allResults)
            {
                foreach (var result in cellLine)
                {
                    if (result is MsPathFinderTResults ms)
                    {
                        ms.CreateDatasetInfoFile();
                    }
                }
            }



        }

        // This is a helper method to get the conversion dictionary for the file names
        [Test]
        public static void GetConversionDictionary()
        {
            var allResults = new AllResults(DirectoryPath);
            var toPull = allResults.First(p => p.CellLine == "Ecoli").SelectMany(p => p.IndividualFileComparisonFile.Select(m => m.FileName))
                .Distinct()
                .ToArray();
            var sb = new StringBuilder();

            foreach (var fileName in toPull.Where(p => p.Contains("jurk")))
            {
                var trimmed = string.Join("_", fileName.Split('_')[^2..]);
                var cleaned = trimmed.Replace("rep", "").Replace("fract", "");
                sb.AppendLine($"{{\"{fileName}\",\"{cleaned}\"}},");
            }

            foreach (var fileName in toPull.Where(p => p.Contains("Ecol")))
            {
                var trimmed = string.Join("_", fileName.Split('_')[^2..]);
                var cleaned = trimmed.Replace("SEC", "").Replace("F", "");
                if (cleaned.Split('_')[1].Length == 1)
                    cleaned = cleaned.Insert(2, "0");
                sb.AppendLine($"{{\"{fileName}\",\"{cleaned}\"}},");
            }

            var result = sb.ToString();
        }


    }
}
