﻿using Nett;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaskLayer;

namespace Test
{
    [TestFixture]
    public static class SlicedTest
    {
        [Test]
        public static void SlicedTest1()
        {
            var task = Toml.ReadFile<SearchTask>(Path.Combine(TestContext.CurrentContext.TestDirectory, @"SlicedSearchTaskConfig.toml"), MetaMorpheusTask.tomlConfig);

            DbForTask db = new DbForTask(Path.Combine(TestContext.CurrentContext.TestDirectory, @"sliced-db.fasta"), false);
            string raw = Path.Combine(TestContext.CurrentContext.TestDirectory, @"sliced-raw.mzML");
            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestSlicedTest1");
            EverythingRunnerEngine a = new EverythingRunnerEngine(new List<(string, MetaMorpheusTask)> { ("Task", task) }, new List<string> { raw }, new List<DbForTask> { db }, outputFolder);

            a.Run();

            var thisTaskOutputFolder = MySetUpClass.outputFolder;

            var peaks = Path.Combine(thisTaskOutputFolder, "Task", "AllQuantifiedPeaks.tsv");

            Assert.That(File.ReadLines(peaks).Count(), Is.EqualTo(2));

            var psms = Path.Combine(thisTaskOutputFolder, "Task", "AllPSMs.psmtsv");

            Assert.That(File.ReadLines(psms).Count(), Is.EqualTo(3));
            var protGroups = Path.Combine(thisTaskOutputFolder, "Task", "AllQuantifiedProteinGroups.tsv");

            Assert.That(File.ReadLines(protGroups).Count(), Is.EqualTo(2));
            Directory.Delete(outputFolder, true);
        }

        [Test]
        public static void FaFormatTest()
        {
            var task = Toml.ReadFile<SearchTask>(Path.Combine(TestContext.CurrentContext.TestDirectory, @"SlicedSearchTaskConfig.toml"), MetaMorpheusTask.tomlConfig);

            DbForTask db = new DbForTask(Path.Combine(TestContext.CurrentContext.TestDirectory, @"sliced-db.fa"), false);
            string raw = Path.Combine(TestContext.CurrentContext.TestDirectory, @"sliced-raw.mzML");
            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"FaFormatTest");
            EverythingRunnerEngine a = new EverythingRunnerEngine(new List<(string, MetaMorpheusTask)> { ("Task", task) }, new List<string> { raw }, new List<DbForTask> { db }, outputFolder);

            a.Run();

            var thisTaskOutputFolder = MySetUpClass.outputFolder;

            var peaks = Path.Combine(thisTaskOutputFolder, "Task", "AllQuantifiedPeaks.tsv");

            Assert.That(File.ReadLines(peaks).Count(), Is.EqualTo(2));

            var psms = Path.Combine(thisTaskOutputFolder, "Task", "AllPSMs.psmtsv");

            Assert.That(File.ReadLines(psms).Count(), Is.EqualTo(3));
            var protGroups = Path.Combine(thisTaskOutputFolder, "Task", "AllQuantifiedProteinGroups.tsv");

            Assert.That(File.ReadLines(protGroups).Count(), Is.EqualTo(2));
            Directory.Delete(outputFolder, true);
        }
    }
}