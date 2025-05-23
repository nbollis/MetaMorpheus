﻿using EngineLayer;
using MassSpectrometry;
using MzLibUtil;
using NUnit.Framework; 
using Proteomics;
using Omics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Omics.Digestion;
using Omics.Modifications;
using Readers;
using TaskLayer;

namespace Test
{
    [TestFixture]
    public static class SearchTaskTest
    {
        /// <summary>
        /// Tests each type of mass difference acceptor type to make sure values are assigned properly
        /// </summary>
        [Test]
        public static void MassDiffAceptorTest()
        {
            SearchTask searchTask = new SearchTask();
            var result = SearchTask.GetMassDiffAcceptor(searchTask.CommonParameters.PrecursorMassTolerance, searchTask.SearchParameters.MassDiffAcceptorType, searchTask.SearchParameters.CustomMdac);
            Assert.That(result.FileNameAddition.Equals("1mm"));

            result = SearchTask.GetMassDiffAcceptor(searchTask.CommonParameters.PrecursorMassTolerance, MassDiffAcceptorType.TwoMM, searchTask.SearchParameters.CustomMdac);
            Assert.That(result.FileNameAddition.Equals("2mm"));

            result = SearchTask.GetMassDiffAcceptor(searchTask.CommonParameters.PrecursorMassTolerance, MassDiffAcceptorType.ThreeMM, searchTask.SearchParameters.CustomMdac);
            Assert.That(result.FileNameAddition.Equals("3mm"));

            result = SearchTask.GetMassDiffAcceptor(searchTask.CommonParameters.PrecursorMassTolerance, MassDiffAcceptorType.ModOpen, searchTask.SearchParameters.CustomMdac);
            Assert.That(result.FileNameAddition.Equals("-187andUp"));

            result = SearchTask.GetMassDiffAcceptor(searchTask.CommonParameters.PrecursorMassTolerance, MassDiffAcceptorType.Open, searchTask.SearchParameters.CustomMdac);
            Assert.That(result.FileNameAddition.Equals("OpenSearch"));

            result = SearchTask.GetMassDiffAcceptor(searchTask.CommonParameters.PrecursorMassTolerance, MassDiffAcceptorType.Custom, "custom ppmAroundZero 4");
            Assert.That(result.FileNameAddition.Equals("4ppmAroundZero"));

            result = SearchTask.GetMassDiffAcceptor(searchTask.CommonParameters.PrecursorMassTolerance, MassDiffAcceptorType.Exact, searchTask.SearchParameters.CustomMdac);
            Assert.That(result.FileNameAddition.Equals("5ppmAroundZero"));

            result = SearchTask.GetMassDiffAcceptor(searchTask.CommonParameters.PrecursorMassTolerance, MassDiffAcceptorType.PlusOrMinusThreeMM, searchTask.SearchParameters.CustomMdac);
            Assert.That(result.FileNameAddition.Equals("PlusOrMinus3Da"));
        }

        /// <summary>
        /// Tests to make sure custom mass difference acceptor inputs are parsed properly
        /// </summary>
        [Test]
        public static void ParseSearchModeTest()
        {
            SearchTask searchTask = new SearchTask();
            var result = SearchTask.GetMassDiffAcceptor(searchTask.CommonParameters.PrecursorMassTolerance, MassDiffAcceptorType.Custom, "TestCustom dot 5 ppm 0,1.0029,2.0052");
            Assert.That(result.NumNotches == 3);

            result = SearchTask.GetMassDiffAcceptor(searchTask.CommonParameters.PrecursorMassTolerance, MassDiffAcceptorType.Custom, "TestCustom dot 5 da 0,1.0029,2.0052");
            Assert.That(result.NumNotches == 3);

            result = SearchTask.GetMassDiffAcceptor(searchTask.CommonParameters.PrecursorMassTolerance, MassDiffAcceptorType.Custom, "TestCustom interval [0,5];[0,5]");
            Assert.That(result.NumNotches == 1);

            result = SearchTask.GetMassDiffAcceptor(searchTask.CommonParameters.PrecursorMassTolerance, MassDiffAcceptorType.Custom, "TestCustom OpenSearch 5");
            Assert.That(result.FileNameAddition.Equals("OpenSearch"));

            result = SearchTask.GetMassDiffAcceptor(searchTask.CommonParameters.PrecursorMassTolerance, MassDiffAcceptorType.Custom, "TestCustom daltonsAroundZero 5");
            Assert.That(result.FileNameAddition.Equals("5daltonsAroundZero"));

            result = SearchTask.GetMassDiffAcceptor(searchTask.CommonParameters.PrecursorMassTolerance, MassDiffAcceptorType.Custom, "TestCustom ppmAroundZero 5");
            Assert.That(result.FileNameAddition.Equals("5ppmAroundZero"));

            Assert.That(() => SearchTask.GetMassDiffAcceptor(searchTask.CommonParameters.PrecursorMassTolerance, MassDiffAcceptorType.Custom, "TestCustom Test 5"),
                Throws.TypeOf<MetaMorpheusException>());
        }

        /// <summary>
        /// Ensures that the minimum peptide length is observed (KLEDHPK)
        /// Ensures semispecific search finds peptides that were cleaved correctly during the first digestion (precursor index is made and searched correctly) (KEDEEDKFDAMGNK)
        /// </summary>
        [Test]
        public static void SemiSpecificFullAndSmallMatches()
        {
            
            SearchTask searchTask = new SearchTask()
            {

                SearchParameters = new SearchParameters
                {
                    WriteMzId = true,
                    SearchType = SearchType.NonSpecific,
                    LocalFdrCategories = new List<FdrCategory>
                        {
                            FdrCategory.FullySpecific,
                            FdrCategory.SemiSpecific
                        }
                },
                CommonParameters = new CommonParameters(addCompIons: true, scoreCutoff: 11,
                    digestionParams: new DigestionParams(minPeptideLength: 7, searchModeType: CleavageSpecificity.Semi, fragmentationTerminus: FragmentationTerminus.N))
            };

            string myFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\tinySemi.mgf");
            string myDatabase = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\semiTest.fasta");
            DbForTask db = new DbForTask(myDatabase, false);

            List<(string, MetaMorpheusTask)> taskList = new List<(string, MetaMorpheusTask)> { ("TestSemiSpecificSmall", searchTask) };


            var engine = new EverythingRunnerEngine(taskList, new List<string> { myFile }, new List<DbForTask> { new DbForTask(myDatabase, false) }, Environment.CurrentDirectory);
            engine.Run();

            string outputPath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestSemiSpecificSmall\AllPSMs.psmtsv");
            var output = File.ReadAllLines(outputPath);
            Assert.That(output.Length == 3);

            var mzId = File.ReadAllLines(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestSemiSpecificSmall\tinySemi.mzID"));
            Assert.That(mzId[115].Equals("          <cvParam name=\"mzML format\" cvRef=\"PSI-MS\" accession=\"MS:1000584\" />"));
            Assert.That(mzId[118].Equals("          <cvParam name=\"mzML unique identifier\" cvRef=\"PSI-MS\" accession=\"MS:1001530\" />"));
            Assert.That(mzId[97].Equals("        <cvParam name=\"pep:FDR threshold\" value=\"0.01\" cvRef=\"PSI-MS\" accession=\"MS:1001448\" />"));

        }

        /// <summary>
        /// Ensures semispecific search runs and outputs properly
        /// </summary>
        [Test]
        public static void SemiSpecificTest()
        {
            List<FragmentationTerminus> terminiToTest = new List<FragmentationTerminus>
            {
                FragmentationTerminus.N,
                FragmentationTerminus.C
            };
            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestSemiSpecific");
            string myFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\PrunedDbSpectra.mzml");
            string myDatabase = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\DbForPrunedDb.fasta");
            foreach (FragmentationTerminus fragTerm in terminiToTest)
            {
                SearchTask searchTask = new SearchTask()
                {
                    SearchParameters = new SearchParameters
                    {
                        SearchType = SearchType.NonSpecific,
                        LocalFdrCategories = new List<FdrCategory>
                        {
                            FdrCategory.FullySpecific,
                            FdrCategory.SemiSpecific
                        }
                    },
                    CommonParameters = new CommonParameters(scoreCutoff: 4, addCompIons: true,
                    digestionParams: new DigestionParams(searchModeType: CleavageSpecificity.Semi, fragmentationTerminus: fragTerm))
                };

                List<(string, MetaMorpheusTask)> taskList = new List<(string, MetaMorpheusTask)> { ("TestSemiSpecific", searchTask) };

                var engine = new EverythingRunnerEngine(taskList, new List<string> { myFile }, new List<DbForTask> { new DbForTask(myDatabase, false) }, outputFolder);
                engine.Run();

                string outputPath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestSemiSpecific\TestSemiSpecific\AllPSMs.psmtsv");
                var output = File.ReadAllLines(outputPath);
                Assert.That(output.Length == 12); //if N is only producing 11 lines, then the c is not being searched with it. //Possibly 13 lines if decon changes because of missed mono
            }
            Directory.Delete(outputFolder, true);
        }


        /// <summary>
        /// Ensure internal fragment ions are being matched correctly and can disambiguate ambiguous proteoforms
        /// </summary>
        [Test]
        public static void InternalFragmentIonTest()
        {
            SearchTask searchTask = new SearchTask()
            {

                SearchParameters = new SearchParameters
                {
                    MinAllowedInternalFragmentLength = 1
                },
                CommonParameters = new CommonParameters(
                   digestionParams: new DigestionParams("top-down"),
                   listOfModsVariable: new List<(string, string)> {
                       ("Common Variable", "Oxidation on M"),
                       ("Common Biological", "Acetylation on K"),
                       ("Common Biological", "Acetylation on X")  })
            };

            string myFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\InternalTest.mgf");
            string myDatabase = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\InternalTest.fasta");
            DbForTask db = new DbForTask(myDatabase, false);

            List<(string, MetaMorpheusTask)> taskList = new List<(string, MetaMorpheusTask)> { ("TestInternal", searchTask) };


            var engine = new EverythingRunnerEngine(taskList, new List<string> { myFile }, new List<DbForTask> { new DbForTask(myDatabase, false) }, Environment.CurrentDirectory);
            engine.Run();

            string outputPath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestInternal\AllPSMs.psmtsv");
            //var output = File.ReadAllLines(outputPath);
            //read the psmtsv
            List<PsmFromTsv> psms = SpectrumMatchTsvReader.ReadPsmTsv(outputPath, out var warning);
            Assert.That(psms.Count == 1);
            //check that it's been disambiguated
            Assert.That(!(psms[0].FullSequence.Contains("|")));
            Assert.That(psms[0].MatchedIons.First().Intensity, Is.EqualTo(161210));
            Assert.That(psms[0].MatchedIons.First().Mz, Is.EqualTo(585.25292));
            Assert.That(psms[0].MatchedIons.First().Charge, Is.EqualTo(1));
            Assert.That(psms[0].MatchedIons[4].Intensity, Is.EqualTo(131546));
            Assert.That(psms[0].MatchedIons[4].Mz, Is.EqualTo(782.84816));
            Assert.That(psms[0].MatchedIons[4].Charge, Is.EqualTo(2));
            int numTotalFragments = psms[0].MatchedIons.Count;


            //test again but no variable acetyl on K. Make sure that internal fragments are still searched even without ambiguity
            searchTask = new SearchTask()
            {

                SearchParameters = new SearchParameters
                {
                    MinAllowedInternalFragmentLength = 1
                },
                CommonParameters = new CommonParameters(
                   digestionParams: new DigestionParams("top-down"),
                   listOfModsVariable: new List<(string, string)> {
                       ("Common Variable", "Oxidation on M"),
                       ("Common Biological", "Acetylation on X")  })
            };
            taskList = new List<(string, MetaMorpheusTask)> { ("TestInternal", searchTask) };
            engine = new EverythingRunnerEngine(taskList, new List<string> { myFile }, new List<DbForTask> { new DbForTask(myDatabase, false) }, Environment.CurrentDirectory);
            engine.Run();
            psms = SpectrumMatchTsvReader.ReadPsmTsv(outputPath, out warning);
            Assert.That(psms.Count == 1);
            Assert.That(psms[0].MatchedIons.Count == numTotalFragments);

            Directory.Delete(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestInternal"), true);
        }

        /// <summary>
        /// Tests that normalization in a search task works properly with an Experimental Design file read in,
        /// and skips quantification when that file is absent
        /// </summary>
        [Test]
        public static void PostSearchNormalizeTest()
        {
            SearchTask searchTask = new SearchTask()
            {
                SearchParameters = new SearchParameters
                {
                    Normalize = true
                },
                CommonParameters = new( precursorDeconParams:new IsoDecDeconvolutionParameters())
                {
                }
            };

            string myFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\PrunedDbSpectra.mzml");
            string myDatabase = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\DbForPrunedDb.fasta");
            string folderPath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestNormalizationExperDesign");
            string experimentalDesignFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\ExperimentalDesign.tsv");
            using (StreamWriter output = new StreamWriter(experimentalDesignFile))
            {
                output.WriteLine("FileName\tCondition\tBiorep\tFraction\tTechrep");
                output.WriteLine("PrunedDbSpectra.mzml" + "\t" + "condition" + "\t" + "1" + "\t" + "1" + "\t" + "1");
            }
            DbForTask db = new DbForTask(myDatabase, false);

            // run the task
            Directory.CreateDirectory(folderPath);
            searchTask.RunTask(folderPath, new List<DbForTask> { db }, new List<string> { myFile }, "normal");

            Directory.Delete(folderPath, true);

            // delete the exper design and try again. this should skip quantification
            File.Delete(experimentalDesignFile);

            // run the task
            Directory.CreateDirectory(folderPath);
            searchTask.RunTask(folderPath, new List<DbForTask> { db }, new List<string> { myFile }, "normal");

            // PSMs should be present but no quant output
            Assert.That(!File.Exists(Path.Combine(folderPath, "AllQuantifiedPeptides.tsv")));
            Assert.That(File.Exists(Path.Combine(folderPath, "AllPSMs.psmtsv")));

            Directory.Delete(folderPath, true);
        }

        /// <summary>
        /// Test that we don't get a crash if protein groups are not constructed
        /// </summary>
        [Test]
        public static void ProteinGroupsNoParsimonyTest()
        {
            SearchTask searchTask = new SearchTask()
            {
                SearchParameters = new SearchParameters
                {
                    DoParsimony = false
                },
            };

            string myFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\PrunedDbSpectra.mzml");
            string myDatabase = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\DbForPrunedDb.fasta");
            string folderPath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestProteinGroupsNoParsimony");

            DbForTask db = new DbForTask(myDatabase, false);
            Directory.CreateDirectory(folderPath);

            searchTask.RunTask(folderPath, new List<DbForTask> { db }, new List<string> { myFile }, "normal");
            Directory.Delete(folderPath, true);
        }

        /// <summary>
        /// Test ensures pruned databases are written when contaminant DB is searched
        /// </summary>
        [Test]
        public static void PrunedDbWithContaminantsTest()
        {
            SearchTask searchTask = new SearchTask()
            {
                SearchParameters = new SearchParameters
                {
                    WritePrunedDatabase = true
                },
            };

            string myFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\PrunedDbSpectra.mzml");
            string myDatabase = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\DbForPrunedDb.fasta");
            string folderPath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestNormalization");
            string filePath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\ExperimentalDesign.tsv");

            // contaminant DB
            DbForTask db = new DbForTask(myDatabase, true);
            Directory.CreateDirectory(folderPath);

            searchTask.RunTask(folderPath, new List<DbForTask> { db }, new List<string> { myFile }, "normal");

            Assert.That(File.ReadAllLines(Path.Combine(folderPath, @"DbForPrunedDbproteinPruned.xml")).Length > 0);
            Assert.That(File.ReadAllLines(Path.Combine(folderPath, @"DbForPrunedDbPruned.xml")).Length > 0);
            Directory.Delete(folderPath, true);
        }

        /// <summary>
        /// Test ensures peptide FDR is calculated and that it doesn't output PSM FDR results
        /// </summary>
        [Test]
        public static void PeptideFDRTest()
        {
            string myFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\PrunedDbSpectra.mzml");
            string myFile2 = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\PrunedDbSpectra2.mzml");
            if (!File.Exists(myFile2)) { File.Copy(myFile, myFile2); }
            string myDatabase = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\DbForPrunedDb.fasta");
            string folderPath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestPeptideFDR");
            DbForTask db = new DbForTask(myDatabase, true);
            Directory.CreateDirectory(folderPath);

            // search something with multiple hits of the same peptide to see if peptide FDR is calculated at the end
            new SearchTask().RunTask(folderPath, new List<DbForTask> { db }, new List<string> { myFile, myFile2 }, "normal");
            List<string> columns = null;
            int cumDecoys = 0;
            int cumTargets = 0;
            double finalQValue = 0;
            foreach (string line in File.ReadAllLines(Path.Combine(folderPath, @"AllPeptides.psmtsv")))
            {
                string[] lineline = line.Split('\t');
                if (line.StartsWith("File Name")) // header
                {
                    columns = lineline.ToList();
                }
                else
                {
                    // since each PSM has a duplicate, these counts will be 1,3,5,7, etc. if peptide FDR isn't calculated
                    // if peptide FDR is calculated, they will be 1,2,3,4, etc. as expected
                    if (lineline[columns.IndexOf("Decoy/Contaminant/Target")] == "D")
                    {
                        Assert.That(++cumDecoys, Is.EqualTo(int.Parse(lineline[columns.IndexOf("Cumulative Decoy")])));
                    }
                    else
                    {
                        Assert.That(++cumTargets, Is.EqualTo(int.Parse(lineline[columns.IndexOf("Cumulative Target")])));
                    }

                    finalQValue = Math.Max(finalQValue, (double)cumDecoys / (double)cumTargets);
                }
                
            }

            // test that the final q-value follows the (target / decoy) formula
            // intermediate q-values no longer always follow this formula, so I'm not testing them here
            Assert.That(0.5, Is.EqualTo(finalQValue).Within(.0005));
            Directory.Delete(folderPath, true);
        }

        /// <summary>
        /// Tests interval mass difference acceptor type to make sure values are assigned properly
        /// </summary>
        [Test]
        public static void IntervalMassDiffAceptorTest()
        {
            var result = new IntervalMassDiffAcceptor("-187andUp", new List<DoubleRange> { new DoubleRange(-187, double.PositiveInfinity) });
            Assert.That(result.Accepts(2.0, 2.0) == 0);
            Assert.That(result.Accepts(double.PositiveInfinity, 2.0) == 0);
            result.GetAllowedPrecursorMassIntervalsFromTheoreticalMass(2.0);
            result.GetAllowedPrecursorMassIntervalsFromObservedMass(2.0);
            result.ToString();
            result.ToProseString();
            result = new IntervalMassDiffAcceptor("-187andUp", new List<DoubleRange> { new DoubleRange(-5, 0) });
            Assert.That(result.Accepts(2.0, 4.0) == 0);
            Assert.That(result.Accepts(2.0, 10.0) == -1);
        }

        /// <summary>
        /// Tests interval mass difference acceptor type to make sure values are assigned properly
        /// </summary>
        [Test]
        public static void SingleAbsoluteAroundZeroSearchMode()
        {
            var result = new SingleAbsoluteAroundZeroSearchMode(2.0);
            Assert.That(result.Accepts(2.0, 2.0) == 0);
            result.GetAllowedPrecursorMassIntervalsFromTheoreticalMass(2.0);
            result.GetAllowedPrecursorMassIntervalsFromObservedMass(2.0);
            result.ToString();
            result.ToProseString();
        }

        [Test]
        public static void TestMzIdentMlWriterWithUniprotResId()
        {
            Protein protein = new Protein("PEPTIDE", "", databaseFilePath: "temp");

            Modification uniProtMod = GlobalVariables.AllModsKnown.First(p =>
                p.IdWithMotif == "FMN phosphoryl threonine on T"
                && p.ModificationType == "UniProt"
                && p.Target.ToString() == "T"
                && p.DatabaseReference.ContainsKey("RESID")
                && p.LocationRestriction == "Anywhere.");

            string resIdAccession = uniProtMod.DatabaseReference["RESID"].First();
            var peptide = protein.Digest(new DigestionParams(), new List<Modification> { uniProtMod }, new List<Modification>()).First();

            MsDataScan dfb = new MsDataScan(new MzSpectrum(new double[] { 1 }, new double[] { 1 }, false), 0, 1, true, Polarity.Positive, double.NaN, null,
                null, MZAnalyzerType.Orbitrap, double.NaN, null, null, "scan=1", double.NaN, null, null, double.NaN, null, DissociationType.AnyActivationType, 0, null);
            Ms2ScanWithSpecificMass scan = new Ms2ScanWithSpecificMass(dfb, 2, 0, "File", new CommonParameters());

            var psm = new PeptideSpectralMatch(peptide, 0, 1, 0, scan, new CommonParameters(), new List<MatchedFragmentIon>());
            psm.ResolveAllAmbiguities();
            psm.SetFdrValues(0, 0, 0, 0, 0, 0, 0, 0);

            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "ResIdOutput.mzID");

            MzIdentMLWriter.WriteMzIdentMl(new List<SpectralMatch> { psm }, new List<ProteinGroup>(), new List<Modification>(),
                new List<Modification>(), new List<SilacLabel>(), new List<DigestionAgent>(), new PpmTolerance(20), new PpmTolerance(20),
                0, path, true);

            var file = File.ReadAllLines(path);
            bool found = false;
            foreach (var line in file)
            {
                if (line.Contains("FMN phosphoryl threonine on T") && line.Contains("RESID:" + resIdAccession))
                {
                    found = true;
                }
            }
            Assert.That(found);

            File.Delete(path);
        }

        [Test]
        public static void TestMzIdentMlWriterWithUniprotPsiMod()
        {
            Protein protein = new Protein("PEPTIDE", "", databaseFilePath: "temp");

            ModificationMotif.TryGetMotif("T", out var motif);

            Modification fakeMod = new Modification(_originalId: "FAKE", _accession: "FAKE_MOD_ACCESSION", _modificationType: "fake",
                _target: motif, _locationRestriction: "Anywhere.", _monoisotopicMass: 0,
                _databaseReference: new Dictionary<string, IList<string>> { { "PSI-MOD", new List<string> { "FAKE_MOD_ACCESSION" } } });

            string resIdAccession = fakeMod.DatabaseReference["PSI-MOD"].First();
            var peptide = protein.Digest(new DigestionParams(), new List<Modification> { fakeMod }, new List<Modification>()).First();

            MsDataScan dfb = new MsDataScan(new MzSpectrum(new double[] { 1 }, new double[] { 1 }, false), 0, 1, true, Polarity.Positive, double.NaN, null,
                null, MZAnalyzerType.Orbitrap, double.NaN, null, null, "scan=1", double.NaN, null, null, double.NaN, null, DissociationType.AnyActivationType, 0, null);
            Ms2ScanWithSpecificMass scan = new Ms2ScanWithSpecificMass(dfb, 2, 0, "File", new CommonParameters());

            var psm = new PeptideSpectralMatch(peptide, 0, 1, 0, scan, new CommonParameters(), new List<MatchedFragmentIon>());
            psm.ResolveAllAmbiguities();
            psm.SetFdrValues(0, 0, 0, 0, 0, 0, 0, 0);

            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "ResIdOutput.mzID");

            MzIdentMLWriter.WriteMzIdentMl(new List<SpectralMatch> { psm }, new List<ProteinGroup>(), new List<Modification>(),
                new List<Modification>(), new List<SilacLabel>(), new List<DigestionAgent>(), new PpmTolerance(20), new PpmTolerance(20),
                0, path, true);

            var file = File.ReadAllLines(path);
            bool found = false;
            foreach (var line in file)
            {
                if (line.Contains("FAKE on T") && line.Contains("PSI-MOD:" + resIdAccession))
                {
                    found = true;
                }
            }
            Assert.That(found);

            // test again w/ NOT appending motifs onto mod names
            MzIdentMLWriter.WriteMzIdentMl(new List<SpectralMatch> { psm }, new List<ProteinGroup>(), new List<Modification>(),
                new List<Modification>(), new List<SilacLabel>(), new List<DigestionAgent>(), new PpmTolerance(20), new PpmTolerance(20),
                0, path, false);

            file = File.ReadAllLines(path);
            found = false;
            foreach (var line in file)
            {
                if (line.Contains("FAKE") && !line.Contains(" on ") && line.Contains("PSI-MOD:" + resIdAccession))
                {
                    found = true;
                }
            }
            Assert.That(found);

            File.Delete(path);
        }

        /// <summary>
        /// 
        /// </summary>
        [Test]
        public static void TestAutodetectDissocationTypeFromScanHeader()
        {
            SearchTask searchTask = new SearchTask()
            {
                SearchParameters = new SearchParameters
                {
                    DoLabelFreeQuantification = false // quant disabled just to save some time
                },

                // use DissociationType.Autodetect as the dissociation type. this signals to the search that the dissociation type
                // should be taken from the scan header on a scan-specific basis
                CommonParameters = new CommonParameters(dissociationType: DissociationType.Autodetect, pepQValueThreshold: 0.01, qValueThreshold: 1.0)
            };

            string myFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\SmallCalibratible_Yeast.mzML");
            string myDatabase = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\smalldb.fasta");
            string folderPath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestAutodetectDissocationTypeFromScanHeader");

            DbForTask db = new DbForTask(myDatabase, false);

            // run the task
            var autoTaskFolder = Path.Combine(folderPath, @"Autodetect");
            Directory.CreateDirectory(autoTaskFolder);
            searchTask.RunTask(autoTaskFolder, new List<DbForTask> { db }, new List<string> { myFile }, "");

            // run identical task but select the HCD dissociation type instead of autodetect
            var hcdTaskFolder = Path.Combine(folderPath, @"HCD");
            Directory.CreateDirectory(hcdTaskFolder);
            searchTask = new SearchTask()
            {
                SearchParameters = new SearchParameters
                {
                    DoLabelFreeQuantification = false
                },

                CommonParameters = new CommonParameters(dissociationType: DissociationType.HCD)
            };

            searchTask.RunTask(hcdTaskFolder, new List<DbForTask> { db }, new List<string> { myFile }, "");

            // check search results
            var psmFileAutodetect = File.ReadAllLines(Path.Combine(autoTaskFolder, "AllPSMs.psmtsv"));
            var psmFileHcd = File.ReadAllLines(Path.Combine(hcdTaskFolder, "AllPSMs.psmtsv"));

            Assert.That(psmFileAutodetect.Length == psmFileHcd.Length);

            for (int i = 0; i < psmFileAutodetect.Length; i++)
            {
                Assert.That(psmFileAutodetect[i].Equals(psmFileHcd[i]));
            }

            // clean up
            Directory.Delete(folderPath, true);
        }

        [Test]
        public static void TestPepFilteringFewerThan100Psms()
        {
            SearchTask searchTask = new SearchTask()
            {
                SearchParameters = new SearchParameters
                {
                    DoLabelFreeQuantification = false // quant disabled just to save some time
                },

                // Use pepQvalue filtering with fewer than 100 psms
                CommonParameters = new CommonParameters(dissociationType: DissociationType.HCD, 
                    pepQValueThreshold: 0.02, qValueThreshold: 1.0)
            };

            string myFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\SmallCalibratible_Yeast.mzML");
            string myDatabase = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\smalldb.fasta");
            string folderPath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestPepFiltering");

            DbForTask db = new DbForTask(myDatabase, false);

            // run the task
            var pepTaskFolder = Path.Combine(folderPath, @"pepTest");
            Directory.CreateDirectory(pepTaskFolder);
            searchTask.RunTask(pepTaskFolder, new List<DbForTask> { db }, 
                new List<string> { myFile }, "");

            string resultsFile = Path.Combine(pepTaskFolder, "results.txt");
            string[] results = File.ReadAllLines(resultsFile);
            Assert.That(results[6], Is.EqualTo("PEP could not be calculated due to an insufficient number of PSMs. Results were filtered by q-value."));
            Assert.That(results[7], Is.EqualTo("All target PSMs with q-value <= 1: 84"));

            // clean up
            Directory.Delete(folderPath, true);
        }
    }
}