﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using MassSpectrometry;
using MzLibUtil;
using Nett;
using NUnit.Framework;
using Omics.Modifications;
using Readers;
using TaskLayer;
using Transcriptomics.Digestion;
using UsefulProteomicsDatabases;

namespace Test.Transcriptomics
{
    [ExcludeFromCodeCoverage]
    internal class TestRnaSearchTask
    {
        private static string GetTestFilePath(string fileName) => Path.Combine(TestContext.CurrentContext.TestDirectory,
            @"Transcriptomics\TestData", fileName);

        [Test]
        public static void TestRnaSearchTask_TwoSpectraFile()
        {
            string dataFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"Transcriptomics\TestData",
                "GUACUG_NegativeMode_Sliced.mzML");
            string databasePath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"Transcriptomics\TestData",
                "6mer.fasta");
            string modFile = Path.Combine(GlobalVariables.DataDir, "Mods", "RnaMods.txt");
            var allMods = PtmListLoader.ReadModsFromFile(modFile, out var errorMods)
                .ToDictionary(p => p.IdWithMotif, p => p);


            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestRnaSearchTask");

            CommonParameters commonParams = new
            (
                dissociationType: DissociationType.CID,
                deconvolutionMaxAssumedChargeState: -20,
                deconvolutionIntensityRatio: 3,
                deconvolutionMassTolerance: new PpmTolerance(20),
                precursorMassTolerance: new PpmTolerance(10),
                productMassTolerance: new PpmTolerance(20),
                scoreCutoff: 5,
                totalPartitions: 1,
                maxThreadsToUsePerFile: 1,
                doPrecursorDeconvolution: true,
                useProvidedPrecursorInfo: false,
                digestionParams: new RnaDigestionParams(),
                listOfModsVariable: new List<(string, string)>(),
                listOfModsFixed: new List<(string, string)>()
            );
            RnaSearchParameters searchParams = new()
            {
                DisposeOfFileWhenDone = true,
                MassDiffAcceptorType = MassDiffAcceptorType.Custom,
                CustomMdac = "Custom interval [-5,5]",
                DecoyType = DecoyType.Reverse
            };

            var searchTask = new RnaSearchTask
            {
                CommonParameters = commonParams,
                SearchParameters = searchParams
            };
            var dbForTask = new List<DbForTask> { new(databasePath, false) };
            var taskList = new List<(string, MetaMorpheusTask)> { ("Task1-RnaSearch", searchTask) };
            var runner = new EverythingRunnerEngine(taskList, new List<string> { dataFile }, dbForTask, outputFolder);
            runner.Run();

            // check output files
            Assert.That(Directory.Exists(outputFolder));

            // task settings
            var taskSettingsDir = Path.Combine(outputFolder, "Task Settings");
            Assert.That(Directory.Exists(taskSettingsDir));
            var taskSettingsFile = Path.Combine(taskSettingsDir, "Task1-RnaSearchconfig.toml");
            Assert.That(File.Exists(taskSettingsFile));
            var loadedTask = Toml.ReadFile<RnaSearchTask>(taskSettingsFile, MetaMorpheusTask.tomlConfig);

            // search results
            // TODO: Check additional files
            var resultDir = Path.Combine(outputFolder, "Task1-RnaSearch");
            Assert.That(Directory.Exists(resultDir));
            var resultFile = Path.Combine(resultDir, "AllOSMs.osmtsv");
            var resultTxtFile = Path.Combine(resultDir, "results.txt");
            var proseFile = Path.Combine(resultDir, "AutoGeneratedManuscriptProse.txt");
            Assert.That(File.Exists(resultFile));
            Assert.That(File.Exists(resultTxtFile));
            Assert.That(File.Exists(proseFile));

            Directory.Delete(outputFolder, true);
        }

        [Test]
        public static void TestRnaSearchTask_ReloadsCorrectly()
        {
            var searchTask = GetModelSearch();
            var tempPath = GetTestFilePath("TestRnaSearchTask_ReloadsCorrectly.toml");
            Toml.WriteFile(searchTask, tempPath, MetaMorpheusTask.tomlConfig);
            var loadedTask = Toml.ReadFile<RnaSearchTask>(tempPath, MetaMorpheusTask.tomlConfig);
            AssertTasksAreEquivalent(loadedTask, searchTask);
            File.Delete(tempPath);
        }

        [Test]
        public static void TestRnaSearchTask_AreEquivalentAfterSearch()
        {
            string dataFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"Transcriptomics\TestData", "GUACUG_NegativeMode_Sliced.mzML");
            string databasePath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"Transcriptomics\TestData", "6mer.fasta");
            var dbForTask = new List<DbForTask> { new(databasePath, false) };
            string outputFolder = GetTestFilePath("TestRnaSearchTask_AreEquivalentAfterSearch");
            string taskOutputFolder = Path.Combine(outputFolder, "Task1-RnaSearch");
            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);
            Directory.CreateDirectory(outputFolder);
            Directory.CreateDirectory(taskOutputFolder);


            var searchTask = GetModelSearch();
            var result = searchTask.RunTask(taskOutputFolder, dbForTask, new List<string> { dataFile }, "task1");
            var loadedTask = Toml.ReadFile<RnaSearchTask>(Path.Combine(outputFolder, @"Task Settings\task1config.toml"), MetaMorpheusTask.tomlConfig);
            AssertTasksAreEquivalent(loadedTask, searchTask);
            Directory.Delete(Path.Combine(outputFolder, "Task Settings"), true);

            searchTask = GetModelSearch();
            var runner = new EverythingRunnerEngine(new List<(string, MetaMorpheusTask)> { ("Task1-RnaSearch", searchTask) }, new List<string> { dataFile }, dbForTask, outputFolder);
            runner.Run();
            loadedTask = Toml.ReadFile<RnaSearchTask>(Path.Combine(outputFolder, @"Task Settings\Task1-RnaSearchconfig.toml"), MetaMorpheusTask.tomlConfig);
            AssertTasksAreEquivalent(loadedTask, searchTask);
            Directory.Delete(outputFolder, true);
        }

        #region Helpers

        private static void AssertTasksAreEquivalent(RnaSearchTask task1, RnaSearchTask task2)
        {
            var task1CommonParams = task1.CommonParameters;
            Assert.That(task1CommonParams.TaskDescriptor, Is.EqualTo(task2.CommonParameters.TaskDescriptor));
            Assert.That(task1CommonParams.MaxThreadsToUsePerFile, Is.EqualTo(task2.CommonParameters.MaxThreadsToUsePerFile));
            CollectionAssert.AreEquivalent(task1CommonParams.ListOfModsVariable, task2.CommonParameters.ListOfModsVariable);
            CollectionAssert.AreEquivalent(task1CommonParams.ListOfModsFixed, task2.CommonParameters.ListOfModsFixed);
            Assert.That(task1CommonParams.DoPrecursorDeconvolution, Is.EqualTo(task2.CommonParameters.DoPrecursorDeconvolution));
            Assert.That(task1CommonParams.UseProvidedPrecursorInfo, Is.EqualTo(task2.CommonParameters.UseProvidedPrecursorInfo));
            Assert.That(task1CommonParams.DeconvolutionMaxAssumedChargeState, Is.EqualTo(task2.CommonParameters.DeconvolutionMaxAssumedChargeState));
            Assert.That(task1CommonParams.DeconvolutionIntensityRatio, Is.EqualTo(task2.CommonParameters.DeconvolutionIntensityRatio));
            Assert.That(task1CommonParams.DeconvolutionMassTolerance.Value, Is.EqualTo(task2.CommonParameters.DeconvolutionMassTolerance.Value));
            Assert.That(task1CommonParams.DeconvolutionMassTolerance.ToString(), Is.EqualTo(task2.CommonParameters.DeconvolutionMassTolerance.ToString()));
            Assert.That(task1CommonParams.TotalPartitions, Is.EqualTo(task2.CommonParameters.TotalPartitions));
            Assert.That(task1CommonParams.PrecursorMassTolerance.Value, Is.EqualTo(task2.CommonParameters.PrecursorMassTolerance.Value));
            Assert.That(task1CommonParams.PrecursorMassTolerance.ToString(), Is.EqualTo(task2.CommonParameters.PrecursorMassTolerance.ToString()));
            Assert.That(task1CommonParams.ProductMassTolerance.Value, Is.EqualTo(task2.CommonParameters.ProductMassTolerance.Value));
            Assert.That(task1CommonParams.ProductMassTolerance.ToString(), Is.EqualTo(task2.CommonParameters.ProductMassTolerance.ToString()));
            Assert.That(task1CommonParams.AddCompIons, Is.EqualTo(task2.CommonParameters.AddCompIons));
            Assert.That(task1CommonParams.QValueThreshold, Is.EqualTo(task2.CommonParameters.QValueThreshold));
            Assert.That(task1CommonParams.PepQValueThreshold, Is.EqualTo(task2.CommonParameters.PepQValueThreshold));
            Assert.That(task1CommonParams.ScoreCutoff, Is.EqualTo(task2.CommonParameters.ScoreCutoff));
            Assert.That(task1CommonParams.DigestionParams.DigestionAgent.Name, Is.EqualTo(task2.CommonParameters.DigestionParams.DigestionAgent.Name));
            Assert.That(task1CommonParams.DigestionParams.DigestionAgent.CleavageSpecificity, Is.EqualTo(task2.CommonParameters.DigestionParams.DigestionAgent.CleavageSpecificity));
            CollectionAssert.AreEquivalent(task1CommonParams.DigestionParams.DigestionAgent.DigestionMotifs, task2.CommonParameters.DigestionParams.DigestionAgent.DigestionMotifs);
            Assert.That(task1CommonParams.DigestionParams.MaxMissedCleavages, Is.EqualTo(task2.CommonParameters.DigestionParams.MaxMissedCleavages));
            Assert.That(task1CommonParams.DigestionParams.MinLength, Is.EqualTo(task2.CommonParameters.DigestionParams.MinLength));
            Assert.That(task1CommonParams.DigestionParams.MaxLength, Is.EqualTo(task2.CommonParameters.DigestionParams.MaxLength));
            Assert.That(task1CommonParams.DigestionParams.MaxMods, Is.EqualTo(task2.CommonParameters.DigestionParams.MaxMods));
            Assert.That(task1CommonParams.DigestionParams.MaxModificationIsoforms, Is.EqualTo(task2.CommonParameters.DigestionParams.MaxModificationIsoforms));
            Assert.That(task1CommonParams.DigestionParams.SearchModeType, Is.EqualTo(task2.CommonParameters.DigestionParams.SearchModeType));
            Assert.That(task1CommonParams.DigestionParams.FragmentationTerminus, Is.EqualTo(task2.CommonParameters.DigestionParams.FragmentationTerminus));
            Assert.That(task1CommonParams.ReportAllAmbiguity, Is.EqualTo(task2.CommonParameters.ReportAllAmbiguity));
            Assert.That(task1CommonParams.NumberOfPeaksToKeepPerWindow, Is.EqualTo(task2.CommonParameters.NumberOfPeaksToKeepPerWindow));
            Assert.That(task1CommonParams.MinimumAllowedIntensityRatioToBasePeak, Is.EqualTo(task2.CommonParameters.MinimumAllowedIntensityRatioToBasePeak));
            Assert.That(task1CommonParams.WindowWidthThomsons, Is.EqualTo(task2.CommonParameters.WindowWidthThomsons));
            Assert.That(task1CommonParams.NumberOfWindows, Is.EqualTo(task2.CommonParameters.NumberOfWindows));
            Assert.That(task1CommonParams.NormalizePeaksAccrossAllWindows, Is.EqualTo(task2.CommonParameters.NormalizePeaksAccrossAllWindows));
            Assert.That(task1CommonParams.TrimMs1Peaks, Is.EqualTo(task2.CommonParameters.TrimMs1Peaks));
            Assert.That(task1CommonParams.TrimMsMsPeaks, Is.EqualTo(task2.CommonParameters.TrimMsMsPeaks));
            Assert.That(task1CommonParams.TrimMs1Peaks, Is.EqualTo(task2.CommonParameters.TrimMs1Peaks));
            Assert.That(task1CommonParams.TrimMsMsPeaks, Is.EqualTo(task2.CommonParameters.TrimMsMsPeaks));
            CollectionAssert.AreEquivalent(task1CommonParams.CustomIons, task2.CommonParameters.CustomIons);
            Assert.That(task1CommonParams.AssumeOrphanPeaksAreZ1Fragments, Is.EqualTo(task2.CommonParameters.AssumeOrphanPeaksAreZ1Fragments));
            Assert.That(task1CommonParams.MaxHeterozygousVariants, Is.EqualTo(task2.CommonParameters.MaxHeterozygousVariants));
            Assert.That(task1CommonParams.MinVariantDepth, Is.EqualTo(task2.CommonParameters.MinVariantDepth));
            Assert.That(task1CommonParams.AddTruncations, Is.EqualTo(task2.CommonParameters.AddTruncations));
            Assert.That(task1CommonParams.DissociationType, Is.EqualTo(task2.CommonParameters.DissociationType));
            Assert.That(task1CommonParams.SeparationType, Is.EqualTo(task2.CommonParameters.SeparationType));

            // search params
            var loadedSearchParams = task1.SearchParameters;
            Assert.That(loadedSearchParams.DisposeOfFileWhenDone, Is.EqualTo(task2.SearchParameters.DisposeOfFileWhenDone));
            Assert.That(loadedSearchParams.DecoyType, Is.EqualTo(task2.SearchParameters.DecoyType));
            Assert.That(loadedSearchParams.MassDiffAcceptorType, Is.EqualTo(task2.SearchParameters.MassDiffAcceptorType));
            Assert.That(loadedSearchParams.CustomMdac, Is.EqualTo(task2.SearchParameters.CustomMdac));
            Assert.That(loadedSearchParams.DoParsimony, Is.EqualTo(task2.SearchParameters.DoParsimony));
            Assert.That(loadedSearchParams.DoLocalizationAnalysis, Is.EqualTo(task2.SearchParameters.DoLocalizationAnalysis));
            Assert.That(loadedSearchParams.HistogramBinTolInDaltons, Is.EqualTo(task2.SearchParameters.HistogramBinTolInDaltons));
            Assert.That(loadedSearchParams.DoHistogramAnalysis, Is.EqualTo(task2.SearchParameters.DoHistogramAnalysis));
            Assert.That(loadedSearchParams.CompressIndividualFiles, Is.EqualTo(task2.SearchParameters.CompressIndividualFiles));
            Assert.That(loadedSearchParams.WriteHighQValueSpectralMatches, Is.EqualTo(task2.SearchParameters.WriteHighQValueSpectralMatches));
            Assert.That(loadedSearchParams.WriteDecoys, Is.EqualTo(task2.SearchParameters.WriteDecoys));
            Assert.That(loadedSearchParams.WriteContaminants, Is.EqualTo(task2.SearchParameters.WriteContaminants));
            Assert.That(loadedSearchParams.WriteAmbiguous, Is.EqualTo(task2.SearchParameters.WriteAmbiguous));
            Assert.That(loadedSearchParams.WriteIndividualFiles, Is.EqualTo(task2.SearchParameters.WriteIndividualFiles));
            CollectionAssert.AreEquivalent(loadedSearchParams.ModsToWriteSelection, task2.SearchParameters.ModsToWriteSelection);
        }

        private static RnaSearchTask GetModelSearch()
        {
            CommonParameters commonParams = new
            (
                taskDescriptor: "testing",
                dissociationType: DissociationType.HCD,
                deconvolutionMaxAssumedChargeState: -20,
                deconvolutionIntensityRatio: 3,
                deconvolutionMassTolerance: new PpmTolerance(20),
                precursorMassTolerance: new PpmTolerance(10),
                productMassTolerance: new PpmTolerance(20),
                scoreCutoff: 5,
                totalPartitions: 1,
                maxThreadsToUsePerFile: 1,
                doPrecursorDeconvolution: true,
                useProvidedPrecursorInfo: false,
                digestionParams: new RnaDigestionParams(),
                listOfModsVariable: new List<(string, string)>() { ("Digestion Termini", "Cyclic Phosphate on X") },
                listOfModsFixed: new List<(string, string)>() { ("Biological", "Methylation on Y") },
                addCompIons: false,
                addTruncations: false,
                qValueThreshold: 1,
                pepQValueThreshold: 1,
                reportAllAmbiguity: true,
                assumeOrphanPeaksAreZ1Fragments: false,
                trimMs1Peaks: false,
                trimMsMsPeaks: false,
                separationType: "HPLC"
            );
            RnaSearchParameters searchParams = new()
            {
                DisposeOfFileWhenDone = true,
                MassDiffAcceptorType = MassDiffAcceptorType.Custom,
                CustomMdac = "Custom interval [-5,5]",
                DecoyType = DecoyType.Reverse,
                WriteAmbiguous = true,
                ModsToWriteSelection = new Dictionary<string, int>
                {
                    { "Biological", 3 },
                    { "Digestion Termini", 3 },
                    { "Metal", 3 },
                },
                WriteContaminants = false,
                WriteDecoys = true,
                WriteIndividualFiles = true,
            };

            return new RnaSearchTask
            {
                CommonParameters = commonParams,
                SearchParameters = searchParams
            };
        }

        #endregion




    }
}