﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using Easy.Common.Extensions;
using MathNet.Numerics;
using NUnit.Framework;
using Plotly.NET;
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
                for (int i = 0; i < controlMs1TsvPaths.Length; i++)
                {
                    var control = new FlashDeconMs1TsvFile(controlMs1TsvPaths[i]);
                    var averaged = new FlashDeconMs1TsvFile(averagedMs1TsvPaths[i]);

                    var targetsControl = control.Entries.Where(p => p.Decoy == 0).ToList();
                    var filteredControl = control.Entries.Where(p => p.Decoy == 0 && p.Qvalue <= 0.01).ToList();
                    filteredControl.ForEach(p =>
                    sw.WriteLine($"Jurkat,{p.FileName},Control,{pair.Key},{p.Qvalue},{p.MassCountInSpec},{p.PeakCount}" +
                                 $",{p.IsotopeCosine},{p.MassSNR},{p.ChargeSNR}"));

                    var targetsAveraged = averaged.Entries.Where(p => p.Decoy == 0).ToList();
                    var filteredAverage = averaged.Entries.Where(p => p.Decoy == 0 && p.Qvalue <= 0.01).ToList();
                    filteredAverage.ForEach(p =>
                    sw.WriteLine($"Jurkat,{p.FileName},Averaged,{pair.Key},{p.Qvalue},{p.MassCountInSpec},{p.PeakCount}" +
                                 $",{p.IsotopeCosine},{p.MassSNR},{p.ChargeSNR}"));

                    // Unfiltered
                    sw2.WriteLine($"Jurkat,{averaged.FilePath},Control,{pair.Key},Unfiltered," +
                                  $"{averaged.Entries.Average(p => p.Qvalue)}," +
                                  $"{averaged.Entries.Average(p => p.MassCountInSpec)}," +
                                  $"{averaged.Entries.Average(p => p.PeakCount)}," +
                                  $"{averaged.Entries.Average(p => p.IsotopeCosine)}," +
                                  $"{averaged.Entries.Average(p => p.MassSNR)}," +
                                  $"{averaged.Entries.Average(p => p.ChargeSNR)},"
                    );
                    sw2.WriteLine($"Jurkat,{averaged.FilePath},Averaged,{pair.Key},Unfiltered," +
                                  $"{averaged.Entries.Average(p => p.Qvalue)}," +
                                  $"{averaged.Entries.Average(p => p.MassCountInSpec)}," +
                                  $"{averaged.Entries.Average(p => p.PeakCount)}," +
                                  $"{averaged.Entries.Average(p => p.IsotopeCosine)}," +
                                  $"{averaged.Entries.Average(p => p.MassSNR)}," +
                                  $"{averaged.Entries.Average(p => p.ChargeSNR)},"
                    );

                    // targets and q <= 0.01
                    sw2.WriteLine($"Jurkat,{filteredAverage.First().FileName},Averaged,{pair.Key},Targets (Q <= 0.01)," +
                                  $"{filteredAverage.Average(p => p.Qvalue)}," +
                                  $"{filteredAverage.Average(p => p.MassCountInSpec)}," +
                                  $"{filteredAverage.Average(p => p.PeakCount)}," +
                                  $"{filteredAverage.Average(p => p.IsotopeCosine)}," +
                                  $"{filteredAverage.Average(p => p.MassSNR)}," +
                                  $"{filteredAverage.Average(p => p.ChargeSNR)},"
                    );
                    sw2.WriteLine($"Jurkat,{filteredControl.First().FileName},Control,{pair.Key},Targets (Q <= 0.01)," +
                                  $"{filteredControl.Average(p => p.Qvalue)}," +
                                  $"{filteredControl.Average(p => p.MassCountInSpec)}," +
                                  $"{filteredControl.Average(p => p.PeakCount)}," +
                                  $"{filteredControl.Average(p => p.IsotopeCosine)}," +
                                  $"{filteredControl.Average(p => p.MassSNR)}," +
                                  $"{filteredControl.Average(p => p.ChargeSNR)},"
                    );

                    // targets
                    sw2.WriteLine($"Jurkat,{targetsAveraged.First().FileName},Averaged,{pair.Key},Targets," +
                                  $"{targetsAveraged.Average(p => p.Qvalue)}," +
                                  $"{targetsAveraged.Average(p => p.MassCountInSpec)}," +
                                  $"{targetsAveraged.Average(p => p.PeakCount)}," +
                                  $"{targetsAveraged.Average(p => p.IsotopeCosine)}," +
                                  $"{targetsAveraged.Average(p => p.MassSNR)}," +
                                  $"{targetsAveraged.Average(p => p.ChargeSNR)},"
                    );
                    sw2.WriteLine($"Jurkat,{targetsControl.First().FileName},Control,{pair.Key},Targets," +
                                  $"{targetsControl.Average(p => p.Qvalue)}," +
                                  $"{targetsControl.Average(p => p.MassCountInSpec)}," +
                                  $"{targetsControl.Average(p => p.PeakCount)}," +
                                  $"{targetsControl.Average(p => p.IsotopeCosine)}," +
                                  $"{targetsControl.Average(p => p.MassSNR)}," +
                                  $"{targetsControl.Average(p => p.ChargeSNR)},"
                    );
                }
            }
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