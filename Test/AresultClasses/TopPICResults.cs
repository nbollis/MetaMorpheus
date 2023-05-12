using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Easy.Common.Extensions;
using NUnit.Framework;

namespace Test
{

    public class TopPICResults
    {
        public string FilePath { get; }
        public string FileName { get; }
        public int Count { get;  }
        public TopPICResults(string filePath)
        {
            FilePath = filePath;
            FileName = Path.GetFileNameWithoutExtension(FilePath);

            var lines = File.ReadAllLines(FilePath);

            var headerIndex = lines.IndexOf(lines.First(p => p.StartsWith("Data file name")));
            Count = lines.Length - headerIndex - 1;
        }

    }

    [TestFixture]
    public static class TestTopPicResults
    {
        public static string AveragedDirectory =>
            @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\TopPIC\CalibAveraged";
        public static string ControlDirectory =>
            @"B:\Users\Nic\ScanAveraging\AveragedDataBulkJurkat\TopPIC\Calib";


        public static string[] GetFiles(string filepath, string type)
        {
            return type switch {
                "PrSMs" => Directory.GetFiles(filepath).Where(p => p.EndsWith("prsm.tsv")).OrderBy(p => p).ToArray(),
                "Proteoforms" => Directory.GetFiles(filepath).Where(p => p.EndsWith("proteoform.tsv")).OrderBy(p => p).ToArray(),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        [Test]
        public static void GetCounts()
        {
            var controlForms = GetFiles(ControlDirectory, "Proteoforms");
            var controlPsms = GetFiles(ControlDirectory, "PrSMs");
            var averagedForms = GetFiles(AveragedDirectory, "Proteoforms");
            var averagedPsms = GetFiles(AveragedDirectory, "PrSMs");

            List < (string Name, string Group, int PrSMs, int Proteoforms) > results = new();

            for (int i = 0; i < controlForms.Length; i++)
            {
                var controlForm = new TopPICResults(controlForms[i]);
                var controlPsm = new TopPICResults(controlPsms[i]);
                var averagedForm = new TopPICResults(averagedForms[i]);
                var averagePsms = new TopPICResults(averagedPsms[i]);
                results.Add(new (controlForm.FileName, "Control", controlPsm.Count, controlForm.Count));
                results.Add(new (averagedForm.FileName, "Averaged", averagePsms.Count, averagedForm.Count));
            }


        }
    }
}
