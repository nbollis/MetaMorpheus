﻿using EngineLayer;
using MassSpectrometry;
using NUnit.Framework; 
using Proteomics;
using Proteomics.AminoAcidPolymer;
using Proteomics.ProteolyticDigestion;
using System;
using System.Collections.Generic;
using System.IO;
using Omics.Modifications;
using TaskLayer;
using UsefulProteomicsDatabases;
using Omics;
using Omics.Digestion;

namespace Test
{
    [TestFixture]
    public static class SilacTest
    {
        [Test]
        public static void TestSilacNoLightProtein()
        {
            //The concern with multiple mods per label is the conversions back and forth between "light" and "heavy" labels
            Residue heavyArginine = new("c", 'c', "c", Chemistry.ChemicalFormula.ParseFormula("C6H12N{15}4O"), ModificationSites.All); //+4 arginine
            Residue heavierArginine = new("d", 'd', "d", Chemistry.ChemicalFormula.ParseFormula("C{13}6H12N{15}4O"), ModificationSites.All); //+10 arginine
            Residue.AddNewResiduesToDictionary(new List<Residue> { heavyArginine }); //These should be added in the  search task, but we need to add this one earlier so that we can create a heavy pwsm

            Residue lightArginine = Residue.GetResidue('R');

            SilacLabel heavyLabel = new(lightArginine.Letter, heavyArginine.Letter, heavyArginine.ThisChemicalFormula.Formula, heavyArginine.MonoisotopicMass - lightArginine.MonoisotopicMass);
            SilacLabel heavierLabel = new(lightArginine.Letter, heavierArginine.Letter, heavierArginine.ThisChemicalFormula.Formula, heavierArginine.MonoisotopicMass - lightArginine.MonoisotopicMass);

            SearchTask task = new()
            {
                SearchParameters = new SearchParameters
                {
                    SilacLabels = new List<SilacLabel> { heavyLabel, heavierLabel }
                },
                CommonParameters = new CommonParameters(digestionParams: new DigestionParams(generateUnlabeledProteinsForSilac: false)) //this is the important part of the unit test
            };

            List<PeptideWithSetModifications> heavyPeptide = new() { new PeptideWithSetModifications("PEPTIDEc", new Dictionary<string, Modification>()) };
            List<List<double>> massDifferences = new() { new List<double> { heavierArginine.MonoisotopicMass - heavyArginine.MonoisotopicMass } };
            MsDataFile myMsDataFile1 = new TestDataFile(heavyPeptide, massDifferences);
            string mzmlName = @"silac.mzML";
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile1, mzmlName, false);

            string xmlName = "SilacDb.xml";
            Protein theProtein = new("PEPTIDER", "accession1");
            _ = ProteinDbWriter.WriteXmlDatabase(new Dictionary<string, HashSet<Tuple<int, Modification>>>(), new List<Protein> { theProtein }, xmlName);

            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestSilac");
            _ = Directory.CreateDirectory(outputFolder);
            _ = task.RunTask(outputFolder, new List<DbForTask> { new DbForTask(xmlName, false) }, new List<string> { mzmlName }, "taskId1").ToString();

            //test proteins
            string[] output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllQuantifiedProteinGroups.tsv");
            Assert.That(output.Length, Is.EqualTo(2));
            Assert.That(output[0].Contains("Modification Info List\tIntensity_silac(R+3.988)\tIntensity_silac(R+10.008)")); //test that two files were made and no light file
            Assert.That(output[1].Contains("875000.0000000009\t437500.00000000047")); //test the heavier intensity is half that of the heavy (per the raw file)

            //test peptides
            output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllQuantifiedPeptides.tsv");
            Assert.That(output.Length, Is.EqualTo(2));
            Assert.That(output[0].Contains("Organism\tIntensity_silac(R+3.988)\tIntensity_silac(R+10.008)")); //test the two files were made and no light file
            Assert.That(output[1].Contains("875000\t437500")); //test intensity

            //test peaks
            output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllQuantifiedPeaks.tsv");
            Assert.That(output.Length, Is.EqualTo(3));

            Assert.That(output[1].Contains("silac\t")); //test the filename was NOT modified (it was for proteins, but we don't want it for peptides)
            Assert.That(output[2].Contains("silac\t"));//test the filename was NOT modified (it was for proteins, but we don't want it for peptides)
            Assert.That(output[1].Contains("PEPTIDER(+3.988)\t")); //test light sequence was not modified
            Assert.That(output[2].Contains("PEPTIDER(+10.008)\t")); //test heavy sequence was output correctly (do NOT want "PEPTIDEa")
            Assert.That(output[1].Contains("959.44")); //test light mass
            Assert.That(output[2].Contains("965.46")); //test heavy mass

            //test PSMs
            output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllPSMs.psmtsv");
            Assert.That(output[1].Contains("959.44")); //test the correct monoisotopic mass
            Assert.That(output[1].Contains("PEPTIDER(+3.988)")); //test the correct psm
            Assert.That(output[1].Contains("silac\t")); //test the filename was NOT modified (it was for proteins, but we don't want it for peptides)

            //Clear the old files
            Directory.Delete(outputFolder, true);
            File.Delete(xmlName);
            File.Delete(mzmlName);
        }

        [Test]
        public static void TestSilacMultipleModsPerCondition()
        {
            //The concern with multiple mods per label is the conversions back and forth between "light" and "heavy" labels
            Residue heavyLysine = new("a", 'a', "a", Chemistry.ChemicalFormula.ParseFormula("C{13}6H12N{15}2O"), ModificationSites.All); //+8 lysine
            Residue heavyArginine = new("b", 'b', "b", Chemistry.ChemicalFormula.ParseFormula("C{13}6H12N4O"), ModificationSites.All); //+6 arginine
            Residue lightLysine = Residue.GetResidue('K');
            Residue lightArginine = Residue.GetResidue('R');

            SilacLabel krLabel = new(lightLysine.Letter, heavyLysine.Letter, heavyLysine.ThisChemicalFormula.Formula, heavyLysine.MonoisotopicMass - lightLysine.MonoisotopicMass);
            krLabel.AddAdditionalSilacLabel(new SilacLabel(lightArginine.Letter, heavyArginine.Letter, heavyArginine.ThisChemicalFormula.Formula, heavyArginine.MonoisotopicMass - lightArginine.MonoisotopicMass));

            SearchTask task = new SearchTask
            {
                SearchParameters = new SearchParameters
                {
                    SilacLabels = new List<SilacLabel> { krLabel }
                },
                // Previously, the default q value output filtering threshold for CommonParameters was 1.0.
                // The q-value output filtering has unintended side effects. This issue will be fix in a later PR
                CommonParameters = new CommonParameters(qValueThreshold: 1.0, digestionParams: new DigestionParams(minPeptideLength: 2))
            };


            List<PeptideWithSetModifications> lightPeptide = new List<PeptideWithSetModifications> { new PeptideWithSetModifications("SEQENEWITHAKANDANR", new Dictionary<string, Modification>()) };
            List<List<double>> massDifferences = new List<List<double>> { new List<double> { (heavyLysine.MonoisotopicMass + heavyArginine.MonoisotopicMass) - (lightLysine.MonoisotopicMass + lightArginine.MonoisotopicMass) } };

            MsDataFile myMsDataFile1 = new TestDataFile(lightPeptide, massDifferences, largePeptideSoDoubleFirstPeakIntensityAndAddAnotherPeak: true);
            string mzmlName = @"silac.mzML";
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile1, mzmlName, false);

            string xmlName = "SilacDb.xml";
            Protein theProtein = new Protein("MPRTEINRSEQENEWITHAKANDANRANDSMSTFF", "accession1");
            ProteinDbWriter.WriteXmlDatabase(new Dictionary<string, HashSet<Tuple<int, Modification>>>(), new List<Protein> { theProtein }, xmlName);

            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestSilac");
            _ = Directory.CreateDirectory(outputFolder);
            _ = task.RunTask(outputFolder, new List<DbForTask> { new DbForTask(xmlName, false) }, new List<string> { mzmlName }, "taskId1");

            //test proteins
            string[] output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllQuantifiedProteinGroups.tsv");
            Assert.That(output.Length, Is.EqualTo(2));
            Assert.That(output[0].Contains("Intensity_silac\tIntensity_silac(K+8.014 & R+6.020)")); //test that two files were made
            Assert.That(output[1].Contains("1374999.999999999\t687499.9999999995")); //test the heavy intensity is half that of the light (per the raw file)

            //test peptides
            output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllQuantifiedPeptides.tsv");
            Assert.That(output.Length, Is.EqualTo(2));
            Assert.That(output[1].Contains("SEQENEWITHAKANDANR\taccession1\t"));//test the sequence and accession were not modified
            Assert.That(output[1].Contains("1375000")); //test intensity
            Assert.That(!output[1].Contains("SEQENEWITHAK(+8.014)ANDANR(+6.020)")); //test the sequence was not doubled modified
            Assert.That(output[1].Contains("687500")); //test intensity

            //test peaks
            output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllQuantifiedPeaks.tsv");
            Assert.That(output.Length, Is.EqualTo(3));
            Assert.That(output[1].Contains("silac\t")); //test the filename was NOT modified (it was for proteins, but we don't want it for peptides)
            Assert.That(output[2].Contains("silac\t"));//test the filename was NOT modified (it was for proteins, but we don't want it for peptides)
            Assert.That(output[1].Contains("SEQENEWITHAKANDANR\t")); //test light sequence was not modified
            Assert.That(output[2].Contains("SEQENEWITHAK(+8.014)ANDANR(+6.020)\t")); //test heavy sequence was output correctly (do NOT want "PEPTIDEa")
            Assert.That(output[1].Contains("2111.96")); //test light mass
            Assert.That(output[2].Contains("2125.99")); //test heavy mass
            Assert.That(output[2].Contains("accession1")); //test heavy accesssion is light in output


            ///Test for when an additional label is the only label on a peptide
            ///Usually crashes in mzId
            //Delete old files
            File.Delete(mzmlName);
            Directory.Delete(outputFolder, true);

            List<PeptideWithSetModifications> heavyPeptide = new() { new PeptideWithSetModifications("ANDANb", new Dictionary<string, Modification>()) }; //has the additional, but not the original
            massDifferences = new List<List<double>> { new List<double> { lightArginine.MonoisotopicMass - heavyArginine.MonoisotopicMass } };

            myMsDataFile1 = new TestDataFile(heavyPeptide, massDifferences);
            mzmlName = @"silac.mzML";
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile1, mzmlName, false);

            Directory.CreateDirectory(outputFolder);
            task.RunTask(outputFolder, new List<DbForTask> { new DbForTask(xmlName, false) }, new List<string> { mzmlName }, "taskId1");

            //Clear the old files
            Directory.Delete(outputFolder, true);
            File.Delete(xmlName);
            File.Delete(mzmlName);
        }

        [Test]
        public static void TestSilacQuantification()
        {
            //make heavy residue and add to search task
            Residue heavyLysine = new("a", 'a', "a", Chemistry.ChemicalFormula.ParseFormula("C{13}6H12N{15}2O"), ModificationSites.All); //+8 lysine
            Residue lightLysine = Residue.GetResidue('K');
            SearchTask task = new()
            {
                SearchParameters = new SearchParameters
                {
                    SilacLabels = new List<SilacLabel> { new SilacLabel(lightLysine.Letter, heavyLysine.Letter, heavyLysine.ThisChemicalFormula.Formula, heavyLysine.MonoisotopicMass - lightLysine.MonoisotopicMass) }
                }
            };

            List<PeptideWithSetModifications> lightPeptide = new List<PeptideWithSetModifications> { new PeptideWithSetModifications("PEPTIDEK", new Dictionary<string, Modification>()) }; //has the additional, but not the original
            List<List<double>> massDifferences1 = new() { new List<double> { heavyLysine.MonoisotopicMass - lightLysine.MonoisotopicMass } };
            List<List<double>> massDifferences2 = new() { new List<double> { heavyLysine.MonoisotopicMass - lightLysine.MonoisotopicMass } };

            MsDataFile myMsDataFile1 = new TestDataFile(lightPeptide, massDifferences1);
            string mzmlName = @"silac.mzML";
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile1, mzmlName, false);

            //create another file to test the handling is done correctly
            MsDataFile myMsDataFile2 = new TestDataFile(lightPeptide, massDifferences2);
            string mzmlName2 = @"silacPart2.mzML";
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile2, mzmlName2, false);

            string xmlName = "SilacDb.xml";
            Protein theProtein = new Protein("PEPTIDEK", "accession1");
            _ = ProteinDbWriter.WriteXmlDatabase(new Dictionary<string, HashSet<Tuple<int, Modification>>>(), new List<Protein> { theProtein }, xmlName);

            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestSilac");
            _ = Directory.CreateDirectory(outputFolder);
            string theStringResult = task.RunTask(outputFolder, new List<DbForTask> { new DbForTask(xmlName, false) }, new List<string> { mzmlName, mzmlName2 }, "taskId1").ToString();

            string mzIDPath1 = Path.ChangeExtension(TestContext.CurrentContext.TestDirectory + @"\TestSilac\Individual File Results\" + mzmlName, ".mzID");
            string mzIDPath2 = Path.ChangeExtension(TestContext.CurrentContext.TestDirectory + @"\TestSilac\Individual File Results\" + mzmlName2, ".mzID");
            Assert.That(File.Exists(mzIDPath1));
            Assert.That(File.Exists(mzIDPath2));

            Assert.That(theStringResult.Contains("All target PSMs with q-value <= 0.01: 2")); //it's not a psm, it's a MBR feature. 2 because there are two files, but not 4 because MBR != psm

            ///Normal Peptide
            //test proteins
            string[] output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"\TestSilac\AllQuantifiedProteinGroups.tsv");
            Assert.That(output.Length, Is.EqualTo(2));
            Assert.That(output[0].Contains("Intensity_silac\tIntensity_silacPart2\tIntensity_silac(K+8.014)\tIntensity_silacPart2(K+8.014)")); //test that two files were made
            Assert.That(output[1].Contains("875000.0000000009\t875000.0000000009\t437500.00000000047\t437500.00000000047")); //test the heavy intensity is half that of the light (per the raw file)

            //test peptides
            output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"\TestSilac\AllQuantifiedPeptides.tsv");
            Assert.That(output.Length, Is.EqualTo(2));
            Assert.That(output[1].Contains("PEPTIDEK\taccession1\t"));//test the sequence and accession were not modified
            Assert.That(output[1].Contains("875000")); //test intensity
            Assert.That(!output[1].Contains("PEPTIDEK(+8.014)")); //test the sequence was not doubled modified
            Assert.That(output[1].Contains("437500")); //test intensity

            //test peaks
            output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"\TestSilac\AllQuantifiedPeaks.tsv");
            Assert.That(output.Length, Is.EqualTo(5));

            Assert.That(output[1].Contains("silac\t")); //test the filename was NOT modified (it was for proteins, but we don't want it for peptides)
            Assert.That(output[2].Contains("silac\t"));//test the filename was NOT modified (it was for proteins, but we don't want it for peptides)
            Assert.That(output[1].Contains("PEPTIDEK\t")); //test light sequence was not modified
            Assert.That(output[2].Contains("PEPTIDEK(+8.014)\t")); //test heavy sequence was output correctly (do NOT want "PEPTIDEa")
            Assert.That(output[1].Contains("927.45")); //test light mass
            Assert.That(output[2].Contains("935.46")); //test heavy mass

            ///Ambiguous base sequence peptide
            //Clear the old files
            Directory.Delete(outputFolder, true);
            File.Delete(xmlName);
            File.Delete(mzmlName);

            //make a heavy peptide
            List<PeptideWithSetModifications> heavyPeptide = new List<PeptideWithSetModifications> { new PeptideWithSetModifications("PEPTIDEa", new Dictionary<string, Modification>()) }; //has the additional, but not the original
            massDifferences1 = new List<List<double>> { new List<double> { (lightLysine.MonoisotopicMass - heavyLysine.MonoisotopicMass) } }; //have to reset because it gets modified
            myMsDataFile1 = new TestDataFile(heavyPeptide, massDifferences1);
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile1, mzmlName, false);

            //make an ambiguous database
            Protein theProtein2 = new Protein("PEPTLDEKPEPTIDEK", "accession2");
            _ = ProteinDbWriter.WriteXmlDatabase(new Dictionary<string, HashSet<Tuple<int, Modification>>>(), new List<Protein> { theProtein, theProtein2 }, xmlName);

            _ = Directory.CreateDirectory(outputFolder);
            _ = task.RunTask(outputFolder, new List<DbForTask> { new DbForTask(xmlName, false) }, new List<string> { mzmlName }, "taskId1").ToString();

            output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllPSMs.psmtsv");
            Assert.That(output[1].Contains("silac\t")); //test the filename was NOT modified (it was for proteins, but we don't want it for peptides)
            Assert.That(output[1].Contains("PEPTIDEK(+8.014)|PEPTLDEK(+8.014)|PEPTIDEK(+8.014)")
                || output[1].Contains("PEPTIDEK(+8.014)|PEPTIDEK(+8.014)|PEPTLDEK(+8.014)")
                || output[1].Contains("PEPTLDEK(+8.014)|PEPTIDEK(+8.014)|PEPTIDEK(+8.014)")); //test the heavy ambiguous peptides were all found
            //Need the options, because output isn't consistent as of 3/26/19

            ///Ambiguous proteinGroup
            //Clear the old files
            Directory.Delete(outputFolder, true);
            File.Delete(xmlName);

            //make an ambiguous database
            theProtein2 = new Protein("PEPTIDEK", "accession2");
            _ = ProteinDbWriter.WriteXmlDatabase(new Dictionary<string, HashSet<Tuple<int, Modification>>>(), new List<Protein> { theProtein, theProtein2 }, xmlName);

            _ = Directory.CreateDirectory(outputFolder);
            _ = task.RunTask(outputFolder, new List<DbForTask> { new DbForTask(xmlName, false) }, new List<string> { mzmlName }, "taskId1").ToString();

            output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllPSMs.psmtsv");
            Assert.That(output[1].Contains("accession1|accession2")
                || output[1].Contains("accession2|accession1")); //test the heavy ambiguous peptides were all found
            //Need the options, because output isn't consistent as of 3/26/19
            Assert.That(output[1].Contains("\tPEPTIDEK(+8.014)\t")); //test the heavy ambiguous peptides were all found

            //delete files
            Directory.Delete(outputFolder, true);
            File.Delete(xmlName);
            File.Delete(mzmlName);
        }

        [Test]
        public static void TestSilacWhenProteinIsMissing()
        {
            //make heavy residue and add to search task
            Residue heavyLysine = new Residue("a", 'a', "a", Chemistry.ChemicalFormula.ParseFormula("C{13}6H12N{15}2O"), ModificationSites.All); //+8 lysine
            Residue lightLysine = Residue.GetResidue('K');

            SearchTask task = new SearchTask
            {
                SearchParameters = new SearchParameters
                {
                    SilacLabels = new List<SilacLabel> { new SilacLabel(lightLysine.Letter, heavyLysine.Letter, heavyLysine.ThisChemicalFormula.Formula, heavyLysine.MonoisotopicMass - lightLysine.MonoisotopicMass) },
                    NoOneHitWonders = true
                    //The NoOneHitWonders=true doesn't really seem like a SILAC test, but we're testing that there's no crash if a quantified peptide's proteinGroup isn't quantified
                    //This happens if somebody messed with parsimony (picked TDS) or from requiring two peptides per protein (and we're only finding one). We're testing the second case here.
                }
            };

            List<PeptideWithSetModifications> lightPeptide = new List<PeptideWithSetModifications> { new PeptideWithSetModifications("PEPTIDEK", new Dictionary<string, Modification>()) }; //has the additional, but not the original
            List<List<double>> massDifferences = new List<List<double>> { new List<double> { (heavyLysine.MonoisotopicMass - lightLysine.MonoisotopicMass) } };

            MsDataFile myMsDataFile1 = new TestDataFile(lightPeptide, massDifferences);
            string mzmlName = @"silac.mzML";
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile1, mzmlName, false);

            string xmlName = "SilacDb.xml";
            Protein theProtein = new Protein("PEPTIDEK", "accession1");
            ProteinDbWriter.WriteXmlDatabase(new Dictionary<string, HashSet<Tuple<int, Modification>>>(), new List<Protein> { theProtein }, xmlName);

            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestSilac");
            Directory.CreateDirectory(outputFolder);
            var theStringResult = task.RunTask(outputFolder, new List<DbForTask> { new DbForTask(xmlName, false) }, new List<string> { mzmlName }, "taskId1").ToString();
            //No assertions, just checking it didn't crash

            //delete files
            Directory.Delete(outputFolder, true);
            File.Delete(xmlName);
            File.Delete(mzmlName);
        }

        [Test]
        public static void TestSilacTurnover()
        {
            //make heavy residue and add to search task
            Residue heavyLysine = new Residue("a", 'a', "a", Chemistry.ChemicalFormula.ParseFormula("C{13}6H12N{15}2O"), ModificationSites.All); //+8 lysine
            Residue.AddNewResiduesToDictionary(new List<Residue> { heavyLysine });
            Residue lightLysine = Residue.GetResidue('K');

            SearchTask task = new SearchTask
            {
                SearchParameters = new SearchParameters
                {
                    EndTurnoverLabel = new SilacLabel(lightLysine.Letter, heavyLysine.Letter, heavyLysine.ThisChemicalFormula.Formula, heavyLysine.MonoisotopicMass - lightLysine.MonoisotopicMass),
                    NoOneHitWonders = true
                    //The NoOneHitWonders=true doesn't really seem like a SILAC test, but we're testing that there's no crash if a quantified peptide's proteinGroup isn't quantified
                    //This happens if somebody messed with parsimony (picked TDS) or from requiring two peptides per protein (and we're only finding one). We're testing the second case here.
                }
            };

            List<PeptideWithSetModifications> mixedPeptide = new List<PeptideWithSetModifications> { new PeptideWithSetModifications("PEPTKIDEK", new Dictionary<string, Modification>()) }; //has the additional, but not the original
            double massShift = heavyLysine.MonoisotopicMass - lightLysine.MonoisotopicMass;
            List<List<double>> massDifferences = new List<List<double>> { new List<double> { massShift, massShift * 2 } }; //LH and HH

            MsDataFile myMsDataFile1 = new TestDataFile(mixedPeptide, massDifferences);

            //nested directory needed to test path mapping in ProteinGroup.ConstructSubsetProteinGroup
            string directoryName = "testDirectory";
            Directory.CreateDirectory(directoryName);
            string mzmlName = Path.Combine(directoryName, "silac.mzML");
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile1, mzmlName, false);

            //create another file to test the handling is done correctly
            MsDataFile myMsDataFile2 = new TestDataFile(mixedPeptide, massDifferences);
            string mzmlName2 = @"silacPart2.mzML";
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile2, mzmlName2, false);

            string xmlName = "SilacDb.xml";
            Protein theProtein = new Protein("PEPEPEPTKIDEKPEPTKIDEKA", "accession1");
            ProteinDbWriter.WriteXmlDatabase(new Dictionary<string, HashSet<Tuple<int, Modification>>>(), new List<Protein> { theProtein }, xmlName);

            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestSilac");
            Directory.CreateDirectory(outputFolder);
            task.RunTask(outputFolder, new List<DbForTask> { new DbForTask(xmlName, false) }, new List<string> { mzmlName, mzmlName2 }, "taskId1").ToString();

            string[] output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllQuantifiedPeptides.tsv");
            Assert.That(output[1].Contains("PEPTKIDEK\t")); //test the unlabeled is present
            Assert.That(output[0].Contains("\tIntensity_silac_Original\tIntensity_silac_NewlySynthesized\tIntensity_silacPart2_Original\tIntensity_silacPart2_NewlySynthesized\t" +
                "Detection Type_silac_Original\tDetection Type_silac_NewlySynthesized\tDetection Type_silacPart2_Original\tDetection Type_silacPart2_NewlySynthesized")); //test filename changes
            Assert.That(output[1].Contains("\t1093750\t437500\t")); //test intensities

            output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllQuantifiedPeaks.tsv");
            Assert.That(output.Length, Is.EqualTo(7)); //header, (unlabeled, mixed, labeled)*2 files
            Assert.That(output[1].Contains("\tPEPTKIDEK\t")); //test the unlabeled is present
            Assert.That(output[1].Contains("\t875000\t")); //test intensity
            Assert.That(output[2].Contains("\tPEPTK(+8.014)IDEK\t")); //test human readable label (and lack thereof) is present
            Assert.That(output[2].Contains("\t437500\t")); //test intensity
            Assert.That(output[3].Contains("\tPEPTK(+8.014)IDEK(+8.014)\t")); //test the unlabeled is present
            Assert.That(output[3].Contains("\t218750\t")); //test intensity
            Assert.That(output[3].Contains("silac\t")); //test human readable labels are present

            //use two turnover labels for start/end
            Residue heavyishLysine = new Residue("b", 'b', "b", Chemistry.ChemicalFormula.ParseFormula("C6H12N{15}2O"), ModificationSites.All); //+2 lysine
            Residue.AddNewResiduesToDictionary(new List<Residue> { heavyishLysine });

            task = new SearchTask
            {
                SearchParameters = new SearchParameters
                {
                    StartTurnoverLabel = new SilacLabel(lightLysine.Letter, heavyLysine.Letter, heavyLysine.ThisChemicalFormula.Formula, heavyLysine.MonoisotopicMass - lightLysine.MonoisotopicMass),
                    EndTurnoverLabel = new SilacLabel(lightLysine.Letter, heavyishLysine.Letter, heavyishLysine.ThisChemicalFormula.Formula, heavyishLysine.MonoisotopicMass - lightLysine.MonoisotopicMass),
                }
            };
            mixedPeptide = new List<PeptideWithSetModifications> { new PeptideWithSetModifications("PEPTbIDEa", new Dictionary<string, Modification>()) }; //+2 +8
            massShift = heavyishLysine.MonoisotopicMass - heavyLysine.MonoisotopicMass;
            massDifferences = new List<List<double>> { new List<double> { massShift, massShift * -1 } }; // -6, +6

            myMsDataFile1 = new TestDataFile(mixedPeptide, massDifferences);
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile1, mzmlName, false);
            task.RunTask(outputFolder, new List<DbForTask> { new DbForTask(xmlName, false) }, new List<string> { mzmlName }, "taskId1").ToString();

            output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllQuantifiedPeptides.tsv");
            Assert.That(output[1].Contains("PEPTKIDEK\t")); //test the unlabeled is present
            Assert.That(output[0].Contains("\tIntensity_silac_Original\tIntensity_silac_NewlySynthesized\tDetection Type_silac_Original\tDetection Type_silac_NewlySynthesized")); //test filename changes
            Assert.That(output[1].Contains("\t656250\t875000\t")); //test intensities

            output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllPSMs.psmtsv");
            Assert.That(output[1].Contains("\tPEPTK(+1.994)IDEK(+8.014)\t")); //test the identified sequence is output

            output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllQuantifiedPeaks.tsv");
            Assert.That(output.Length, Is.EqualTo(4)); //header, unlabeled, mixed, labeled
            Assert.That(output[3].Contains("\tPEPTK(+8.014)IDEK(+8.014)\t")); //test the original is present
            Assert.That(output[3].Contains("\t218750\t")); //test intensity
            Assert.That(output[1].Contains("\tPEPTK(+1.994)IDEK(+8.014)\t")); //test human readable label (and lack thereof) is present
            Assert.That(output[1].Contains("\t875000\t")); //test intensity
            Assert.That(output[2].Contains("\tPEPTK(+1.994)IDEK(+1.994)\t")); //test other label is present
            Assert.That(output[2].Contains("\t437500\t")); //test intensity
            Assert.That(output[2].Contains("silac\t")); //test human readable labels are present

            //Try with conflicting probability values (have a missed cleavage and a non missed cleavage, but set the non missed cleavage past the equilibrium point)
            //test that we don't get negative quantification values after the correction
            //test that the probability calculation is considering the conflicting peptide in its calculation
            List<PeptideWithSetModifications> peptides = new List<PeptideWithSetModifications>
            {
                new PeptideWithSetModifications("PEPTaIDEa",new Dictionary<string,Modification>()),
                new PeptideWithSetModifications("PEPEPEPTb",new Dictionary<string,Modification>())
            };
            massDifferences = new List<List<double>>
            {
                new List<double>{massShift, massShift*2 },
                new List<double>{-1*massShift}
            };
            List<List<double>> intensities = new List<List<double>>
            {
                new List<double>{9,6,3 }, //implies the probability of heavy incorporation (Ph) is 0.5 (LL/LH/HH)
                new List<double>{7,3} //implies the Ph is AT LEAST 0.7, which conflicts with 0.5 (H/L)
            };
            myMsDataFile1 = new TestDataFile(peptides, massDifferences, intensities);
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile1, mzmlName, false);
            task.RunTask(outputFolder, new List<DbForTask> { new DbForTask(xmlName, false) }, new List<string> { mzmlName }, "taskId1").ToString();

            output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllQuantifiedPeptides.tsv");
            Assert.That(output[1].Contains("PEPEPEPTK\t")); //test the unlabeled is present
            Assert.That(output[2].Contains("PEPTKIDEK\t")); //test the unlabeled is present
            Assert.That(output[0].Contains("\tIntensity_silac_Original\tIntensity_silac_NewlySynthesized\tDetection Type_silac_Original\tDetection Type_silac_NewlySynthesized")); //test filename changes
            Assert.That(output[1].Contains("\t2625000\t6125000\t")); //test the light intensity is not negative.
            Assert.That(output[2].Contains("\t10500000\t5250000\t")); //test intensities. The observation is 9/6/3.

            output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllQuantifiedProteinGroups.tsv");
            //test sequence coverage and output worked from multiple labels
            // Both labels should be included, but the order doesnt matter
            Assert.That(output[1].Contains("PEPTK(+8.014)IDEK(+8.014)|PEPEPEPTK(+1.994)") | output[1].Contains("PEPEPEPTK(+1.994)|PEPTK(+8.014)IDEK(+8.014)"));
            Assert.That(output[1].Contains("PEPEPEPTKidekPEPTKIDEKa\tPEPEPEPTKidekPEPTKIDEKa\tPEPEPEPTKidekPEPTKIDEKa"));

            //try modern search (testing indexing)
            task = new SearchTask
            {
                SearchParameters = new SearchParameters
                {
                    StartTurnoverLabel = new SilacLabel(lightLysine.Letter, heavyLysine.Letter, heavyLysine.ThisChemicalFormula.Formula, heavyLysine.MonoisotopicMass - lightLysine.MonoisotopicMass),
                    EndTurnoverLabel = new SilacLabel(lightLysine.Letter, heavyishLysine.Letter, heavyishLysine.ThisChemicalFormula.Formula, heavyishLysine.MonoisotopicMass - lightLysine.MonoisotopicMass),
                    SearchType = SearchType.Modern
                }
            };
            task.RunTask(outputFolder, new List<DbForTask> { new DbForTask(xmlName, false) }, new List<string> { mzmlName }, "taskId1").ToString();

            //delete files
            Directory.Delete(outputFolder, true);
            File.Delete(xmlName);
            File.Delete(mzmlName);
            Directory.Delete(directoryName, true);
        }

        [Test]
        public static void TestSilacMissingPeaks()
        {
            //make heavy residue and add to search task
            Residue heavyLysine = new Residue("a", 'a', "a", Chemistry.ChemicalFormula.ParseFormula("C{13}6H12N{15}2O"), ModificationSites.All); //+8 lysine
            Residue.AddNewResiduesToDictionary(new List<Residue> { heavyLysine });
            Residue lightLysine = Residue.GetResidue('K');

            SearchTask task = new SearchTask
            {
                SearchParameters = new SearchParameters
                {
                    EndTurnoverLabel = new SilacLabel(lightLysine.Letter, heavyLysine.Letter, heavyLysine.ThisChemicalFormula.Formula, heavyLysine.MonoisotopicMass - lightLysine.MonoisotopicMass)
                }
            };

            List<PeptideWithSetModifications> mixedPeptide = new List<PeptideWithSetModifications> { new PeptideWithSetModifications("PEPTKIDEa", new Dictionary<string, Modification>()) }; //has the additional, but not the original
            List<List<double>> massDifferences = new List<List<double>> { new List<double>() };
            MsDataFile myMsDataFile1 = new TestDataFile(mixedPeptide, massDifferences);
            string mzmlName = @"silac.mzML";
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile1, mzmlName, false);

            string xmlName = "SilacDb.xml";
            Protein theProtein = new Protein("PEPTKIDEK", "accession1");
            ProteinDbWriter.WriteXmlDatabase(new Dictionary<string, HashSet<Tuple<int, Modification>>>(), new List<Protein> { theProtein }, xmlName);

            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestSilac");
            Directory.CreateDirectory(outputFolder);
            var theStringResult = task.RunTask(outputFolder, new List<DbForTask> { new DbForTask(xmlName, false) }, new List<string> { mzmlName }, "taskId1").ToString();

            string[] output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllQuantifiedPeaks.tsv");
            Assert.That(output.Length == 4);
            Assert.That(output[1].Contains("\tPEPTK(+8.014)IDEK\t") && output[1].Contains("\t875000\t")); //Doesn't matter where the +8.014 is, just matters that it's mixed (one is light, one is heavy)

            output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllQuantifiedProteinGroups.tsv");
            Assert.That(output[1].Contains("\t\t\t\t1\t")); //check that no intensity is present when only a single missed cleavage value exists
            Assert.That(output[1].Contains("\t1\tPEPTKIDEK\tPEPTKIDEK\t")); //check that the sequence coverage isn't PEPTaIDEa
            Assert.That(output[1].Contains("\t1\tPEPTKIDEK(+8.014)\t")); //check that the peptide id'd has the +8

            output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllPeptides.psmtsv");
            Assert.That(output.Length == 2);
            Assert.That(output[1].Contains("\tPEPTKIDEK(+8.014)\t")); //ensure the order is correct here for the id (not PEPTK(+8.014)IDEK)

            //delete files
            Directory.Delete(outputFolder, true);
            File.Delete(xmlName);
            File.Delete(mzmlName);
        }

        [Test]
        public static void TestSilacTurnoverLabelSites()
        {
            //this tests for handling of no labels on a peptide, one label, six labels, and seven labels.
            //also tests for decoy handling (no protein group)
            //make heavy residue and add to search task
            Residue heavyLysine = new Residue("a", 'a', "a", Chemistry.ChemicalFormula.ParseFormula("C{13}6H12N{15}2O"), ModificationSites.All); //+8 lysine
            Residue.AddNewResiduesToDictionary(new List<Residue> { heavyLysine });
            Residue lightLysine = Residue.GetResidue('K');

            SearchTask task = new SearchTask
            {
                SearchParameters = new SearchParameters
                {
                    EndTurnoverLabel = new SilacLabel(lightLysine.Letter, heavyLysine.Letter, heavyLysine.ThisChemicalFormula.Formula, heavyLysine.MonoisotopicMass - lightLysine.MonoisotopicMass)
                },
                CommonParameters = new CommonParameters
                (
                    digestionParams: new DigestionParams(maxMissedCleavages: 5)
                )
            };

            PeptideWithSetModifications zeroPeptide = new PeptideWithSetModifications("PEPTIDER", new Dictionary<string, Modification>());
            PeptideWithSetModifications onePeptide = new PeptideWithSetModifications("PEPTIDEK", new Dictionary<string, Modification>());
            PeptideWithSetModifications fivePeptide = new PeptideWithSetModifications("PaEKPKTaIK", new Dictionary<string, Modification>());
            PeptideWithSetModifications sixPeptide = new PeptideWithSetModifications("PKEaPaTKIKDa", new Dictionary<string, Modification>());
            MsDataFile myMsDataFile1 = new TestDataFile(new List<IBioPolymerWithSetMods> { zeroPeptide, onePeptide, sixPeptide, fivePeptide });
            string mzmlName = @"silac.mzML";
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile1, mzmlName, false);

            string xmlName = "SilacDb.xml";
            Protein theProtein = new Protein("PEPTIDERPEPTIDEKPKEKPKTKIKDKEK", "accession1");
            Protein decoyProtein = new Protein("KEDITPEP", "accession2");
            ProteinDbWriter.WriteXmlDatabase(new Dictionary<string, HashSet<Tuple<int, Modification>>>(), new List<Protein> { theProtein, decoyProtein }, xmlName);

            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestSilac");
            Directory.CreateDirectory(outputFolder);
            task.RunTask(outputFolder, new List<DbForTask> { new DbForTask(xmlName, false) }, new List<string> { mzmlName }, "taskId1").ToString();
            //Just don't crash
            //This unit test doesn't (currently) include the array of peaks for the possible combinations of labels.
            //Only a single peak is found, and because there's no heavy/light, it's unable to produce a ratio, so NaN values are returned.

            //delete files
            Directory.Delete(outputFolder, true);
            File.Delete(xmlName);
            File.Delete(mzmlName);
        }

        //This test is to check that silac compares profiles between heavy/light peaks and selects intensities from ms1 scans where both were observed, if possible
        [Test]
        public static void TestSilacPeakComparisons()
        {
            //make heavy residue and add to search task
            Residue heavyLysine = new Residue("a", 'a', "a", Chemistry.ChemicalFormula.ParseFormula("C{13}6H12N{15}2O"), ModificationSites.All); //+8 lysine
            Residue lightLysine = Residue.GetResidue('K');

            SearchTask task = new SearchTask
            {
                SearchParameters = new SearchParameters
                {
                    SilacLabels = new List<SilacLabel> { new SilacLabel(lightLysine.Letter, heavyLysine.Letter, heavyLysine.ThisChemicalFormula.Formula, heavyLysine.MonoisotopicMass - lightLysine.MonoisotopicMass) },
                }
            };

            List<PeptideWithSetModifications> lightPeptide = new List<PeptideWithSetModifications> { new PeptideWithSetModifications("PEPTIDEK", new Dictionary<string, Modification>()) }; //has the additional, but not the original
            List<List<double>> massDifferences = new List<List<double>> { new List<double> { (heavyLysine.MonoisotopicMass - lightLysine.MonoisotopicMass) } };

            //ms1, ms2, 4 more ms1s
            //intensities look like (L,H) (5,1.5), (4,2), (3, 1.5), (2,2), (0,3)
            //we want a ratio of 2:1, L:H
            List<List<double>> precursorIntensities = new List<List<double>> { new List<double> { 5, 1.5, 4, 2, 3, 1.5, 2, 2, 0, 3 } };
            MsDataFile myMsDataFile1 = new TestDataFile(lightPeptide, massDifferences, precursorIntensities);
            string mzmlName = @"silac.mzML";
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile1, mzmlName, false);

            string xmlName = "SilacDb.xml";
            Protein theProtein = new Protein("PEPTIDEK", "accession1");
            ProteinDbWriter.WriteXmlDatabase(new Dictionary<string, HashSet<Tuple<int, Modification>>>(), new List<Protein> { theProtein }, xmlName);

            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestSilac");
            Directory.CreateDirectory(outputFolder);
            task.RunTask(outputFolder, new List<DbForTask> { new DbForTask(xmlName, false) }, new List<string> { mzmlName }, "taskId1").ToString();
            string[] output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllQuantifiedPeptides.tsv");
            Assert.That(output[1].Contains("\t12250000\t6125000\t")); //check that it's 2:1 and not 5:3 like it would be for apex


            //TEST for blips, where two peaks are found for a single identification
            massDifferences = new List<List<double>> { new List<double> { (heavyLysine.MonoisotopicMass - lightLysine.MonoisotopicMass) } }; //must be reset, since the method below edits it
            myMsDataFile1 = new TestDataFile(lightPeptide, massDifferences, precursorIntensities, 2);
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile1, mzmlName, false);
            task.RunTask(outputFolder, new List<DbForTask> { new DbForTask(xmlName, false) }, new List<string> { mzmlName }, "taskId1").ToString();
            output = File.ReadAllLines(TestContext.CurrentContext.TestDirectory + @"/TestSilac/AllQuantifiedPeptides.tsv");
            Assert.That(output[1].Contains("\t24500000\t12250000\t")); //intensities will be twice as large as before, but still the same ratio

            //delete files
            Directory.Delete(outputFolder, true);
            File.Delete(xmlName);
            File.Delete(mzmlName);
        }

        [Test]
        public static void TestSilacHelperMethods()
        {
            string sequence = "ASDF[SomeSebuance]GHaASDF"; //keep the 'b' in the modification to test it's not grabbed
            //make heavy residue and add to search task
            Residue heavyLysine = new Residue("a", 'a', "a", Chemistry.ChemicalFormula.ParseFormula("C{13}6H12N{15}2O"), ModificationSites.All); //+8 lysine
            Residue heavyishLysine = new Residue("b", 'b', "b", Chemistry.ChemicalFormula.ParseFormula("C6H12N{15}2O"), ModificationSites.All); //+2 lysine
            Residue lightLysine = Residue.GetResidue('K');

            var silacLabels = new List<SilacLabel>
            {
                new SilacLabel(lightLysine.Letter, heavyLysine.Letter, heavyLysine.ThisChemicalFormula.Formula, heavyLysine.MonoisotopicMass - lightLysine.MonoisotopicMass),
                new SilacLabel(lightLysine.Letter, heavyishLysine.Letter, heavyishLysine.ThisChemicalFormula.Formula, heavyishLysine.MonoisotopicMass - lightLysine.MonoisotopicMass)
            };

            //Test SilacConversions.GetRelevantLabelFromFullSequence
            SilacLabel relevantLabel = SilacConversions.GetRelevantLabelFromFullSequence(sequence, silacLabels);
            Assert.That(relevantLabel.Equals(silacLabels[0]));

            //Test SilacConversions.GetAmbiguousLightSequence
            string asdf = SilacConversions.GetAmbiguousLightSequence("", silacLabels, true);
            Assert.That(asdf.Equals("")); //test that no "|" was added.

            //Test SilacConversions.GetSilacLightBaseSequence
            string asdff = SilacConversions.GetSilacLightBaseSequence("ASDF", null);
            Assert.That(asdff.Equals("ASDF")); //test that there's no change if the label's not present

            //Test SilacConversions.GetSilacLightFullSequence
            string asdfff = SilacConversions.GetSilacLightFullSequence(sequence, silacLabels[0], false);
            Assert.That(asdfff.Equals("ASDF[SomeSebuance]GHKASDF"));

            //Test no crash in weird situations
            SilacConversions.SilacConversionsPostQuantification(null, null, null, new List<FlashLFQ.SpectraFileInfo>(), null, new HashSet<IDigestionParams>(), null, new List<PeptideSpectralMatch>(), new Dictionary<string, int>(), true);
        }
    }
}