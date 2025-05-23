﻿using Chemistry;
using EngineLayer;
using EngineLayer.ClassicSearch;
using EngineLayer.Indexing;
using EngineLayer.ModernSearch;
using EngineLayer.NonSpecificEnzymeSearch;
using MassSpectrometry;
using MzLibUtil;
using Nett;
using NUnit.Framework;
using Proteomics;
using Proteomics.AminoAcidPolymer;
using Omics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Omics;
using Omics.Digestion;
using Omics.Modifications;
using Readers;
using TaskLayer;
using UsefulProteomicsDatabases;
using static Nett.TomlObjectFactory;
using EngineLayer.FdrAnalysis;
using Omics.BioPolymer;

namespace Test
{
    [TestFixture]
    public static class SearchEngineTests
    {
        public static Protease _customProtease;

        [OneTimeSetUp]
        public static void OneTimeSetUp()
        {
            _customProtease = new Protease("Customized Protease", CleavageSpecificity.Full, null, null, new List<DigestionMotif> { new DigestionMotif("K", null, 1, "") });
            ProteaseDictionary.Dictionary.Add(_customProtease.Name, _customProtease);
        }

        [Test]
        public static void TestClassicSearchEngine()
        {
            CommonParameters CommonParameters = new CommonParameters
                (digestionParams: new DigestionParams(
                    protease: _customProtease.Name,
                    minPeptideLength: 1),
                scoreCutoff: 1);

            var myMsDataFile = new TestDataFile();
            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();
            var proteinList = new List<Protein> { new Protein("MNNNKQQQ", null) };
            var searchModes = new SinglePpmAroundZeroSearchMode(5);
            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();

            SpectralMatch[] allPsmsArray = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            bool writeSpectralLibrary = false;
            new ClassicSearchEngine(allPsmsArray, listOfSortedms2Scans, variableModifications, fixedModifications, null, null, null,
                proteinList, searchModes, CommonParameters, null, null, new List<string>(), writeSpectralLibrary).Run();

            // Single search mode
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));

            // One scan
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));

            Assert.That(allPsmsArray[0].Score > 1);
            Assert.That(allPsmsArray[0].ScanNumber, Is.EqualTo(2));

            Assert.That(allPsmsArray[0].BaseSequence, Is.EqualTo("QQQ"));
        }

        [Test]
        public static void TestClassicSearchEngine_IsoDec()
        {
            CommonParameters commonParameters = new CommonParameters
            (digestionParams: new DigestionParams(
                    protease: _customProtease.Name,
                    minPeptideLength: 1),
                scoreCutoff: 1, 
                precursorDeconParams: new IsoDecDeconvolutionParameters());
            commonParameters.PrecursorDeconvolutionParameters.MaxAssumedChargeState = 12;

            var myMsDataFile = new TestDataFile();
            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();
            var proteinList = new List<Protein> { new Protein("MNNNKQQQ", null) };
            var searchModes = new SinglePpmAroundZeroSearchMode(5);
            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();

            SpectralMatch[] allPsmsArray = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            bool writeSpectralLibrary = false;
            new ClassicSearchEngine(allPsmsArray, listOfSortedms2Scans, variableModifications, fixedModifications, null, null, null,
                proteinList, searchModes, commonParameters, null, null, new List<string>(), writeSpectralLibrary).Run();

            // Single search mode
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));

            // One scan
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));

            Assert.That(allPsmsArray[0].Score > 1);
            Assert.That(allPsmsArray[0].ScanNumber, Is.EqualTo(2));

            Assert.That(allPsmsArray[0].BaseSequence, Is.EqualTo("QQQ"));
        }

        [Test]
        [NonParallelizable]
        public static void TestSearchEngineResultsPsmFromTsv()
        {
            // only for unit test. must set to false at the end of each tests
            var type = typeof(FdrAnalysisEngine);
            var property = type.GetProperty("QvalueThresholdOverride");
            property.SetValue(null, true); 

            var myTomlPath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\Task1-SearchTaskconfig.toml");
            var searchTaskLoaded = Toml.ReadFile<SearchTask>(myTomlPath, MetaMorpheusTask.tomlConfig);
            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TestConsistency");
            string myFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TaGe_SA_A549_3_snip.mzML");
            string myDatabase = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TaGe_SA_A549_3_snip.fasta");

            searchTaskLoaded.CommonParameters.QValueCutoffForPepCalculation = 0.01;
            var engineToml = new EverythingRunnerEngine(new List<(string, MetaMorpheusTask)> { ("SearchTOML", searchTaskLoaded) }, new List<string> { myFile }, new List<DbForTask> { new DbForTask(myDatabase, false) }, outputFolder);
            engineToml.Run();

            string psmFile = Path.Combine(outputFolder, @"SearchTOML\AllPSMs.psmtsv");

            List<PsmFromTsv> parsedPsms = SpectrumMatchTsvReader.ReadPsmTsv(psmFile, out var warnings);
            PsmFromTsv psm = parsedPsms.First();
            Assert.That(psm.BaseSeq, Is.EqualTo("FTQTSGETTDADKEPAGEDK"));
            Assert.That(psm.DecoyContamTarget, Is.EqualTo("T"));
            Assert.That(psm.DeltaScore, Is.EqualTo(541.386));
            Assert.That(psm.EssentialSeq, Is.EqualTo("FTQTSGETTDADKEPAGEDK"));
            Assert.That(psm.FileNameWithoutExtension, Is.EqualTo("TaGe_SA_A549_3_snip"));
            Assert.That(psm.FullSequence, Is.EqualTo("FTQTSGETTDADKEPAGEDK"));
            Assert.That(psm.GeneName, Is.EqualTo("primary:MKI67"));
            Assert.That(psm.IdentifiedSequenceVariations, Is.EqualTo(""));
            Assert.That(psm.MassDiffDa, Is.EqualTo("-0.00544"));
            Assert.That(psm.MassDiffPpm, Is.EqualTo("-2.56000"));
            Assert.That(psm.MatchedIons.Count, Is.EqualTo(40));
            Assert.That(psm.MissedCleavage, Is.EqualTo("1"));
            Assert.That(psm.Ms2ScanNumber, Is.EqualTo(56));
            Assert.That(psm.NextAminoAcid, Is.EqualTo("G"));
            Assert.That(psm.Notch, Is.EqualTo("0"));
            Assert.That(psm.OrganismName, Is.EqualTo("Homo sapiens"));
            Assert.That(0, Is.EqualTo(psm.PEP).Within(1E-04));
            Assert.That(0.005, Is.EqualTo(psm.PEP_QValue).Within(1E-03));
            Assert.That(psm.PeptideDescription, Is.EqualTo("full"));
            Assert.That(psm.PeptideMonoMass, Is.EqualTo("2125.92875"));
            Assert.That(psm.PrecursorCharge, Is.EqualTo(3));
            Assert.That(psm.PrecursorMass, Is.EqualTo(2125.92331));
            Assert.That(psm.PrecursorMz, Is.EqualTo(709.64838));
            Assert.That(psm.PrecursorScanNum, Is.EqualTo(49));
            Assert.That(psm.PreviousAminoAcid, Is.EqualTo("K"));
            Assert.That(psm.ProteinAccession, Is.EqualTo("P46013"));
            Assert.That(psm.ProteinName, Is.EqualTo("Proliferation marker protein Ki-67"));
            Assert.That(0.004739, Is.EqualTo(psm.QValue).Within(1E-04));
            Assert.That(0.004739, Is.EqualTo(psm.QValueNotch).Within(1E-04));
            Assert.That(psm.RetentionTime, Is.EqualTo(45.59512));
            Assert.That(psm.Score, Is.EqualTo(662.486));
            Assert.That(psm.StartAndEndResiduesInParentSequence, Is.EqualTo("[2742 to 2761]"));
            Assert.That(psm.TotalIonCurrent, Is.EqualTo(159644.25225));
            Assert.That(psm.VariantCrossingIons.Count, Is.EqualTo(0));
            property.SetValue(null, false);
        }

        //tests a weird crash from ms2 scans that had experimental peaks but they were all removed from the xarray by XCorr processing
        [Test]
        public static void TestXCorrWithNoPeaks()
        {
            MsDataScan datascan = new MsDataScan(new MzSpectrum(new double[,] { }), 0, 0, true, Polarity.Positive, 0, new MzLibUtil.MzRange(0, 2000), "", MZAnalyzerType.FTICR, 1, null, null, "");
            Ms2ScanWithSpecificMass scan = new Ms2ScanWithSpecificMass(datascan, 0, 0, "", new CommonParameters(), new IsotopicEnvelope[] { new IsotopicEnvelope(new List<(double mz, double intensity)> { (1, 1) }, 32, 2, 23, 1) });
            scan.TheScan.MassSpectrum.XCorrPrePreprocessing(0, 1, 32);
            MetaMorpheusEngine.MatchFragmentIons(scan, new List<Product> { (new Product(ProductType.y, FragmentationTerminus.C, 0, 1, 1, 0)) }, new CommonParameters(dissociationType: DissociationType.LowCID));
        }

        [Test]
        public static void TestClassicSearchXcorrWithToml()
        {
            var myTomlPath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\Search.toml");
            var searchTaskLoaded = Toml.ReadFile<SearchTask>(myTomlPath, MetaMorpheusTask.tomlConfig);
            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TestConsistency");
            string myFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TaGe_SA_A549_3_snip.mzML");
            string myDatabase = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TaGe_SA_A549_3_snip.fasta");

            var engineToml = new EverythingRunnerEngine(new List<(string, MetaMorpheusTask)> { ("SearchTOML", searchTaskLoaded) }, new List<string> { myFile }, new List<DbForTask> { new DbForTask(myDatabase, false) }, outputFolder);
            engineToml.Run();

            string psmFile = Path.Combine(outputFolder, @"SearchTOML\AllPSMs.psmtsv");

            List<PsmFromTsv> parsedPsms = SpectrumMatchTsvReader.ReadPsmTsv(psmFile, out var warnings);

            Assert.That(parsedPsms.Count, Is.EqualTo(385)); //total psm count
            Assert.That(parsedPsms.Count(p => p.QValue < 0.01), Is.EqualTo(218)); //psms with q-value < 0.01 as read from psmtsv, including decoys
            Assert.That(warnings.Count, Is.EqualTo(0));

            int countFromResultsTxt = Convert.ToInt32(File.ReadAllLines(Path.Combine(outputFolder, @"SearchTOML\results.txt")).ToList().FirstOrDefault(l => l.Contains("All target")).Split(":")[1].Trim());
            Assert.That(countFromResultsTxt, Is.EqualTo(216));
        }

        [Test]
        public static void TestFilteringAndXCorrProcessing()
        {
            var origDataFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\sliced_b6.mzML");
            CommonParameters CommonParameters = new CommonParameters(
                dissociationType: DissociationType.LowCID,
                maxThreadsToUsePerFile: 1,
                precursorMassTolerance: new PpmTolerance(5),
                numberOfPeaksToKeepPerWindow: 200, // this is set to a value, but since the dissociation type is set to LowCID, no peak filtering should actually happen
                minimumAllowedIntensityRatioToBasePeak: 0.01, // this is set to a value, but since the dissociation type is set to LowCID, no peak filtering should actually happen
                trimMs1Peaks: false,
                trimMsMsPeaks: true, // this is set to true, but since the dissociation type is set to LowCID, no peak filtering should actually happen
                digestionParams: new DigestionParams(
                    protease: "trypsin",
                    minPeptideLength: 1,
                    maxMissedCleavages: 2,
                    initiatorMethionineBehavior: InitiatorMethionineBehavior.Variable),
                scoreCutoff: 5);

            MyFileManager myFileManager = new MyFileManager(true);

            // load the file/scan with no filtering or XCorr processing
            var myMsDataFile = myFileManager.LoadFile(origDataFile, CommonParameters);
            var myScan1 = myMsDataFile.GetAllScansList().First(p => p.OneBasedScanNumber == 240);

            Assert.That(myScan1.MassSpectrum.XcorrProcessed == false);
            Assert.That(myScan1.MassSpectrum.XArray.Length == 980);
            Assert.That(827.422, Is.EqualTo(myScan1.IsolationMz.Value).Within(0.001));

            var expectedResultsUnprocessed = File.ReadAllLines(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\unprocessedMzAndIntensities.tsv"));

            // assert peak m/zs and intensities
            for (int p = 0; p < myScan1.MassSpectrum.XArray.Length; p++)
            {
                double expectedMz = double.Parse(expectedResultsUnprocessed[p].Split(new char[] { '\t' })[0]);
                double expectedIntensity = double.Parse(expectedResultsUnprocessed[p].Split(new char[] { '\t' })[1]);

                double mz = Math.Round(myScan1.MassSpectrum.XArray[p], 3);
                double intensity = Math.Round(myScan1.MassSpectrum.YArray[p], 3);

                Assert.That(mz, Is.EqualTo(expectedMz).Within(0.001));
                Assert.That(intensity, Is.EqualTo(expectedIntensity).Within(0.001));
            }

            // this runs XCorr processing as part of the GetMs2Scans method
            Ms2ScanWithSpecificMass[] arrayOfMs2ScansSortedByMass = MetaMorpheusTask.GetMs2Scans(myMsDataFile, origDataFile, CommonParameters).OrderBy(b => b.PrecursorMass).ToArray();

            // this scan should be xcorr processed
            Assert.That(447, Is.EqualTo(arrayOfMs2ScansSortedByMass.Length));
            Assert.That(myScan1.MassSpectrum.XcorrProcessed == true);
            Assert.That(myScan1.MassSpectrum.XArray.Length == 459);

            var expectedResults = File.ReadAllLines(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\XCorrUnitTest.tsv"));

            // assert peak m/zs and intensities
            for (int p = 0; p < myScan1.MassSpectrum.XArray.Length; p++)
            {
                double expectedMz = double.Parse(expectedResults[p].Split(new char[] { '\t' })[0]);
                double expectedIntensity = double.Parse(expectedResults[p].Split(new char[] { '\t' })[1]);

                double mz = Math.Round(myScan1.MassSpectrum.XArray[p], 3);
                double intensity = Math.Round(myScan1.MassSpectrum.YArray[p], 3);

                Assert.That(mz, Is.EqualTo(expectedMz).Within(0.001));
                Assert.That(intensity, Is.EqualTo(expectedIntensity).Within(0.001));
            }
        }

        [Test]
        public static void TestProcessXcorrInMzSpectrumSlicedB6()
        {
            Dictionary<string, MsDataFile> MyMsDataFiles = new Dictionary<string, MsDataFile>();
            string origDataFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "DatabaseTests", "sliced_b6.mzML");
            FilteringParams filter = new FilteringParams(200, 0.01, null, 1, false, false, false);

            MyMsDataFiles[origDataFile] = Mzml.LoadAllStaticData(origDataFile, filter, 1);

            var scans = MyMsDataFiles[origDataFile].GetAllScansList();

            Assert.That(scans[1].MassSpectrum.XArray.Count(), Is.EqualTo(912));
            Assert.That(200.33052062988281, Is.EqualTo(scans[1].MassSpectrum.XArray[0]).Within(0.00001));

            foreach (MsDataScan scan in scans.Where(s => s.MsnOrder > 1))
            {
                if (scan.OneBasedScanNumber == 240)
                {
                    int j = 43;
                    j++;
                }
                scan.MassSpectrum.XCorrPrePreprocessing(0, 1969, scan.IsolationMz.Value);
            }

            Assert.That(scans[1].MassSpectrum.XcorrProcessed);
            Assert.That(scans[1].MassSpectrum.XArray.Count(), Is.EqualTo(220));
            Assert.That(201.1020879, Is.EqualTo(scans[1].MassSpectrum.XArray[0]).Within(0.00001));

            var bubba = scans[239];

            Assert.That(bubba.MassSpectrum.XArray.Count(), Is.EqualTo(459));
            Assert.That(201.102, Is.EqualTo(bubba.MassSpectrum.XArray[0]).Within(0.01));
            Assert.That(203.1031037, Is.EqualTo(bubba.MassSpectrum.XArray[1]).Within(0.01));
        }

        [Test]
        public static void TestClassicSearchEngineXcorr()
        {
            CommonParameters CommonParameters = new CommonParameters
                (dissociationType: DissociationType.LowCID,
                scoreCutoff: 1);

            double[] mzs = new double[] { 130.0499, 148.0604, 199.1077, 209.0921, 227.1026, 245.0768, 263.0874, 296.1605, 306.1448, 324.1554, 358.1609, 376.1714, 397.2082, 407.1925, 425.2031, 459.2086, 477.2191, 510.2922, 520.2766, 538.2871, 556.2613, 574.2719, 625.3192, 635.3035, 653.3141, 685.3039, 703.3145, 782.3567, 800.3672 };
            double[] intensities = new double[] { 20, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10 };
            var myXcorrMsDataFile = new TestDataFile(mzs, intensities, 799.359968, 1, 1.0);

            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();
            var proteinList = new List<Protein> { new Protein("PEPTIDE", null) };

            var searchModes = new SinglePpmAroundZeroSearchMode(5);

            var listOfSortedXcorrms2Scans = MetaMorpheusTask.GetMs2Scans(myXcorrMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();
            var copyOflistOfSortedXcorrms2Scans = listOfSortedXcorrms2Scans;

            Assert.That(mzs.ToList().SequenceEqual(listOfSortedXcorrms2Scans[0].TheScan.MassSpectrum.XArray.ToList()));
            Assert.That(intensities.ToList().SequenceEqual(listOfSortedXcorrms2Scans[0].TheScan.MassSpectrum.YArray.ToList()));

            foreach (var scan in copyOflistOfSortedXcorrms2Scans)
            {
                scan.TheScan.MassSpectrum.XCorrPrePreprocessing(0, 1969, scan.TheScan.IsolationMz.Value);
            }

            SpectralMatch[] allPsmsArray = new PeptideSpectralMatch[listOfSortedXcorrms2Scans.Length];

            bool writeSpectralLibrary = false;
            new ClassicSearchEngine(allPsmsArray, listOfSortedXcorrms2Scans, variableModifications, fixedModifications, null, null, null, 
                proteinList, searchModes, CommonParameters, null, null, new List<string>(), writeSpectralLibrary).Run();

            Assert.That(listOfSortedXcorrms2Scans[0].TheScan.MassSpectrum.XcorrProcessed);

            List<double> expectedXarray = copyOflistOfSortedXcorrms2Scans[0].TheScan.MassSpectrum.XArray.ToList();
            List<double> expectedYarray = copyOflistOfSortedXcorrms2Scans[0].TheScan.MassSpectrum.XArray.ToList();

            List<double> processedXarray = listOfSortedXcorrms2Scans[0].TheScan.MassSpectrum.XArray.ToList();
            List<double> processedYarray = listOfSortedXcorrms2Scans[0].TheScan.MassSpectrum.XArray.ToList();

            //this assures that the mass and intensities of the input spectrum have been xcorr processed and normalized. Note, the molecular ion has been removed
            Assert.That(expectedXarray.SequenceEqual(processedXarray));
            Assert.That(expectedYarray.SequenceEqual(processedYarray));

            Assert.That(allPsmsArray[0].MatchedFragmentIons.Where(p => p.NeutralTheoreticalProduct.ProductType == ProductType.b).ToList().Count, Is.EqualTo(5));
            Assert.That(allPsmsArray[0].MatchedFragmentIons.Where(p => p.NeutralTheoreticalProduct.ProductType == ProductType.bWaterLoss).ToList().Count, Is.EqualTo(5));
            Assert.That(allPsmsArray[0].MatchedFragmentIons.Where(p => p.NeutralTheoreticalProduct.ProductType == ProductType.y).ToList().Count, Is.EqualTo(6));
            Assert.That(allPsmsArray[0].MatchedFragmentIons.Where(p => p.NeutralTheoreticalProduct.ProductType == ProductType.yWaterLoss).ToList().Count, Is.EqualTo(6));

            Assert.That(Math.Round(allPsmsArray[0].Score, 1), Is.EqualTo(518.2));

            // Single search mode
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));

            // One scan
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));

            Assert.That(allPsmsArray[0].Score > 1);
        }

        [Test]
        public static void TestClassicSearchEngineWithWeirdPeptide()
        {
            CommonParameters CommonParameters = new CommonParameters(
                digestionParams: new DigestionParams(
                    protease: "Customized Protease",
                    maxMissedCleavages: 0,
                    minPeptideLength: 1),
                scoreCutoff: 1);

            var myMsDataFile = new TestDataFile();
            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();
            var proteinList = new List<Protein> { new Protein("QXQ", null) };

            var searchModes = new OpenSearchMode();

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();

            SpectralMatch[] allPsmsArray = new PeptideSpectralMatch[listOfSortedms2Scans.Length];

            bool writeSpectralLibrary = false;
            new ClassicSearchEngine(allPsmsArray, listOfSortedms2Scans, variableModifications, fixedModifications, null, null, null, 
                proteinList, searchModes, CommonParameters, null, null, new List<string>(), writeSpectralLibrary).Run();

            // Single search mode
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));

            // One Scan
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));

            Assert.That(allPsmsArray[0].Score > 1);
            Assert.That(allPsmsArray[0].ScanNumber, Is.EqualTo(2));

            Assert.That(allPsmsArray[0].BaseSequence, Is.EqualTo("QXQ"));
        }

        [Test]
        public static void TestModernSearchEngine()
        {
            SearchParameters SearchParameters = new SearchParameters
            {
                MassDiffAcceptorType = MassDiffAcceptorType.Exact,
                SearchTarget = true,
            };

            CommonParameters CommonParameters = new CommonParameters(
                precursorMassTolerance: new PpmTolerance(5),
                digestionParams: new DigestionParams(
                    protease: "trypsin",
                    minPeptideLength: 1),
                scoreCutoff: 1);

            var myMsDataFile = new TestDataFile();
            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();

            var proteinList = new List<Protein> { new Protein("MNNNKQQQ", null) };

            var indexEngine = new IndexingEngine(proteinList, variableModifications, fixedModifications, null, null, null, 1, DecoyType.Reverse, CommonParameters,
                null, SearchParameters.MaxFragmentSize, false, new List<FileInfo>(), TargetContaminantAmbiguity.RemoveContaminant, new List<string>());
            var indexResults = (IndexingResults)indexEngine.Run();

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();

            MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(CommonParameters.PrecursorMassTolerance, SearchParameters.MassDiffAcceptorType, SearchParameters.CustomMdac);

            SpectralMatch[] allPsmsArray = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            new ModernSearchEngine(allPsmsArray, listOfSortedms2Scans, indexResults.PeptideIndex, indexResults.FragmentIndex, 0, CommonParameters, null, massDiffAcceptor, SearchParameters.MaximumMassThatFragmentIonScoreIsDoubled, new List<string>()).Run();

            // Single search mode
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));

            // Single ms2 scan
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));
            Assert.That(allPsmsArray[0] != null);

            Assert.That(allPsmsArray[0].Score > 1);
            Assert.That(allPsmsArray[0].ScanNumber, Is.EqualTo(2));

            Assert.That(allPsmsArray[0].BaseSequence, Is.EqualTo("QQQ"));
        }

        [Test]
        public static void TestModernSearchEngineLowResTrivial()
        {
            SearchParameters SearchParameters = new SearchParameters
            {
                MassDiffAcceptorType = MassDiffAcceptorType.Exact,
                SearchTarget = true,
            };

            CommonParameters CommonParameters = new CommonParameters(
                dissociationType: DissociationType.LowCID,
                maxThreadsToUsePerFile: 1,
                precursorMassTolerance: new PpmTolerance(5),
                digestionParams: new DigestionParams(
                    protease: "trypsin",
                    minPeptideLength: 1),
                scoreCutoff: 1);

            var myMsDataFile = new TestDataFile();

            foreach (var scan in myMsDataFile.GetAllScansList().Where(p => p.MsnOrder > 1))
            {
                scan.MassSpectrum.XCorrPrePreprocessing(0, 1969, scan.IsolationMz.Value);
            }

            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();

            var proteinList = new List<Protein> { new Protein("MNNNKQQQ", null) };

            var indexEngine = new IndexingEngine(proteinList, variableModifications, fixedModifications, null, null, null, 1, DecoyType.Reverse, CommonParameters,
                null, SearchParameters.MaxFragmentSize, false, new List<FileInfo>(), TargetContaminantAmbiguity.RemoveContaminant, new List<string>());
            var indexResults = (IndexingResults)indexEngine.Run();

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, CommonParameters).OrderBy(b => b.PrecursorMass).ToArray();

            MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(CommonParameters.PrecursorMassTolerance, SearchParameters.MassDiffAcceptorType, SearchParameters.CustomMdac);

            SpectralMatch[] allPsmsArray = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            new ModernSearchEngine(allPsmsArray, listOfSortedms2Scans, indexResults.PeptideIndex, indexResults.FragmentIndex, 0, CommonParameters, null, massDiffAcceptor, SearchParameters.MaximumMassThatFragmentIonScoreIsDoubled, new List<string>()).Run();

            // Single search mode
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));

            // Single ms2 scan
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));
            Assert.That(allPsmsArray[0] != null);

            Assert.That(allPsmsArray[0].Score > 1);
            Assert.That(allPsmsArray[0].ScanNumber, Is.EqualTo(2));

            Assert.That(allPsmsArray[0].BaseSequence, Is.EqualTo("QQQ"));
        }

        [Test]
        public static void TestModernSearchEngineLowResOneRealSpectrum()
        {
            CommonParameters CommonParameters = new CommonParameters(
                dissociationType: DissociationType.LowCID,
                maxThreadsToUsePerFile: 1,
                precursorMassTolerance: new PpmTolerance(5),
                numberOfPeaksToKeepPerWindow: 200,
                minimumAllowedIntensityRatioToBasePeak: 0.01,
                trimMs1Peaks: false,
                trimMsMsPeaks: false,
                digestionParams: new DigestionParams(
                    protease: "trypsin",
                    minPeptideLength: 1,
                    maxMissedCleavages: 2,
                    initiatorMethionineBehavior: InitiatorMethionineBehavior.Variable),
                scoreCutoff: 5);

            SearchParameters SearchParameters = new SearchParameters
            {
                MassDiffAcceptorType = MassDiffAcceptorType.Exact,
                SearchTarget = true,
                SearchType = SearchType.Modern
            };

            var myMsDataFile = new TestDataFile(false, true);

            foreach (var scan in myMsDataFile.GetAllScansList().Where(p => p.MsnOrder > 1))
            {
                Assert.That(scan.MassSpectrum.XArray.Count(), Is.EqualTo(984));
                scan.MassSpectrum.XCorrPrePreprocessing(0, 1969, scan.IsolationMz.Value);
            }

            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();

            var proteinList = new List<Protein> { new Protein("LEEGPPVTTVLTR", null) };

            var indexEngine = new IndexingEngine(proteinList, variableModifications, fixedModifications, null, null, null, 1, DecoyType.Reverse, CommonParameters,
                null, SearchParameters.MaxFragmentSize, false, new List<FileInfo>(), TargetContaminantAmbiguity.RemoveContaminant, new List<string>());
            var indexResults = (IndexingResults)indexEngine.Run();

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, CommonParameters).OrderBy(b => b.PrecursorMass).ToArray();

            var remainingMasses = listOfSortedms2Scans[0].TheScan.MassSpectrum.XArray;

            Assert.That(listOfSortedms2Scans[0].TheScan.MassSpectrum.XArray.Count(), Is.EqualTo(195));

            var mzs = listOfSortedms2Scans[0].TheScan.MassSpectrum.XArray.ToList();
            List<int> mzsRounded = new List<int>();
            foreach (double mz in mzs)
            {
                mzsRounded.Add((int)Math.Round(mz, 0));
            }

            List<int> mzsExpected = new List<int>() { 201, 211, 212, 213, 215, 216, 225, 226, 227, 231, 238, 240, 241, 242, 243, 244, 252, 254, 255, 259, 266, 269, 270, 276, 282, 284, 294, 298, 300, 312, 316, 323, 326, 327, 334, 335, 340,
                344, 349, 351, 354, 355, 363, 367, 369, 371, 372, 373, 377, 379, 381, 383, 384, 389, 390, 395, 397, 401, 405, 407, 411, 413, 423, 429, 430, 434, 437, 441, 444, 452, 453, 462, 463, 464, 469, 470, 474, 480, 481, 483, 488, 489,
                490, 492, 493, 496, 498, 503, 508, 512, 519, 521, 522, 526, 536, 545, 553, 558, 563, 567, 568, 571, 573, 575, 576, 581, 584, 585, 586, 589, 590, 591, 593, 595, 609, 610, 618, 619, 621, 623, 625, 628, 632, 633, 634, 641, 646,
                650, 652, 655, 657, 658, 664, 670, 672, 674, 676, 679, 682, 688, 690, 691, 694, 697, 698, 700, 704, 708, 722, 745, 747, 755, 763, 765, 771, 772, 773, 782, 789, 795, 805, 809, 823, 828, 831, 839, 866, 867, 868, 874, 876,
                885, 886, 906, 914, 916, 924, 951, 959, 965, 966, 973, 977, 982, 983, 988, 996, 1006, 1023, 1024, 1040, 1041, 1137, 1152, 1170};

            Assert.That(mzsRounded, Is.EqualTo(mzsExpected));

            Ms2ScanWithSpecificMass[] losm2 = listOfSortedms2Scans.Where(mass => mass.PrecursorMass > 1410 && mass.PrecursorMass < 1411).ToArray();

            List<double> precursorPeaks = new List<double>();
            precursorPeaks.AddRange(losm2.Select(pm => pm.PrecursorMass).ToList());
            precursorPeaks.Sort();

            List<int> filledIndicies = new List<int>();
            for (int i = 0; i < indexResults.FragmentIndex.Length; i++)
            {
                if (indexResults.FragmentIndex[i] != null)
                {
                    filledIndicies.Add(i);
                }
            }

            List<int> expectedIndicies = new List<int> { 157080, 174088, 196100, 224114, 242123, 257131, 258131, 275140, 325165, 353179, 370188, 371188, 382194, 388197, 410208, 428217, 469238, 470239, 479243, 487247, 507258, 525267,
                570290, 571290, 576293, 588299, 604307, 622316, 671341, 672341, 675343, 689350, 703357, 721366, 770391, 771392, 776394, 788400, 804408, 822417, 867440, 868441, 877445, 885449, 905460, 923469, 964490, 965490, 976496,
                982499, 1004510, 1021519, 1022519, 1039528, 1089553, 1117567, 1135576, 1150584, 1151585, 1168593, 1190604, 1218619, 1236628, 1279650, 1280650, 1297659};

            Assert.That(filledIndicies, Is.EqualTo(expectedIndicies));

            MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(CommonParameters.PrecursorMassTolerance, SearchParameters.MassDiffAcceptorType, SearchParameters.CustomMdac);

            SpectralMatch[] allPsmsArray = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            new ModernSearchEngine(allPsmsArray, losm2, indexResults.PeptideIndex, indexResults.FragmentIndex, 0, CommonParameters, null, massDiffAcceptor, SearchParameters.MaximumMassThatFragmentIonScoreIsDoubled, new List<string>()).Run();

            var nonNullPsms = allPsmsArray.Where(p => p != null).ToList();


            List<(string fileName, CommonParameters fileSpecificParameters)> fsp = new List<(string fileName, CommonParameters fileSpecificParameters)> { ("filename", CommonParameters) };

            FdrAnalysisResults fdrResultsModernDelta = (FdrAnalysisResults)(new FdrAnalysisEngine(nonNullPsms, 1, CommonParameters, fsp, new List<string>()).Run());

            // Single search mode
            Assert.That(allPsmsArray.Length, Is.EqualTo(12));

            var goodPsm = nonNullPsms.Where(p => p.FdrInfo.QValue <= 0.01).ToList();

            var myMatchedIons = goodPsm[0].MatchedFragmentIons;

            Assert.That(myMatchedIons.Count(), Is.EqualTo(47));

            var goodScore = nonNullPsms.Where(p => p.FdrInfo.QValue <= 0.01).Select(s => s.Score).ToList();
            goodScore.Sort();
            Assert.That(goodPsm.Count(), Is.EqualTo(2));
        }

        [Test]
        [NonParallelizable]
        public static void TestClassicSearchEngineLowResSimple()
        {
            //override to be only used for unit tests in non-parallelizable format
            //must set to false at the end of this method
            var type = typeof(FdrAnalysisEngine);
            var property = type.GetProperty("QvalueThresholdOverride");
            property.SetValue(null, true);

            var origDataFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TaGe_SA_A549_3_snip.mzML");
            MyFileManager myFileManager = new MyFileManager(true);

            CommonParameters CommonParameters = new CommonParameters(
                dissociationType: DissociationType.LowCID,
                maxThreadsToUsePerFile: 1,
                precursorMassTolerance: new PpmTolerance(5),
                numberOfPeaksToKeepPerWindow: 200,
                minimumAllowedIntensityRatioToBasePeak: 0.01,
                trimMs1Peaks: false,
                trimMsMsPeaks: false,
                digestionParams: new DigestionParams(
                    protease: "trypsin",
                    minPeptideLength: 1,
                    maxMissedCleavages: 2,
                    initiatorMethionineBehavior: InitiatorMethionineBehavior.Variable),
                scoreCutoff: 5);

            SearchParameters SearchParameters = new SearchParameters
            {
                MassDiffAcceptorType = MassDiffAcceptorType.Exact,
                SearchTarget = true,
                SearchType = SearchType.Classic
            };

            MsDataFile myMsDataFile = myFileManager.LoadFile(origDataFile, CommonParameters);

            Ms2ScanWithSpecificMass[] arrayOfMs2ScansSortedByMass = MetaMorpheusTask.GetMs2Scans(myMsDataFile, origDataFile, CommonParameters).OrderBy(b => b.PrecursorMass).ToArray();

            Assert.That(arrayOfMs2ScansSortedByMass.Count(), Is.EqualTo(535));

            int numSpectra = myMsDataFile.GetAllScansList().Count(p => p.MsnOrder == 2);

            Assert.That(numSpectra, Is.EqualTo(147));

            SpectralMatch[] fileSpecificPsms = new PeptideSpectralMatch[arrayOfMs2ScansSortedByMass.Length];

            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestClassicSearchEngineLowResSimple");
            string myDatabase = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TaGe_SA_A549_3_snip.fasta");

            Directory.CreateDirectory(outputFolder);

            foreach (var scan in myMsDataFile.GetAllScansList().Where(p => p.MsnOrder > 1))
            {
                scan.MassSpectrum.XCorrPrePreprocessing(0, 1969, scan.IsolationMz.Value);
            }

            ModificationMotif.TryGetMotif("M", out ModificationMotif motif1);
            Modification mod1 = new Modification(_originalId: "Oxidation on M", _modificationType: "Common Variable", _target: motif1, _locationRestriction: "Anywhere.", _monoisotopicMass: ChemicalFormula.ParseFormula("O1").MonoisotopicMass);

            ModificationMotif.TryGetMotif("C", out ModificationMotif motif2);
            Modification mod2 = new Modification(_originalId: "Carbamidomethyl on C", _modificationType: "Common Fixed", _target: motif2, _locationRestriction: "Anywhere.", _monoisotopicMass: 57.02146372068994);

            var variableModifications = new List<Modification>
            {
                mod1
            };

            var fixedModifications = new List<Modification>
            {
                mod2
            };
            Dictionary<string, Modification> u = new Dictionary<string, Modification>();

            List<Protein> proteinList = ProteinDbLoader.LoadProteinFasta(myDatabase, true, DecoyType.Reverse, false, out List<string> errors);

            var searchModes = new SinglePpmAroundZeroSearchMode(5);

            var listOfSortedXcorrms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, origDataFile, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();

            Assert.That(listOfSortedXcorrms2Scans.Count(), Is.EqualTo(535));

            SpectralMatch[] allPsmsArray = new PeptideSpectralMatch[listOfSortedXcorrms2Scans.Length];

            var fsp = new List<(string, CommonParameters)>();
            fsp.Add(("sliced_b6.mzML", CommonParameters));

            bool writeSpectralLibrary = false;
            new ClassicSearchEngine(allPsmsArray, listOfSortedXcorrms2Scans, variableModifications, fixedModifications, null, null, null,
                proteinList, searchModes, CommonParameters, fsp, null, new List<string>(), writeSpectralLibrary).Run();

            var nonNullPsms = allPsmsArray.Where(p => p != null).ToList();
            Assert.That(nonNullPsms.Count, Is.EqualTo(432)); //if you run the test separately, it will be 111 because mods won't have been read in a previous test...

            FdrAnalysisResults fdrResultsModernDelta = (FdrAnalysisResults)(new FdrAnalysisEngine(nonNullPsms, 1, CommonParameters, fsp, new List<string>()).Run());

            // Single search mode
            Assert.That(allPsmsArray.Length, Is.EqualTo(535));

            var goodScore = nonNullPsms.Where(p => p.FdrInfo.QValue <= 0.01).Select(s => s.Score).ToList();

            Assert.That(goodScore.Count(), Is.EqualTo(181));
            Directory.Delete(outputFolder, true);
            property.SetValue(null, false);
        }

        [Test]
        [NonParallelizable]
        public static void TestModernSearchEngineLowResSimple()
        {
            //override to be only used for unit tests in non-parallelizable format
            //must set to false at the end of this method
            var type = typeof(FdrAnalysisEngine);
            var property = type.GetProperty("QvalueThresholdOverride");
            property.SetValue(null, true);

            var origDataFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TaGe_SA_A549_3_snip.mzML");
            MyFileManager myFileManager = new MyFileManager(true);

            CommonParameters CommonParameters = new CommonParameters(
                dissociationType: DissociationType.LowCID,
                maxThreadsToUsePerFile: 1,
                precursorMassTolerance: new PpmTolerance(5),
                numberOfPeaksToKeepPerWindow: 200,
                minimumAllowedIntensityRatioToBasePeak: 0.01,
                trimMs1Peaks: false,
                trimMsMsPeaks: false,
                separationType: "HPLC",
                digestionParams: new DigestionParams(
                    protease: "trypsin",
                    minPeptideLength: 1,
                    maxMissedCleavages: 2,
                    initiatorMethionineBehavior: InitiatorMethionineBehavior.Variable),
                scoreCutoff: 5);

            var fsp = new List<(string, CommonParameters)>();
            fsp.Add((origDataFile, CommonParameters));

            SearchParameters SearchParameters = new SearchParameters
            {
                MassDiffAcceptorType = MassDiffAcceptorType.Exact,
                SearchTarget = true,
                SearchType = SearchType.Modern
            };
            //
            MsDataFile myMsDataFile = myFileManager.LoadFile(origDataFile, CommonParameters);

            Ms2ScanWithSpecificMass[] arrayOfMs2ScansSortedByMass = MetaMorpheusTask.GetMs2Scans(myMsDataFile, origDataFile, CommonParameters).OrderBy(b => b.PrecursorMass).ToArray();
            int numSpectra = myMsDataFile.GetAllScansList().Count(p => p.MsnOrder == 2);
            SpectralMatch[] fileSpecificPsms = new PeptideSpectralMatch[arrayOfMs2ScansSortedByMass.Length];

            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestModernSearchEngineLowResSimple");
            //string myDatabase = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\LowResSnip_B6_mouse_11700_117500.xml.gz");
            string myDatabase = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TaGe_SA_A549_3_snip.fasta");

            Directory.CreateDirectory(outputFolder);

            foreach (var scan in myMsDataFile.GetAllScansList().Where(p => p.MsnOrder > 1))
            {
                scan.MassSpectrum.XCorrPrePreprocessing(0, 1969, scan.IsolationMz.Value);
            }

            ModificationMotif.TryGetMotif("M", out ModificationMotif motif1);
            Modification mod1 = new Modification(_originalId: "Oxidation of M", _modificationType: "Common Variable", _target: motif1, _locationRestriction: "Anywhere.", _monoisotopicMass: ChemicalFormula.ParseFormula("O1").MonoisotopicMass);

            ModificationMotif.TryGetMotif("C", out ModificationMotif motif2);
            Modification mod2 = new Modification(_originalId: "Carbamidomethyl on C", _modificationType: "Common Fixed", _target: motif2, _locationRestriction: "Anywhere.", _monoisotopicMass: 57.02146372068994);

            var variableModifications = new List<Modification>
            {
                mod1
            };

            var fixedModifications = new List<Modification>
            {
                mod2
            };
            Dictionary<string, Modification> u = new Dictionary<string, Modification>();

            List<Protein> proteinList = ProteinDbLoader.LoadProteinFasta(myDatabase, true, DecoyType.Reverse,false,out var errors);

            var indexEngine = new IndexingEngine(proteinList, variableModifications, fixedModifications, null, null, null, 1, DecoyType.Reverse, CommonParameters,
                fsp, SearchParameters.MaxFragmentSize, false, new List<FileInfo>(), TargetContaminantAmbiguity.RemoveContaminant, new List<string>());
            var indexResults = (IndexingResults)indexEngine.Run();

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, origDataFile, CommonParameters).OrderBy(b => b.PrecursorMass).ToArray();

            MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(CommonParameters.PrecursorMassTolerance, SearchParameters.MassDiffAcceptorType, SearchParameters.CustomMdac);

            SpectralMatch[] allPsmsArray = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            new ModernSearchEngine(allPsmsArray, listOfSortedms2Scans, indexResults.PeptideIndex, indexResults.FragmentIndex, 0, CommonParameters, fsp, massDiffAcceptor, SearchParameters.MaximumMassThatFragmentIonScoreIsDoubled, new List<string>()).Run();

            var nonNullPsms = allPsmsArray.Where(p => p != null).ToList();

            EngineLayer.FdrAnalysis.FdrAnalysisResults fdrResultsModernDelta = (EngineLayer.FdrAnalysis.FdrAnalysisResults)(new EngineLayer.FdrAnalysis.FdrAnalysisEngine(nonNullPsms, 1, CommonParameters, fsp, new List<string>()).Run());

            // Single search mode
            Assert.That(allPsmsArray.Length, Is.EqualTo(535));

            var goodScore = nonNullPsms.Where(p => p.FdrInfo.QValue <= 0.01).Select(s => s.Score).ToList();

            Assert.That(goodScore.Count(), Is.EqualTo(181));
            property.SetValue(null, false);
        }

        [Test]
        public static void TestModernSearchFragmentEdges()
        {
            // purpose of this test is to check that fragments at the edge of the fragment index in modern search do not fall out of bounds
            // example: fragment mass 400 when max frag mass is 400 may go over the edge of the array because of ppm tolerances
            SearchParameters SearchParameters = new SearchParameters
            {
                MassDiffAcceptorType = MassDiffAcceptorType.Exact,
                MaxFragmentSize = 1, // super small index
            };
            CommonParameters CommonParameters = new CommonParameters(
                productMassTolerance: new AbsoluteTolerance(100), // super large tolerance (100 Da)
                digestionParams: new DigestionParams(
                    protease: "trypsin",
                    minPeptideLength: 1),
                scoreCutoff: 1,
                addCompIons: true);

            var proteinList = new List<Protein> { new Protein("K", null) };

            var indexEngine = new IndexingEngine(proteinList, new List<Modification>(), new List<Modification>(), null, null, null, 1, DecoyType.Reverse,
                CommonParameters, null, SearchParameters.MaxFragmentSize, false, new List<FileInfo>(), TargetContaminantAmbiguity.RemoveContaminant, new List<string>());
            var indexResults = (IndexingResults)indexEngine.Run();

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(new TestDataFile(), null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();

            MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(CommonParameters.PrecursorMassTolerance, SearchParameters.MassDiffAcceptorType, SearchParameters.CustomMdac);

            SpectralMatch[] allPsmsArray = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            new ModernSearchEngine(allPsmsArray, listOfSortedms2Scans, indexResults.PeptideIndex, indexResults.FragmentIndex, 0, CommonParameters, null, massDiffAcceptor, SearchParameters.MaximumMassThatFragmentIonScoreIsDoubled, new List<string>()).Run();

            // no assertions... just don't crash...
        }

        [Test]
        public static void TestNonViablePSM()
        {
            //just check if it crashes or not.
            SearchParameters SearchParameters = new SearchParameters();

            CommonParameters CommonParameters = new CommonParameters(
                digestionParams: new DigestionParams(
                    protease: "Customized Protease"),
                scoreCutoff: 255);

            MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(CommonParameters.PrecursorMassTolerance, SearchParameters.MassDiffAcceptorType, SearchParameters.CustomMdac);

            var myMsDataFile = new TestDataFile(true); //empty
            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();

            var proteinList = new List<Protein> { new Protein("MNNNKQQQ", null) };

            var searchModes = new SinglePpmAroundZeroSearchMode(5);

            var indexEngine = new IndexingEngine(proteinList, variableModifications, fixedModifications, null, null, null, 1, DecoyType.Reverse, CommonParameters,
                null, SearchParameters.MaxFragmentSize, false, new List<FileInfo>(), TargetContaminantAmbiguity.RemoveContaminant, new List<string>());
            var indexResults = (IndexingResults)indexEngine.Run();

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();

            SpectralMatch[][] allPsmsArrays = new PeptideSpectralMatch[1][];
            allPsmsArrays[0] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            SpectralMatch[] allPsmsArray = allPsmsArrays[0];

            //Classic
            bool writeSpectralLibrary = false;
            new ClassicSearchEngine(allPsmsArray, listOfSortedms2Scans, variableModifications, fixedModifications, null, null, null, 
                proteinList, searchModes, CommonParameters, null, null, new List<string>(), writeSpectralLibrary).Run();

            //Modern
            new ModernSearchEngine(allPsmsArray, listOfSortedms2Scans, indexResults.PeptideIndex, indexResults.FragmentIndex, 0, CommonParameters, null, massDiffAcceptor, SearchParameters.MaximumMassThatFragmentIonScoreIsDoubled, new List<string>()).Run();

            //coisolation index
            List<int>[] coisolationIndex = new List<int>[listOfSortedms2Scans.Length];
            for (int i = 0; i < listOfSortedms2Scans.Length; i++)
            {
                coisolationIndex[i] = new List<int> { i };
            }
            //NonSpecific
            new NonSpecificEnzymeSearchEngine(allPsmsArrays, listOfSortedms2Scans, coisolationIndex, indexResults.PeptideIndex, indexResults.FragmentIndex, indexResults.FragmentIndex, 0, CommonParameters, null, new List<Modification>(), massDiffAcceptor, SearchParameters.MaximumMassThatFragmentIonScoreIsDoubled, new List<string>()).Run();
        }

        [Test]
        public static void TestModernSearchEngineWithWeirdPeptide()
        {
            SearchParameters SearchParameters = new SearchParameters
            {
                MassDiffAcceptorType = MassDiffAcceptorType.Open,
                SearchTarget = true,
            };

            CommonParameters CommonParameters = new CommonParameters(
                digestionParams: new DigestionParams(
                    protease: "trypsin",
                    minPeptideLength: 1),
                scoreCutoff: 1);

            var myMsDataFile = new TestDataFile();
            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();
            var localizeableModifications = new List<Modification>();

            var proteinList = new List<Protein> { new Protein("MNNNKQXQ", null) };

            var indexEngine = new IndexingEngine(proteinList, variableModifications, fixedModifications, null, null, null, 1, DecoyType.Reverse, CommonParameters,
                null, SearchParameters.MaxFragmentSize, false, new List<FileInfo>(), TargetContaminantAmbiguity.RemoveContaminant, new List<string>());
            var indexResults = (IndexingResults)indexEngine.Run();

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();
            MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(CommonParameters.PrecursorMassTolerance, SearchParameters.MassDiffAcceptorType, SearchParameters.CustomMdac);

            SpectralMatch[] allPsmsArray = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            var engine = new ModernSearchEngine(allPsmsArray, listOfSortedms2Scans, indexResults.PeptideIndex, indexResults.FragmentIndex, 0, CommonParameters, null, massDiffAcceptor, SearchParameters.MaximumMassThatFragmentIonScoreIsDoubled, new List<string>());
            var searchResults = engine.Run();

            // Single search mode
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));

            // Single ms2 scan
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));
            Assert.That(allPsmsArray[0] != null);

            Assert.That(allPsmsArray[0].Score > 1);
            Assert.That(allPsmsArray[0].ScanNumber, Is.EqualTo(2));

            Assert.That(allPsmsArray[0].NumDifferentMatchingPeptides, Is.EqualTo(3));
        }

        [Test]
        public static void TestNonSpecificEnzymeSearchEngineSingleN()
        {
            SearchParameters SearchParameters = new SearchParameters
            {
                MassDiffAcceptorType = MassDiffAcceptorType.Exact,
                LocalFdrCategories = new List<FdrCategory>
                {
                    FdrCategory.FullySpecific,
                    FdrCategory.SemiSpecific,
                    FdrCategory.NonSpecific
                }
            };
            List<DigestionMotif> motifs = new List<DigestionMotif>
            {
                new DigestionMotif("K", null, 0, null),
                new DigestionMotif("K", null, 1, null),
                new DigestionMotif("G", null, 0, null),
                new DigestionMotif("G", null, 1, null),
            };
            DigestionParams dp = new DigestionParams("singleN", minPeptideLength: 1, fragmentationTerminus: FragmentationTerminus.N, searchModeType: CleavageSpecificity.None);
            CommonParameters CommonParameters = new CommonParameters(
                dissociationType: DissociationType.HCD,
                precursorMassTolerance: new PpmTolerance(5),
                digestionParams: dp,
                scoreCutoff: 1,
                addCompIons: true);

            var myMsDataFile = new TestDataFile("Yes, I'd like one slightly larger please");
            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();

            var proteinList = new List<Protein> { new Protein("GGGGGMNNNKQQQGGGGG", "TestProtein") };

            var indexEngine = new IndexingEngine(proteinList, variableModifications, fixedModifications, null, null, null, 1, DecoyType.Reverse, CommonParameters,
                null, 100000, false, new List<FileInfo>(), TargetContaminantAmbiguity.RemoveContaminant, new List<string>());
            var indexResults = (IndexingResults)indexEngine.Run();
            var peptideIndex = indexResults.PeptideIndex;
            var fragmentIndexDict = indexResults.FragmentIndex;

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();

            MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(CommonParameters.PrecursorMassTolerance, SearchParameters.MassDiffAcceptorType, SearchParameters.CustomMdac);

            SpectralMatch[][] allPsmsArrays = new PeptideSpectralMatch[3][];
            allPsmsArrays[0] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            allPsmsArrays[1] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            allPsmsArrays[2] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            SpectralMatch[] allPsmsArray = allPsmsArrays[2];

            //coisolation index
            List<int>[] coisolationIndex = new List<int>[listOfSortedms2Scans.Length];
            for (int i = 0; i < listOfSortedms2Scans.Length; i++)
            {
                coisolationIndex[i] = new List<int> { i };
            }
            new NonSpecificEnzymeSearchEngine(allPsmsArrays, listOfSortedms2Scans, coisolationIndex, peptideIndex, fragmentIndexDict, fragmentIndexDict, 0, CommonParameters, null, variableModifications, massDiffAcceptor, SearchParameters.MaximumMassThatFragmentIonScoreIsDoubled, new List<string>()).Run();

            // Single search mode
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));

            // Single ms2 scan
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));

            Assert.That(allPsmsArray[0].Score > 4);
            Assert.That(allPsmsArray[0].ScanNumber, Is.EqualTo(2));
            CommonParameters = new CommonParameters(
                 digestionParams: new DigestionParams("singleN", minPeptideLength: 1),
                 precursorMassTolerance: new PpmTolerance(5),
                 scoreCutoff: 1);

            allPsmsArray[0].ResolveAllAmbiguities();
            Assert.That(allPsmsArray[0].BaseSequence, Is.EqualTo("QQQGGGG"));
        }

        [Test]
        public static void TestNonSpecificEnzymeSearchEngine()
        {
            var myTomlPath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\NonSpecificSearchToml.toml");
            var searchTaskLoaded = Toml.ReadFile<SearchTask>(myTomlPath, MetaMorpheusTask.tomlConfig);
            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\NonSpecificSearchTest");
            Directory.CreateDirectory(outputFolder);
            string myFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TaGe_SA_A549_3_snip.mzML");
            string myDatabase = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\bosTaurusEnamPruned.xml");

            var engineToml = new EverythingRunnerEngine(new List<(string, MetaMorpheusTask)> { ("SearchTOML", searchTaskLoaded) }, new List<string> { myFile }, new List<DbForTask> { new DbForTask(myDatabase, false) }, outputFolder);
            engineToml.Run();

            string psmFile = Path.Combine(outputFolder, @"SearchTOML\AllPSMs.psmtsv");

            List<PsmFromTsv> parsedPsms = SpectrumMatchTsvReader.ReadPsmTsv(psmFile, out var warnings);

            Assert.That(parsedPsms.Count, Is.EqualTo(38)); //total psm count

            Directory.Delete(outputFolder, true);

        }


        [Test]
        public static void TestNonSpecificEnzymeSearchEngineSingleNLowCID()
        {
            SearchParameters SearchParameters = new SearchParameters
            {
                MassDiffAcceptorType = MassDiffAcceptorType.Exact,
                LocalFdrCategories = new List<FdrCategory>
                {
                    FdrCategory.FullySpecific,
                    FdrCategory.SemiSpecific,
                    FdrCategory.NonSpecific
                }
            };
            List<DigestionMotif> motifs = new List<DigestionMotif>
            {
                new DigestionMotif("K", null, 0, null),
                new DigestionMotif("K", null, 1, null),
                new DigestionMotif("G", null, 0, null),
                new DigestionMotif("G", null, 1, null),
            };
            DigestionParams dp = new DigestionParams("singleN", minPeptideLength: 1, fragmentationTerminus: FragmentationTerminus.N, searchModeType: CleavageSpecificity.None);
            CommonParameters CommonParameters = new CommonParameters(
                dissociationType: DissociationType.LowCID,
                precursorMassTolerance: new PpmTolerance(250),
                digestionParams: dp,
                scoreCutoff: 1,
                addCompIons: true);

            var myMsDataFile = new TestDataFile("Yes, I'd like one slightly larger please");
            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();

            var proteinList = new List<Protein> { new Protein("GGGGGMNNNKQQQGGGGG", "TestProtein") };

            var indexEngine = new IndexingEngine(proteinList, variableModifications, fixedModifications, null, null, null, 1, DecoyType.Reverse, CommonParameters,
                null, 100000, false, new List<FileInfo>(), TargetContaminantAmbiguity.RemoveContaminant, new List<string>());
            var indexResults = (IndexingResults)indexEngine.Run();
            var peptideIndex = indexResults.PeptideIndex;
            var fragmentIndexDict = indexResults.FragmentIndex;

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, CommonParameters).OrderBy(b => b.PrecursorMass).ToArray();

            MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(CommonParameters.PrecursorMassTolerance, SearchParameters.MassDiffAcceptorType, SearchParameters.CustomMdac);

            SpectralMatch[][] allPsmsArrays = new PeptideSpectralMatch[3][];
            allPsmsArrays[0] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            allPsmsArrays[1] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            allPsmsArrays[2] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            SpectralMatch[] allPsmsArray = allPsmsArrays[2];

            //coisolation index
            List<int>[] coisolationIndex = new List<int>[listOfSortedms2Scans.Length];
            for (int i = 0; i < listOfSortedms2Scans.Length; i++)
            {
                coisolationIndex[i] = new List<int> { i };
            }
            new NonSpecificEnzymeSearchEngine(allPsmsArrays, listOfSortedms2Scans, coisolationIndex, peptideIndex, fragmentIndexDict, fragmentIndexDict, 0, CommonParameters, null, variableModifications, massDiffAcceptor, SearchParameters.MaximumMassThatFragmentIonScoreIsDoubled, new List<string>()).Run();

            // Single search mode
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));

            // Single ms2 scan
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));

            Assert.That(allPsmsArray[0].Score > 4);
            Assert.That(allPsmsArray[0].ScanNumber, Is.EqualTo(2));
            CommonParameters = new CommonParameters(
                 digestionParams: new DigestionParams("singleN", minPeptideLength: 1),
                 precursorMassTolerance: new PpmTolerance(5),
                 scoreCutoff: 1);

            allPsmsArray[0].ResolveAllAmbiguities();
            Assert.That(allPsmsArray[0].BaseSequence, Is.EqualTo("QQQGGGG"));
        }


        [Test]
        public static void TestNonSpecificEnzymeSearchEngineSingleCModifications()
        {
            SearchParameters SearchParameters = new SearchParameters
            {
                SearchTarget = true,
                MassDiffAcceptorType = MassDiffAcceptorType.Exact,
                LocalFdrCategories = new List<FdrCategory>
                {
                    FdrCategory.NonSpecific
                }
            };
            DigestionParams dp = new DigestionParams("singleC", minPeptideLength: 1, fragmentationTerminus: FragmentationTerminus.C, searchModeType: CleavageSpecificity.None);

            CommonParameters CommonParameters = new CommonParameters(
                dissociationType: DissociationType.HCD,
                digestionParams: dp,
                scoreCutoff: 5,
                precursorMassTolerance: new PpmTolerance(5),
                addCompIons: true);

            PeptideWithSetModifications guiltyPwsm = new PeptideWithSetModifications("DQPKLLGIETPLPKKE", null);
            var fragments = new List<Product>();
            guiltyPwsm.Fragment(CommonParameters.DissociationType, FragmentationTerminus.Both, fragments);

            var myMsDataFile = new TestDataFile(guiltyPwsm.MonoisotopicMass, fragments.Select(x => x.NeutralMass.ToMz(1)).ToArray());
            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();
            ModificationMotif.TryGetMotif("C", out ModificationMotif motif2);
            Modification mod2 = new Modification(_originalId: "Carbamidomethyl on C", _modificationType: "Common Fixed", _target: motif2, _locationRestriction: "Anywhere.", _monoisotopicMass: 57.02146372068994);
            fixedModifications.Add(mod2);

            var proteinList = new List<Protein> { new Protein("GGGGGCDQPKLLGIETPLPKKEGGGGG", null) };

            var indexEngine = new IndexingEngine(proteinList, variableModifications, fixedModifications, null, null, null, 1, DecoyType.None, CommonParameters,
                null, SearchParameters.MaxFragmentSize, true, new List<FileInfo>(), TargetContaminantAmbiguity.RemoveContaminant, new List<string>());

            var indexResults = (IndexingResults)indexEngine.Run();
            var peptideIndex = indexResults.PeptideIndex;
            var fragmentIndexDict = indexResults.FragmentIndex;
            var precursorIndexDict = indexResults.PrecursorIndex;

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();
            MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(CommonParameters.PrecursorMassTolerance, SearchParameters.MassDiffAcceptorType, SearchParameters.CustomMdac);

            SpectralMatch[][] allPsmsArrays = new PeptideSpectralMatch[3][];
            allPsmsArrays[0] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            allPsmsArrays[1] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            allPsmsArrays[2] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            SpectralMatch[] allPsmsArray = allPsmsArrays[2];

            //coisolation index
            List<int>[] coisolationIndex = new List<int>[listOfSortedms2Scans.Length];
            for (int i = 0; i < listOfSortedms2Scans.Length; i++)
            {
                coisolationIndex[i] = new List<int> { i };
            }
            var engine = new NonSpecificEnzymeSearchEngine(allPsmsArrays, listOfSortedms2Scans, coisolationIndex, peptideIndex, fragmentIndexDict, precursorIndexDict, 0, CommonParameters, null, variableModifications, massDiffAcceptor, SearchParameters.MaximumMassThatFragmentIonScoreIsDoubled, new List<string>());

            var searchResults = engine.Run();

            allPsmsArray[0].ResolveAllAmbiguities();
            //Check that there is no modification hanging out on the n-terminus
            Assert.That(allPsmsArray[0].FullSequence, Is.EqualTo(guiltyPwsm.FullSequence));

            proteinList = new List<Protein> { new Protein("CDQPKLLGIETPLPKKEGGGGG", null) };
            guiltyPwsm = new PeptideWithSetModifications("C[Common Fixed:Carbamidomethyl on C]DQPKLLGIETPLPKKE", new Dictionary<string, Modification> { { "Carbamidomethyl on C", mod2 } });
            guiltyPwsm.Fragment(CommonParameters.DissociationType, FragmentationTerminus.Both, fragments);
            myMsDataFile = new TestDataFile(guiltyPwsm.MonoisotopicMass, fragments.Select(x => x.NeutralMass.ToMz(1)).ToArray());
            indexEngine = new IndexingEngine(proteinList, variableModifications, fixedModifications, null, null, null, 1, DecoyType.None, CommonParameters,
                null, SearchParameters.MaxFragmentSize, true, new List<FileInfo>(), TargetContaminantAmbiguity.RemoveContaminant, new List<string>());
            indexResults = (IndexingResults)indexEngine.Run();
            precursorIndexDict = indexResults.PrecursorIndex;
            peptideIndex = indexResults.PeptideIndex;
            fragmentIndexDict = indexResults.FragmentIndex;
            listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();

            allPsmsArrays[2] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            allPsmsArray = allPsmsArrays[2];
            //coisolation index
            coisolationIndex = new List<int>[listOfSortedms2Scans.Length];
            for (int i = 0; i < listOfSortedms2Scans.Length; i++)
            {
                coisolationIndex[i] = new List<int> { i };
            }
            engine = new NonSpecificEnzymeSearchEngine(allPsmsArrays, listOfSortedms2Scans, coisolationIndex, peptideIndex, fragmentIndexDict, precursorIndexDict, 0, CommonParameters, null, variableModifications, massDiffAcceptor, SearchParameters.MaximumMassThatFragmentIonScoreIsDoubled, new List<string>());
            searchResults = engine.Run();
            allPsmsArray[0].ResolveAllAmbiguities();
            //Check that there is a modification hanging out on the protein n-terminus
            Assert.That(allPsmsArray[0].FullSequence, Is.EqualTo(guiltyPwsm.FullSequence));

            proteinList = new List<Protein> { new Protein("GGGGGCDQPKLLGIETPLPKKEGG", null) };
            indexEngine = new IndexingEngine(proteinList, variableModifications, fixedModifications, null, null, null, 1, DecoyType.None, CommonParameters,
                null, SearchParameters.MaxFragmentSize, true, new List<FileInfo>(), TargetContaminantAmbiguity.RemoveContaminant, new List<string>());
            indexResults = (IndexingResults)indexEngine.Run();
            peptideIndex = indexResults.PeptideIndex;
            fragmentIndexDict = indexResults.FragmentIndex;
            precursorIndexDict = indexResults.PrecursorIndex;
            allPsmsArrays[2] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            allPsmsArray = allPsmsArrays[2];
            engine = new NonSpecificEnzymeSearchEngine(allPsmsArrays, listOfSortedms2Scans, coisolationIndex, peptideIndex, fragmentIndexDict, precursorIndexDict, 0, CommonParameters, null, variableModifications, massDiffAcceptor, SearchParameters.MaximumMassThatFragmentIonScoreIsDoubled, new List<string>());
            searchResults = engine.Run();
            allPsmsArray[0].ResolveAllAmbiguities();
            //Check that there is a modification hanging out on the peptide n-terminus
            Assert.That(allPsmsArray[0].FullSequence, Is.EqualTo(guiltyPwsm.FullSequence));
        }

        [Test]
        public static void TestNonSpecificEnzymeSearchEngineSingleNModifications()
        {
            SearchParameters SearchParameters = new SearchParameters
            {
                SearchTarget = true,
                MassDiffAcceptorType = MassDiffAcceptorType.Exact,
                LocalFdrCategories = new List<FdrCategory>
                {
                    FdrCategory.NonSpecific
                }
            };
            DigestionParams dp = new DigestionParams("singleN", minPeptideLength: 1, fragmentationTerminus: FragmentationTerminus.N, searchModeType: CleavageSpecificity.None);

            CommonParameters CommonParameters = new CommonParameters(
                dissociationType: DissociationType.HCD,
                digestionParams: dp,
                scoreCutoff: 5,
                precursorMassTolerance: new PpmTolerance(5),
                addCompIons: true);

            PeptideWithSetModifications guiltyPwsm = new PeptideWithSetModifications("DQPKLLGIETPLPKKE", null);
            var fragments = new List<Product>();
            guiltyPwsm.Fragment(CommonParameters.DissociationType, FragmentationTerminus.Both, fragments);

            var myMsDataFile = new TestDataFile(guiltyPwsm.MonoisotopicMass, fragments.Select(x => x.NeutralMass.ToMz(1)).ToArray());
            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();
            ModificationMotif.TryGetMotif("C", out ModificationMotif motif2);
            Modification mod2 = new Modification(_originalId: "Carbamidomethyl on C", _modificationType: "Common Fixed", _target: motif2, _locationRestriction: "Anywhere.", _monoisotopicMass: 57.02146372068994);
            fixedModifications.Add(mod2);

            var proteinList = new List<Protein> { new Protein("GGDQPKLLGIETPLPKKECGGGGG", null) };

            var indexEngine = new IndexingEngine(proteinList, variableModifications, fixedModifications, null, null, null, 1, DecoyType.None, CommonParameters,
                null, SearchParameters.MaxFragmentSize, true, new List<FileInfo>(), TargetContaminantAmbiguity.RemoveContaminant, new List<string>());

            var indexResults = (IndexingResults)indexEngine.Run();
            var peptideIndex = indexResults.PeptideIndex;
            var fragmentIndexDict = indexResults.FragmentIndex;
            var precursorIndexDict = indexResults.PrecursorIndex;

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();
            MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(CommonParameters.PrecursorMassTolerance, SearchParameters.MassDiffAcceptorType, SearchParameters.CustomMdac);

            SpectralMatch[][] allPsmsArrays = new PeptideSpectralMatch[3][];
            allPsmsArrays[0] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            allPsmsArrays[1] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            allPsmsArrays[2] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            SpectralMatch[] allPsmsArray = allPsmsArrays[2];

            //coisolation index
            List<int>[] coisolationIndex = new List<int>[listOfSortedms2Scans.Length];
            for (int i = 0; i < listOfSortedms2Scans.Length; i++)
            {
                coisolationIndex[i] = new List<int> { i };
            }
            var engine = new NonSpecificEnzymeSearchEngine(allPsmsArrays, listOfSortedms2Scans, coisolationIndex, peptideIndex, fragmentIndexDict, precursorIndexDict, 0, CommonParameters, null, variableModifications, massDiffAcceptor, SearchParameters.MaximumMassThatFragmentIonScoreIsDoubled, new List<string>());
            var searchResults = engine.Run();

            allPsmsArray[0].ResolveAllAmbiguities();
            //Check that there is no modification hanging out on the n-terminus
            Assert.That(allPsmsArray[0].FullSequence, Is.EqualTo(guiltyPwsm.FullSequence));

            proteinList = new List<Protein> { new Protein("GGGGGDQPKLLGIETPLPKKEC", null) };
            guiltyPwsm = new PeptideWithSetModifications("GGDQPKLLGIETPLPKKEC[Common Fixed:Carbamidomethyl on C]", new Dictionary<string, Modification> { { "Carbamidomethyl on C", mod2 } });
            guiltyPwsm.Fragment(CommonParameters.DissociationType, FragmentationTerminus.Both, fragments);
            myMsDataFile = new TestDataFile(guiltyPwsm.MonoisotopicMass, fragments.Select(x => x.NeutralMass.ToMz(1)).ToArray());
            indexEngine = new IndexingEngine(proteinList, variableModifications, fixedModifications, null, null, null, 1, DecoyType.None, CommonParameters,
                null, SearchParameters.MaxFragmentSize, true, new List<FileInfo>(), TargetContaminantAmbiguity.RemoveContaminant, new List<string>());
            indexResults = (IndexingResults)indexEngine.Run();
            precursorIndexDict = indexResults.PrecursorIndex;
            peptideIndex = indexResults.PeptideIndex;
            fragmentIndexDict = indexResults.FragmentIndex;
            listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();

            allPsmsArrays[2] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            allPsmsArray = allPsmsArrays[2];

            //coisolation index
            coisolationIndex = new List<int>[listOfSortedms2Scans.Length];
            for (int i = 0; i < listOfSortedms2Scans.Length; i++)
            {
                coisolationIndex[i] = new List<int> { i };
            }
            engine = new NonSpecificEnzymeSearchEngine(allPsmsArrays, listOfSortedms2Scans, coisolationIndex, peptideIndex, fragmentIndexDict, precursorIndexDict, 0, CommonParameters, null, variableModifications, massDiffAcceptor, SearchParameters.MaximumMassThatFragmentIonScoreIsDoubled, new List<string>());
            searchResults = engine.Run();
            allPsmsArray[0].ResolveAllAmbiguities();
            //Check that there is a modification hanging out on the protein n-terminus
            Assert.That(allPsmsArray[0].FullSequence, Is.EqualTo(guiltyPwsm.FullSequence));

            proteinList = new List<Protein> { new Protein("GGDQPKLLGIETPLPKKECGGGGG", null) };
            indexEngine = new IndexingEngine(proteinList, variableModifications, fixedModifications, null, null, null, 1, DecoyType.None, CommonParameters,
                null, SearchParameters.MaxFragmentSize, true, new List<FileInfo>(), TargetContaminantAmbiguity.RemoveContaminant, new List<string>());
            indexResults = (IndexingResults)indexEngine.Run();
            peptideIndex = indexResults.PeptideIndex;
            fragmentIndexDict = indexResults.FragmentIndex;
            precursorIndexDict = indexResults.PrecursorIndex;
            allPsmsArrays[2] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            allPsmsArray = allPsmsArrays[2];
            engine = new NonSpecificEnzymeSearchEngine(allPsmsArrays, listOfSortedms2Scans, coisolationIndex, peptideIndex, fragmentIndexDict, precursorIndexDict, 0, CommonParameters, null, variableModifications, massDiffAcceptor, SearchParameters.MaximumMassThatFragmentIonScoreIsDoubled, new List<string>());
            searchResults = engine.Run();
            allPsmsArray[0].ResolveAllAmbiguities();
            //Check that there is a modification hanging out on the peptide n-terminus
            Assert.That(allPsmsArray[0].FullSequence, Is.EqualTo(guiltyPwsm.FullSequence));
        }

        [Test]
        public static void TestSnesFifteenMer()
        {
            //ensures peptides at the min/max extremes aren't being excluded
            Protein protein = new Protein("MACDEFGHIKLMNPQRSTVWY", "Test");
            PeptideWithSetModifications pwsm = new PeptideWithSetModifications("ACDEFGHIKLMNPQR", new Dictionary<string, Modification>());
            TestDataFile msFile = new TestDataFile(pwsm);
            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(msFile, null, new CommonParameters()).ToArray();
            SpectralMatch[][] allPsmsArrays = new PeptideSpectralMatch[3][];
            allPsmsArrays[0] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            allPsmsArrays[1] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            allPsmsArrays[2] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];

            DigestionParams dp = new DigestionParams("singleC", 14, 15, 15, 1024, InitiatorMethionineBehavior.Variable, 2, CleavageSpecificity.None, FragmentationTerminus.C);
            CommonParameters cp = new CommonParameters(digestionParams: dp);
            SearchParameters sp = new SearchParameters();
            IndexingEngine indexingEngine = new IndexingEngine(new List<Protein> { protein }, null, null, null, null, null, 1, DecoyType.None, cp,
                null, 4500, true, new List<FileInfo>(), TargetContaminantAmbiguity.RemoveContaminant, new List<string>());
            IndexingResults indexingResults = (IndexingResults)indexingEngine.Run();

            MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(cp.PrecursorMassTolerance, sp.MassDiffAcceptorType, sp.CustomMdac);

            //coisolation index
            List<int>[] coisolationIndex = new List<int>[listOfSortedms2Scans.Length];
            for (int i = 0; i < listOfSortedms2Scans.Length; i++)
            {
                coisolationIndex[i] = new List<int> { i };
            }
            NonSpecificEnzymeSearchEngine searchEngine = new NonSpecificEnzymeSearchEngine(allPsmsArrays, listOfSortedms2Scans, coisolationIndex, indexingResults.PeptideIndex, indexingResults.FragmentIndex, indexingResults.PrecursorIndex, 1, cp, null, null, massDiffAcceptor, 0, new List<string>());

            searchEngine.Run();
            Assert.That(allPsmsArrays[2][0] != null);
        }

        [Test]
        public static void TestNonSpecificEnzymeSearchEngineSingleC()
        {
            SearchParameters SearchParameters = new SearchParameters
            {
                SearchTarget = true,
                MassDiffAcceptorType = MassDiffAcceptorType.Exact,
                LocalFdrCategories = new List<FdrCategory>
                {
                    FdrCategory.FullySpecific,
                    FdrCategory.SemiSpecific,
                    FdrCategory.NonSpecific
                }
            };

            List<DigestionMotif> motifs = new List<DigestionMotif>
            {
                new DigestionMotif("K", null, 0, null),
                new DigestionMotif("K", null, 1, null),
                new DigestionMotif("G", null, 0, null),
                new DigestionMotif("G", null, 1, null),
            };

            DigestionParams dp = new DigestionParams("singleC", minPeptideLength: 1, fragmentationTerminus: FragmentationTerminus.C, searchModeType: CleavageSpecificity.None);

            CommonParameters CommonParameters = new CommonParameters(
                dissociationType: DissociationType.HCD,
                digestionParams: dp,
                scoreCutoff: 4,
                precursorMassTolerance: new PpmTolerance(5),
                addCompIons: true);

            var myMsDataFile = new TestDataFile("Yes, I'd like one slightly larger please");
            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();
            var localizeableModifications = new List<Modification>();

            var proteinList = new List<Protein> { new Protein("GGGGGMNNNKQQQGGGGG", null) };

            var indexEngine = new IndexingEngine(proteinList, variableModifications, fixedModifications, null, null, null, 1, DecoyType.Reverse,
                CommonParameters, null, SearchParameters.MaxFragmentSize, false, new List<FileInfo>(), TargetContaminantAmbiguity.RemoveContaminant, new List<string>());

            var indexResults = (IndexingResults)indexEngine.Run();
            var peptideIndex = indexResults.PeptideIndex;
            var fragmentIndexDict = indexResults.FragmentIndex;

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();
            MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(CommonParameters.PrecursorMassTolerance, SearchParameters.MassDiffAcceptorType, SearchParameters.CustomMdac);

            SpectralMatch[][] allPsmsArrays = new PeptideSpectralMatch[3][];
            allPsmsArrays[0] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            allPsmsArrays[1] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            allPsmsArrays[2] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            SpectralMatch[] allPsmsArray = allPsmsArrays[2];

            //coisolation index
            List<int>[] coisolationIndex = new List<int>[listOfSortedms2Scans.Length];
            for (int i = 0; i < listOfSortedms2Scans.Length; i++)
            {
                coisolationIndex[i] = new List<int> { i };
            }
            var engine = new NonSpecificEnzymeSearchEngine(allPsmsArrays, listOfSortedms2Scans, coisolationIndex, peptideIndex, fragmentIndexDict, fragmentIndexDict, 0, CommonParameters, null, variableModifications, massDiffAcceptor, SearchParameters.MaximumMassThatFragmentIonScoreIsDoubled, new List<string>());
            var searchResults = engine.Run();

            // Single search mode
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));

            //Single ms2 scan
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));
            Assert.That(allPsmsArray[0] != null);

            Assert.That(allPsmsArray[0].Score > 7);
            Assert.That(allPsmsArray[0].ScanNumber, Is.EqualTo(2));
            allPsmsArray[0].ResolveAllAmbiguities();
            Assert.That(allPsmsArray[0].BaseSequence, Is.EqualTo("QQQGGGG"));
        }

        [Test]
        public static void TestNonSpecificEnzymeVariableModificationHandlingNTerm()
        {
            var protein = new Protein("MGGGGGMNNNKQQQMGGGGMGM", "TestProtein");

            List<DigestionMotif> motifs = new List<DigestionMotif>
            {
                new DigestionMotif("K", null, 0, null),
                new DigestionMotif("K", null, 1, null),
                new DigestionMotif("G", null, 0, null),
                new DigestionMotif("G", null, 1, null),
                new DigestionMotif("M", null, 0, null),
                new DigestionMotif("M", null, 1, null),
                new DigestionMotif("N", null, 0, null),
                new DigestionMotif("N", null, 1, null),
                new DigestionMotif("Q", null, 0, null),
                new DigestionMotif("Q", null, 1, null),
            };
            var protease = new Protease("singleN2", CleavageSpecificity.SingleN, null, null, motifs);
            ProteaseDictionary.Dictionary.Add(protease.Name, protease);
            ModificationMotif.TryGetMotif("M", out ModificationMotif motifM);
            var variableModifications = new List<Modification> { new Modification(_originalId: "16", _target: motifM, _locationRestriction: "Anywhere.", _monoisotopicMass: 15.994915) };
            DigestionParams digestionParams = new DigestionParams(protease: protease.Name, minPeptideLength: 5, maxModsForPeptides: 3);
            var ListOfModifiedPeptides = protein.Digest(digestionParams, new List<Modification>(), variableModifications).ToList();
            Assert.That(ListOfModifiedPeptides.Count, Is.EqualTo(192));

            var protein2 = new Protein(new string("MGGGGGMNNNKQQQMGGGGMGM".ToCharArray().Reverse().ToArray()), "TestProtein");
            var ListOfModifiedPeptides2 = protein2.Digest(digestionParams, new List<Modification>(), variableModifications).ToList();
            Assert.That(ListOfModifiedPeptides2.Count, Is.EqualTo(132));
        }

        [Test]
        public static void TestNonSpecificEnzymeVariableModificationHandlingCTerm()
        {
            var protein = new Protein("MGGGGGMNNNKQQQMGGGGMGM", "TestProtein");
            List<DigestionMotif> motifs = new List<DigestionMotif>
            {
                new DigestionMotif("K", null, 0, null),
                new DigestionMotif("K", null, 1, null),
                new DigestionMotif("G", null, 0, null),
                new DigestionMotif("G", null, 1, null),
                new DigestionMotif("M", null, 0, null),
                new DigestionMotif("M", null, 1, null),
                new DigestionMotif("N", null, 0, null),
                new DigestionMotif("N", null, 1, null),
                new DigestionMotif("Q", null, 0, null),
                new DigestionMotif("Q", null, 1, null),
            };
            var protease = new Protease("singleC2", CleavageSpecificity.SingleC, null, null, motifs);
            ProteaseDictionary.Dictionary.Add(protease.Name, protease);
            ModificationMotif.TryGetMotif("M", out ModificationMotif motifM);
            var variableModifications = new List<Modification> { new Modification(_originalId: "16", _target: motifM, _locationRestriction: "Anywhere.", _monoisotopicMass: 15.994915) };
            DigestionParams digestionParams = new DigestionParams(protease: protease.Name, minPeptideLength: 5, maxModsForPeptides: 3);
            var ListOfModifiedPeptides = protein.Digest(digestionParams, new List<Modification>(), variableModifications).ToList();
            Assert.That(ListOfModifiedPeptides.Count, Is.EqualTo(132));

            var protein2 = new Protein(new string("MGGGGGMNNNKQQQMGGGGMGM".ToCharArray().Reverse().ToArray()), "TestProtein");
            var ListOfModifiedPeptides2 = protein2.Digest(digestionParams, new List<Modification>(), variableModifications).ToList();
            Assert.That(ListOfModifiedPeptides2.Count, Is.EqualTo(192));
        }

        [Test]
        public static void TestSemiSpecificEnzymeEngineSingleN()
        {
            var myMsDataFile = new TestDataFile("Yes, I'd like one slightly larger please");
            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();
            var localizeableModifications = new List<Modification>();
            Dictionary<Modification, ushort> modsDictionary = new Dictionary<Modification, ushort>();
            foreach (var mod in fixedModifications)
            {
                modsDictionary.Add(mod, 0);
            }

            int ii = 1;
            foreach (var mod in variableModifications)
            {
                modsDictionary.Add(mod, (ushort)ii);
                ii++;
            }
            foreach (var mod in localizeableModifications)
            {
                modsDictionary.Add(mod, (ushort)ii);
                ii++;
            }

            var proteinList = new List<Protein> { new Protein("GGGGGMNNNKQQQGGGGGGKKRKG", "TestProtein") };

            var productMassTolerance = new AbsoluteTolerance(0.01);
            var searchModes = new SinglePpmAroundZeroSearchMode(5);
            List<DigestionMotif> motifs = new List<DigestionMotif>
            {
                new DigestionMotif("K", null, 1, null),
            };
            var protease = new Protease("SingleN", CleavageSpecificity.None, null, null, motifs);
            ProteaseDictionary.Dictionary.Add(protease.Name, protease);
            CommonParameters CommonParameters = new CommonParameters(
                dissociationType: DissociationType.HCD,
                productMassTolerance: productMassTolerance,
                digestionParams: new DigestionParams(protease: protease.Name, minPeptideLength: 5, maxModsForPeptides: 2, fragmentationTerminus: FragmentationTerminus.N, searchModeType: CleavageSpecificity.Semi),
                scoreCutoff: 2,
                addCompIons: true);

            var digestParams = new HashSet<IDigestionParams> { CommonParameters.DigestionParams };
            var indexEngine = new IndexingEngine(proteinList, variableModifications, fixedModifications, null, null, null, 1, DecoyType.Reverse,
                CommonParameters, null, 100000, true, new List<FileInfo>(), TargetContaminantAmbiguity.RemoveContaminant, new List<string>());
            var indexResults = (IndexingResults)indexEngine.Run();
            var peptideIndex = indexResults.PeptideIndex;
            var fragmentIndexDict = indexResults.FragmentIndex;
            var precursorIndexDict = indexResults.PrecursorIndex;

            Tolerance DeconvolutionMassTolerance = new PpmTolerance(5);

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();

            SpectralMatch[][] allPsmsArrays = new PeptideSpectralMatch[2][];
            allPsmsArrays[0] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            allPsmsArrays[1] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            SpectralMatch[] allPsmsArray = allPsmsArrays[1];

            //coisolation index
            List<int>[] coisolationIndex = new List<int>[listOfSortedms2Scans.Length];
            for (int i = 0; i < listOfSortedms2Scans.Length; i++)
            {
                coisolationIndex[i] = new List<int> { i };
            }
            var engine = new NonSpecificEnzymeSearchEngine(allPsmsArrays, listOfSortedms2Scans, coisolationIndex, peptideIndex, fragmentIndexDict, precursorIndexDict, 1, CommonParameters, null, variableModifications, searchModes, 0, new List<string>());
            var searchResults = engine.Run();

            // Single search mode
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));

            // Single ms2 scan
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));

            Assert.That(allPsmsArray[0].Score > 4);
            Assert.That(allPsmsArray[0].ScanNumber, Is.EqualTo(2));
            allPsmsArray[0].ResolveAllAmbiguities();
            Assert.That(allPsmsArray[0].BaseSequence, Is.EqualTo("QQQGGGG"));
        }

        [Test]
        public static void TestSemiSpecificEnzymeEngineSingleC()
        {
            var myMsDataFile = new TestDataFile("Yes, I'd like one slightly larger please");
            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();
            var localizeableModifications = new List<Modification>();
            Dictionary<Modification, ushort> modsDictionary = new Dictionary<Modification, ushort>();
            foreach (var mod in fixedModifications)
            {
                modsDictionary.Add(mod, 0);
            }
            int ii = 1;
            foreach (var mod in variableModifications)
            {
                modsDictionary.Add(mod, (ushort)ii);
                ii++;
            }
            foreach (var mod in localizeableModifications)
            {
                modsDictionary.Add(mod, (ushort)ii);
                ii++;
            }

            var proteinList = new List<Protein> { new Protein("GGGGGMKNNNQQQGGGGKGG", null, null, null, null, new List<TruncationProduct> { new TruncationProduct(null, null, "test") }) };

            var productMassTolerance = new AbsoluteTolerance(0.01);
            var searchModes = new SinglePpmAroundZeroSearchMode(5);
            Protease protease = new Protease("SingleC", CleavageSpecificity.Full, null, null, new List<DigestionMotif>());
            ProteaseDictionary.Dictionary.Add(protease.Name, protease);
            CommonParameters CommonParameters = new CommonParameters(
                scoreCutoff: 1,
                productMassTolerance: productMassTolerance,
                digestionParams: new DigestionParams(protease: protease.Name, maxMissedCleavages: 5, minPeptideLength: 5, searchModeType: CleavageSpecificity.None, fragmentationTerminus: FragmentationTerminus.C),
                dissociationType: DissociationType.HCD,
                addCompIons: true);

            var digestParams = new HashSet<IDigestionParams> { CommonParameters.DigestionParams };
            var indexEngine = new IndexingEngine(proteinList, variableModifications, fixedModifications, null, null, null, 1, DecoyType.Reverse,
                CommonParameters, null, 30000, false, new List<FileInfo>(), TargetContaminantAmbiguity.RemoveContaminant, new List<string>());
            var indexResults = (IndexingResults)indexEngine.Run();
            var peptideIndex = indexResults.PeptideIndex;
            var fragmentIndexDict = indexResults.FragmentIndex;

            Tolerance DeconvolutionMassTolerance = new PpmTolerance(5);

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();

            SpectralMatch[][] allPsmsArrays = new PeptideSpectralMatch[3][];
            allPsmsArrays[0] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            allPsmsArrays[1] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            allPsmsArrays[2] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            SpectralMatch[] allPsmsArray = allPsmsArrays[2];

            //coisolation index
            List<int>[] coisolationIndex = new List<int>[listOfSortedms2Scans.Length];
            for (int i = 0; i < listOfSortedms2Scans.Length; i++)
            {
                coisolationIndex[i] = new List<int> { i };
            }
            var engine = new NonSpecificEnzymeSearchEngine(allPsmsArrays, listOfSortedms2Scans, coisolationIndex, peptideIndex, fragmentIndexDict, fragmentIndexDict, 1, CommonParameters, null, variableModifications, searchModes, 0, new List<string>());
            var searchResults = engine.Run();

            // Single search mode
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));

            // Single ms2 scan
            Assert.That(allPsmsArray.Length, Is.EqualTo(1));

            Assert.That(allPsmsArray[0].Score > 4);
            Assert.That(allPsmsArray[0].ScanNumber, Is.EqualTo(2));
            allPsmsArray[0].ResolveAllAmbiguities();
            Assert.That(allPsmsArray[0].BaseSequence, Is.EqualTo("QQQGGGG"));
        }

        [Test]
        public static void TestClassicSemiProtease()
        {
            var myMsDataFile = new TestDataFile("Yes, I'd like one slightly larger please");
            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();
            var localizeableModifications = new List<Modification>();
            Dictionary<Modification, ushort> modsDictionary = new Dictionary<Modification, ushort>();
            foreach (var mod in fixedModifications)
            {
                modsDictionary.Add(mod, 0);
            }

            int ii = 1;
            foreach (var mod in variableModifications)
            {
                modsDictionary.Add(mod, (ushort)ii);
                ii++;
            }
            foreach (var mod in localizeableModifications)
            {
                modsDictionary.Add(mod, (ushort)ii);
                ii++;
            }

            var proteinList = new List<Protein> { new Protein("MGGGGGMKNNNQQQGGGGKGKKNKKGN", "hello") };

            var productMassTolerance = new AbsoluteTolerance(0.01);
            var searchModes = new SinglePpmAroundZeroSearchMode(5);

            List<DigestionMotif> motifs1 = new List<DigestionMotif>
            {
                new DigestionMotif("G", null, 1, null),
            };
            List<DigestionMotif> motifs2 = new List<DigestionMotif>
            {
                new DigestionMotif("N", null, 1, null),
            };

            var protease = new Protease("semi-trypsin1", CleavageSpecificity.Semi, null, null, motifs1);
            var protease2 = new Protease("semi-trypsin2", CleavageSpecificity.Semi, null, null, motifs2);

            ProteaseDictionary.Dictionary.Add(protease.Name, protease);
            ProteaseDictionary.Dictionary.Add(protease2.Name, protease2);
            CommonParameters CommonParameters = new CommonParameters(
                productMassTolerance: productMassTolerance,
                digestionParams: new DigestionParams(protease: protease.Name, maxMissedCleavages: 5)
                );

            Tolerance DeconvolutionMassTolerance = new PpmTolerance(5);

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();

            SpectralMatch[] allPsmsArray = new PeptideSpectralMatch[listOfSortedms2Scans.Length];

            bool writeSpectralLibrary = false;
            new ClassicSearchEngine(allPsmsArray, listOfSortedms2Scans, variableModifications, fixedModifications, null, null, null, 
                proteinList, searchModes, CommonParameters, null, null, new List<string>(), writeSpectralLibrary).Run();

            //////////////////////////////

            CommonParameters CommonParameters2 = new CommonParameters
            (
                productMassTolerance: productMassTolerance,
                digestionParams: new DigestionParams(protease: protease2.Name, maxMissedCleavages: 5)
            );

            var digestParams2 = new HashSet<IDigestionParams> { CommonParameters2.DigestionParams };

            Tolerance DeconvolutionMassTolerance2 = new PpmTolerance(5);

            var listOfSortedms2Scans2 = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();

            SpectralMatch[] allPsmsArray2 = new PeptideSpectralMatch[listOfSortedms2Scans.Length];

            new ClassicSearchEngine(allPsmsArray2, listOfSortedms2Scans2, variableModifications, fixedModifications, null, null, null, 
                proteinList, searchModes, CommonParameters2, null, null, new List<string>(), writeSpectralLibrary).Run();
            // Single search mode
            Assert.That(allPsmsArray2.Length, Is.EqualTo(1));
            Assert.That(allPsmsArray.Length, Is.EqualTo(allPsmsArray2.Length));

            // Single ms2 scan
            Assert.That(allPsmsArray2.Length, Is.EqualTo(1));
            Assert.That(allPsmsArray.Length, Is.EqualTo(allPsmsArray2.Length));

            Assert.That(allPsmsArray2[0].Score > 4);
            Assert.That(allPsmsArray[0].Score > 4);
            Assert.That(allPsmsArray2[0].ScanNumber, Is.EqualTo(2));
            Assert.That(allPsmsArray[0].ScanNumber, Is.EqualTo(allPsmsArray2[0].ScanNumber));

            Assert.That(allPsmsArray2[0].BaseSequence, Is.EqualTo("QQQGGGG"));
            Assert.That(allPsmsArray[0].BaseSequence, Is.EqualTo(allPsmsArray2[0].BaseSequence));
        }

        [Test]
        public static void TestClassicSemiProteolysis()
        {
            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();
            var localizeableModifications = new List<Modification>();
            Dictionary<Modification, ushort> modsDictionary = new Dictionary<Modification, ushort>();
            foreach (var mod in fixedModifications)
            {
                modsDictionary.Add(mod, 0);
            }
            int ii = 1;
            foreach (var mod in variableModifications)
            {
                modsDictionary.Add(mod, (ushort)ii);
                ii++;
            }
            foreach (var mod in localizeableModifications)
            {
                modsDictionary.Add(mod, (ushort)ii);
                ii++;
            }
            List<TruncationProduct> protprod = new List<TruncationProduct> { new TruncationProduct(9, 21, "chain") };
            var proteinList = new List<Protein> { new Protein("MGGGGGMKNNNQQQGGGGKLKGKKNKKGN", "hello", null, null, null, protprod) };

            List<DigestionMotif> motifs1 = new List<DigestionMotif>
            {
                new DigestionMotif("G", null, 1, null),
            };
            var protease = new Protease("semi-Trypsin", CleavageSpecificity.Semi, null, null, motifs1);
            ProteaseDictionary.Dictionary.Add(protease.Name, protease);
            DigestionParams digestParams = new DigestionParams(protease: protease.Name, minPeptideLength: 2);

            //expect NNNQQQ, NNNQQ, NNNQ, NNN, NN and LK, KLK
            Dictionary<string, bool> found = new Dictionary<string, bool>
            {
                {"NNNQQQ", false},
                {"NNNQQ", false} ,
                {"NNNQ", false},
                {"NNN", false},
                {"NN", false},
                {"LK", false},
                {"KLK", false}
            };
            IEnumerable<PeptideWithSetModifications> PWSMs = proteinList[0].Digest(digestParams, new List<Modification>(), modsDictionary.Keys.ToList());
            foreach (PeptideWithSetModifications PWSM in PWSMs)
            {
                if (found.TryGetValue(PWSM.BaseSequence, out bool b))
                {
                    found[PWSM.BaseSequence] = true;
                }
            }
            foreach (KeyValuePair<string, bool> kvp in found)
            {
                Assert.That(kvp.Value);
            }
        }

        [Test]
        public static void TestClassicSearchOneNterminalModifiedPeptideOneScan()
        {
            //Create the modification and select the motif
            ModificationMotif.TryGetMotif("A", out ModificationMotif motif);
            Modification mod = new Modification(_originalId: "acetylation", _modificationType: "testModType", _target: motif, _chemicalFormula: ChemicalFormula.ParseFormula("C2H2O1"), _locationRestriction: "Anywhere.");

            //Create the protein and add the mod
            List<Modification> modlist = new List<Modification> { mod };
            int modPositionInProtein = 1;
            var proteinListForSearch = new Protein("AAAGSAAVSGAGTPVAGPTGR", null, oneBasedModifications: new Dictionary<int, List<Modification>> { { modPositionInProtein, modlist } });
            List<Protein> proteins = new List<Protein> { proteinListForSearch };

            //Prepare the scan
            double[] mz = { 101.814659, 101.851852, 101.856758, 101.920807, 101.957397, 101.993172, 102.029053, 102.035851, 102.065628, 102.102516, 102.123955, 102.129654, 102.136391, 102.144806, 102.173798, 102.182594, 102.19574, 102.201569, 102.208534, 102.227333, 102.231323, 102.237877, 102.266922, 110.071281, 124.284775, 125.057686, 129.101776, 136.075348, 141.059647, 141.067139, 147.112045, 152.296143, 157.095963, 159.321686, 159.391266, 159.461609, 159.505859, 159.530792, 159.567841, 159.673218, 159.688232, 159.708939, 159.717117, 159.744415, 159.883804, 169.132446, 175.118637, 175.639145, 185.091217, 185.629135, 185.649612, 198.086334, 202.961533, 225.122726, 230.112839, 232.139282, 247.539169, 256.128204, 257.129761, 269.123596, 283.467743, 287.133331, 304.127319, 306.832855, 307.01059, 307.281647, 313.149384, 314.151093, 329.183044, 358.160583, 360.700592, 365.218567, 370.208008, 372.185425, 373.445496, 382.170685, 383.1745, 400.180878, 401.178406, 415.016876, 425.206726, 430.238647, 431.253052, 438.198273, 439.226074, 442.213654, 453.207336, 454.210938, 467.186005, 470.226654, 471.218048, 472.219635, 487.260254, 488.259033, 498.980499, 524.244202, 525.244385, 528.277832, 541.27124, 542.254883, 543.258118, 558.296448, 559.29895, 602.023743, 637.564087, 638.122498, 638.68103, 639.24469, 641.322876, 642.328674, 651.320923, 665.653137, 665.812622, 678.225586, 678.837524, 684.52533, 699.105225, 702.819702, 710.320374, 718.544617, 731.125793, 731.804626, 732.479858, 732.635559, 745.458618, 754.418152, 755.42041, 770.024719, 835.083557, 855.465454, 883.47406, 884.485962, 885.430664, 894.471252, 912.484192, 913.487305, 983.525391, 984.515137, 1022.541321, 1040.545166, 1041.545166, 1109.568115, 1110.565918, 1122.717896, 1127.574707, 1128.582764, 1143.576172, 1192.367432, 1209.613403, 1226.649048, 1227.651123, 1255.27356, 1270.274414, 1273.796753, 1297.668091, 1298.676636, 1520.323364, 1541.422729, 1546.209839, 1639.540283, 1669.039673, 1670.780518, 1671.45459, 1671.927368, 1672.41272, 1673.799194, 1674.881836, 1687.070557 };
            double[] intensities = { 2084.605469, 1503.049316, 1508.572144, 1997.866089, 3907.81958, 3168.024902, 2187.750488, 2006.690186, 1900.748047, 2346.96875, 5075.943359, 1605.881958, 3754.241699, 2652.021484, 2389.109375, 1921.068115, 7624.829102, 2316.127441, 4926.003906, 4061.111328, 4849.558105, 1925.951172, 2153.752441, 1949.42981, 2058.943604, 1687.470215, 1928.76416, 4824.449219, 1376.267334, 1439.787109, 2010.894897, 1483.543213, 1929.985596, 3110.926025, 3372.582275, 2304.003174, 2679.970215, 2560.099854, 1692.449585, 4134.439453, 3104.67627, 3688.150146, 3676.4646, 2814.727051, 1772.865723, 2332.302002, 4913.470703, 1477.579102, 19578.2793, 2073.369141, 2618.062988, 4346.097168, 1499.773193, 1665.242065, 2426.635742, 7056.563477, 1847.738281, 34847.85547, 2724.011963, 6279.092773, 1623.552734, 6072.998535, 3376.6521, 1946.103149, 2381.508301, 1723.251953, 43135.01953, 2463.636719, 2124.577393, 3385.583008, 1918.600098, 3191.296387, 1865.107422, 3907.073975, 2091.009766, 47135.79688, 5113.383301, 19901.54883, 1716.9729, 1691.421753, 2594.325928, 6075.093262, 1948.982544, 1560.634033, 2563.348145, 1857.768311, 96429.07813, 12289.67578, 2152.950928, 1781.416992, 49654.84766, 7577.02832, 19761.38867, 3075.865967, 2130.962158, 6758.932617, 2022.507935, 4839.808105, 2863.790039, 67360.53906, 11148.44824, 23283.18945, 4307.828125, 1818.466187, 4734.982422, 2687.210449, 4251.597168, 3365.632813, 14722.28027, 3734.265869, 1774.239624, 1827.377075, 2492.476074, 9175.493164, 2313.220215, 2536.213867, 2223.19165, 2145.397461, 2189.492676, 3675.263184, 2770.946289, 4485.819336, 2932.245605, 1713.99939, 1857.43042, 45807.97266, 10789.84961, 1848.404297, 1901.754517, 10514.50977, 14093.94141, 5819.866699, 6942.87207, 3736.169189, 34573.74219, 11260.63867, 13557.64746, 3463.163818, 7701.758301, 39518.83203, 15681.95898, 13644.61426, 7358.266602, 5733.412598, 46497.80469, 20523.03516, 1688.138916, 1865.104248, 2219.646729, 9009.290039, 4519.379395, 2556.661621, 1627.402466, 2145.759766, 4486.545898, 3421.69873, 1836.025391, 1980.848999, 2378.1521, 5128.462402, 2796.475586, 2296.582764, 8305.148438, 9054.726563, 6919.573242, 3180.789063, 2163.666504, 1787.661133 };

            MsDataScan[] msDataScanArray = new MsDataScan[1];
            msDataScanArray[0] = new MsDataScan(massSpectrum: new MzSpectrum(mz, intensities, false), oneBasedScanNumber: 1, msnOrder: 1, isCentroid: true,
                    polarity: Polarity.Positive, retentionTime: 10.0, scanWindowRange: new MzRange(400, 1600), scanFilter: "f",
                    mzAnalyzer: MZAnalyzerType.Orbitrap, totalIonCurrent: intensities.Sum(), injectionTime: 1.0, noiseData: null, nativeId: "scan=" + 1);

            Ms2ScanWithSpecificMass[] scans = new Ms2ScanWithSpecificMass[1];
            double precursorMz = 884.4541;
            int precursorCharge = 2;
            string filePath = "path";
            scans[0] = new Ms2ScanWithSpecificMass(msDataScanArray[0], precursorMz, precursorCharge, filePath, new CommonParameters());

            //set digestion parameters and digest the protein
            DigestionParams testDigestionParams = new DigestionParams(
                protease: "trypsin",
                maxMissedCleavages: 0,
                minPeptideLength: 1,
                initiatorMethionineBehavior: InitiatorMethionineBehavior.Retain);

            //set the common parameters
            Tolerance productTolerance = new PpmTolerance(20);
            Tolerance precursorTolerance = new PpmTolerance(10);
            DissociationType d = DissociationType.CID;
            CommonParameters commonParameters = new CommonParameters(dissociationType: d, precursorMassTolerance: precursorTolerance, productMassTolerance: productTolerance, digestionParams: testDigestionParams);

            //Search the scan against the protein
            SpectralMatch[] allPsmsArray = new PeptideSpectralMatch[1];

            bool writeSpectralLibrary = false;
            new ClassicSearchEngine(allPsmsArray, scans, new List<Modification>(), new List<Modification>(), null, null, null, 
                proteins, new SinglePpmAroundZeroSearchMode(20), new CommonParameters(dissociationType: DissociationType.CID), null, null, new List<string>(), writeSpectralLibrary).Run();

            //Process the results
            List<MatchedFragmentIon> matchedFragmentIonList = allPsmsArray.SelectMany(p => p.MatchedFragmentIons).ToList();
            List<double> neutralFragmentMasses = matchedFragmentIonList.Select(m => m.NeutralTheoreticalProduct.NeutralMass).ToList();
            List<double> massToCharges = new List<double>();
            foreach (double mass in neutralFragmentMasses)
            {
                massToCharges.Add(mass.ToMz(1));
            }
            Assert.That(massToCharges.Count(), Is.EqualTo(20));
        }

        [Test]
        public static void TestFileSpecificUpdateMaintainsSpecificProteaseForNonSpecificEnzymeSearches()
        {
            CommonParameters commonParams = new CommonParameters(digestionParams: new DigestionParams(protease: "Arg-C", searchModeType: CleavageSpecificity.None));
            FileSpecificParameters fileSpecificParams = new FileSpecificParameters();
            CommonParameters updatedCommonParams = MetaMorpheusTask.SetAllFileSpecificCommonParams(commonParams, fileSpecificParams);
            Assert.That(((DigestionParams)updatedCommonParams.DigestionParams).SpecificProtease, Is.EqualTo(((DigestionParams)commonParams.DigestionParams).SpecificProtease));
        }

        [Test]
        public static void TestTerminalModificationsForSnes()
        {
            //peptide and ms file prep
            PeptideWithSetModifications nTermModifiedPwsm = new PeptideWithSetModifications("[Uniprot:N-acetylalanine on A]AGIAAKLAKDREAAEGLGSHA", GlobalVariables.AllModsKnownDictionary);
            PeptideWithSetModifications cTermModifiedPwsm = new PeptideWithSetModifications("AGIAAKLAKDREAAEGLGSHA[Uniprot:Alanine amide on A]", GlobalVariables.AllModsKnownDictionary);
            TestDataFile msFile = new TestDataFile(new List<IBioPolymerWithSetMods> { nTermModifiedPwsm, cTermModifiedPwsm });
            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(msFile, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();

            //params for singleN and singleC
            CommonParameters nCommonParameters = new CommonParameters(digestionParams: new DigestionParams(protease: "singleN", fragmentationTerminus: FragmentationTerminus.N, searchModeType: CleavageSpecificity.None), addCompIons: true);
            CommonParameters cCommonParameters = new CommonParameters(digestionParams: new DigestionParams(protease: "singleC", fragmentationTerminus: FragmentationTerminus.C, searchModeType: CleavageSpecificity.None), addCompIons: true);
            CommonParameters nCleaveParams = new CommonParameters(digestionParams: new DigestionParams(protease: "singleN", initiatorMethionineBehavior: InitiatorMethionineBehavior.Cleave, fragmentationTerminus: FragmentationTerminus.N, searchModeType: CleavageSpecificity.None), addCompIons: true);
            CommonParameters cCleaveParams = new CommonParameters(digestionParams: new DigestionParams(protease: "singleC", initiatorMethionineBehavior: InitiatorMethionineBehavior.Cleave, fragmentationTerminus: FragmentationTerminus.C, searchModeType: CleavageSpecificity.None), addCompIons: true);

            //params for annotated and variable mods
            List<Protein> proteinWithMods = new List<Protein> {new Protein("MAGIAAKLAKDREAAEGLGSHA", "testProtein",
                oneBasedModifications: new Dictionary<int, List<Modification>>
                {
                    { 2, new List<Modification>{GlobalVariables.AllModsKnownDictionary["N-acetylalanine on A"] } },
                    { 22, new List<Modification>{GlobalVariables.AllModsKnownDictionary["Alanine amide on A"] } }
                })
            };
            List<Protein> proteinWithoutMods = new List<Protein> { new Protein("MAGIAAKLAKDREAAEGLGSHA", "testProtein") };
            List<Modification> empty = new List<Modification>();
            List<Modification> terminalVariableMods = new List<Modification> { GlobalVariables.AllModsKnownDictionary["N-acetylalanine on A"], GlobalVariables.AllModsKnownDictionary["Alanine amide on A"] };

            List<(List<Protein> proteinList, List<Modification> variableMods)> variableParamsToTest = new List<(List<Protein> protein, List<Modification> variableMods)>
            {
                (proteinWithoutMods, terminalVariableMods),
                (proteinWithMods, empty),
                (proteinWithMods, terminalVariableMods)
            };

            //Test all params and ensure the results are the same
            string[] psmAnswer = new string[2] { "[UniProt:N-acetylalanine on A]AGIAAKLAKDREAAEGLGSHA", "AGIAAKLAKDREAAEGLGSHA[UniProt:Alanine amide on A]" };
            foreach (var termParams in variableParamsToTest)
            {
                List<CommonParameters> paramsToTest = new List<CommonParameters> { cCommonParameters, nCommonParameters, cCleaveParams, nCleaveParams };
                foreach (CommonParameters commonParams in paramsToTest)
                {
                    var digestParams = new HashSet<IDigestionParams> { commonParams.DigestionParams };
                    var indexEngine = new IndexingEngine(termParams.proteinList, termParams.variableMods, empty, null, null, null, 1, DecoyType.None,
                        commonParams, null, 100000, true, new List<FileInfo>(), TargetContaminantAmbiguity.RemoveContaminant, new List<string>());
                    var indexResults = (IndexingResults)indexEngine.Run();
                    var peptideIndex = indexResults.PeptideIndex;
                    var fragmentIndexDict = indexResults.FragmentIndex;
                    var precursorIndexDict = indexResults.PrecursorIndex;
                    SpectralMatch[][] allPsmsArrays = new PeptideSpectralMatch[3][];
                    allPsmsArrays[2] = new PeptideSpectralMatch[listOfSortedms2Scans.Length];

                    //coisolation index
                    List<int>[] coisolationIndex = new List<int>[listOfSortedms2Scans.Length];
                    for (int i = 0; i < listOfSortedms2Scans.Length; i++)
                    {
                        coisolationIndex[i] = new List<int> { i };
                    }
                    var engine = new NonSpecificEnzymeSearchEngine(allPsmsArrays, listOfSortedms2Scans, coisolationIndex, peptideIndex, fragmentIndexDict, precursorIndexDict, 1, commonParams, null, termParams.variableMods, new SinglePpmAroundZeroSearchMode(5), 0, new List<string>());
                    var searchResults = engine.Run();
                    for (int i = 0; i < listOfSortedms2Scans.Length; i++)
                    {
                        SpectralMatch testPsm = allPsmsArrays[2][i];
                        Assert.That(testPsm != null);
                        testPsm.ResolveAllAmbiguities();
                        Assert.That(psmAnswer[1 - i].Equals(testPsm.FullSequence));
                    }
                }
            }
        }

        [Test]
        public static void TestFileWithNoMs2Scans()
        {
            var scans = new MsDataScan[1];
            var spectrum = new MzSpectrum(new double[1], new double[1], false);
            scans[0] = new MsDataScan(spectrum, 1, 1, true, Polarity.Positive, 1.0, new MzRange(0, 1), "", MZAnalyzerType.Orbitrap,
                1, null, null, "");
            var fileWithNoMs2Scans = new GenericMsDataFile(scans, null);
            var ms2Scans = MetaMorpheusTask.GetMs2Scans(fileWithNoMs2Scans, null, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();
            Assert.That(!ms2Scans.Any());
        }

        //This is a code coverage unit test that mostly just checks there are no crashes in specific situations
        [Test]
        public static void TestUndefinedProteinGroup()
        {
            SearchTask task = new SearchTask
            {
                SearchParameters = new SearchParameters
                {
                    NoOneHitWonders = true
                }
            };

            PeptideWithSetModifications peptide = new PeptideWithSetModifications("PEPTIDEK", new Dictionary<string, Modification>()); //alone
            PeptideWithSetModifications peptide2 = new PeptideWithSetModifications("ANTHERPEPK", new Dictionary<string, Modification>()); //same group as peptide3
            PeptideWithSetModifications peptide3 = new PeptideWithSetModifications("LNGEEPEPK", new Dictionary<string, Modification>());

            PeptideWithSetModifications peptide4 = new PeptideWithSetModifications("SMTHERPEPSK", new Dictionary<string, Modification>()); //same group as peptide3
            PeptideWithSetModifications peptide5 = new PeptideWithSetModifications("THEPEPSK", new Dictionary<string, Modification>());
            PeptideWithSetModifications peptide6 = new PeptideWithSetModifications("LNGEEPEPKSMTHERPEPSK", new Dictionary<string, Modification>());
            PeptideWithSetModifications peptide7 = new PeptideWithSetModifications("LNGEEPEPKASWELASKTHEPEPSK", new Dictionary<string, Modification>());

            MsDataFile myMsDataFile1 = new TestDataFile(new List<IBioPolymerWithSetMods> { peptide, peptide2, peptide3, peptide4, peptide5, peptide6, peptide7 });
            string mzmlName = @"test.mzML";
            Readers.MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(myMsDataFile1, mzmlName, false);

            string xmlName = "testDb.xml";
            Protein theProtein = new Protein("PEPTIDEK", "accession1");
            Protein theProtein2 = new Protein("ANTHERPEPKLNGEEPEPKSMTHERPEPSK", "accession2");
            Protein theProtein3 = new Protein("ANTHERPEPKLNGEEPEPKASWELASKTHEPEPSK", "accession3");
            ProteinDbWriter.WriteXmlDatabase(new Dictionary<string, HashSet<Tuple<int, Modification>>>(), new List<Protein> { theProtein, theProtein2, theProtein3 }, xmlName);

            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"testFolder");
            Directory.CreateDirectory(outputFolder);
            task.RunTask(outputFolder, new List<DbForTask> { new DbForTask(xmlName, false) }, new List<string> { mzmlName }, "taskId1").ToString();

            //do it for SILAC code, too
            //make heavy residue and add to search task
            Residue heavyLysine = new Residue("a", 'a', "a", Chemistry.ChemicalFormula.ParseFormula("C{13}6H12N{15}2O"), ModificationSites.All); //+8 lysine
            Residue lightLysine = Residue.GetResidue('K');
            task = new SearchTask
            {
                SearchParameters = new SearchParameters
                {
                    NoOneHitWonders = true,
                    SilacLabels = new List<SilacLabel> { new SilacLabel(lightLysine.Letter, heavyLysine.Letter, heavyLysine.ThisChemicalFormula.Formula, heavyLysine.MonoisotopicMass - lightLysine.MonoisotopicMass) },
                }
            };
            task.RunTask(outputFolder, new List<DbForTask> { new DbForTask(xmlName, false) }, new List<string> { mzmlName }, "taskId1").ToString();

            //delete files
            Directory.Delete(outputFolder, true);
            File.Delete(xmlName);
            File.Delete(mzmlName);
        }
    }
}