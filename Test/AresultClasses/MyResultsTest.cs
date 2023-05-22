using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using NUnit.Framework;
using Test.AveragingPaper;

namespace Test
{
    [TestFixture]
    public class MyResultsTest
    {
        public const string msAlignParsingPath =
            @"C:\Users\Nic\OneDrive - UW-Madison\AUSTIN V CARR - AUSTIN V CARR's files\SpectralAveragingPaper\ResultsData\Deconvolution\SecondRun\msAlignParsing.csv";

        public const string directoryPath =
            @"D:\Averaging";

        [Test]
        public static void MakeOutput()
        {
            var directories = Directory.GetDirectories(directoryPath)
                .Where(p => !p.Contains("MMTask", StringComparison.CurrentCultureIgnoreCase))
                .ToList();
            MyResults results = new MyResults(msAlignParsingPath);

            // foreach entry, add desired files
            foreach (var entry in results.Entries.Take(10))
            {
                var targetDirectory = directories
                    .First(p => p.Split("\\").Last().Equals($"Rep{entry.Rep}{entry.Processing}"));
                Assert.That(targetDirectory, Is.EqualTo(entry.DirPath));

                var withinTargetDirectory = Directory.GetDirectories(targetDirectory);
                if (withinTargetDirectory.Any(p => p.Contains("FlashDeconv")) && entry.DeconSoftware.Equals("FLASHDecon"))
                {
                    var files = Directory.GetFiles(Path.Combine(targetDirectory, "FlashDeconv"))
                        .Where(p => p.Contains($"fract{entry.Fraction}")).ToList();
                    var ms1FeatureFilePath = files.First(p => p.EndsWith("ms1.feature"));
                    var ms1TsvPath = files.First(p => p.EndsWith("ms1.tsv"));

                    var ms1FeatureFile = new Ms1FeatureFile(ms1FeatureFilePath,
                        DeconResults.DeconSoftware.FLASHDeconv, entry.Dataset, entry.Processing);
                    var ms1TsvFile = new FlashDeconMs1TsvFile(ms1TsvPath);
                    entry.AddFeatureFile(ms1FeatureFile);
                    entry.AddFlashDeconMs1Tsv(ms1TsvFile);
                }

                if (withinTargetDirectory.Any(p => p.Contains("TopFD")) && entry.DeconSoftware.Equals("TopFD"))
                {
                    var files = Directory.GetFiles(Path.Combine(targetDirectory, "TopFD"))
                        .Where(p => p.Contains($"fract{entry.Fraction}")).ToList();
                    var ms1FeatureFilePath = files.First(p => p.EndsWith("ms1.feature"));
                    var ms1FeatureFile = new Ms1FeatureFile(ms1FeatureFilePath,
                        DeconResults.DeconSoftware.TopFD, entry.Dataset, entry.Processing);
                    entry.AddFeatureFile(ms1FeatureFile);
                }

                if (withinTargetDirectory.Any(p => p.Contains("MMSearch")))
                {
                    // TODO: add this functionality and fields to entry
                }
            }

            // outputResults
            string outDirectory =
                @"C:\Users\Nic\OneDrive - UW-Madison\AUSTIN V CARR - AUSTIN V CARR's files\SpectralAveragingPaper\ResultsData\Deconvolution\SecondRun";
            string outpath = Path.Combine(outDirectory, "parsed.csv");
            using var csv = new CsvWriter(new StreamWriter(File.Create(outpath)), MyResultEntry.CsvConfiguration);
            csv.WriteHeader<MyResultEntry>();
            csv.WriteRecords(results.Entries);

        }
    }
}
