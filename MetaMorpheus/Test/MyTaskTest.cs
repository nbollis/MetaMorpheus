using EngineLayer;
using MassSpectrometry;
using MzLibUtil;
using Nett;
using NUnit.Framework; 
using Proteomics;
using Proteomics.ProteolyticDigestion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Omics;
using Omics.Modifications;
using TaskLayer;
using UsefulProteomicsDatabases;
using NUnit.Framework.Legacy;
using System.Text;
using Omics.BioPolymer;

namespace Test
{
    [TestFixture]
    public static class MyTaskTest
    {
        [Test]
        public static void TestEverythingRunner()
        {
            foreach (var modFile in Directory.GetFiles(@"Mods"))
                GlobalVariables.AddMods(PtmListLoader.ReadModsFromFile(modFile, out var fmww), false);

            CalibrationTask task1 = new()
            {
                CommonParameters = new CommonParameters(digestionParams: new DigestionParams(maxMissedCleavages: 0, minPeptideLength: 1, initiatorMethionineBehavior: InitiatorMethionineBehavior.Retain)),

                CalibrationParameters = new CalibrationParameters
                {
                    WriteIntermediateFiles = true,
                    NumFragmentsNeededForEveryIdentification = 6,
                }
            };

            GptmdTask task2 = new()
            {
                CommonParameters = new CommonParameters()
            };
            task2.CommonParameters.DigestionParams.MaxLength = int.MaxValue - 3;

            SearchTask task3 = new()
            {
                CommonParameters = new CommonParameters(),

                SearchParameters = new SearchParameters
                {
                    DoParsimony = true,
                    SearchTarget = true,
                    SearchType = SearchType.Modern
                }
            };

            SearchTask task4 = new()
            {
                CommonParameters = new CommonParameters(),

                SearchParameters = new SearchParameters
                {
                    SearchType = SearchType.Modern,
                }
            };

            List<(string, MetaMorpheusTask)> taskList = new() {
                ("task1", task1),
                ("task2", task2),
                ("task3", task3),
                ("task4", task4),};

            List<Modification> variableModifications = GlobalVariables.AllModsKnown.OfType<Modification>().Where(b => task1.CommonParameters.ListOfModsVariable.Contains((b.ModificationType, b.IdWithMotif))).ToList();
            List<Modification> fixedModifications = GlobalVariables.AllModsKnown.OfType<Modification>().Where(b => task1.CommonParameters.ListOfModsFixed.Contains((b.ModificationType, b.IdWithMotif))).ToList();

            // Generate data for files
            Protein ParentProtein = new("MPEPTIDEKANTHE", "accession1");

            var digestedList = ParentProtein.Digest(task1.CommonParameters.DigestionParams, fixedModifications, variableModifications).ToList();

            Assert.That(digestedList.Count, Is.EqualTo(3));

            IBioPolymerWithSetMods pepWithSetMods1 = digestedList[0];

            IBioPolymerWithSetMods pepWithSetMods2 = digestedList[2];

            var dictHere = new Dictionary<int, List<Modification>>();
            ModificationMotif.TryGetMotif("E", out ModificationMotif motif);
            dictHere.Add(3, new List<Modification> { new Modification(_originalId: "21", _target: motif, _locationRestriction: "Anywhere.", _monoisotopicMass: 21.981943) });
            Protein ParentProteinToNotInclude = new("MPEPTIDEK", "accession2", "organism", new List<Tuple<string, string>>(), dictHere);
            digestedList = ParentProteinToNotInclude.Digest(task1.CommonParameters.DigestionParams, fixedModifications, variableModifications).ToList();

            MsDataFile myMsDataFile = new TestDataFile(new List<IBioPolymerWithSetMods> { pepWithSetMods1, pepWithSetMods2, digestedList[1] });

            Protein proteinWithChain = new("MAACNNNCAA", "accession3", "organism", new List<Tuple<string, string>>(), new Dictionary<int, List<Modification>>(), new List<TruncationProduct> { new TruncationProduct(4, 8, "chain") }, "name2", "fullname2");

            string mzmlName = @"ok.mzML";
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile, mzmlName, false);

            string inputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestEverythingRunnerInput");
            if (Directory.Exists(inputFolder))
            {
                Directory.Delete(inputFolder, true);
            }
            Directory.CreateDirectory(inputFolder);
            File.Copy(Path.Combine(TestContext.CurrentContext.TestDirectory, mzmlName), Path.Join(inputFolder, mzmlName));

            string xmlName = "okk.xml";
            ProteinDbWriter.WriteXmlDatabase(new Dictionary<string, HashSet<Tuple<int, Modification>>>(), new List<Protein> { ParentProtein, proteinWithChain }, xmlName);
            File.Copy(Path.Combine(TestContext.CurrentContext.TestDirectory, mzmlName), Path.Join(inputFolder, xmlName));

            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestEverythingRunnerOutput");
            if (Directory.Exists(outputFolder))
            {
                Directory.Delete(outputFolder, true);
            }
            Directory.CreateDirectory(outputFolder);

            // RUN!
            var engine = new EverythingRunnerEngine(taskList, new List<string> { Path.Join(inputFolder, mzmlName) }, new List<DbForTask> { new DbForTask(Path.Join(inputFolder, xmlName), false) }, outputFolder);
            engine.Run();
            File.Delete(Path.Combine(TestContext.CurrentContext.TestDirectory, mzmlName));
            File.Delete(Path.Combine(TestContext.CurrentContext.TestDirectory, xmlName));
            //Directory.Delete(inputFolder, true);
            //Directory.Delete(outputFolder, true);
        }
        /// <summary>
        /// Designed to hit the nooks and crannies of ModificationAnalysisEngine
        /// </summary>
        [Test]
        public static void TestModificationAnalysisEngine()
        {
            var myGptmdTomlPath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\ModificationAnalysis\GPTMDTaskconfig.toml");
            var gptmdTaskLoaded = Toml.ReadFile<GptmdTask>(myGptmdTomlPath, MetaMorpheusTask.tomlConfig);

            var mySearchTomlPath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\ModificationAnalysis\SearchTaskconfig.toml");
            var searchTaskLoaded = Toml.ReadFile<SearchTask>(mySearchTomlPath, MetaMorpheusTask.tomlConfig);

            List<(string, MetaMorpheusTask)> taskList = new() {
                ("gptmd", gptmdTaskLoaded),
                ("search", searchTaskLoaded)};

            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\ModificationAnalysis\Results");
            string myFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\ModificationAnalysis\modificationAnalysis.mzML");
            string myDatabase = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\ModificationAnalysis\modificationAnalysis.fasta");

            List<DbForTask> CurrentXmlDbFilenameList = new() { new DbForTask(myDatabase, false) };

            StringBuilder allResultsText = new StringBuilder();
            for (int i = 0; i < taskList.Count; i++)
            {
                var ok = taskList[i];

                // reset product types for custom fragmentation
                ok.Item2.CommonParameters.SetCustomProductTypes();

                var outputFolderForThisTask = Path.Combine(outputFolder, ok.Item1);

                if (!Directory.Exists(outputFolderForThisTask))
                    Directory.CreateDirectory(outputFolderForThisTask);

                // Actual task running code
                var myTaskResults = ok.Item2.RunTask(outputFolderForThisTask, CurrentXmlDbFilenameList, new List<string> { myFile}, ok.Item1);
                allResultsText.AppendLine(Environment.NewLine + Environment.NewLine + Environment.NewLine + Environment.NewLine + myTaskResults.ToString());

                if (myTaskResults.NewDatabases != null)
                {
                    CurrentXmlDbFilenameList = myTaskResults.NewDatabases;
                }
            }

            var k = allResultsText.ToString();

            Assert.That(k.Contains("Localized mods seen below q-value 0.01:\r\n\tCarbamidomethyl on C\t84"));
            Assert.That(k.Contains("(Approx) Additional localized but protein ambiguous mods seen below q-value 0.01:\r\n\tCarbamidomethyl on C\t9"));
            Assert.That(k.Contains("(Approx) Additional unlocalized mods seen below q-value 0.01:\r\n\tDecarboxylation on E\t2"));
            Assert.That(k.Contains("(Approx) Additional unlocalized modification formulas seen below q-value 0.01:\r\n\tO3\t4"));

            Directory.Delete(outputFolder, true);
        }
        [Test]
        public static void TestMultipleFilesRunner()
        {
            foreach (var modFile in Directory.GetFiles(@"Mods"))
                GlobalVariables.AddMods(PtmListLoader.ReadModsFromFile(modFile, out var fmww), false);

            CalibrationTask task1 = new CalibrationTask
            {
                CommonParameters = new CommonParameters
                (
                    digestionParams: new DigestionParams(maxMissedCleavages: 0, minPeptideLength: 1, initiatorMethionineBehavior: InitiatorMethionineBehavior.Retain),
                    listOfModsVariable: new List<(string, string)> { ("Common Variable", "Oxidation on M") },
                    listOfModsFixed: new List<(string, string)> { ("Common Fixed", "Carbamidomethyl on C") },
                    productMassTolerance: new AbsoluteTolerance(0.01)
                ),
                CalibrationParameters = new CalibrationParameters
                {
                    NumFragmentsNeededForEveryIdentification = 6,
                }
            };
            GptmdTask task2 = new()
            {
                CommonParameters = new CommonParameters
                (
                    digestionParams: new DigestionParams(),
                    productMassTolerance: new AbsoluteTolerance(0.01)
                ),
            };

            SearchTask task3 = new()
            {
                CommonParameters = new CommonParameters(),

                SearchParameters = new SearchParameters
                {
                    DoParsimony = true,
                    SearchTarget = true,
                    SearchType = SearchType.Modern,
                }
            };
            SearchTask task4 = new()
            {
                CommonParameters = new CommonParameters(),

                SearchParameters = new SearchParameters
                {
                    SearchType = SearchType.Modern,
                }
            };
            List<(string, MetaMorpheusTask)> taskList = new List<(string, MetaMorpheusTask)> {
                ("task1", task1),
                ("task2", task2),
                ("task3", task3),
                ("task4", task4),};

            List<Modification> variableModifications = GlobalVariables.AllModsKnown.OfType<Modification>().Where(b => task1.CommonParameters.ListOfModsVariable.Contains((b.ModificationType, b.IdWithMotif))).ToList();
            List<Modification> fixedModifications = GlobalVariables.AllModsKnown.OfType<Modification>().Where(b => task1.CommonParameters.ListOfModsFixed.Contains((b.ModificationType, b.IdWithMotif))).ToList();

            // Generate data for files
            Protein ParentProtein = new("MPEPTIDEKANTHE", "accession1");

            var digestedList = ParentProtein.Digest(task1.CommonParameters.DigestionParams, fixedModifications, variableModifications).ToList();

            Assert.That(digestedList.Count, Is.EqualTo(3));

            var pepWithSetMods1 = digestedList[0];

            var pepWithSetMods2 = digestedList[2];

            var dictHere = new Dictionary<int, List<Modification>>();
            ModificationMotif.TryGetMotif("E", out ModificationMotif motif);
            dictHere.Add(3, new List<Modification> { new Modification(_originalId: "21", _modificationType: "myModType", _target: motif, _locationRestriction: "Anywhere.", _monoisotopicMass: 21.981943) });
            Protein ParentProteinToNotInclude = new("MPEPTIDEK", "accession2", "organism", new List<Tuple<string, string>>(), dictHere);
            digestedList = ParentProteinToNotInclude.Digest(task1.CommonParameters.DigestionParams, fixedModifications, variableModifications).ToList();
            Assert.That(digestedList.Count, Is.EqualTo(4));

            MsDataFile myMsDataFile1 = new TestDataFile(new List<IBioPolymerWithSetMods> { pepWithSetMods1, pepWithSetMods2, digestedList[1] });

            string mzmlName1 = @"ok1.mzML";
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile1, mzmlName1, false);

            MsDataFile myMsDataFile2 = new TestDataFile(new List<IBioPolymerWithSetMods> { pepWithSetMods1, pepWithSetMods2, digestedList[1] });

            string mzmlName2 = @"ok2.mzML";
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile2, mzmlName2, false);

            Protein proteinWithChain1 = new("MAACNNNCAA", "accession3", "organism", new List<Tuple<string, string>>(), new Dictionary<int, List<Modification>>(), new List<TruncationProduct> { new TruncationProduct(4, 8, "chain") }, "name2", "fullname2", false, false, new List<DatabaseReference>(), new List<SequenceVariation>(), null);
            Protein proteinWithChain2 = new("MAACNNNCAA", "accession3", "organism", new List<Tuple<string, string>>(), new Dictionary<int, List<Modification>>(), new List<TruncationProduct> { new TruncationProduct(4, 8, "chain") }, "name2", "fullname2", false, false, new List<DatabaseReference>(), new List<SequenceVariation>(), null);

            string xmlName = "okk.xml";
            ProteinDbWriter.WriteXmlDatabase(new Dictionary<string, HashSet<Tuple<int, Modification>>>(), new List<Protein> { ParentProtein, proteinWithChain1, proteinWithChain2 }, xmlName);

            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestMultipleFilesRunner");
            // RUN!
            var engine = new EverythingRunnerEngine(taskList, new List<string> { mzmlName1, mzmlName2 }, new List<DbForTask> { new DbForTask(xmlName, false) }, outputFolder);
            engine.Run();
            Directory.Delete(outputFolder, true);
            File.Delete(xmlName);
            File.Delete(mzmlName1);
            File.Delete(mzmlName2);
        }

        [Test]
        public static void MakeSureFdrDoesntSkip()
        {
            MetaMorpheusTask task = new SearchTask
            {
                CommonParameters = new CommonParameters
                (
                    digestionParams: new DigestionParams(minPeptideLength: 2),
                    scoreCutoff: 1,
                    deconvolutionIntensityRatio: 999,
                    deconvolutionMassTolerance: new PpmTolerance(50),
                    maxThreadsToUsePerFile: 1
                ),
                SearchParameters = new SearchParameters
                {
                    DecoyType = DecoyType.None,
                    MassDiffAcceptorType = MassDiffAcceptorType.Open,
                }
            };

            string xmlName = "MakeSureFdrDoesntSkip.xml";

            {
                Protein theProtein = new("MG", "accession1");
                ProteinDbWriter.WriteXmlDatabase(new Dictionary<string, HashSet<Tuple<int, Modification>>>(), new List<Protein> { theProtein }, xmlName);
            }

            string mzmlName = @"MakeSureFdrDoesntSkip.mzML";

            var theProteins = ProteinDbLoader.LoadProteinXML(xmlName, true, DecoyType.Reverse, new List<Modification>(), false, new List<string>(), out Dictionary<string, Modification> ok);

            List<Modification> fixedModifications = new();

            var targetDigested = theProteins[0].Digest(task.CommonParameters.DigestionParams, fixedModifications, GlobalVariables.AllModsKnown.OfType<Modification>().ToList()).ToList();

            var targetGood = targetDigested.First();

            TestDataFile myMsDataFile = new(new List<IBioPolymerWithSetMods> { targetGood });

            var ms1IntensityList = myMsDataFile.GetOneBasedScan(1).MassSpectrum.YArray.ToList();

            ms1IntensityList.Add(1);
            ms1IntensityList.Add(1);
            ms1IntensityList.Add(1);

            var newIntensityArray = ms1IntensityList.ToArray();

            var ms1MzList = myMsDataFile.GetOneBasedScan(1).MassSpectrum.XArray.ToList();
            Assert.That(ms1MzList.Count, Is.EqualTo(6));

            List<double> expectedMzList = new List<double>() { 69.70, 70.03, 70.37, 104.04, 104.55, 105.05 };
            CollectionAssert.AreEquivalent(expectedMzList, ms1MzList.Select(m=>Math.Round(m,2)).ToList());

            var firstMz = 104.35352; //this mz is close to one of original mz values, but not exactly the same, it should not disrupt deconvolution
            ms1MzList.Add(firstMz);
            ms1MzList.Add(firstMz + 1);
            ms1MzList.Add(firstMz + 2);

            var newMzArray = ms1MzList.ToArray();

            Array.Sort(newMzArray, newIntensityArray);

            myMsDataFile.ReplaceFirstMs1ScanArrays(newMzArray, newIntensityArray);

            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile, mzmlName, false);
            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestMakeSureFdrDoesntSkip");
            Directory.CreateDirectory(outputFolder);

            // RUN!
            var theStringResult = task.RunTask(outputFolder, new List<DbForTask> { new DbForTask(xmlName, false) }, new List<string> { mzmlName }, "taskId1").ToString();


            //There is one PSM with close peptide mass (0 ppm difference) and one PSM with large mass difference (>1000 ppm difference)
            //Since this is an open search, both PSMs should be reported because they share the exact same MS2 scan

            Assert.That(theStringResult.Contains("All target PSMs with q-value <= 0.01: 1"));
            Directory.Delete(outputFolder, true);
            File.Delete(xmlName);
            File.Delete(mzmlName);
        }

        [Test]
        public static void MakeSureGptmdTaskMatchesExactMatches()
        {
            MetaMorpheusTask task1;

            {
                ModificationMotif.TryGetMotif("T", out ModificationMotif motif);
                Modification myNewMod = new(_originalId: "ok", _modificationType: "okType", _target: motif, _locationRestriction: "Anywhere.", _monoisotopicMass: 229);

                GlobalVariables.AddMods(new List<Modification> { myNewMod }, false);
                task1 = new GptmdTask
                {
                    CommonParameters = new CommonParameters
                    (
                        digestionParams: new DigestionParams(initiatorMethionineBehavior: InitiatorMethionineBehavior.Retain),
                        listOfModsVariable: new List<(string, string)>(),
                        listOfModsFixed: new List<(string, string)>(),
                        scoreCutoff: 1,
                        precursorMassTolerance: new AbsoluteTolerance(1)
                    ),

                    GptmdParameters = new GptmdParameters
                    {
                        ListOfModsGptmd = new List<(string, string)> { ("okType", "ok on T") },
                    }
                };
            }

            string xmlName = "sweetness.xml";

            {
                Protein theProtein = new Protein("MPEPTIDEKANTHE", "accession1");
                ProteinDbWriter.WriteXmlDatabase(new Dictionary<string, HashSet<Tuple<int, Modification>>>(), new List<Protein> { theProtein }, xmlName);
            }

            string mzmlName = @"ok.mzML";

            {
                var theProteins = ProteinDbLoader.LoadProteinXML(xmlName, true, DecoyType.Reverse, new List<Modification>(), false, new List<string>(), out Dictionary<string, Modification> ok);

                List<Modification> fixedModifications = new List<Modification>();

                var targetDigested = theProteins[0].Digest(task1.CommonParameters.DigestionParams, fixedModifications, GlobalVariables.AllModsKnown.OfType<Modification>().Where(b => b.OriginalId.Equals("ok")).ToList()).ToList();

                ModificationMotif.TryGetMotif("T", out ModificationMotif motif);
                var targetGood = targetDigested[0];

                var targetWithUnknownMod = targetDigested[1];
                MsDataFile myMsDataFile = new TestDataFile(new List<IBioPolymerWithSetMods> { targetGood, targetWithUnknownMod });

                Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile, mzmlName, false);
            }
            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestMakeSureGptmdTaskMatchesExactMatchesTest");
            Directory.CreateDirectory(outputFolder);

            // RUN!
            var theStringResult = task1.RunTask(outputFolder, new List<DbForTask> { new DbForTask(xmlName, false) }, new List<string> { mzmlName }, "taskId1").ToString();
            Assert.That(theStringResult.Contains("Modifications added: 1"));
            Directory.Delete(outputFolder, true);
            File.Delete(xmlName);
            File.Delete(mzmlName);
            Directory.Delete(Path.Combine(TestContext.CurrentContext.TestDirectory, @"Task Settings"), true);
        }
        [Test]
        public static void TestGptmdTaskWithContaminantDatabase()
        {
            MetaMorpheusTask task1;

            {
                ModificationMotif.TryGetMotif("T", out ModificationMotif motif);
                Modification myNewMod = new(_originalId: "ok", _modificationType: "okType", _target: motif, _locationRestriction: "Anywhere.", _monoisotopicMass: 229);

                GlobalVariables.AddMods(new List<Modification> { myNewMod }, false);
                task1 = new GptmdTask
                {
                    CommonParameters = new CommonParameters
                    (
                        digestionParams: new DigestionParams(initiatorMethionineBehavior: InitiatorMethionineBehavior.Retain),
                        listOfModsVariable: new List<(string, string)>(),
                        listOfModsFixed: new List<(string, string)>(),
                        scoreCutoff: 1,
                        precursorMassTolerance: new AbsoluteTolerance(1)
                    ),

                    GptmdParameters = new GptmdParameters
                    {
                        ListOfModsGptmd = new List<(string, string)> { ("okType", "ok on T") },
                    }
                };
            }

            string xmlName = "sweetness.xml";

            {
                Protein theProtein = new Protein("MPEPTIDEKANTHE", "accession1", isContaminant: true);
                ProteinDbWriter.WriteXmlDatabase(new Dictionary<string, HashSet<Tuple<int, Modification>>>(), new List<Protein> { theProtein }, xmlName);
            }

            string mzmlName = @"ok.mzML";

            {
                var theProteins = ProteinDbLoader.LoadProteinXML(xmlName, true, DecoyType.Reverse, new List<Modification>(), false, new List<string>(), out Dictionary<string, Modification> ok);

                List<Modification> fixedModifications = new List<Modification>();

                var targetDigested = theProteins[0].Digest(task1.CommonParameters.DigestionParams, fixedModifications, GlobalVariables.AllModsKnown.OfType<Modification>().Where(b => b.OriginalId.Equals("ok")).ToList()).ToList();

                ModificationMotif.TryGetMotif("T", out ModificationMotif motif);
                IBioPolymerWithSetMods targetGood = targetDigested[0];

                IBioPolymerWithSetMods targetWithUnknownMod = targetDigested[1];
                MsDataFile myMsDataFile = new TestDataFile(new List<IBioPolymerWithSetMods> { targetGood, targetWithUnknownMod });

                Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile, mzmlName, false);
            }
            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestMakeSureGptmdTaskMatchesExactMatchesTest");
            Directory.CreateDirectory(outputFolder);

            // RUN!
            var theContaminantDbResult = task1.RunTask(outputFolder, new List<DbForTask> { new DbForTask(xmlName, true) }, new List<string> { mzmlName }, "taskId1").ToString();
            Assert.That(theContaminantDbResult.Contains("Contaminant modifications added: 1"));
            Directory.Delete(outputFolder, true);
            File.Delete(xmlName);
            File.Delete(mzmlName);
            Directory.Delete(Path.Combine(TestContext.CurrentContext.TestDirectory, @"Task Settings"), true);
        }

        [Test]
        public static void TestPeptideCount()
        {
            SearchTask testPeptides = new()
            {
                CommonParameters = new CommonParameters
                (

                    digestionParams: new DigestionParams(minPeptideLength: 5)
                ),
                SearchParameters = new SearchParameters
                {
                    WritePrunedDatabase = true,
                    SearchTarget = true,
                    MassDiffAcceptorType = MassDiffAcceptorType.Exact
                }
            };

            List<(string, MetaMorpheusTask)> taskList = new List<(string, MetaMorpheusTask)>
            {
               ("TestPeptides", testPeptides)
            };

            ModificationMotif.TryGetMotif("P", out ModificationMotif motif);

            var testUniqeMod = new Modification(_originalId: "testPeptideMod", _modificationType: "mt", _target: motif, _locationRestriction: "Anywhere.", _monoisotopicMass: 10);
            GlobalVariables.AddMods(new List<Modification>
            {
                testUniqeMod
            }, false);

            //create modification lists

            List<Modification> variableModifications = GlobalVariables.AllModsKnown.OfType<Modification>().Where
                (b => testPeptides.CommonParameters.ListOfModsVariable.Contains((b.ModificationType, b.IdWithMotif))).ToList();

            //add modification to Protein object
            var modDictionary = new Dictionary<int, List<Modification>>();
            Modification modToAdd = testUniqeMod;
            modDictionary.Add(1, new List<Modification> { modToAdd });
            modDictionary.Add(3, new List<Modification> { modToAdd });

            //protein Creation (One with mod and one without)
            Protein TestProtein = new Protein("PEPTID", "accession1", "organism", new List<Tuple<string, string>>(), modDictionary);

            //First Write XML Database

            string xmlName = "singleProteinWithTwoMods.xml";

            //Add Mod to list and write XML input database
            Dictionary<string, HashSet<Tuple<int, Modification>>> modList = new Dictionary<string, HashSet<Tuple<int, Modification>>>();
            var Hash = new HashSet<Tuple<int, Modification>>
            {
                new Tuple<int, Modification>(3, modToAdd)
            };
            modList.Add("test", Hash);
            ProteinDbWriter.WriteXmlDatabase(modList, new List<Protein> { TestProtein }, xmlName);

            //now write MZML file
            var protein = ProteinDbLoader.LoadProteinXML(xmlName, true, DecoyType.Reverse, new List<Modification>(), false, new List<string>(), out Dictionary<string, Modification> ok);
            var setList1 = protein[0].Digest(testPeptides.CommonParameters.DigestionParams, new List<Modification> { }, variableModifications).ToList();
            Assert.That(setList1.Count, Is.EqualTo(4));

            //Finally Write MZML file
            MsDataFile myMsDataFile = new TestDataFile(new List<IBioPolymerWithSetMods> { setList1[0], setList1[1], setList1[2], setList1[3], setList1[0], setList1[1] });
            string mzmlName = @"singleProteinWithRepeatedMods.mzML";
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile, mzmlName, false);

            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestMultipleFilesRunner");
            var engine = new EverythingRunnerEngine(taskList, new List<string> { mzmlName }, new List<DbForTask> { new DbForTask(xmlName, false) }, outputFolder);
            engine.Run();

            string line;

            bool foundD = false;
            using (StreamReader file = new(Path.Combine(MySetUpClass.outputFolder, "TestPeptides", "results.txt")))
            {
                while ((line = file.ReadLine()) != null)
                {
                    if (line.Contains("All target peptides with q-value <= 0.01: 4"))
                    {
                        foundD = true;
                    }
                }
            }
            Assert.That(foundD);
            Directory.Delete(outputFolder, true);
            File.Delete(mzmlName);
            File.Delete(xmlName);
        }

        [Test]
        public static void TestFileOutput()
        {
            string thisTaskOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestFileOutput");

            SearchTask task = Toml.ReadFile<SearchTask>(Path.Combine(TestContext.CurrentContext.TestDirectory, @"SlicedSearchTaskConfig.toml"), MetaMorpheusTask.tomlConfig);
            task.SearchParameters.DecoyType = DecoyType.None;
            task.SearchParameters.WriteMzId = true;

            DbForTask db = new(Path.Combine(TestContext.CurrentContext.TestDirectory, @"sliced-db.fasta"), false);
            DbForTask db2 = new(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", @"DbForPrunedDb.fasta"), false);
            string raw = Path.Combine(TestContext.CurrentContext.TestDirectory, @"sliced-raw.mzML");
            string raw2 = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", @"PrunedDbSpectra.mzml");
            EverythingRunnerEngine singleMassSpectraFile = new(new List<(string, MetaMorpheusTask)> { ("SingleMassSpectraFileOutput", task) }, new List<string> { raw }, new List<DbForTask> { db }, thisTaskOutputFolder);
            EverythingRunnerEngine multipleMassSpectraFiles = new(new List<(string, MetaMorpheusTask)> { ("MultipleMassSpectraFileOutput", task) }, new List<string> { raw, raw2 }, new List<DbForTask> { db, db2 }, thisTaskOutputFolder);

            singleMassSpectraFile.Run();
            multipleMassSpectraFiles.Run();

            // test single file output. With only 1 file, all results including mzid get written to the parent directory.
            HashSet<string> expectedFiles = new HashSet<string> {
                "AllPeptides.psmtsv", "AllQuantifiedProteinGroups.tsv", "AllPSMs.psmtsv", "AllPSMs_FormattedForPercolator.tab", "AllQuantifiedPeaks.tsv",
                "AllQuantifiedPeptides.tsv", "AutoGeneratedManuscriptProse.txt", "results.txt", "sliced-raw.mzID" };

            HashSet<string> files = new(Directory.GetFiles(Path.Combine(thisTaskOutputFolder, "SingleMassSpectraFileOutput")).Select(v => Path.GetFileName(v)));

            // these 2 lines are for debug purposes, so you can see which files you're missing (if any)
            var missingFiles = expectedFiles.Except(files);
            var extraFiles = files.Except(expectedFiles);

            // test that output is what's expected
            Assert.That(files.SetEquals(expectedFiles));

            // test multi file output. With multiple files, .mzid results are inside a folder.
            expectedFiles = new HashSet<string> {
                "AllPeptides.psmtsv", "AllQuantifiedProteinGroups.tsv", "AllPSMs.psmtsv", "AllPSMs_FormattedForPercolator.tab", "AllQuantifiedPeaks.tsv",
                "AllQuantifiedPeptides.tsv", "AutoGeneratedManuscriptProse.txt", "results.txt"};
            files = new HashSet<string>(Directory.GetFiles(Path.Combine(thisTaskOutputFolder, "MultipleMassSpectraFileOutput")).Select(v => Path.GetFileName(v)));
            missingFiles = expectedFiles.Except(files);
            extraFiles = files.Except(expectedFiles);

            Assert.That(files.SetEquals(expectedFiles));

            expectedFiles = new HashSet<string> {
                "PrunedDbSpectra.mzID", "PrunedDbSpectra_PSMs.psmtsv", "PrunedDbSpectra_PSMsFormattedForPercolator.tab", "PrunedDbSpectra_Peptides.psmtsv", "PrunedDbSpectra_ProteinGroups.tsv", "PrunedDbSpectra_QuantifiedPeaks.tsv",
                "sliced-raw.mzID", "sliced-raw_PSMs.psmtsv", "sliced-raw_PSMsFormattedForPercolator.tab", "sliced-raw_Peptides.psmtsv", "sliced-raw_ProteinGroups.tsv", "sliced-raw_QuantifiedPeaks.tsv" };

            string individualFilePath = Path.Combine(thisTaskOutputFolder, "MultipleMassSpectraFileOutput", "Individual File Results");
            Assert.That(Directory.Exists(individualFilePath));

            files = new HashSet<string>(Directory.GetFiles(individualFilePath).Select(v => Path.GetFileName(v)));
            missingFiles = expectedFiles.Except(files);
            extraFiles = files.Except(expectedFiles);

            CollectionAssert.AreEquivalent(expectedFiles, files);

            files = new HashSet<string>(Directory.GetFiles(Path.Combine(thisTaskOutputFolder, "Task Settings")).Select(v => Path.GetFileName(v)));
            expectedFiles = new HashSet<string> {
                "MultipleMassSpectraFileOutputconfig.toml", "SingleMassSpectraFileOutputconfig.toml" };
            Assert.That(files.SetEquals(expectedFiles));
            Directory.Delete(thisTaskOutputFolder, true);
        }

        /// <summary>
        /// This tests for a bug in annotating mods in the search task. The situation is that if you search with a fasta database (no mods annotated),
        /// and then do GPTMD, then search with the GPTMD database, the resulting PSM will have a UniProt mod annotated on it.
        /// Also, if GPTMD has a mod with the same name as a UniProt mod, the annotated PSM will be ambiguous between
        /// the UniProt and the MetaMorpheus modification.
        /// </summary>
        [Test]
        public static void TestUniprotNamingConflicts()
        {
            // write the mod
            var outputDir = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestUniprotNamingConflicts");
            Directory.CreateDirectory(outputDir);
            string modToWrite = "Custom List\nID   Hydroxyproline\nTG   P\nPP   Anywhere.\nMT   Biological\nCF   O1\n" + @"//";
            var filePath = Path.Combine(GlobalVariables.DataDir, @"Mods", @"hydroxyproline.txt");
            File.WriteAllLines(filePath, new string[] { modToWrite });

            // read the mod
            GlobalVariables.AddMods(PtmListLoader.ReadModsFromFile(filePath, out var fmww), false);
            Assert.That(GlobalVariables.AllModsKnown.Where(v => v.IdWithMotif == "Hydroxyproline on P").Count() == 1);

            // should have an error message...
            Assert.That(GlobalVariables.ErrorsReadingMods.Where(v => v.Contains("Hydroxyproline")).Count() > 0);
            Directory.Delete(outputDir, true);
        }

        /// <summary>
        /// Tests that pepXML is written
        ///
        /// TODO: Assert pepXML properties
        /// </summary>
        [Test]
        public static void TestPepXmlOutput()
        {
            SearchTask search = new SearchTask
            {
                SearchParameters = new SearchParameters
                {
                    WritePepXml = true
                }
            };

            List<(string, MetaMorpheusTask)> taskList = new List<(string, MetaMorpheusTask)> { ("TestPepXmlOutput", search) };

            string mzmlName = @"TestData\PrunedDbSpectra.mzml";
            string fastaName = @"TestData\DbForPrunedDb.fasta";
            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestPepXmlOutput");

            var engine = new EverythingRunnerEngine(taskList, new List<string> { mzmlName }, new List<DbForTask> { new DbForTask(fastaName, false) }, outputFolder);
            engine.Run();

            string outputPepXmlPath = Path.Combine(outputFolder, @"TestPepXmlOutput\PrunedDbSpectra.pep.XML");
            Assert.That(File.Exists(outputPepXmlPath));
            Directory.Delete(outputFolder, true);
        }

        [Test]
        public static void TestModernAndClassicSearch()
        {
            SearchTask classicSearch = new();

            SearchTask modernSearch = new()
            {
                SearchParameters = new SearchParameters
                {
                    SearchType = SearchType.Modern
                }
            };
            List<int> counts = new List<int>();

            List<(string, MetaMorpheusTask)> taskList = new List<(string, MetaMorpheusTask)> { ("ClassicSearch", classicSearch), ("ModernSearch", modernSearch) };

            string mzmlName = @"TestData\PrunedDbSpectra.mzml";
            string fastaName = @"TestData\DbForPrunedDb.fasta";
            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestPepXmlOutput");

            var engine = new EverythingRunnerEngine(taskList, new List<string> { mzmlName }, new List<DbForTask> { new DbForTask(fastaName, false) }, outputFolder);
            engine.Run();

            string classicPath = Path.Combine(outputFolder, @"ClassicSearch\AllPSMs.psmtsv");
            var classicPsms = File.ReadAllLines(classicPath).ToList();

            string modernPath = Path.Combine(outputFolder, @"ModernSearch\AllPSMs.psmtsv");
            var modernPsms = File.ReadAllLines(modernPath).ToList();
            counts.Add(modernPsms.Count);

            Assert.That(modernPsms.SequenceEqual(classicPsms));
            Directory.Delete(outputFolder, true);
        }

        [Test]
        public static void CheckOpenFileTest()
        {
            var manager = new MyFileManager(true);
            Assert.That(manager.SeeIfOpen(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\allResults.txt")) == false);
        }

        [Test]
        public static void MissingDbInSpectralLibrarySearch()
        {

            SearchTask classicSearch = new();

            List<(string, MetaMorpheusTask)> taskList = new() { ("ClassicSearch", classicSearch)};

            string mzmlName = @"TestData\PrunedDbSpectra.mzml";
            string fastaName = @"TestData\SpectralLibrarySearch\P16858_target.msp";
            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestMspNoXML");

            var engine = new EverythingRunnerEngine(taskList, new List<string> { mzmlName }, new List<DbForTask> { new DbForTask(fastaName, false) }, outputFolder);
            engine.Run();

            List<string> warnings = engine.Warnings;

            Assert.That(warnings[0], Is.EqualTo("Cannot proceed. No protein database files selected."));
        }
    }
}