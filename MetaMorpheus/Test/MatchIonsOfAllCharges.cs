﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using EngineLayer;
using EngineLayer.ClassicSearch;
using MzLibUtil;
using NUnit.Framework;
using Proteomics;
using Omics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using TaskLayer;
using Chemistry;
using System;
using MassSpectrometry;
using Nett;
using NUnit.Framework.Legacy;
using Omics.Digestion;
using Omics.Modifications;
using Omics.SpectrumMatch;
using Readers;
using Mzml = IO.MzML.Mzml;

namespace Test
{
    [TestFixture]
    public class SearchEngineForWritingSpectralLibraryTest
    {
        [Test]
        public static void TestMatchIonsOfAllChargesBottomUp()
        {
            CommonParameters CommonParameters = new CommonParameters();

            MetaMorpheusEngine.DetermineAnalyteType(CommonParameters);

            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();

            var proteinList = new List<Protein>
            {
                new Protein("AAAHSSLK", ""),new Protein("RQPAQPR", ""),new Protein("EKAEAEAEK", "")
            };
            var myMsDataFile = Mzml.LoadAllStaticData(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\SmallCalibratible_Yeast.mzML"));


            var searchMode = new SinglePpmAroundZeroSearchMode(5);

            Tolerance DeconvolutionMassTolerance = new PpmTolerance(5);

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();

            //search by new method of looking for all charges 
            SpectralMatch[] allPsmsArray = new PeptideSpectralMatch[listOfSortedms2Scans.Length];

            new ClassicSearchEngine(allPsmsArray, listOfSortedms2Scans, variableModifications, fixedModifications, null, null, null,
                proteinList, searchMode, CommonParameters, null, null, new List<string>(), true).Run();
            var psm = allPsmsArray.Where(p => p != null).ToList();
            Assert.That(psm[1].MatchedFragmentIons.Count == 14);
            //there are ions with same product type and same fragment number but different charges 
            Assert.That(psm[1].MatchedFragmentIons[8].NeutralTheoreticalProduct.ProductType == psm[1].MatchedFragmentIons[9].NeutralTheoreticalProduct.ProductType &&
                psm[1].MatchedFragmentIons[8].NeutralTheoreticalProduct.FragmentNumber == psm[1].MatchedFragmentIons[9].NeutralTheoreticalProduct.FragmentNumber &&
                psm[1].MatchedFragmentIons[8].Charge != psm[1].MatchedFragmentIons[9].Charge);
            Assert.That(psm[2].MatchedFragmentIons.Count == 14);
            Assert.That(psm[4].MatchedFragmentIons.Count == 16);

            //search by old method of looking for only one charge 
            SpectralMatch[] allPsmsArray_oneCharge = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            new ClassicSearchEngine(allPsmsArray_oneCharge, listOfSortedms2Scans, variableModifications, fixedModifications, null, null, null,
                proteinList, searchMode, CommonParameters, null, null, new List<string>(), false).Run();
            var psm_oneCharge = allPsmsArray_oneCharge.Where(p => p != null).ToList();

            //compare 2 scores , they should have same integer part but new search has a little higher score than old search
            Assert.That(psm[1].Score > psm_oneCharge[1].Score);
            Assert.That(Math.Truncate(psm[1].Score), Is.EqualTo(12));
            Assert.That(Math.Truncate(psm_oneCharge[1].Score), Is.EqualTo(12));

            //compare 2 results and evaluate the different matched ions
            var peptideTheorProducts = new List<Product>();
            Assert.That(psm_oneCharge[1].MatchedFragmentIons.Count == 12);
            var differences = psm[1].MatchedFragmentIons.Except(psm_oneCharge[1].MatchedFragmentIons);
            psm[1].BestMatchingBioPolymersWithSetMods.First().SpecificBioPolymer.Fragment(CommonParameters.DissociationType, CommonParameters.DigestionParams.FragmentationTerminus, peptideTheorProducts);
            foreach (var ion in differences)
            {
                foreach (var product in peptideTheorProducts)
                {
                    if (product.Annotation.ToString().Equals(ion.NeutralTheoreticalProduct.Annotation.ToString()))
                    {
                        //to see if the different matched ions are qualified
                        Assert.That(CommonParameters.ProductMassTolerance.Within(ion.Mz.ToMass(ion.Charge), product.NeutralMass));
                    }
                }
            }

            //test specific condition: unknown fragment mass; this only happens rarely for sequences with unknown amino acids
            var myMsDataFile1 = new TestDataFile();
            var variableModifications1 = new List<Modification>();
            var fixedModifications1 = new List<Modification>();
            var proteinList1 = new List<Protein> { new Protein("QXQ", null) };
            var productMassTolerance = new AbsoluteTolerance(0.01);
            var searchModes = new OpenSearchMode();

            Tolerance DeconvolutionMassTolerance1 = new PpmTolerance(5);

            var listOfSortedms2Scans1 = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();

            List<DigestionMotif> motifs = new List<DigestionMotif> { new DigestionMotif("K", null, 1, null) };
            Protease protease = new Protease("Custom Protease3", CleavageSpecificity.Full, null, null, motifs);
            ProteaseDictionary.Dictionary.Add(protease.Name, protease);

            CommonParameters CommonParameters1 = new CommonParameters(
                digestionParams: new DigestionParams(protease: protease.Name, maxMissedCleavages: 0, minPeptideLength: 1),
                scoreCutoff: 1,
                addCompIons: false);
            var fsp = new List<(string fileName, CommonParameters fileSpecificParameters)>();
            fsp.Add(("", CommonParameters));
            SpectralMatch[] allPsmsArray1 = new PeptideSpectralMatch[listOfSortedms2Scans.Length];

            bool writeSpectralLibrary = true;
            new ClassicSearchEngine(allPsmsArray1, listOfSortedms2Scans1, variableModifications1, fixedModifications1, null, null, null,
                proteinList1, searchModes, CommonParameters1, fsp, null, new List<string>(), writeSpectralLibrary).Run();

            var psm1 = allPsmsArray1.Where(p => p != null).ToList();
            Assert.That(psm1.Count, Is.EqualTo(222));
        }

        [Test]
        public static void TestMatchIonsOfAllChargesTopDown()
        {
            CommonParameters CommonParameters = new CommonParameters(
               digestionParams: new DigestionParams(protease: "top-down"),
               scoreCutoff: 1,
               assumeOrphanPeaksAreZ1Fragments: false);

            MetaMorpheusEngine.DetermineAnalyteType(CommonParameters);

            // test output file name (should be proteoform and not peptide)
            Assert.That(GlobalVariables.AnalyteType.ToString() == "Proteoform");

            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();
            var proteinList = new List<Protein>
            {
                new Protein("MPKVYSYQEVAEHNGPENFWIIIDDKVYDVSQFKDEHPGGDEIIMDLGGQDATESFVDIGHSDEALRLLKGLYIGDVDKTSERVSVEKVSTSENQSKGSGTLVVILAILMLGVAYYLLNE", "P40312")
            };

            var myMsDataFile = Mzml.LoadAllStaticData(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TopDownTestData\slicedTDYeast.mzML"));

            var searchMode = new SinglePpmAroundZeroSearchMode(5);

            Tolerance DeconvolutionMassTolerance = new PpmTolerance(5);

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();

            //search by new method of looking for all charges 
            SpectralMatch[] allPsmsArray = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            new ClassicSearchEngine(allPsmsArray, listOfSortedms2Scans, variableModifications, fixedModifications, null, null, null,
                proteinList, searchMode, CommonParameters, null, null, new List<string>(), true).Run();

            var psm = allPsmsArray.Where(p => p != null).FirstOrDefault();
            Assert.That(psm.MatchedFragmentIons.Count == 62);


            //search by old method of looking for only one charge 
            SpectralMatch[] allPsmsArray_oneCharge = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            new ClassicSearchEngine(allPsmsArray_oneCharge, listOfSortedms2Scans, variableModifications, fixedModifications, null, null, null,
                proteinList, searchMode, CommonParameters, null, null, new List<string>(), false).Run();

            var psm_oneCharge = allPsmsArray_oneCharge.Where(p => p != null).FirstOrDefault();
            Assert.That(psm_oneCharge.MatchedFragmentIons.Count == 47);

            //compare 2 scores , they should have same integer but new search has a little higher score than old search
            Assert.That(psm.Score > psm_oneCharge.Score);
            Assert.That(Math.Truncate(psm.Score), Is.EqualTo(47));
            Assert.That(Math.Truncate(psm_oneCharge.Score), Is.EqualTo(47));

            //compare 2 results and evaluate the different matched ions
            var peptideTheorProducts = new List<Product>();
            var differences = psm.MatchedFragmentIons.Except(psm_oneCharge.MatchedFragmentIons);
            psm.BestMatchingBioPolymersWithSetMods.First().SpecificBioPolymer.Fragment(CommonParameters.DissociationType, CommonParameters.DigestionParams.FragmentationTerminus, peptideTheorProducts);
            foreach (var ion in differences)
            {
                foreach (var product in peptideTheorProducts)
                {
                    if (product.Annotation.ToString().Equals(ion.NeutralTheoreticalProduct.Annotation.ToString()))
                    {
                        //to see if the different matched ions are qualified
                        Assert.That(CommonParameters.ProductMassTolerance.Within(ion.Mz.ToMass(ion.Charge), product.NeutralMass));
                    }
                }
            }
        }

        [Test]
        public static void TestLookingForAcceptableIsotopicEnvelopes()
        {
            CommonParameters CommonParameters = new CommonParameters();

            MetaMorpheusEngine.DetermineAnalyteType(CommonParameters);

            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();

            var proteinList = new List<Protein>
            {
                new Protein("AAAHSSLK", ""),new Protein("RQPAQPR", ""),new Protein("EKAEAEAEK", "")
            };
            var myMsDataFile = Mzml.LoadAllStaticData(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\SmallCalibratible_Yeast.mzML"));


            var searchMode = new SinglePpmAroundZeroSearchMode(5);

            Tolerance DeconvolutionMassTolerance = new PpmTolerance(5);

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();

            var ms2ScanTest = listOfSortedms2Scans[0];

            //test when all the masses are not in the given range

            //test1 when all the masses are too small
            var test1 = ms2ScanTest.GetClosestExperimentalIsotopicEnvelopeList(50, 95);
            Assert.That(test1, Is.EqualTo(null));
            //test2 when all the masses are too big
            var test2 = ms2ScanTest.GetClosestExperimentalIsotopicEnvelopeList(582, 682);
            Assert.That(test2, Is.EqualTo(null));
            //test3 when the mass which is bigger than given min mass is bigger than the mass which is smaller than the given max mass
            //for example: the mass array is [1,2,3,4,5], the given min mass is 2.2, the given max mass is 2.8
            var test3 = ms2ScanTest.GetClosestExperimentalIsotopicEnvelopeList(110, 111);
            Assert.That(test3, Is.EqualTo(null));

            //test normal conditions:look for IsotopicEnvelopes which are in the range of acceptable mass 
            var test4 = ms2ScanTest.GetClosestExperimentalIsotopicEnvelopeList(120, 130);
            IsotopicEnvelope[] expected4 = ms2ScanTest.ExperimentalFragments.Skip(15).Take(9).ToArray();
            Assert.That(ms2ScanTest.ExperimentalFragments[15].MonoisotopicMass > 120 && ms2ScanTest.ExperimentalFragments[14].MonoisotopicMass < 120);
            Assert.That(ms2ScanTest.ExperimentalFragments[23].MonoisotopicMass < 130 && ms2ScanTest.ExperimentalFragments[24].MonoisotopicMass > 130);
            Assert.That(test4, Is.EqualTo(expected4));

            var test5 = ms2ScanTest.GetClosestExperimentalIsotopicEnvelopeList(400, 500);
            IsotopicEnvelope[] expected5 = ms2ScanTest.ExperimentalFragments.Skip(150).Take(7).ToArray();
            Assert.That(ms2ScanTest.ExperimentalFragments[150].MonoisotopicMass > 400 && ms2ScanTest.ExperimentalFragments[149].MonoisotopicMass < 400);
            Assert.That(ms2ScanTest.ExperimentalFragments[156].MonoisotopicMass < 500 && ms2ScanTest.ExperimentalFragments[157].MonoisotopicMass > 500);
            Assert.That(test5, Is.EqualTo(expected5));
        }

        [Test]
        public static void TestReverseDecoyGenerationDuringSearch()
        {
            CommonParameters CommonParameters = new CommonParameters();

            MetaMorpheusEngine.DetermineAnalyteType(CommonParameters);

            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();

            var proteinList = new List<Protein>
            {
                new Protein ("KKAEDGINK",""),new Protein("AVNSISLK", ""),new Protein("EKAEAEAEK", ""), new Protein("DITANLR",""), new Protein("QNAIGTAK",""),
                new Protein("FHKSQLNK",""),new Protein ("KQVAQWNK",""),new Protein ("NTRIEELK",""),new Protein("RQPAQPR", ""),
            };
            var myMsDataFile = Mzml.LoadAllStaticData(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\SmallCalibratible_Yeast.mzML"));


            var searchMode = new SinglePpmAroundZeroSearchMode(5);

            Tolerance DeconvolutionMassTolerance = new PpmTolerance(5);

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();


            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\myPrositLib.msp");

            var testLibrary = new SpectralLibrary(new List<string> { path });



            //test when doing spectral library search without generating library
            SpectralMatch[] allPsmsArray1 = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            new ClassicSearchEngine(allPsmsArray1, listOfSortedms2Scans, variableModifications, fixedModifications, null, null, null,
                proteinList, searchMode, CommonParameters, null, testLibrary, new List<string>(), false).Run();
            var psm1 = allPsmsArray1.Where(p => p != null).ToList();
            Assert.That(psm1[0].IsDecoy == false && psm1[0].FullSequence == "DITANLR");
            Assert.That(psm1[1].IsDecoy == true && psm1[1].FullSequence == "LSISNVAK");
            Assert.That(psm1[2].IsDecoy == true && psm1[2].FullSequence == "LSISNVAK");
            Assert.That(psm1[3].IsDecoy == false && psm1[3].FullSequence == "RQPAQPR");
            Assert.That(psm1[4].IsDecoy == false && psm1[4].FullSequence == "KKAEDGINK");
            Assert.That(psm1[5].IsDecoy == false && psm1[5].FullSequence == "EKAEAEAEK");
            Assert.That(psm1[6].IsDecoy == false && psm1[6].FullSequence == "EKAEAEAEK");


            proteinList.Add(new Protein("LSISNVAK", "", isDecoy: true));
            //test when doing spectral library search with generating library; non spectral search won't generate decoy by "decoy on the fly" , so proteinlist used by non spectral library search would contain decoys
            SpectralMatch[] allPsmsArray2 = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            new ClassicSearchEngine(allPsmsArray2, listOfSortedms2Scans, variableModifications, fixedModifications, null, null, null,
                proteinList, searchMode, CommonParameters, null, testLibrary, new List<string>(), true).Run();
            var psm2 = allPsmsArray2.Where(p => p != null).ToList();
            Assert.That(psm2[0].IsDecoy == false && psm2[0].FullSequence == "DITANLR");
            Assert.That(psm2[1].IsDecoy == true && psm2[1].FullSequence == "LSISNVAK");
            Assert.That(psm2[2].IsDecoy == true && psm2[2].FullSequence == "LSISNVAK");
            Assert.That(psm2[3].IsDecoy == false && psm2[3].FullSequence == "RQPAQPR");
            Assert.That(psm2[4].IsDecoy == false && psm2[4].FullSequence == "KKAEDGINK");
            Assert.That(psm2[5].IsDecoy == false && psm2[5].FullSequence == "EKAEAEAEK");
            Assert.That(psm2[6].IsDecoy == false && psm2[6].FullSequence == "EKAEAEAEK");

            //test when doing non spectral library search without generating library
            SpectralMatch[] allPsmsArray3 = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            new ClassicSearchEngine(allPsmsArray3, listOfSortedms2Scans, variableModifications, fixedModifications, null, null, null,
                proteinList, searchMode, CommonParameters, null, null, new List<string>(), false).Run();
            var psm3 = allPsmsArray3.Where(p => p != null).ToList();
            Assert.That(psm3[0].IsDecoy == false && psm3[0].FullSequence == "DITANLR");
            Assert.That(psm3[1].IsDecoy == true && psm3[1].FullSequence == "LSISNVAK");
            Assert.That(psm3[2].IsDecoy == true && psm3[2].FullSequence == "LSISNVAK");
            Assert.That(psm3[3].IsDecoy == false && psm3[3].FullSequence == "RQPAQPR");
            Assert.That(psm3[4].IsDecoy == false && psm3[4].FullSequence == "KKAEDGINK");
            Assert.That(psm3[5].IsDecoy == false && psm3[5].FullSequence == "EKAEAEAEK");
            Assert.That(psm3[6].IsDecoy == false && psm3[6].FullSequence == "EKAEAEAEK");


            //test when doing non spectral library search with generating library
            SpectralMatch[] allPsmsArray4 = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            new ClassicSearchEngine(allPsmsArray4, listOfSortedms2Scans, variableModifications, fixedModifications, null, null, null,
                proteinList, searchMode, CommonParameters, null, null, new List<string>(), true).Run();
            var psm4 = allPsmsArray4.Where(p => p != null).ToList();
            Assert.That(psm4[0].IsDecoy == false && psm4[0].FullSequence == "DITANLR");
            Assert.That(psm4[1].IsDecoy == true && psm4[1].FullSequence == "LSISNVAK");
            Assert.That(psm4[2].IsDecoy == true && psm4[2].FullSequence == "LSISNVAK");
            Assert.That(psm4[3].IsDecoy == false && psm4[3].FullSequence == "RQPAQPR");
            Assert.That(psm4[4].IsDecoy == false && psm4[4].FullSequence == "KKAEDGINK");
            Assert.That(psm4[5].IsDecoy == false && psm4[5].FullSequence == "EKAEAEAEK");
            Assert.That(psm4[6].IsDecoy == false && psm4[6].FullSequence == "EKAEAEAEK");


            //compare psm's target/decoy results in 4 conditions. they should be same as new decoy methods shouldn't change the t/d results
            for (int i = 0; i < psm1.Count; i++)
            {
                Assert.That(psm1[i].FullSequence == psm2[i].FullSequence && psm3[i].FullSequence == psm3[i].FullSequence && psm2[i].FullSequence == psm3[i].FullSequence);
                Assert.That(psm1[i].IsDecoy == psm2[i].IsDecoy && psm3[i].IsDecoy == psm3[i].IsDecoy && psm2[i].IsDecoy == psm3[i].IsDecoy);
            }

            //compare MetaMorpheus scores in 4 conditions; for some psms, they should have a little higher score when "generating library" as they switch to all charges ions matching function
            for (int j = 0; j < psm1.Count; j++)
            {
                if (psm1[j].FullSequence == psm2[j].FullSequence && psm1[j].MatchedFragmentIons.Count != psm2[j].MatchedFragmentIons.Count)
                {
                    Assert.That(psm1[j].Score < psm2[j].Score);
                }
            }
        }
      
        [Test]
        public static void TestLibraryGeneration()
        {
            string thisTaskOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\SpectralLibrarySearch\FileOutput");
            if(Directory.Exists(thisTaskOutputFolder))
                Directory.Delete(thisTaskOutputFolder, true);
            Directory.CreateDirectory(thisTaskOutputFolder);

            SearchTask task = Toml.ReadFile<SearchTask>(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\SpectralLibrarySearch\SpectralSearchTask.toml"), MetaMorpheusTask.tomlConfig);
            task.SearchParameters.WriteMzId = true;
            task.SearchParameters.WriteSpectralLibrary = true;

            DbForTask db1 = new(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TaGe_SA_A549_3_snip.fasta"),false);
            DbForTask db2 = new(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\hela_snip_for_unitTest.fasta"), false);

            string raw1 = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TaGe_SA_A549_3_snip.mzML");
            string raw2 = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TaGe_SA_HeLa_04_subset_longestSeq.mzML");
            
            EverythingRunnerEngine MassSpectraFile = new(new List<(string, MetaMorpheusTask)> { ("SpectraFileOutput", task) }, new List<string> { raw1, raw2 }, new List<DbForTask> { db1,db2 }, thisTaskOutputFolder);

            MassSpectraFile.Run();
            var list = Directory.GetFiles(thisTaskOutputFolder, "*.*", SearchOption.AllDirectories);
            string matchingvalue = list.First(p => Path.GetFileName(p).Contains("SpectralLibrary")).ToString();
            var lib = new SpectralLibrary(new List<string> { Path.Combine(thisTaskOutputFolder, matchingvalue) });
            var libPath = Path.Combine(thisTaskOutputFolder, matchingvalue);
           
            string testDir = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\SpectralLibraryGeneration");
            string outputDir = Path.Combine(testDir, @"SpectralLibraryTest");

            if(Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
            Directory.CreateDirectory(outputDir);

            SearchTask searchTask = new();

            List<(string, MetaMorpheusTask)> taskList = new List<(string, MetaMorpheusTask)> { ("ClassicSearch", searchTask) };

            var engine = new EverythingRunnerEngine(taskList, new List<string> { raw1,raw2 }, new List<DbForTask> { db1,db2,new DbForTask(libPath, false) }, outputDir);
            engine.Run();
            var test11 = Path.Combine(outputDir, @"ClassicSearch\AllPSMs.psmtsv");
            string[] results = System.IO.File.ReadAllLines(test11);
            string[] split = results[0].Split('\t');
            int ind = Array.IndexOf(split, "Normalized Spectral Angle");
            int indOfTarget = Array.IndexOf(split, "Decoy/Contaminant/Target");
            Assert.That(ind >= 0);
            List<double> spectralAngleList = new();
            for (int i = 1; i < results.Length; i++)
            {
                double spectralAngle = double.Parse(results[i].Split('\t')[ind]);
                string targetOrDecoy = results[i].Split('\t')[indOfTarget].ToString();

                if (targetOrDecoy.Equals("T") && spectralAngle >= 0)
                {
                    spectralAngleList.Add(spectralAngle);
                }
            }
            Assert.That(spectralAngleList.Average() > 0.9);
            lib.CloseConnections();
            Directory.Delete(thisTaskOutputFolder, true);
        }

        [Test]

        public static void TestLibraryUpdate()
        {
            string thisTaskOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\SpectralLibrarySearch\UpdateLibrary");
            _ = Directory.CreateDirectory(thisTaskOutputFolder);
            SearchTask task = Toml.ReadFile<SearchTask>(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\SpectralLibrarySearch\SpectralSearchTask.toml"), MetaMorpheusTask.tomlConfig);

            //update library
            task.SearchParameters.UpdateSpectralLibrary = true;
            task.SearchParameters.MassDiffAcceptorType = MassDiffAcceptorType.Exact;

            string db1 = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\hela_snip_for_unitTest.fasta");
            string db2 = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TaGe_SA_A549_3_snip.fasta");
            string raw1 = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TaGe_SA_HeLa_04_subset_longestSeq.mzML");
            string raw2 = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TaGe_SA_A549_3_snip.mzML");
            string lib = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\SpectralLibrarySearch\SpectralLibrary.msp");
            string rawCopy = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\SpectralLibrarySearch\UpdateLibrary\rawCopy.mzML");
            System.IO.File.Copy(raw1, rawCopy);
            EverythingRunnerEngine UpdateLibrary = new(new List<(string, MetaMorpheusTask)> { ("UpdateSpectraFileOutput", task) }, new List<string> { raw1, raw2 }, new List<DbForTask> { new DbForTask(lib, false), new DbForTask( db1,false), new DbForTask(db2, false) }, thisTaskOutputFolder);

            UpdateLibrary.Run();

            System.IO.File.Delete(rawCopy);
            var list = Directory.GetFiles(thisTaskOutputFolder, "*.*", SearchOption.AllDirectories);
            string matchingvalue = list.Where(p => p.Contains("updateSpectralLibrary")).First().ToString();
            var updatedLib = new SpectralLibrary(new List<string> { Path.Combine(thisTaskOutputFolder, matchingvalue) });
            var oldLib = new SpectralLibrary(new List<string> { lib });

            //get the spectra from original library and the update library
            Assert.That(oldLib.TryGetSpectrum("IEFEGQPVDFVDPNKQNLIAEVSTK", 4, out var old_spectrum1));
            Assert.That(updatedLib.TryGetSpectrum("IEFEGQPVDFVDPNKQNLIAEVSTK", 4, out var new_spectrum1));
            Assert.That(oldLib.TryGetSpectrum("AIAELGIYPAVDPLDSTSR", 3, out var old_spectrum2));
            Assert.That(updatedLib.TryGetSpectrum("AIAELGIYPAVDPLDSTSR", 3, out var new_spectrum2));
            Assert.That(oldLib.TryGetSpectrum("TTQVTQFILDNYIER", 3, out var old_spectrum3));
            Assert.That(updatedLib.TryGetSpectrum("TTQVTQFILDNYIER", 3, out var new_spectrum3));

            //test if the updated spectra are better than old spectra
            Assert.That(old_spectrum1.MatchedFragmentIons.Count < new_spectrum1.MatchedFragmentIons.Count);
            Assert.That(old_spectrum2.MatchedFragmentIons.Count < new_spectrum2.MatchedFragmentIons.Count);
            Assert.That(old_spectrum3.MatchedFragmentIons.Count < new_spectrum3.MatchedFragmentIons.Count);
            Assert.That(oldLib.GetAllLibrarySpectra().ToList().Count < updatedLib.GetAllLibrarySpectra().ToList().Count);

            updatedLib.CloseConnections();
            Directory.Delete(thisTaskOutputFolder, true);
        }

        [Test]
        public static void TestDecoyLibrarySpectraGenerationFunction()
        {
            Product a = new Product(ProductType.b, FragmentationTerminus.N, 1, 1, 1, 0);
            Product b = new Product(ProductType.b, FragmentationTerminus.N, 2, 2, 1, 0);
            Product c = new Product(ProductType.b, FragmentationTerminus.N, 3, 3, 1, 0);
            Product d = new Product(ProductType.b, FragmentationTerminus.N, 4, 4, 1, 0);
            var decoyPeptideTheorProducts = new List<Product> { a, b, c, d };
            MatchedFragmentIon aa = new MatchedFragmentIon(a, 1, 1, 1);
            MatchedFragmentIon bb = new MatchedFragmentIon(b, 2, 2, 1);
            MatchedFragmentIon cc = new MatchedFragmentIon(c, 3, 3, 1);
            MatchedFragmentIon dd = new MatchedFragmentIon(d, 4, 4, 1);
            var peaks = new List<MatchedFragmentIon> { aa, bb, cc, dd };
            var librarySpectrum = new LibrarySpectrum("library", 0, 0, peaks, 0);
            var decoySpectum = SpectralLibrarySearchFunction.GetDecoyLibrarySpectrumFromTargetByReverse(librarySpectrum, decoyPeptideTheorProducts);
            Assert.That(decoySpectum[0].NeutralTheoreticalProduct.ProductType == ProductType.b && decoySpectum[0].NeutralTheoreticalProduct.FragmentNumber == 1 && decoySpectum[0].Intensity == 1);
            Assert.That(decoySpectum[1].NeutralTheoreticalProduct.ProductType == ProductType.b && decoySpectum[1].NeutralTheoreticalProduct.FragmentNumber == 2 && decoySpectum[1].Intensity == 2);
            Assert.That(decoySpectum[2].NeutralTheoreticalProduct.ProductType == ProductType.b && decoySpectum[2].NeutralTheoreticalProduct.FragmentNumber == 3 && decoySpectum[2].Intensity == 3);
            Assert.That(decoySpectum[3].NeutralTheoreticalProduct.ProductType == ProductType.b && decoySpectum[3].NeutralTheoreticalProduct.FragmentNumber == 4 && decoySpectum[3].Intensity == 4);
        }

        [Test]

        public static void TestLibraryExistAfterGPTMDsearch()
        {
            string thisTaskOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\SpectralLibrarySearch\UpdateLibrary");
            _ = Directory.CreateDirectory(thisTaskOutputFolder);
            SearchTask task = Toml.ReadFile<SearchTask>(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\SpectralLibrarySearch\SpectralSearchTask.toml"), MetaMorpheusTask.tomlConfig);

            string db = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\hela_snip_for_unitTest.fasta");
            string raw = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TaGe_SA_HeLa_04_subset_longestSeq.mzML");
            string lib = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\SpectralLibrarySearch\SpectralLibrary.msp");
            
            GptmdTask GptmdTask = new();
            MyTaskResults afterGPTMD = GptmdTask.RunTask(thisTaskOutputFolder, new List<DbForTask> { new DbForTask(db, false), new DbForTask(lib, false) }, new List<string> { raw }, "test");
            Assert.That(afterGPTMD.NewDatabases.Count > 0);
            Assert.That(afterGPTMD.NewDatabases.Select(p => p.IsSpectralLibrary == true).ToList().Count() > 0);

            Directory.Delete(thisTaskOutputFolder, true);
        }

        [Test]
        public static void TestLibrarySpectrumCalculateSpectralAngleOnTheFly()
        {

            var librarySpectrumPath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\SpectralLibrarySearch\SLSNVIAHEISHSWTGNLVTNK.msp");
            var testLibrary = new SpectralLibrary(new List<string> { librarySpectrumPath });
            testLibrary.TryGetSpectrum("SLSNVIAHEISHSWTGNLVTNK", 3, out var spectrum);


            string psmsPath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\SpectralLibrarySearch\SLSNVIAHEISHSWTGNLVTNK.psmtsv");
            List<PsmFromTsv> psms = SpectrumMatchTsvReader.ReadPsmTsv(psmsPath, out List<string> warnings).Where(p => p.AmbiguityLevel == "1").ToList();

            CollectionAssert.AreEqual(psms[0].MatchedIons.Select(p => (p.NeutralTheoreticalProduct.ProductType, p.NeutralTheoreticalProduct.FragmentNumber))
                    .OrderBy(p => p.Item1).ThenBy(p => p.Item2),
                spectrum.MatchedFragmentIons.Select(p => (p.NeutralTheoreticalProduct.ProductType, p.NeutralTheoreticalProduct.FragmentNumber))
                    .OrderBy(p => p.Item1).ThenBy(p => p.Item2));

            var computedSpectralSimilarity = spectrum.CalculateSpectralAngleOnTheFly(psms[0].MatchedIons);

            Assert.That(Convert.ToDouble(computedSpectralSimilarity), Is.EqualTo(1).Within(0.01));
            Assert.That(spectrum.CalculateSpectralAngleOnTheFly(new List<MatchedFragmentIon>()), Is.EqualTo("N/A"));
        }


    }
}
