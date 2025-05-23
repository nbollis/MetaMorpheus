﻿using EngineLayer;
using MassSpectrometry;
using NUnit.Framework;
using Proteomics;
using Proteomics.ProteolyticDigestion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EngineLayer.HistogramAnalysis;
using Omics.Modifications;
using TaskLayer;
using UsefulProteomicsDatabases;
using Omics;

namespace Test
{
    [TestFixture]
    public static class BinGenerationTest
    {
        [Test]
        public static void TestBinGeneration()
        {
            SearchTask st = new SearchTask
            {
                CommonParameters = new CommonParameters(scoreCutoff: 1, digestionParams: new DigestionParams(minPeptideLength: 5, initiatorMethionineBehavior: InitiatorMethionineBehavior.Retain)),

                SearchParameters = new SearchParameters
                {
                    DoHistogramAnalysis = true,
                    MassDiffAcceptorType = MassDiffAcceptorType.Open,
                    DecoyType = DecoyType.None,
                    DoParsimony = true,
                    DoLabelFreeQuantification = true
                },
            };

            string proteinDbFilePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "BinGenerationTest.xml");
            string mzmlFilePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "BinGenerationTest.mzML");

            Protein prot1 = new Protein("MEDEEK", "prot1");
            Protein prot2 = new Protein("MENEEK", "prot2");

            ModificationMotif.TryGetMotif("D", out ModificationMotif motif);
            Modification mod = new Modification(_target: motif, _locationRestriction: "Anywhere.", _monoisotopicMass: 10);

            var pep1_0 = prot1.Digest(st.CommonParameters.DigestionParams, new List<Modification>(), new List<Modification>()).First();
            var pep1_10 = prot1.Digest(st.CommonParameters.DigestionParams, new List<Modification>(), new List<Modification>()).Last();

            Protein prot3 = new Protein("MAAADAAAAAAAAAAAAAAA", "prot3");

            var pep2_0 = prot3.Digest(st.CommonParameters.DigestionParams, new List<Modification>(), new List<Modification>()).First();
            var pep2_10 = prot3.Digest(st.CommonParameters.DigestionParams, new List<Modification>(), new List<Modification> { mod }).Last();

            Protein prot4 = new Protein("MNNDNNNN", "prot4");
            var pep3_10 = prot4.Digest(st.CommonParameters.DigestionParams, new List<Modification>(), new List<Modification> { mod }).Last();

            var pepsWithSetMods = new List<IBioPolymerWithSetMods> { pep1_0, pep1_10, pep2_0, pep2_10, pep3_10 };
            MsDataFile myMsDataFile = new TestDataFile(pepsWithSetMods);

            List<Protein> proteinList = new List<Protein> { prot1, prot2, prot3, prot4 };

            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile, mzmlFilePath, false);
            ProteinDbWriter.WriteXmlDatabase(new Dictionary<string, HashSet<Tuple<int, Modification>>>(), proteinList, proteinDbFilePath);

            string output_folder = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestBinGeneration");
            Directory.CreateDirectory(output_folder);
            st.RunTask(
                output_folder,
                new List<DbForTask> { new DbForTask(proteinDbFilePath, false) },
                new List<string> { mzmlFilePath },
                null);


            Assert.That(File.ReadLines(Path.Combine(output_folder, @"MassDifferenceHistogram.tsv")).Count(), Is.EqualTo(3));
            Directory.Delete(output_folder, true);
            File.Delete(proteinDbFilePath);
            File.Delete(mzmlFilePath);
            Directory.Delete(Path.Combine(TestContext.CurrentContext.TestDirectory, @"Task Settings"), true);
        }

        [Test]
        public static void TestProteinSplitAcrossFiles()
        {
            SearchTask st = new SearchTask()
            {
                CommonParameters = new CommonParameters(
                    scoreCutoff: 1,
                    digestionParams: new DigestionParams(
                        maxMissedCleavages: 0,
                        minPeptideLength: 5,
                        initiatorMethionineBehavior: InitiatorMethionineBehavior.Retain)),

                SearchParameters = new SearchParameters
                {
                    DoHistogramAnalysis = true,
                    MassDiffAcceptorType = MassDiffAcceptorType.Open,
                    MatchBetweenRuns = true,
                    DoLabelFreeQuantification = true
                },
            };

            string proteinDbFilePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestProteinSplitAcrossFiles.xml");
            string mzmlFilePath1 = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestProteinSplitAcrossFiles1.mzML");
            string mzmlFilePath2 = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestProteinSplitAcrossFiles2.mzML");

            ModificationMotif.TryGetMotif("D", out ModificationMotif motif);
            Modification mod = new Modification(_originalId: "mod1 on D", _modificationType: "mt", _target: motif, _locationRestriction: "Anywhere.", _monoisotopicMass: 10);

            IDictionary<int, List<Modification>> oneBasedModification = new Dictionary<int, List<Modification>>
            {
                { 3, new List<Modification>{ mod } }
            };

            Protein prot1 = new Protein("MEDEEK", "prot1", oneBasedModifications: oneBasedModification);

            var pep1 = prot1.Digest(st.CommonParameters.DigestionParams, new List<Modification>(), new List<Modification>()).First();
            var pep2 = prot1.Digest(st.CommonParameters.DigestionParams, new List<Modification>(), new List<Modification>()).Last();

            var listForFile1 = new List<IBioPolymerWithSetMods> { pep1, pep2 };
            var listForFile2 = new List<IBioPolymerWithSetMods> { pep2 };
            MsDataFile myMsDataFile1 = new TestDataFile(listForFile1);
            MsDataFile myMsDataFile2 = new TestDataFile(listForFile2);

            List<Protein> proteinList = new List<Protein> { prot1 };

            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile1, mzmlFilePath1, false);
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile2, mzmlFilePath2, false);
            ProteinDbWriter.WriteXmlDatabase(new Dictionary<string, HashSet<Tuple<int, Modification>>>(), proteinList, proteinDbFilePath);

            string output_folder = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestProteinSplitAcrossFiles");
            Directory.CreateDirectory(output_folder);

            st.RunTask(
                output_folder,
                new List<DbForTask> { new DbForTask(proteinDbFilePath, false) },
                new List<string> { mzmlFilePath1, mzmlFilePath2, },
                null);
            Directory.Delete(output_folder, true);
            File.Delete(proteinDbFilePath);
            File.Delete(mzmlFilePath1);
            File.Delete(mzmlFilePath2);
            Directory.Delete(Path.Combine(TestContext.CurrentContext.TestDirectory, @"Task Settings"), true);
        }


        [Test]
        public static void TestBin_IdentifyAA()
        {
            var bin = new Bin(71);
            bin.IdentifyAA(1);
            Assert.That(bin.AA, Is.EqualTo("Add Alanine"));

            bin = new Bin(-56.1);
            bin.IdentifyAA(1);
            Assert.That(bin.AA, Is.EqualTo("Remove Glycine"));

            bin = new Bin(114.102);
            bin.IdentifyAA(1);
            Assert.That(bin.AA, Is.EqualTo("Add Aspartic Acid|Add (Glycine+Glycine)|Add Asparagine"));

            bin = new Bin(-142.156);
            bin.IdentifyAA(1);
            Assert.That(bin.AA, Is.EqualTo("Remove (Alanine+Alanine)"));
        }

        [Test]
        public static void TestBin_IdentifyUnimodBins()
        {
            var bin = new Bin(77.987066);
            bin.IdentifyUnimodBins(0.001);
            Assert.That(bin.UnimodId, Is.EqualTo("Methylphosphonate on Y|Methylphosphonate on T|Methylphosphonate on S"));
            Assert.That(bin.UnimodFormulas, Is.EqualTo("CH3O2P"));
        }
    }
}