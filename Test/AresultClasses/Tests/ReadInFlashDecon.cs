using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using Easy.Common.Extensions;
using MathNet.Numerics;
using NUnit.Framework;
using Plotly.NET;
using Readers;
using Test.AveragingPaper;

namespace Test
{
    [TestFixture]
    public class ReadInFlashDecon
    {
        [Test]
        public static void GenerateIsoEnvelopeDeconOutput()
        {

            using var sw = new StreamWriter(File.Create(Path.Combine(ResultPaths.OutDirectory, "isoEnvelopesWithFiltering.csv")));
            sw.WriteLine("DataSet,FileName,FileGroup,Software,QValue,MassCountInSpec,PeakCount,IsotopeCosine,MassSNR,ChargeSNR");
            using var sw2 = new StreamWriter(File.Create(Path.Combine(ResultPaths.OutDirectory, "isoEnvelopesCombined.csv")));
            sw2.WriteLine("DataSet,FileName,FileGroup,Software,Filtered,QValue,MassCountInSpec,PeakCount,IsotopeCosine,MassSNR,ChargeSNR");
            
            foreach (var pair in ResultPaths.JurkatDirectoryDictionary.Where(p => p.Key == DeconResults.DeconSoftware.FLASHDeconv))
            {
                var controlMs1TsvPaths = ResultPaths.GetMs1TsvFiles(pair.Value.Control);
                var averagedMs1TsvPaths = ResultPaths.GetMs1TsvFiles(pair.Value.Averaged);
                var noRejection = ResultPaths.GetMs1TsvFiles(pair.Value.NoRejection);
                for (int i = 0; i < controlMs1TsvPaths.Length; i++)
                {
                    var control = new FlashDeconMs1TsvFile(controlMs1TsvPaths[i]);
                    var averaged = new FlashDeconMs1TsvFile(averagedMs1TsvPaths[i]);
                    var noRejectionFile = new FlashDeconMs1TsvFile(noRejection[i]);

                    var targetsControl = control.Entries.Where(p => p.Decoy == 0).ToList();
                    var filteredControl = control.Entries.Where(p => p.Decoy == 0 && p.Qvalue <= 0.01).ToList();
                    filteredControl.ForEach(p =>
                    sw.WriteLine($"Jurkat,{GetFileName(p.FileName)},Control,{pair.Key},{p.Qvalue},{p.MassCountInSpec},{p.PeakCount}" +
                                 $",{p.IsotopeCosine},{p.MassSNR},{p.ChargeSNR}"));

                    var targetsAveraged = averaged.Entries.Where(p => p.Decoy == 0).ToList();
                    var filteredAverage = averaged.Entries.Where(p => p.Decoy == 0 && p.Qvalue <= 0.01).ToList();
                    filteredAverage.ForEach(p =>
                    sw.WriteLine($"Jurkat,{GetFileName(p.FileName)},Averaged,{pair.Key},{p.Qvalue},{p.MassCountInSpec},{p.PeakCount}" +
                                 $",{p.IsotopeCosine},{p.MassSNR},{p.ChargeSNR}"));

                    var targetsNoRejection = noRejectionFile.Entries.Where(p => p.Decoy == 0).ToList();
                    var filteredNoRejection = noRejectionFile.Entries.Where(p => p.Decoy == 0 && p.Qvalue <= 0.01).ToList();
                    filteredNoRejection.ForEach(p => 
                        sw.WriteLine($"Jurkat,{GetFileName(p.FileName)},NoRejection,{pair.Key},{p.Qvalue},{p.MassCountInSpec},{p.PeakCount}" +
                                                                                                  $",{p.IsotopeCosine},{p.MassSNR},{p.ChargeSNR}"));

                    // Unfiltered
                    sw2.WriteLine($"Jurkat,{GetFileName(control.FilePath)},Control,{pair.Key},Unfiltered," +
                                  $"{averaged.Entries.Average(p => p.Qvalue)}," +
                                  $"{averaged.Entries.Average(p => p.MassCountInSpec)}," +
                                  $"{averaged.Entries.Average(p => p.PeakCount)}," +
                                  $"{averaged.Entries.Average(p => p.IsotopeCosine)}," +
                                  $"{averaged.Entries.Average(p => p.MassSNR)}," +
                                  $"{averaged.Entries.Average(p => p.ChargeSNR)},"
                    );
                    sw2.WriteLine($"Jurkat,{GetFileName(averaged.FilePath)},Averaged,{pair.Key},Unfiltered," +
                                  $"{averaged.Entries.Average(p => p.Qvalue)}," +
                                  $"{averaged.Entries.Average(p => p.MassCountInSpec)}," +
                                  $"{averaged.Entries.Average(p => p.PeakCount)}," +
                                  $"{averaged.Entries.Average(p => p.IsotopeCosine)}," +
                                  $"{averaged.Entries.Average(p => p.MassSNR)}," +
                                  $"{averaged.Entries.Average(p => p.ChargeSNR)},"
                    );
                    sw2.WriteLine($"Jurkat,{GetFileName(noRejectionFile.FilePath)},NoRejection,{pair.Key},Unfiltered," +
                                  $"{control.Entries.Average(p => p.Qvalue)}," +
                                  $"{control.Entries.Average(p => p.MassCountInSpec)}," +
                                  $"{control.Entries.Average(p => p.PeakCount)}," +
                                  $"{control.Entries.Average(p => p.IsotopeCosine)}," +
                                  $"{control.Entries.Average(p => p.MassSNR)}," +
                                  $"{control.Entries.Average(p => p.ChargeSNR)},"
                    );

                    // targets and q <= 0.01
                    sw2.WriteLine($"Jurkat,{GetFileName(filteredAverage.First().FileName)},Averaged,{pair.Key},Targets (Q <= 0.01)," +
                                  $"{filteredAverage.Average(p => p.Qvalue)}," +
                                  $"{filteredAverage.Average(p => p.MassCountInSpec)}," +
                                  $"{filteredAverage.Average(p => p.PeakCount)}," +
                                  $"{filteredAverage.Average(p => p.IsotopeCosine)}," +
                                  $"{filteredAverage.Average(p => p.MassSNR)}," +
                                  $"{filteredAverage.Average(p => p.ChargeSNR)},"
                    );
                    sw2.WriteLine($"Jurkat,{GetFileName(filteredControl.First().FileName)},Control,{pair.Key},Targets (Q <= 0.01)," +
                                  $"{filteredControl.Average(p => p.Qvalue)}," +
                                  $"{filteredControl.Average(p => p.MassCountInSpec)}," +
                                  $"{filteredControl.Average(p => p.PeakCount)}," +
                                  $"{filteredControl.Average(p => p.IsotopeCosine)}," +
                                  $"{filteredControl.Average(p => p.MassSNR)}," +
                                  $"{filteredControl.Average(p => p.ChargeSNR)},"
                    );
                    sw2.WriteLine(
                        $"Jurkat,{GetFileName(filteredNoRejection.First().FileName)},NoRejection,{pair.Key},Targets (Q <= 0.01)," +
                        $"{filteredNoRejection.Average(p => p.Qvalue)}," +
                        $"{filteredNoRejection.Average(p => p.MassCountInSpec)}," +
                        $"{filteredNoRejection.Average(p => p.PeakCount)}," +
                        $"{filteredNoRejection.Average(p => p.IsotopeCosine)}," +
                        $"{filteredNoRejection.Average(p => p.MassSNR)}," +
                        $"{filteredNoRejection.Average(p => p.ChargeSNR)},"
                    );

                    // targets
                    sw2.WriteLine($"Jurkat,{GetFileName(targetsAveraged.First().FileName)},Averaged,{pair.Key},Targets," +
                                  $"{targetsAveraged.Average(p => p.Qvalue)}," +
                                  $"{targetsAveraged.Average(p => p.MassCountInSpec)}," +
                                  $"{targetsAveraged.Average(p => p.PeakCount)}," +
                                  $"{targetsAveraged.Average(p => p.IsotopeCosine)}," +
                                  $"{targetsAveraged.Average(p => p.MassSNR)}," +
                                  $"{targetsAveraged.Average(p => p.ChargeSNR)},"
                    );
                    sw2.WriteLine($"Jurkat,{GetFileName(targetsControl.First().FileName)},Control,{pair.Key},Targets," +
                                  $"{targetsControl.Average(p => p.Qvalue)}," +
                                  $"{targetsControl.Average(p => p.MassCountInSpec)}," +
                                  $"{targetsControl.Average(p => p.PeakCount)}," +
                                  $"{targetsControl.Average(p => p.IsotopeCosine)}," +
                                  $"{targetsControl.Average(p => p.MassSNR)}," +
                                  $"{targetsControl.Average(p => p.ChargeSNR)},"
                    );
                    sw2.WriteLine($"Jurkat,{GetFileName(targetsNoRejection.First().FileName)},NoRejection,{pair.Key},Targets," +
                                  $"{targetsNoRejection.Average(p => p.Qvalue)}," +
                                  $"{targetsNoRejection.Average(p => p.MassCountInSpec)}," +
                                  $"{targetsNoRejection.Average(p => p.PeakCount)}," +
                                  $"{targetsNoRejection.Average(p => p.IsotopeCosine)}," +
                                  $"{targetsNoRejection.Average(p => p.MassSNR)}," +
                                  $"{targetsNoRejection.Average(p => p.ChargeSNR)},"
                    );
                }
            }
        }

        [Test]
        public static void GenerateFeatureArtifactOutput()
        {
            using var sw = new StreamWriter(File.Create(Path.Combine(ResultPaths.OutDirectory, "ArtifactDetection.csv")));
            List<Ms1FeatureFile> featureFiles = new();
            foreach (var pair in ResultPaths.JurkatDirectoryDictionary)
            {
                var controlFeatureFiles = ResultPaths.GetMs1FeatureFiles(pair.Value.Control);
                var averagedFeatureFiles = ResultPaths.GetMs1FeatureFiles(pair.Value.Averaged);
                var noRejectionFeatureFiles = ResultPaths.GetMs1FeatureFiles(pair.Value.NoRejection);

                for (int i = 0; i < controlFeatureFiles.Length; i++)
                {
                    featureFiles.Add(new Ms1FeatureFile(controlFeatureFiles[i], pair.Key, "Jurkat", "Control"));
                    featureFiles.Add(new Ms1FeatureFile(averagedFeatureFiles[i], pair.Key, "Jurkat", "Averaged"));
                    featureFiles.Add(new Ms1FeatureFile(noRejectionFeatureFiles[i], pair.Key, "Jurkat", "No Rejection"));
                }
            }
            
            sw.WriteLine(featureFiles.First().TabSeparatedHeader);
            foreach (var featureFile in featureFiles)
            {
                featureFile.PerformArtifactDetection();
                sw.WriteLine(featureFile.ToTsvString());
            }
        }



        [Test]
        public static void GenerateMs1AlignCountingOutput()
        {
            using var sw = new StreamWriter(File.Create(Path.Combine(ResultPaths.OutDirectory, "Ms1AlignPeakCounting.csv")));
            sw.WriteLine("DataSet,FileName,Processing,DeconSoftware,PeakCount");

            List<Ms1AlignResults> alignFiles = new();
            foreach (var pair in ResultPaths.JurkatDirectoryDictionary)
            {
                var controlMs1AlignFiles = ResultPaths.GetMs1AlignFiles(pair.Value.Control);
                var averagedMs1AlignFiles = ResultPaths.GetMs1AlignFiles(pair.Value.Averaged);
                var noRejectionMs1AlignFiles = ResultPaths.GetMs1AlignFiles(pair.Value.NoRejection);

                for (int i = 0; i < controlMs1AlignFiles.Length; i++)
                {
                    var control = new Ms1Align(controlMs1AlignFiles[i]);
                    var averaged = new Ms1Align(averagedMs1AlignFiles[i]);
                    var noRejection = new Ms1Align(noRejectionMs1AlignFiles[i]);

                    alignFiles.Add(new (control, "Jurkat", GetFileName(controlMs1AlignFiles[i]), "Calibrated", pair.Key, 0));
                    alignFiles.Add(new(averaged, "Jurkat", GetFileName(averagedMs1AlignFiles[i]), "Averaged: Rejection", pair.Key, 0));
                    alignFiles.Add(new(noRejection, "Jurkat", GetFileName(noRejectionMs1AlignFiles[i]), "Averaged: No Rejection", pair.Key, 0));
                }


                foreach (var align in alignFiles)
                {
                    var count = GetIonCount(align.DataFile.FilePath);
                    sw.WriteLine($"{align.DataSet},{align.FileName},{align.FileGroup},{align.Software},{count}");
                }
            }
        }

        [Test]
        public static void GenerateMs2AlignCountingOutput()
        {
            using var sw = new StreamWriter(File.Create(Path.Combine(ResultPaths.OutDirectory, "Ms2AlignPeakCounting.csv")));
            sw.WriteLine("DataSet,FileName,Processing,DeconSoftware,PeakCount");

            List<Ms1AlignResults> alignFiles = new();
            foreach (var pair in ResultPaths.JurkatDirectoryDictionary)
            {
                var controlMs1AlignFiles = ResultPaths.GetMs2AlignFiles(pair.Value.Control);
                var averagedMs1AlignFiles = ResultPaths.GetMs2AlignFiles(pair.Value.Averaged);
                var noRejectionMs1AlignFiles = ResultPaths.GetMs2AlignFiles(pair.Value.NoRejection);

                for (int i = 0; i < controlMs1AlignFiles.Length; i++)
                {
                    var control = new Ms1Align(controlMs1AlignFiles[i]);
                    var averaged = new Ms1Align(averagedMs1AlignFiles[i]);
                    var noRejection = new Ms1Align(noRejectionMs1AlignFiles[i]);

                    alignFiles.Add(new(control, "Jurkat", GetFileName(controlMs1AlignFiles[i]), "Calibrated", pair.Key, 0));
                    alignFiles.Add(new(averaged, "Jurkat", GetFileName(averagedMs1AlignFiles[i]), "Averaged: Rejection", pair.Key, 0));
                    alignFiles.Add(new(noRejection, "Jurkat", GetFileName(noRejectionMs1AlignFiles[i]), "Averaged: No Rejection", pair.Key, 0));
                }


                foreach (var align in alignFiles)
                {
                    var count = GetIonCount(align.DataFile.FilePath, true);
                    sw.WriteLine($"{align.DataSet},{align.FileName},{align.FileGroup},{align.Software},{count}");
                }
            }
        }

        private static int GetIonCount(string filepath, bool isMs2Align= false)
        {
            int count = 0;
            var lines = File.ReadAllLines(filepath);

            var header = Ms2Align.ReadingProgress.NotFound;
            var entry = Ms2Align.ReadingProgress.NotFound;

            foreach (var line in lines)
            {
                if (line.Contains("BEGIN IONS"))
                {
                    header = Ms2Align.ReadingProgress.Found;
                    continue;
                }

                if (header == Ms2Align.ReadingProgress.Found)
                {
                    if ((!isMs2Align && line.Contains("LEVEL")) || (isMs2Align && line.Contains("PRECURSOR_INTENSITY")))
                    {
                        header = Ms2Align.ReadingProgress.NotFound;
                        entry = Ms2Align.ReadingProgress.Found;
                    }
                    continue;
                }

                if (entry == Ms2Align.ReadingProgress.Found)
                {
                    if (line.Contains("END IONS"))
                    {
                        entry = Ms2Align.ReadingProgress.NotFound;
                        continue;
                    }

                    count++;
                }
            }

            return count;
        }

        internal record struct Ms1AlignResults(Ms1Align DataFile, string DataSet, string FileName, string FileGroup, DeconResults.DeconSoftware Software, int PeakCount);

        public static string GetFileName(string input)
        {
            var temp = Path.GetFileNameWithoutExtension(input);
            var newLine = temp.Replace("id_02-18-20_jurkat_td_rep2_", "")
                    .Replace("id_02-17-20_jurkat_td_rep2_", "")
                    .Replace("-calib-centroided_ms1", "")
                    .Replace("-calib-centroided", "")
                    .Replace("-calib-averaged-centroided_ms1", "")
                    .Replace("-calib-averaged-centroided", "")
                    .Replace("-calib-averaged_ms1", "")
                    .Replace("-calib-averaged", "")
                    .Replace("fract", "Fraction ")
                    .Replace("a_ms2", "")
                    .Replace("_ms2", "");
        
            return newLine;
        }

        [Test]
        public static void GetTargetDecoyCurve()
        {

            var outPathControl = Path.Combine(ResultPaths.OutDirectory, "targetDecoyCurveControlScore.csv");
            var outPathAveraged = Path.Combine(ResultPaths.OutDirectory, "targetDecoyCurveAveragedScore.csv");

            foreach (var pair in ResultPaths.JurkatDirectoryDictionary.Where(p =>
                         p.Key == DeconResults.DeconSoftware.FLASHDeconv))
            {
                var controlMs1TsvPaths = ResultPaths.GetMs1TsvFiles(pair.Value.Control);
                var averagedMs1TsvPaths = ResultPaths.GetMs1TsvFiles(pair.Value.Averaged);

                List<FlashDeconMs1TsvFile> controlFiles = new();
                List<FlashDeconMs1TsvFile> averagedFiles = new();
                for (int i = 0; i < controlMs1TsvPaths.Length; i++)
                {
                    controlFiles.Add(new FlashDeconMs1TsvFile(controlMs1TsvPaths[i]));
                    averagedFiles.Add(new FlashDeconMs1TsvFile(averagedMs1TsvPaths[i]));
                }

                var controlCurve = FlashDeconMs1TsvFile.GetTargetDecoyCurve(controlFiles, 0.001);
                var averagedCurve = FlashDeconMs1TsvFile.GetTargetDecoyCurve(averagedFiles, 0.001);

                controlCurve.ExportAsCsv(outPathControl);
                averagedCurve.ExportAsCsv(outPathAveraged);
            }
        }



        [Test]
        public static void GenerateTicData()
        {
            using var sw = new StreamWriter(File.Create(Path.Combine(ResultPaths.OutDirectory, "TicData.csv")));
            //sw.WriteLine("DataSet,FileName,FileGroup,Software,QValue,MassCountInSpec,PeakCount,IsotopeCosine,MassSNR,ChargeSNR");

            foreach (var pair in ResultPaths.JurkatDirectoryDictionary)
            {
                var controlFeatures = ResultPaths.GetMs1FeatureFiles(pair.Value.Control);
                var averagedFeatures = ResultPaths.GetMs1FeatureFiles(pair.Value.Averaged);

                for (int i = 0; i < controlFeatures.Length; i++)
                {
                    var controlFeatureFile = new Ms1FeatureFile(controlFeatures[i], pair.Key, "Jurkat", "Control");
                    var averagedFeatureFile = new Ms1FeatureFile(averagedFeatures[i], pair.Key, "Jurkat", "Averaged");

                    var controlPlot = controlFeatureFile.GetTicChart();
                    controlPlot.Show();

                }
            }

        }
    }
}
