﻿using Chemistry;
using EngineLayer;
using EngineLayer.ClassicSearch;
using EngineLayer.FdrAnalysis;
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
using TaskLayer;
using UsefulProteomicsDatabases;
using Omics;
using Omics.BioPolymer;

namespace Test
{
    [TestFixture]
    public static class FdrTest
    {
        [Test]
        public static void TestSeeModsThatShiftMobility()
        {
            Modification ac = new Modification(_originalId: "Acetylation");
            Modification am = new Modification(_originalId: "Ammonia loss");
            List<Modification> real = new List<Modification> { ac, am };

            Assert.That(PepAnalysisEngine.ContainsModificationsThatShiftMobility(real));
            Assert.That(PepAnalysisEngine.CountModificationsThatShiftMobility(real), Is.EqualTo(2));

            Modification fac = new Modification(_originalId: "fake Acetylation");
            Modification fam = new Modification(_originalId: "fake Ammonia loss");
            List<Modification> fake = new List<Modification> { fac, fam };

            Assert.That(!PepAnalysisEngine.ContainsModificationsThatShiftMobility(fake));
            Assert.That(PepAnalysisEngine.CountModificationsThatShiftMobility(fake), Is.EqualTo(0));

            Assert.That(PepAnalysisEngine.ContainsModificationsThatShiftMobility(real.Concat(fake)));
            Assert.That(PepAnalysisEngine.CountModificationsThatShiftMobility(real.Concat(fake)), Is.EqualTo(2));
        }

        [Test]
        public static void FdrTestMethod()
        {
            MassDiffAcceptor searchModes = new DotMassDiffAcceptor(null, new List<double> { 0, 1.0029 }, new PpmTolerance(5));
            List<string> nestedIds = new List<string>();

            Protein p = new Protein("MNKNNKNNNKNNNNK", null);
            CommonParameters commonParameters = new CommonParameters();

            var digested = p.Digest(commonParameters.DigestionParams, new List<Modification>(), new List<Modification>()).ToList();

            var pep1 = digested[0];
            var pep2 = digested[1];
            var pep3 = digested[2];
            var pep4 = digested[3];

            TestDataFile t = new TestDataFile(new List<IBioPolymerWithSetMods> { pep1, pep2, pep3 });

            MsDataScan mzLibScan1 = t.GetOneBasedScan(2);
            Ms2ScanWithSpecificMass scan1 = new Ms2ScanWithSpecificMass(mzLibScan1, pep1.MonoisotopicMass.ToMz(1), 1, null, new CommonParameters());
            SpectralMatch psm1 = new PeptideSpectralMatch(pep1, 0, 3, 0, scan1, commonParameters, new List<MatchedFragmentIon>());

            MsDataScan mzLibScan2 = t.GetOneBasedScan(4);
            Ms2ScanWithSpecificMass scan2 = new Ms2ScanWithSpecificMass(mzLibScan2, pep2.MonoisotopicMass.ToMz(1), 1, null, new CommonParameters());
            SpectralMatch psm2 = new PeptideSpectralMatch(pep2, 1, 2, 1, scan2, commonParameters, new List<MatchedFragmentIon>());

            MsDataScan mzLibScan3 = t.GetOneBasedScan(6);
            Ms2ScanWithSpecificMass scan3 = new Ms2ScanWithSpecificMass(mzLibScan3, pep3.MonoisotopicMass.ToMz(1), 1, null, new CommonParameters());
            SpectralMatch psm3 = new PeptideSpectralMatch(pep3, 0, 1, 2, scan3, commonParameters, new List<MatchedFragmentIon>());

            psm3.AddOrReplace(pep4, 1, 1, true, new List<MatchedFragmentIon>());

            var newPsms = new List<SpectralMatch> { psm1, psm2, psm3 };
            foreach (SpectralMatch psm in newPsms)
            {
                psm.ResolveAllAmbiguities();
            }

            List<(string fileName, CommonParameters fileSpecificParameters)> fsp = new List<(string fileName, CommonParameters fileSpecificParameters)> { ("filename", new CommonParameters()) };

            FdrAnalysisEngine fdr = new FdrAnalysisEngine(newPsms, searchModes.NumNotches, new CommonParameters(), fsp, nestedIds);

            fdr.Run();

            Assert.That(searchModes.NumNotches, Is.EqualTo(2));
            Assert.That(newPsms[0].FdrInfo.CumulativeDecoyNotch, Is.EqualTo(0));
            Assert.That(newPsms[0].FdrInfo.CumulativeTargetNotch, Is.EqualTo(1));
            Assert.That(newPsms[1].FdrInfo.CumulativeDecoyNotch, Is.EqualTo(0));
            Assert.That(newPsms[1].FdrInfo.CumulativeTargetNotch, Is.EqualTo(1));
            Assert.That(newPsms[2].FdrInfo.CumulativeDecoyNotch, Is.EqualTo(0));
            Assert.That(newPsms[2].FdrInfo.CumulativeTargetNotch, Is.EqualTo(1));

            Assert.That(newPsms[0].FdrInfo.CumulativeDecoy, Is.EqualTo(0));
            Assert.That(newPsms[0].FdrInfo.CumulativeTarget, Is.EqualTo(1));
            Assert.That(newPsms[1].FdrInfo.CumulativeDecoy, Is.EqualTo(0));
            Assert.That(newPsms[1].FdrInfo.CumulativeTarget, Is.EqualTo(2));
            Assert.That(newPsms[2].FdrInfo.CumulativeDecoy, Is.EqualTo(0));
            Assert.That(newPsms[2].FdrInfo.CumulativeTarget, Is.EqualTo(3));
        }

        [Test]
        public static void FdrAnalysisEngineFileSpecificParametersNotNull()
        {
            MassDiffAcceptor searchModes = new DotMassDiffAcceptor(null, new List<double> { 0, 1.0029 }, new PpmTolerance(5));
            List<string> nestedIds = new List<string>();

            Protein p = new Protein("MNKNNKNNNKNNNNK", null);
            CommonParameters commonParameters = new CommonParameters();

            var digested = p.Digest(commonParameters.DigestionParams, new List<Modification>(), new List<Modification>()).ToList();

            var pep1 = digested[0];
            var pep2 = digested[1];
            var pep3 = digested[2];
            var pep4 = digested[3];

            TestDataFile t = new TestDataFile(new List<IBioPolymerWithSetMods> { pep1, pep2, pep3 });

            MsDataScan mzLibScan1 = t.GetOneBasedScan(2);
            Ms2ScanWithSpecificMass scan1 = new Ms2ScanWithSpecificMass(mzLibScan1, pep1.MonoisotopicMass.ToMz(1), 1, null, new CommonParameters());
            SpectralMatch psm1 = new PeptideSpectralMatch(pep1, 0, 3, 0, scan1, commonParameters, new List<MatchedFragmentIon>());

            MsDataScan mzLibScan2 = t.GetOneBasedScan(4);
            Ms2ScanWithSpecificMass scan2 = new Ms2ScanWithSpecificMass(mzLibScan2, pep2.MonoisotopicMass.ToMz(1), 1, null, new CommonParameters());
            SpectralMatch psm2 = new PeptideSpectralMatch(pep2, 1, 2, 1, scan2, commonParameters, new List<MatchedFragmentIon>());

            MsDataScan mzLibScan3 = t.GetOneBasedScan(6);
            Ms2ScanWithSpecificMass scan3 = new Ms2ScanWithSpecificMass(mzLibScan3, pep3.MonoisotopicMass.ToMz(1), 1, null, new CommonParameters());
            SpectralMatch psm3 = new PeptideSpectralMatch(pep3, 0, 1, 2, scan3, commonParameters, new List<MatchedFragmentIon>());

            psm3.AddOrReplace(pep4, 1, 1, true, new List<MatchedFragmentIon>());

            var newPsms = new List<SpectralMatch> { psm1, psm2, psm3 };
            foreach (SpectralMatch psm in newPsms)
            {
                psm.ResolveAllAmbiguities();
            }

            Assert.Throws<ArgumentNullException>(() => new FdrAnalysisEngine(newPsms, searchModes.NumNotches, new CommonParameters(), null, nestedIds));
        }


        [Test]
        public static void TestComputePEPValue()
        {
            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();
            var origDataFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TaGe_SA_HeLa_04_subset_longestSeq.mzML");
            MyFileManager myFileManager = new MyFileManager(true);
            CommonParameters CommonParameters = new CommonParameters(digestionParams: new DigestionParams());
            SearchParameters SearchParameters = new SearchParameters();
            var fsp = new List<(string fileName, CommonParameters fileSpecificParameters)>();
            fsp.Add(("TaGe_SA_HeLa_04_subset_longestSeq.mzML", CommonParameters));

            var myMsDataFile = myFileManager.LoadFile(origDataFile, CommonParameters);
            var searchModes = new SinglePpmAroundZeroSearchMode(5);
            List<Protein> proteinList = ProteinDbLoader.LoadProteinFasta(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\hela_snip_for_unitTest.fasta"), true, DecoyType.Reverse, false, out var dbErrors, ProteinDbLoader.UniprotAccessionRegex, ProteinDbLoader.UniprotFullNameRegex, ProteinDbLoader.UniprotFullNameRegex, ProteinDbLoader.UniprotGeneNameRegex,
                    ProteinDbLoader.UniprotOrganismRegex, -1);
            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, @"TestData\TaGe_SA_HeLa_04_subset_longestSeq.mzML", CommonParameters).OrderBy(b => b.PrecursorMass).ToArray();
            SpectralMatch[] allPsmsArray = new PeptideSpectralMatch[listOfSortedms2Scans.Length];
            new ClassicSearchEngine(allPsmsArray, listOfSortedms2Scans, variableModifications, fixedModifications, null, null, null, 
                proteinList, searchModes, CommonParameters, fsp, null, new List<string>(), SearchParameters.WriteSpectralLibrary).Run();
            FdrAnalysisResults fdrResultsClassicDelta = (FdrAnalysisResults)(new FdrAnalysisEngine(allPsmsArray.Where(p => p != null).ToList(), 1, 
                CommonParameters, fsp, new List<string>()).Run());

            var nonNullPsms = allPsmsArray.Where(p => p != null).ToList();

            var maxScore = nonNullPsms.Select(n => n.Score).Max();
            SpectralMatch maxScorePsm = nonNullPsms.Where(n => n.Score == maxScore).First();

            Dictionary<string, int> sequenceToPsmCount = new Dictionary<string, int>();


            List<string> sequences = new List<string>();
            foreach (SpectralMatch psm in nonNullPsms)
            {
                var ss = psm.BestMatchingBioPolymersWithSetMods.Select(b => b.SpecificBioPolymer.FullSequence).ToList();
                sequences.Add(String.Join("|", ss));
            }

            var s = sequences.GroupBy(i => i);

            foreach (var grp in s)
            {
                sequenceToPsmCount.Add(grp.Key, grp.Count());
            }

            Dictionary<string, Dictionary<int, Tuple<double, double>>> fileSpecificRetTimeHI_behavior = new Dictionary<string, Dictionary<int, Tuple<double, double>>>();
            Dictionary<string, Dictionary<int, Tuple<double, double>>> fileSpecificRetTemHI_behaviorModifiedPeptides = new Dictionary<string, Dictionary<int, Tuple<double, double>>>();

            //average hydrophobicity, standard deviation hydrophobicity
            Tuple<double, double> at = new Tuple<double, double>(33.0, 1.0);

            Dictionary<int, Tuple<double, double>> HI_Time_avg_dev = new Dictionary<int, Tuple<double, double>>
            {
                { 154, at }
            };

            fileSpecificRetTimeHI_behavior.Add("TaGe_SA_HeLa_04_subset_longestSeq.mzML", HI_Time_avg_dev);

            int chargeStateMode = 4;
            var bestMatch = maxScorePsm.BestMatchingBioPolymersWithSetMods.First();
            Dictionary<string, float> massError = new Dictionary<string, float>
            {
                { Path.GetFileName(maxScorePsm.FullFilePath), 0 }
            };

            // Set values within PEP_Analysis through reflection
            PepAnalysisEngine pepEngine = new PepAnalysisEngine(nonNullPsms, "standard", fsp, Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\"));
            var pepEngineProperties = pepEngine.GetType().GetProperties();
            foreach (var p in pepEngineProperties)
            {
                switch(p.Name)
                {
                    case "FileSpecificTimeDependantHydrophobicityAverageAndDeviation_unmodified":
                        p.SetValue(pepEngine, fileSpecificRetTimeHI_behavior);
                        break;
                    case "FileSpecificTimeDependantHydrophobicityAverageAndDeviation_modified":
                        p.SetValue(pepEngine, fileSpecificRetTimeHI_behavior);
                        break;
                    case "ChargeStateMode":
                        p.SetValue(pepEngine, chargeStateMode);
                        break;
                    case "FileSpecificMedianFragmentMassErrors":
                        p.SetValue(pepEngine, massError);
                        break;
                    default:
                        break;
                }             
            }

            var maxPsmData = pepEngine.CreateOnePsmDataEntry("standard", maxScorePsm, bestMatch, !bestMatch.IsDecoy);
            Assert.That(maxScorePsm.BestMatchingBioPolymersWithSetMods.Count() - 1, Is.EqualTo(maxPsmData.Ambiguity));
            double normalizationFactor = (double)bestMatch.SpecificBioPolymer.BaseSequence.Length;
            float maxPsmDeltaScore = (float)Math.Round(maxScorePsm.DeltaScore / normalizationFactor * 10.0, 0);
            Assert.That(maxPsmDeltaScore, Is.EqualTo(maxPsmData.DeltaScore).Within(0.05));
            float maxPsmIntensity = Math.Min(50, (float)Math.Round((maxScorePsm.Score - (int)maxScorePsm.Score) / normalizationFactor * 100.0, 0));
            Assert.That(maxPsmIntensity, Is.EqualTo(maxPsmData.Intensity).Within(0.05));
            Assert.That(maxPsmData.HydrophobicityZScore, Is.EqualTo(52.0).Within(0.05));
            Assert.That(maxScorePsm.BestMatchingBioPolymersWithSetMods.Select(p => p.SpecificBioPolymer).First().MissedCleavages, Is.EqualTo(maxPsmData.MissedCleavagesCount));
            Assert.That(maxScorePsm.BestMatchingBioPolymersWithSetMods.Select(p => p.SpecificBioPolymer).First().AllModsOneIsNterminus.Values.Count(), Is.EqualTo(maxPsmData.ModsCount));
            Assert.That(maxScorePsm.Notch ?? 0, Is.EqualTo(maxPsmData.Notch));
            Assert.That(-Math.Abs(chargeStateMode - maxScorePsm.ScanPrecursorCharge), Is.EqualTo(maxPsmData.PrecursorChargeDiffToMode));
            Assert.That((float)0, Is.EqualTo(maxPsmData.IsVariantPeptide));

            List<SpectralMatch> psmCopyForCZETest = nonNullPsms.ToList();
            List<SpectralMatch> psmCopyForPEPFailure = nonNullPsms.ToList();
            List<SpectralMatch> psmCopyForNoOutputFolder = nonNullPsms.ToList();

            pepEngine.ComputePEPValuesForAllPSMs();

            int trueCount = 0;

            foreach (var item in allPsmsArray.Where(p => p != null))
            {
                var b = item.FdrInfo.PEP;
                if (b >= 0.5)
                {
                    trueCount++;
                }
            }

            List<SpectralMatch> moreNonNullPSMs = new List<SpectralMatch>();

            for (int i = 0; i < 3; i++)
            {
                foreach (SpectralMatch psm in nonNullPsms)
                {
                    moreNonNullPSMs.Add(psm);
                }
            }


            pepEngine = new PepAnalysisEngine(moreNonNullPSMs, "standard", fsp, Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\"));
            string metrics = pepEngine.ComputePEPValuesForAllPSMs();
            Assert.That(32 >= trueCount);

            //Test Variant Peptide as Input is identified as such as part of PEP calculation input much of the next several lines simply necessry to create a psm.

            var anMzSpectrum = new MzSpectrum(new double[] { 1, 1 }, new double[] { 2, 2 }, true);
            Ms2ScanWithSpecificMass scan = new Ms2ScanWithSpecificMass(new MsDataScan(anMzSpectrum, 1, 1, true, Polarity.Negative, 2, null, "", MZAnalyzerType.Orbitrap, 2, null, null, null), 1, 1, "path", new CommonParameters());
            Protein variantProtein = new Protein("MPEPPPTIDE", "protein3", sequenceVariations: new List<SequenceVariation> { new SequenceVariation(4, 6, "PPP", "P", @"1\t50000000\t.\tA\tG\t.\tPASS\tANN=G||||||||||||||||\tGT:AD:DP\t1/1:30,30:30", null) });
            IBioPolymerWithSetMods varPep = variantProtein.GetVariantBioPolymers().SelectMany(p => p.Digest(CommonParameters.DigestionParams, null, null)).FirstOrDefault();

            Product prod = new Product(ProductType.b, FragmentationTerminus.N, 1, 1, 1, 0);
            List<MatchedFragmentIon> mfi = new List<MatchedFragmentIon> { new MatchedFragmentIon(prod, 1, 1.0, 1) };

            SpectralMatch variantPSM = new PeptideSpectralMatch(varPep, 0, maxScorePsm.Score, maxScorePsm.ScanIndex, scan, new CommonParameters(), mfi);

            sequenceToPsmCount = new Dictionary<string, int>();
            sequences = new List<string>();
            nonNullPsms.Add(variantPSM);
            foreach (SpectralMatch psm in nonNullPsms)
            {
                var ss = psm.BestMatchingBioPolymersWithSetMods.Select(b => b.SpecificBioPolymer.FullSequence).ToList();
                sequences.Add(String.Join("|", ss));
            }

            s = sequences.GroupBy(i => i);

            foreach (var grp in s)
            {
                sequenceToPsmCount.Add(grp.Key, grp.Count());
            }
            var variantMatch = variantPSM.BestMatchingBioPolymersWithSetMods.First();

            massError.Add(Path.GetFileName(variantPSM.FullFilePath), 0);

            // edit the FileSpecificMedianFragmentMassErrors property of PEP_Analysis_Cross_Validation to include the mass error for the variant peptide file
            pepEngineProperties = pepEngine.GetType().GetProperties();
            foreach (var p in pepEngineProperties)
            {
                switch (p.Name)
                {
                    case "FileSpecificMedianFragmentMassErrors":
                        p.SetValue(pepEngine, massError);
                        break;
                    default:
                        break;
                }
            }


            PsmData variantPsmData = pepEngine.CreateOnePsmDataEntry("standard", variantPSM, variantMatch, !maxScorePsm.IsDecoy);

            Assert.That(variantPsmData.IsVariantPeptide, Is.EqualTo((float)1));

            //TEST CZE
            fsp = new List<(string fileName, CommonParameters fileSpecificParameters)>();
            var cp = new CommonParameters(separationType: "CZE");

            fsp.Add((origDataFile, cp));

            
            trueCount = 0;

            foreach (var item in psmCopyForCZETest.Where(p => p != null))
            {
                var b = item.FdrInfo.PEP;
                if (b >= 0.5)
                {
                    trueCount++;
                }
            }

            List<SpectralMatch> moreNonNullPSMsCZE = new List<SpectralMatch>();

            for (int i = 0; i < 3; i++)
            {
                foreach (SpectralMatch psm in psmCopyForCZETest)
                {
                    moreNonNullPSMsCZE.Add(psm);
                }
            }

            pepEngine = new PepAnalysisEngine(moreNonNullPSMsCZE, "standard", fsp, Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\"));
            metrics = pepEngine.ComputePEPValuesForAllPSMs();
            Assert.That(32 >= trueCount);

            //TEST PEP calculation failure
            psmCopyForPEPFailure.RemoveAll(x => x.IsDecoy);
            pepEngine = new PepAnalysisEngine(psmCopyForPEPFailure, "standard", fsp, Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\"));
            string result = pepEngine.ComputePEPValuesForAllPSMs();
            Assert.That(result, Is.EqualTo("Posterior error probability analysis failed. This can occur for small data sets when some sample groups are missing positive or negative training examples."));

            //Run PEP with no output folder;
            //There is no assertion here. We simply want to show that PEP calculation does not fail with null folder.
            string outputFolder = null;
            pepEngine = new PepAnalysisEngine(psmCopyForNoOutputFolder, "standard", fsp, outputFolder);
            string nullOutputFolderResults = pepEngine.ComputePEPValuesForAllPSMs();
        }

        [Test]
        public static void TestComputePEPValueTopDown()
        {
            //just making sure that topdown data goes through the pep calculator without crashing.
            CommonParameters CommonParameters = new CommonParameters(
                digestionParams: new DigestionParams(protease: "top-down"),
                scoreCutoff: 1,
                assumeOrphanPeaksAreZ1Fragments: false);

            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();
            List<Protein> proteinList = ProteinDbLoader.LoadProteinFasta(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\HeLaFakeTopDown.fasta"), true, DecoyType.Reverse, false, out var dbErrors,
                ProteinDbLoader.UniprotAccessionRegex, ProteinDbLoader.UniprotFullNameRegex, ProteinDbLoader.UniprotFullNameRegex, ProteinDbLoader.UniprotGeneNameRegex,
                    ProteinDbLoader.UniprotOrganismRegex, -1);

            var origDataFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TaGe_SA_HeLa_04_subset_longestSeq.mzML");

            var fsp = new List<(string fileName, CommonParameters fileSpecificParameters)>();
            fsp.Add(("TaGe_SA_HeLa_04_subset_longestSeq.mzML", CommonParameters));

            MyFileManager myFileManager = new MyFileManager(true);
            var myMsDataFile = myFileManager.LoadFile(origDataFile, CommonParameters);

            var searchMode = new SinglePpmAroundZeroSearchMode(5);

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, origDataFile, new CommonParameters()).OrderBy(b => b.PrecursorMass).ToArray();

            SpectralMatch[] allPsmsArray = new PeptideSpectralMatch[listOfSortedms2Scans.Length];

            bool writeSpectralLibrary = false;
            new ClassicSearchEngine(allPsmsArray, listOfSortedms2Scans, variableModifications, fixedModifications, null, null, null, 
                proteinList, searchMode, CommonParameters, fsp, null, new List<string>(), writeSpectralLibrary).Run();
            var nonNullPsms = allPsmsArray.Where(p => p != null).ToList();
            List<SpectralMatch> moreNonNullPSMs = new List<SpectralMatch>();

            int reps = 10;
            for (int i = 0; i < reps; i++)
            {
                foreach (SpectralMatch psm in nonNullPsms)
                {
                    moreNonNullPSMs.Add(psm);
                }
            }

            FdrAnalysisResults fdrResultsClassicDelta = (FdrAnalysisResults)(new FdrAnalysisEngine(moreNonNullPSMs.Where(p => p != null).OrderByDescending(f=>f.Score).ToList(), 1, CommonParameters, fsp, new List<string>(), analysisType: "PSM", outputFolder: Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\")).Run());

            var maxScore = nonNullPsms.Select(n => n.Score).Max();
            SpectralMatch maxScorePsm = nonNullPsms.Where(n => n.Score == maxScore).First();
            Dictionary<string, int> sequenceToPsmCount = new Dictionary<string, int>();
            List<string> sequences = new List<string>();
            foreach (SpectralMatch psm in nonNullPsms)
            {
                var ss = psm.BestMatchingBioPolymersWithSetMods.Select(b => b.SpecificBioPolymer.FullSequence).ToList();
                sequences.Add(String.Join(" | ", ss));
            }
            var s = sequences.GroupBy(i => i);

            foreach (var grp in s)
            {
                sequenceToPsmCount.Add(grp.Key, grp.Count());
            }

            Dictionary<string, Dictionary<int, Tuple<double, double>>> fileSpecificRetTimeHI_behavior = new Dictionary<string, Dictionary<int, Tuple<double, double>>>();
            Dictionary<string, Dictionary<int, Tuple<double, double>>> fileSpecificRetTemHI_behaviorModifiedPeptides = new Dictionary<string, Dictionary<int, Tuple<double, double>>>();

            int chargeStateMode = 4;
            var bestMatch = maxScorePsm.BestMatchingBioPolymersWithSetMods.First();

            Dictionary<string, float> massError = new Dictionary<string, float>
            {
                { Path.GetFileName(maxScorePsm.FullFilePath), 0 }
            };

            // Set values within PEP_Analysis through reflection
            PepAnalysisEngine pepEngine = new PepAnalysisEngine(nonNullPsms, "top-down", fsp, Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\"));
            var pepEngineProperties = pepEngine.GetType().GetProperties();
            foreach (var p in pepEngineProperties)
            {
                switch (p.Name)
                {
                    case "ChargeStateMode":
                        p.SetValue(pepEngine, chargeStateMode);
                        break;
                    case "FileSpecificMedianFragmentMassErrors":
                        p.SetValue(pepEngine, massError);
                        break;
                    default:
                        break;
                }
            }

            var maxPsmData = pepEngine.CreateOnePsmDataEntry("top-down", maxScorePsm, bestMatch, !bestMatch.SpecificBioPolymer.Parent.IsDecoy);
            Assert.That(maxScorePsm.BestMatchingBioPolymersWithSetMods.Count() - 1, Is.EqualTo(maxPsmData.Ambiguity));
            double normalizationFactor = 1;
            float maxPsmDeltaScore = (float)Math.Round(maxScorePsm.DeltaScore / normalizationFactor * 10.0, 0);
            Assert.That(maxPsmDeltaScore, Is.EqualTo(maxPsmData.DeltaScore).Within(0.05));
            float maxPsmIntensity = (float)Math.Min(50, Math.Round((maxScorePsm.Score - (int)maxScorePsm.Score) / normalizationFactor * 100.0, 0));
            Assert.That(maxPsmIntensity, Is.EqualTo(maxPsmData.Intensity).Within(0.05));
            Assert.That(maxPsmData.HydrophobicityZScore, Is.EqualTo(float.NaN));
            Assert.That(maxScorePsm.BestMatchingBioPolymersWithSetMods.Select(p => p.SpecificBioPolymer).First().MissedCleavages, Is.EqualTo(maxPsmData.MissedCleavagesCount));
            Assert.That(maxScorePsm.BestMatchingBioPolymersWithSetMods.Select(p => p.SpecificBioPolymer).First().AllModsOneIsNterminus.Values.Count(), Is.EqualTo(maxPsmData.ModsCount));
            Assert.That(maxScorePsm.Notch ?? 0, Is.EqualTo(maxPsmData.Notch));
            Assert.That(-Math.Abs(chargeStateMode - maxScorePsm.ScanPrecursorCharge), Is.EqualTo(maxPsmData.PrecursorChargeDiffToMode));
            Assert.That((float)0, Is.EqualTo(maxPsmData.IsVariantPeptide));
        }

        [Test]
        public static void TestPEP_peptideRemoval()
        {
            int ambiguousPeptidesRemovedCount = 0;

            Ms2ScanWithSpecificMass scan = new Ms2ScanWithSpecificMass(
                new MsDataScan(
                    new MzSpectrum(new double[] { }, new double[] { }, false),
                    2, 1, true, Polarity.Positive, double.NaN, null, null, MZAnalyzerType.Orbitrap, double.NaN, null, null, "scan=1", double.NaN, null, null, double.NaN, null, DissociationType.AnyActivationType, 1, null),
                100, 1, null, new CommonParameters(), null);

            PeptideWithSetModifications pwsm = new PeptideWithSetModifications(new Protein("PEPTIDE", "ACCESSION", "ORGANISM"), new DigestionParams(), 1, 2, CleavageSpecificity.Full, "", 0, new Dictionary<int, Modification>(), 0);

            SpectralMatch psm = new PeptideSpectralMatch(pwsm, 0, 1, 1, scan, new CommonParameters(), new List<MatchedFragmentIon>());
            psm.AddOrReplace(pwsm, 1, 1, true, new List<MatchedFragmentIon>());
            psm.AddOrReplace(pwsm, 1, 2, true, new List<MatchedFragmentIon>());
            psm.SetFdrValues(1, 0, 0, 1, 0, 0, 1, 0);
            psm.PeptideFdrInfo = new FdrInfo();

            List<int> indiciesOfPeptidesToRemove = new List<int>();
            List<(int notch, PeptideWithSetModifications pwsm)> bestMatchingPeptidesToRemove = new List<(int notch, PeptideWithSetModifications pwsm)>();
            List<double> pepValuePredictions = new List<double> { 1.0d, 0.99d, 0.9d };

            PepAnalysisEngine.GetIndiciesOfPeptidesToRemove(indiciesOfPeptidesToRemove, pepValuePredictions);
            Assert.That(indiciesOfPeptidesToRemove.Count, Is.EqualTo(1));
            Assert.That(indiciesOfPeptidesToRemove.FirstOrDefault(), Is.EqualTo(2));
            Assert.That(pepValuePredictions.Count, Is.EqualTo(2));

            PepAnalysisEngine.RemoveBestMatchingPeptidesWithLowPEP(psm, indiciesOfPeptidesToRemove, psm.BestMatchingBioPolymersWithSetMods.ToList(), ref ambiguousPeptidesRemovedCount);
            Assert.That(ambiguousPeptidesRemovedCount, Is.EqualTo(1));
            Assert.That(psm.BestMatchingBioPolymersWithSetMods.Select(b => b.Notch).ToList().Count, Is.EqualTo(2));
        }

        [Test]
        public static void TestPEP_standardDeviationsToChange()
        {
            double globalStDev = 1;
            Dictionary<int, Tuple<double, double>> stDevsToChange = new Dictionary<int, Tuple<double, double>>();
            Dictionary<int, Tuple<double, double>> averagesCommaStandardDeviations = new Dictionary<int, Tuple<double, double>>();

            averagesCommaStandardDeviations.Add(0, new Tuple<double, double>(1.0d, Double.NaN));//will get removed because NaN
            averagesCommaStandardDeviations.Add(1, new Tuple<double, double>(1.0d, 0.01d));//will get removed becuase 0.01 is too small
            averagesCommaStandardDeviations.Add(2, new Tuple<double, double>(1.0d, 1.1d));//will NOT get removed becuase its perfectly fine
            averagesCommaStandardDeviations.Add(3, new Tuple<double, double>(1.0d, 10.0d));//will  get removed becuase its too big

            PepAnalysisEngine.GetStDevsToChange(stDevsToChange, averagesCommaStandardDeviations, globalStDev);
            Assert.That(stDevsToChange.ContainsKey(0));
            Assert.That(stDevsToChange.ContainsKey(1));
            Assert.That(stDevsToChange.ContainsKey(3));
            Assert.That(stDevsToChange.Keys.Count, Is.EqualTo(3));

            PepAnalysisEngine.UpdateOutOfRangeStDevsWithGlobalAverage(stDevsToChange, averagesCommaStandardDeviations);

            Assert.That(averagesCommaStandardDeviations[0].Item2, Is.EqualTo(1.0d));
            Assert.That(averagesCommaStandardDeviations[1].Item2, Is.EqualTo(1.0d));
            Assert.That(1.1d, Is.EqualTo(averagesCommaStandardDeviations[2].Item2).Within(0.01));
            Assert.That(averagesCommaStandardDeviations[3].Item2, Is.EqualTo(1.0d));
        }

        [Test]
        public static void ReplaceBadStdevOne()
        {
            //adding a new scan that creates a psm at an isolated retention time. This will ultimately cause PEP to replace its retention time standard deviation "Z-score" with the global average.
            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();
            var origDataFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TaGe_SA_HeLa_04_subset_longestSeq.mzML");
            MyFileManager myFileManager = new MyFileManager(true);
            CommonParameters CommonParameters = new CommonParameters(digestionParams: new DigestionParams(), separationType: "HPLC");

            var fsp = new List<(string fileName, CommonParameters fileSpecificParameters)>();
            fsp.Add((origDataFile, CommonParameters));

            var myMsDataFile = myFileManager.LoadFile(origDataFile, CommonParameters);
            var searchModes = new SinglePpmAroundZeroSearchMode(5);
            List<Protein> proteinList = ProteinDbLoader.LoadProteinFasta(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\hela_snip_for_unitTest.fasta"), true, DecoyType.Reverse, false, out var dbErrors,
                ProteinDbLoader.UniprotAccessionRegex, ProteinDbLoader.UniprotFullNameRegex, ProteinDbLoader.UniprotFullNameRegex, ProteinDbLoader.UniprotGeneNameRegex,
                    ProteinDbLoader.UniprotOrganismRegex, -1);
            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, @"TestData\TaGe_SA_HeLa_04_subset_longestSeq.mzML", CommonParameters).OrderBy(b => b.PrecursorMass).ToArray();

            Ms2ScanWithSpecificMass topMs2Scan = listOfSortedms2Scans[395];
            int newOneBasedScanNumber = 1000;
            MzRange range = new MzRange(topMs2Scan.TheScan.MassSpectrum.XArray.Min(), topMs2Scan.TheScan.MassSpectrum.XArray.Max());
            MzSpectrum mzs = new MzSpectrum(topMs2Scan.TheScan.MassSpectrum.XArray, topMs2Scan.TheScan.MassSpectrum.YArray, true);
            double newRetentionTime = topMs2Scan.TheScan.RetentionTime - 25;
            MsDataScan msd = new MsDataScan(mzs, newOneBasedScanNumber, 2, topMs2Scan.TheScan.IsCentroid, Polarity.Positive, newRetentionTime, range, "", MZAnalyzerType.Orbitrap, topMs2Scan.TheScan.TotalIonCurrent, topMs2Scan.TheScan.InjectionTime, topMs2Scan.TheScan.NoiseData, "", topMs2Scan.TheScan.SelectedIonMZ, topMs2Scan.TheScan.SelectedIonChargeStateGuess, topMs2Scan.TheScan.SelectedIonIntensity, topMs2Scan.TheScan.IsolationMz, topMs2Scan.TheScan.IsolationWidth, DissociationType.HCD, topMs2Scan.TheScan.OneBasedPrecursorScanNumber, topMs2Scan.TheScan.SelectedIonMonoisotopicGuessMz);
            Ms2ScanWithSpecificMass mwsm = new Ms2ScanWithSpecificMass(msd, topMs2Scan.PrecursorMonoisotopicPeakMz, topMs2Scan.PrecursorCharge, topMs2Scan.FullFilePath, new CommonParameters(), topMs2Scan.ExperimentalFragments);

            Ms2ScanWithSpecificMass[] extendedArray = new Ms2ScanWithSpecificMass[listOfSortedms2Scans.Length + 1];
            for (int i = 0; i < listOfSortedms2Scans.Length; i++)
            {
                extendedArray[i] = listOfSortedms2Scans[i];
            }
            extendedArray[listOfSortedms2Scans.Length] = mwsm;

            extendedArray = extendedArray.OrderBy(b => b.PrecursorMass).ToArray();

            SpectralMatch[] allPsmsArray = new PeptideSpectralMatch[extendedArray.Length];
            bool writeSpectralLibrary = false;
            new ClassicSearchEngine(allPsmsArray, extendedArray, variableModifications, fixedModifications, null, null, null, 
                proteinList, searchModes, CommonParameters, fsp, null, new List<string>(), writeSpectralLibrary).Run();

            List<SpectralMatch> nonNullPsms = allPsmsArray.Where(p => p != null).ToList();
            nonNullPsms = nonNullPsms.OrderByDescending(p => p.Score).ToList();
            List<SpectralMatch> psmBloated = new List<SpectralMatch>();
            psmBloated.AddRange(nonNullPsms);
            int arrayMax = nonNullPsms.Count;
            psmBloated.AddRange(nonNullPsms.GetRange(2, arrayMax - 2));
            psmBloated.AddRange(nonNullPsms.GetRange(2, arrayMax - 2));

            FdrAnalysisResults fdrResultsClassicDelta = (FdrAnalysisResults)(new FdrAnalysisEngine(psmBloated.Where(p => p != null).ToList(), 1, CommonParameters, fsp, new List<string>(), outputFolder: Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\")).Run());
        }

        [Test]
        public static void ReplaceBadStdevTwo()
        {
            //here we are adding a really hydrophobic psm at the same time as a regular peptide so that there is a big difference in their computed hydrophobicities. The stdev of these hydrophobicities is out of whach the the collective and so it needs to get replaced by the global average

            var variableModifications = new List<Modification>();
            var fixedModifications = new List<Modification>();
            var origDataFile = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\TaGe_SA_HeLa_04_subset_longestSeq.mzML");
            MyFileManager myFileManager = new MyFileManager(true);
            CommonParameters CommonParameters = new CommonParameters(digestionParams: new DigestionParams());
            var fsp = new List<(string fileName, CommonParameters fileSpecificParameters)>();
            fsp.Add((origDataFile, CommonParameters));

            var myMsDataFile = myFileManager.LoadFile(origDataFile, CommonParameters);
            var searchModes = new SinglePpmAroundZeroSearchMode(5);
            List<Protein> proteinList = ProteinDbLoader.LoadProteinFasta(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\hela_snip_for_unitTest.fasta"), true, DecoyType.Reverse, false, out var dbErrors, 
                ProteinDbLoader.UniprotAccessionRegex, ProteinDbLoader.UniprotFullNameRegex, ProteinDbLoader.UniprotFullNameRegex, ProteinDbLoader.UniprotGeneNameRegex,
                    ProteinDbLoader.UniprotOrganismRegex, -1);
            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, @"TestData\TaGe_SA_HeLa_04_subset_longestSeq.mzML", CommonParameters).OrderBy(b => b.PrecursorMass).ToArray();

            //adding a new scan that creates a psm at an isolated retention time. This will ultimately cause PEP to replace its retention time standard deviation "Z-score" with the global average.
            Ms2ScanWithSpecificMass topMs2Scan = listOfSortedms2Scans[395];
            int newOneBasedScanNumber = 1000;
            MzRange range = new MzRange(topMs2Scan.TheScan.MassSpectrum.XArray.Min(), topMs2Scan.TheScan.MassSpectrum.XArray.Max());
            MzSpectrum mzs = new MzSpectrum(topMs2Scan.TheScan.MassSpectrum.XArray, topMs2Scan.TheScan.MassSpectrum.YArray, true);
            double newRetentionTime = topMs2Scan.TheScan.RetentionTime - 25;
            MsDataScan msd = new MsDataScan(mzs, newOneBasedScanNumber, 2, topMs2Scan.TheScan.IsCentroid, Polarity.Positive, newRetentionTime, range, "", MZAnalyzerType.Orbitrap, topMs2Scan.TheScan.TotalIonCurrent, topMs2Scan.TheScan.InjectionTime, topMs2Scan.TheScan.NoiseData, "", topMs2Scan.TheScan.SelectedIonMZ, topMs2Scan.TheScan.SelectedIonChargeStateGuess, topMs2Scan.TheScan.SelectedIonIntensity, topMs2Scan.TheScan.IsolationMz, topMs2Scan.TheScan.IsolationWidth, DissociationType.HCD, topMs2Scan.TheScan.OneBasedPrecursorScanNumber, topMs2Scan.TheScan.SelectedIonMonoisotopicGuessMz);
            Ms2ScanWithSpecificMass mwsm = new Ms2ScanWithSpecificMass(msd, topMs2Scan.PrecursorMonoisotopicPeakMz, topMs2Scan.PrecursorCharge, topMs2Scan.FullFilePath, new CommonParameters(), topMs2Scan.ExperimentalFragments);

            Ms2ScanWithSpecificMass[] extendedArray = new Ms2ScanWithSpecificMass[listOfSortedms2Scans.Length + 1];
            for (int i = 0; i < listOfSortedms2Scans.Length; i++)
            {
                extendedArray[i] = listOfSortedms2Scans[i];
            }
            extendedArray[listOfSortedms2Scans.Length] = mwsm;

            extendedArray = extendedArray.OrderBy(b => b.PrecursorMass).ToArray();

            SpectralMatch[] allPsmsArray = new PeptideSpectralMatch[extendedArray.Length];
            bool writeSpectralLibrary = false;
            new ClassicSearchEngine(allPsmsArray, extendedArray, variableModifications, fixedModifications, null, null, null,
                proteinList, searchModes, CommonParameters, fsp, null, new List<string>(), writeSpectralLibrary).Run();

            List<SpectralMatch> nonNullPsms = allPsmsArray.Where(p => p != null).ToList();
            nonNullPsms = nonNullPsms.OrderByDescending(p => p.Score).ToList();
            List<SpectralMatch> psmBloated = new List<SpectralMatch>();
            psmBloated.AddRange(nonNullPsms);
            int arrayMax = nonNullPsms.Count;
            psmBloated.AddRange(nonNullPsms.GetRange(2, arrayMax - 2));
            psmBloated.AddRange(nonNullPsms.GetRange(2, arrayMax - 2));
            SpectralMatch pp = psmBloated.OrderBy(p => p.ScanRetentionTime).First();

            PeptideWithSetModifications newPwsmTwo = new PeptideWithSetModifications(new Protein("WAGVLPWFPWAAVVWGFWF", "ACCESSION", "ORGANISM"), new DigestionParams(), 1, 2, CleavageSpecificity.Full, "", 0, new Dictionary<int, Modification>(), 0);
            SpectralMatch newPsmTwo = new PeptideSpectralMatch(newPwsmTwo, pp.BestMatchingBioPolymersWithSetMods.First().Notch, pp.Score, pp.ScanIndex, mwsm, new CommonParameters(), pp.MatchedFragmentIons);

            psmBloated.Add(newPsmTwo);

            FdrAnalysisResults fdrResultsClassicDelta = (FdrAnalysisResults)(new FdrAnalysisEngine(psmBloated.Where(p => p != null).ToList(), 1, CommonParameters, fsp, new List<string>(), outputFolder: Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\")).Run());
        }

        [Test]
        public static void TestRemoveThisAmbiguousePeptide()
        {
            Ms2ScanWithSpecificMass scanB = new Ms2ScanWithSpecificMass(
                new MsDataScan(
                    new MzSpectrum(new double[] { }, new double[] { }, false),
                    2, 1, true, Polarity.Positive, double.NaN, null, null, MZAnalyzerType.Orbitrap, double.NaN, null, null, "scan=1", double.NaN, null, null, double.NaN, null, DissociationType.AnyActivationType, 1, null),
                100, 1, null, new CommonParameters(), null);

            SpectralMatch psm1 = new PeptideSpectralMatch(new PeptideWithSetModifications(new Protein("PEPTIDE", "ACCESSION", "ORGANISM"), new DigestionParams(), 1, 2, CleavageSpecificity.Full, "", 0, new Dictionary<int, Modification>(), 0), 0, 10, 1, scanB, new CommonParameters(), new List<MatchedFragmentIon>());

            PeptideWithSetModifications pwsm = new PeptideWithSetModifications(new Protein("PEPTIDE", "ACCESSION", "ORGANISM"), new DigestionParams(), 1, 2, CleavageSpecificity.Full, "", 0, new Dictionary<int, Modification>(), 0);

            psm1.AddOrReplace(pwsm, 10, 1, true, new List<MatchedFragmentIon>());

            Assert.That(psm1.BestMatchingBioPolymersWithSetMods.Count(), Is.EqualTo(2));

            psm1.RemoveThisAmbiguousPeptide(psm1.BestMatchingBioPolymersWithSetMods.ElementAt(1));

            Assert.That(psm1.BestMatchingBioPolymersWithSetMods.Count(), Is.EqualTo(1));
        }

        [Test]
        public static void TestPEPValuesReduceAmbiguity()
        {
            //TODO
            //This is hard becuase you have to have a big enough file to create a model, enough peptides with the same exact score for the same scan, and enough differences in those ambiguous peptides that one or more get deleted after pepvalue comutation.
        }

        [Test]
        public static void TestPEPValueWorksWithStoredTrainedModel()
        {
            //TODO
            //This is hard becuase you have to have a big enough file to creat a model, enough peptides with the same exact score for the same scan, and enough differences in those ambiguous peptides that one or more get deleted after pepvalue comutation.
        }

        [Test]
        public static void TestPsmData()
        {
            string searchType = "standard";
            string[] trainingInfoStandard = PsmData.trainingInfos[searchType];
            string[] expectedTrainingInfoStandard = new[]
            {
                "TotalMatchingFragmentCount", "Intensity", "PrecursorChargeDiffToMode", "DeltaScore", "Notch",
                "ModsCount", "AbsoluteAverageFragmentMassErrorFromMedian", "MissedCleavagesCount", "Ambiguity",
                "LongestFragmentIonSeries", "ComplementaryIonCount", "HydrophobicityZScore", "IsVariantPeptide",
                "IsDeadEnd", "IsLoop", "SpectralAngle", "HasSpectralAngle"
            };
            Assert.That(trainingInfoStandard, Is.EqualTo(expectedTrainingInfoStandard));

            searchType = "top-down";
            string[] trainingInfoTopDown = PsmData.trainingInfos[searchType];
            string[] expectedTrainingInfoTopDown = new[]
            {
                "TotalMatchingFragmentCount", "Intensity", "PrecursorChargeDiffToMode", "DeltaScore", "Notch",
                "ModsCount", "AbsoluteAverageFragmentMassErrorFromMedian", "Ambiguity", "LongestFragmentIonSeries",
                "ComplementaryIonCount", "SpectralAngle", "HasSpectralAngle", "PeaksInPrecursorEnvelope",
                "ChimeraCount", "MostAbundantPrecursorPeakIntensity", "PrecursorFractionalIntensity", "InternalIonCount"
            };
            Assert.That(trainingInfoTopDown, Is.EqualTo(expectedTrainingInfoTopDown));

            List<string> positiveAttributes = new List<string>
            {
                "TotalMatchingFragmentCount", "Intensity", "PrecursorChargeDiffToMode", "DeltaScore",
                "LongestFragmentIonSeries", "ComplementaryIonCount", "AlphaIntensity", "BetaIntensity",
                "LongestFragmentIonSeries_Alpha", "LongestFragmentIonSeries_Beta", "PeaksInPrecursorEnvelope",
                "MostAbundantPrecursorPeakIntensity", "PrecursorFractionalIntensity", "InternalIonCount"
            };
            List<string> negativeAttributes = new List<string>
            {
                "Notch", "ModsCount", "AbsoluteAverageFragmentMassErrorFromMedian", "MissedCleavagesCount", "Ambiguity",
                "HydrophobicityZScore", "IsVariantPeptide", "IsDeadEnd", "IsLoop", "IsInter", "IsIntra", "ChimeraCount"
            };

            foreach (string attribute in positiveAttributes)
            {
                Assert.That(PsmData.assumedAttributeDirection[attribute], Is.EqualTo(1));
            }
            foreach (string attribute in negativeAttributes)
            {
                Assert.That(PsmData.assumedAttributeDirection[attribute], Is.EqualTo(-1));
            }

            PsmData pd = new PsmData
            {
                TotalMatchingFragmentCount = 0,
                Intensity = 1,
                PrecursorChargeDiffToMode = 2,
                DeltaScore = 3,
                Notch = 4,
                ModsCount = 5,
                AbsoluteAverageFragmentMassErrorFromMedian = 6,
                MissedCleavagesCount = 7,
                Ambiguity = 8,
                LongestFragmentIonSeries = 9,
                ComplementaryIonCount = 10,
                HydrophobicityZScore = 11,
                IsVariantPeptide = 12,
                AlphaIntensity = 13,
                BetaIntensity = 14,
                LongestFragmentIonSeries_Alpha = 15,
                LongestFragmentIonSeries_Beta = 16,
                IsDeadEnd = 17,
                IsLoop = 18,
                IsInter = 19,
                IsIntra = 20,
                Label = false,
                SpectralAngle = 21,
                HasSpectralAngle = 22,
                PeaksInPrecursorEnvelope = 23,
                ChimeraCount = 24,
                MostAbundantPrecursorPeakIntensity = 25,
                PrecursorFractionalIntensity = 26,
                InternalIonCount = 27,
            };

            string standardToString = "\t0\t1\t2\t3\t4\t5\t6\t7\t8\t9\t10\t11\t12\t17\t18\t21\t22";
            Assert.That(pd.ToString("standard"), Is.EqualTo(standardToString));

            string topDownToString = "\t0\t1\t2\t3\t4\t5\t6\t8\t9\t10\t21\t22\t23\t24\t25\t26\t27";
            Assert.That(pd.ToString("top-down"), Is.EqualTo(topDownToString));
        }
    }
}