﻿using EngineLayer;
using IO.MzML;
using IO.ThermoRawFileReader;
using MassSpectrometry;
using NUnit.Framework;
using Proteomics;
using Proteomics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Chemistry;
using Easy.Common.Extensions;
using GuiFunctions;
using MathNet.Numerics.Distributions;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Engine;
using MzLibUtil;
using SpectralAveraging;
using TaskLayer;
using UsefulProteomicsDatabases;
using Easy.Common;

namespace Test
{
    [TestFixture]
    public class AaHelpers
    {

        [Test]
        public static void GPTMDComparison()
        {
            var proteinPruned = @"D:\Projects\GPTMD_Validation_uWu\BU_pruned\homosapienGPTMDproteinPruned.xml";
            var pruned = @"D:\Projects\GPTMD_Validation_uWu\BU_pruned\homosapienGPTMDpruned.xml";

            string tdDirectory = @"D:\Projects\GPTMD_Validation_uWu\BU_TD_ComparativeSearch\A-D_TD_search";
            string buDirectory = @"D:\Projects\GPTMD_Validation_uWu\BU_TD_ComparativeSearch\A-D_BU_search";
            var outDirectory = @"D:\Projects\GPTMD_Validation_uWu\WorkDone";
            string outPath = Path.Combine(outDirectory, "comparativeAnalysis3.tsv");

        


            var tdRun = new MetaMorpheusRun(tdDirectory);
            var buRun = new MetaMorpheusRun(buDirectory);
            var runs = new MultiMetaMorpheusRunAnalyzer();
            var tdSearch = (SearchTaskResult)tdRun.TaskResults[MyTask.Search];
            var buSearch = (SearchTaskResult)buRun.TaskResults[MyTask.Search];
            runs.AddRuns(new List<MetaMorpheusRun>() { tdRun, buRun });


            var str = runs.GetSankeyFromTwoTdBuComparisons2(tdSearch.AllPsms, buSearch.AllPsms, "Psms");

           // var sankey = runs.TdBuComparisonToSankeyScripts();


            //runs.CompareTdBu();
            //runs.ExportBuTdComparisonToTsv(outPath);
        }

        


       


        
        [Test]
        public static void FindSizeOfDatabase()
        {
            string search = "AllMods";
            string filepathGPTMDAllMod = @"D:\Projects\Top Down MetaMorpheus\For paper KHB\Cali_PhosphoAcetylGPTMD_Search\Task1-GPTMDTask\uniprot-proteome_UP000005640_HumanRevPlusUnrev_012422GPTMD.xml";
            string filepathGPTMDPhosphoAcetyl = @"D:\Projects\Top Down MetaMorpheus\For paper KHB\Cali_PhosphoAcetylGPTMD_Search\Task1-GPTMDTask\uniprot-proteome_UP000005640_HumanRevPlusUnrev_012422GPTMD.xml";
            string filepathVariable = @"D:\Projects\Top Down MetaMorpheus\For paper KHB\uniprot-proteome_UP000005640_HumanRevPlusUnrev_012422.xml";
            List<Protein> proteins = new();
            List<PeptideWithSetModifications> proteoforms = new();
            List<Modification> allFixedMods = new();
            List<Modification> allVariableMods = new();
            CommonParameters commonparams = new CommonParameters();
            DigestionParams digestionparams = new DigestionParams("top-down");

            if (search.Equals("AllMods"))
            {
                proteins = ProteinDbLoader.LoadProteinXML(filepathGPTMDAllMod, true, DecoyType.Reverse, new List<Modification>(), false, new List<string>(), out Dictionary<string, Modification> ok);
                foreach (var mod in commonparams.ListOfModsFixed)
                {
                    allFixedMods.Add(new Modification(mod.Item2));
                }
            }
            else if (search.Equals("PhosphoAcetyl"))
            {
                proteins = ProteinDbLoader.LoadProteinXML(filepathGPTMDPhosphoAcetyl, true, DecoyType.Reverse, new List<Modification>(), false, new List<string>(), out Dictionary<string, Modification> ok2);
                foreach (var mod in commonparams.ListOfModsFixed)
                {
                    allFixedMods.Add(new Modification(mod.Item2));
                }
            }
            else if (search.Equals("Variable"))
            {
                proteins = ProteinDbLoader.LoadProteinXML(filepathVariable, true, DecoyType.Reverse, new List<Modification>(), false, new List<string>(), out Dictionary<string, Modification> ok2);
            }

            int count = 0;
            foreach (var protein in proteins)
            {
                var pwsm = protein.Digest(digestionparams, allFixedMods, allVariableMods);
                count += pwsm.Count();
            }

            int breakpoint = 0;
        }

        [Test]
        public static void ExploreModificationFields()
        {
            string folderPath = @"D:\Projects\Top Down MetaMorpheus\NBReplicate";
            string filepath = Path.Combine(folderPath, @"Cali_PhosphoAcetylGPTMD_Search\Task2-SearchTask\AllPSMs.psmtsv");
            //string filepath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\SequenceCoverageTestPSM.psmtsv");

            string[] searchFolders = Directory.GetDirectories(folderPath);
            List<PsmFromTsv> psmsWithInternalFragmentIons = new();
            List<PsmFromTsv> psmsWithInternalFragmentIonsControl = new();
            List<PsmFromTsv> psms = new();

            foreach (var folder in searchFolders)
            {
                string[] taskFolders = Directory.GetDirectories(folder);
                foreach (var task in taskFolders)
                {
                    if (task.Contains("SearchTask"))
                    {
                        filepath = Directory.GetFiles(task).Where(p => p.Contains("AllPSMs.psmtsv")).First();
                        psms = PsmTsvReader.ReadTsv(filepath, out List<string> errors);
                        Assert.That(errors.Count() == 0);
                        psmsWithInternalFragmentIons.AddRange(psms.Where(p => p.MatchedIons.Any(p => p.Annotation.Contains("yIb")) || p.MatchedIons.Any(p => p.Annotation.Contains("bIy"))));
                    }
                }
            }

            filepath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\SequenceCoverageTestPSM.psmtsv");
            psms = PsmTsvReader.ReadTsv(filepath, out List<string> error);
            Assert.That(error.Count() == 0);
            psmsWithInternalFragmentIonsControl.AddRange(psms.Where(p => p.MatchedIons.Any(p => p.Annotation.Contains("yIb")) || p.MatchedIons.Any(p => p.Annotation.Contains("bIy"))));
        }
        [Test]
        public static void TestSearchResultParser()
        {
            string folderPath = @"D:\Projects\Top Down MetaMorpheus\NBReplicate";
            string filesSearched = "KHB Jurkat fxns 3-12 rep 1";
            //string[] results = AAASearchResultParser.GetSpecificSearchFolderInfo(folderPath, "KHBStyle", filesSearched);
            string[] results = SearchResultParser.GetSpecificSearchFolderInfo(folderPath, "Ambiguity");
            string outputPath = @"D:\Projects\Top Down MetaMorpheus\NBReplicate\ReranAmbiguityResults.txt";
            using (FileStream stream = File.Create(outputPath))
            {
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    foreach (var line in results)
                    {
                        writer.WriteLine(line);
                    }
                }
            }
        }
        [Test]
        public static void RunIScansParser()
        {
            IScansTextParser.ParseIScans();
        }
        [Test]
        public static void RunAAAPsmFromTsvAmbiguityExtensions()
        {
            string folderPath = @"D:\Projects\Top Down MetaMorpheus\NBReplicate";
            string searchPath = Path.Combine(folderPath, @"Cali_MOxAndBioMetArtModsGPTMD_Search\Task2-SearchTask\AllPSMs.psmtsv");
            string internalPath = Path.Combine(folderPath, @"Cali_MOxAndBioMetArtModsGPTMD_SearchInternal\Task3-SearchTask\AllPSMs.psmtsv");
            string delimiter = "\t";

            string[] values = new string[] { "1", "2A", "2B", "2C", "2D", "5" };
            List<PsmFromTsv> normalSearch = PsmTsvReader.ReadTsv(searchPath, out List<string> warnings);
            foreach (var psm in normalSearch)
            {
                psm.UniqueID = psm.FileNameWithoutExtension + "_" + psm.Ms2ScanNumber + "_" + psm.PrecursorCharge + "_" + psm.PrecursorMz;
                psm.AmbiguityInfo = new();
                AmbiguityInfo.SetAmbiguityInfo(psm);

                if (values.Any(p => p == psm.AmbiguityLevel))
                {
                    Assert.That(psm.AmbiguityLevel == psm.AmbiguityInfo.AmbigType);
                }
            }
            bool[] expected = new bool[] { true, true, true, true };
            Assert.That(normalSearch.Where(p => p.AmbiguityLevel == "1").Count() == normalSearch.Where(p => p.AmbiguityInfo.AllValues.SequenceEqual(expected)).Count());
            expected = new bool[] { false, true, true, true };
            Assert.That(normalSearch.Where(p => p.AmbiguityLevel == "2A").Count() == normalSearch.Where(p => p.AmbiguityInfo.AllValues.SequenceEqual(expected)).Count());
            expected = new bool[] { true, false, true, true };
            Assert.That(normalSearch.Where(p => p.AmbiguityLevel == "2B").Count() == normalSearch.Where(p => p.AmbiguityInfo.AllValues.SequenceEqual(expected)).Count());
            expected = new bool[] { true, true, false, true };
            Assert.That(normalSearch.Where(p => p.AmbiguityLevel == "2C").Count() == normalSearch.Where(p => p.AmbiguityInfo.AllValues.SequenceEqual(expected)).Count());
            expected = new bool[] { true, true, true, false };
            Assert.That(normalSearch.Where(p => p.AmbiguityLevel == "2D").Count() == normalSearch.Where(p => p.AmbiguityInfo.AllValues.SequenceEqual(expected)).Count());

            List<PsmFromTsv> internalSearch = PsmTsvReader.ReadTsv(internalPath, out List<string> warnings2);
            foreach (var psm in internalSearch)
            {
                psm.UniqueID = psm.FileNameWithoutExtension + "_" + psm.Ms2ScanNumber + "_" + psm.PrecursorCharge + "_" + psm.PrecursorMz;
                psm.AmbiguityInfo = new();
                AmbiguityInfo.SetAmbiguityInfo(psm);

                if (values.Any(p => p == psm.AmbiguityLevel))
                {
                    Assert.That(psm.AmbiguityLevel == psm.AmbiguityInfo.AmbigType);
                }
            }
            expected = new bool[] { true, true, true, true };
            Assert.That(internalSearch.Where(p => p.AmbiguityLevel == "1").Count() == internalSearch.Where(p => p.AmbiguityInfo.AllValues.SequenceEqual(expected)).Count());
            expected = new bool[] { false, true, true, true };
            Assert.That(internalSearch.Where(p => p.AmbiguityLevel == "2A").Count() == internalSearch.Where(p => p.AmbiguityInfo.AllValues.SequenceEqual(expected)).Count());
            expected = new bool[] { true, false, true, true };
            Assert.That(internalSearch.Where(p => p.AmbiguityLevel == "2B").Count() == internalSearch.Where(p => p.AmbiguityInfo.AllValues.SequenceEqual(expected)).Count());
            expected = new bool[] { true, true, false, true };
            Assert.That(internalSearch.Where(p => p.AmbiguityLevel == "2C").Count() == internalSearch.Where(p => p.AmbiguityInfo.AllValues.SequenceEqual(expected)).Count());
            expected = new bool[] { true, true, true, false };
            Assert.That(internalSearch.Where(p => p.AmbiguityLevel == "2D").Count() == internalSearch.Where(p => p.AmbiguityInfo.AllValues.SequenceEqual(expected)).Count());

            PsmFromTsvAmbiguityExtension normalResults = new(normalSearch);
            PsmFromTsvAmbiguityExtension internalResults = new(internalSearch);

            // write ambig count results
            if (false)
            {
                string filepath = Path.Combine(folderPath, @"IntneralCountingResults.txt");
                using (FileStream stream = File.Create(filepath))
                {
                    using (StreamWriter writer = new StreamWriter(stream))
                    {
                        writer.WriteLine(PsmFromTsvAmbiguityExtension.GetAmbiguityStringHeader(delimiter));
                        writer.WriteLine(normalResults.GetAmbiguityCountString(delimiter));
                        writer.WriteLine(internalResults.GetAmbiguityCountString(delimiter));
                    }
                }
            }

            // write ambig to/from results
            if (true)
            {
                string ambig = PsmFromTsvAmbiguityExtension.ChangesInAmbiguity(normalResults, internalResults, delimiter);
                string filepath = Path.Combine(folderPath, @"InternalAmbigChangeResults.txt");
                using (FileStream stream = File.Create(filepath))
                {
                    using (StreamWriter writer = new StreamWriter(stream))
                    {
                        writer.WriteLine(ambig);
                    }
                }
            }
        }

        [Test]
        public static void LookForChimericIDsInManySearches()
        {
            string folderpath = @"D:\Projects\Top Down MetaMorpheus\NBReplicate";
            Dictionary<string, List<PsmFromTsv>> allPsms = SearchResultParser.GetPSMsFromNumerousSearchResults(folderpath, true, true);
            Dictionary<string, List<IGrouping<int, PsmFromTsv>>> groups = new();

            Dictionary<string, List<PsmFromTsv>> chimeras = new();
            foreach (var psmList in allPsms)
            {
                var groupedByScanNum = psmList.Value.GroupBy(p => new { p.PrecursorScanNum, p.RetentionTime });
                var chimerasOnly = groupedByScanNum.Where(p => p.Count() > 1);
                foreach (var chim in chimerasOnly)
                {
                    chimeras.Add(psmList.Key + chim.Key, chim.ToList());
                }
            }

        }

        [Test]
        public static void LookForChimericIDsInOneSearch()
        {
            string filepath = @"D:\Projects\Top Down MetaMorpheus\NBReplicate\Cali_MOxAndBioMetArtModsGPTMD_Search\Task2-SearchTask\AllPSMs.psmtsv";
            List<PsmFromTsv> psms = PsmTsvReader.ReadTsv(filepath, out List<string> warnings).Where(
                p => p.QValue <= 0.01 && p.DecoyContamTarget == "T" && p.AmbiguityLevel == "1" && Math.Abs(double.Parse(p.MassDiffDa)) < 1
                ).ToList();
            var chimeras = psms.GroupBy(p => new { p.PrecursorScanNum, p.RetentionTime, p.FileNameWithoutExtension }).Where(p => p.Count() > 1).ToList();

            var differentLocalizedMods = chimeras.Where(p => p.Select(m => m.BaseSeq).Distinct().Count() != p.Count()).ToList();
            var temp = differentLocalizedMods.Where(p => p.Key.FileNameWithoutExtension.Equals("FXN5_tr1_032017-calib")).ToList();
            var ms2Nums = temp.Select(p => p.Select(m => m.Ms2ScanNumber)).ToList();


        }

        [Test]
        public static void PullOutProteoformsWithSpecificPTMs()
        {
            string filepath = @"D:\Projects\Top Down MetaMorpheus\NBReplicate\Cali_MOxAndBioMetArtModsGPTMD_SearchComplimentaryInternal\Task3-SearchTask\AllProteoforms.psmtsv";
            var proteoforms = PsmTsvReader.ReadTsv(filepath, out List<string> warnings);
            proteoforms = proteoforms.Where(p => p.QValue <= 0.01).ToList();

            List<PsmFromTsv> acceptableProteoforms = new();
            foreach (var proteoform in proteoforms)
            {
                Dictionary<int, List<string>> modifications = PsmFromTsv.ParseModifications(proteoform.FullSequence);
                List<string> mods = new();
                foreach (var mod in modifications.Values)
                {
                    mods.AddRange(mod);
                }

                if (mods.Count() == 0 || mods.All(p => p.Contains("Oxidation") || p.Contains("Acetylation")))
                {
                    acceptableProteoforms.Add(proteoform);
                }
            }

            string outpath = @"D:\Projects\Top Down MetaMorpheus\Output Folder\Yuling\TrimmedProteoforms.psmtsv";

            using (StreamWriter writer = new StreamWriter(outpath))
            {
                string delim = "\t";
                writer.WriteLine("FileName" + delim + "Ms2ScanNumber" + delim + "RetentionTime" + delim + "Total Ion Current" + delim + "Precursor Mz" + delim +
                    "Precursor Charge" + delim + "Precursor Mass" + delim + "Score" + delim + "DeltaScore" + delim + "Norch" + delim + "Base Sequence" + delim +
                    "FullSequence" + delim + "Ambiguity Level" + delim + "Mods" + delim + "Protein Name" + delim + "Protein Accession" + delim + "Gene Name" + delim +
                    "Organism" + delim + "Decoy/Contaminant/Target");


                foreach (var psm in acceptableProteoforms)
                {
                    StringBuilder sb = new StringBuilder();
                    PeptideWithSetModifications pepWithMods = new(psm.FullSequence, GlobalVariables.AllModsKnownDictionary);
                    List<PeptideWithSetModifications> pepsWithMods = new() { pepWithMods };
                    sb.Append(psm.FileNameWithoutExtension + delim);
                    sb.Append(psm.Ms2ScanNumber + delim);
                    sb.Append(psm.RetentionTime + delim);
                    sb.Append(psm.TotalIonCurrent + delim);
                    sb.Append(psm.PrecursorMz + delim);
                    sb.Append(psm.PrecursorCharge + delim);
                    sb.Append(psm.PrecursorMass + delim);
                    sb.Append(psm.Score + delim);
                    sb.Append(psm.DeltaScore + delim);
                    sb.Append(psm.Notch + delim);
                    sb.Append(psm.BaseSeq + delim);
                    sb.Append(psm.FullSequence + delim);
                    sb.Append(psm.AmbiguityLevel + delim);
                    sb.Append(PsmTsvWriter.Resolve(pepsWithMods.Select(b => b.AllModsOneIsNterminus)).ResolvedString + delim);
                    sb.Append(psm.ProteinName + delim);
                    sb.Append(psm.ProteinAccession + delim);
                    sb.Append(psm.GeneName + delim);
                    sb.Append(psm.OrganismName + delim);
                    sb.Append(psm.DecoyContamTarget);
                    writer.WriteLine(sb.ToString());
                }
            }

        }

        [Test]
        public static void LookAtDifferencesInPTMs()
        {
            string directoryPath = @"D:\Projects\Top Down MetaMorpheus\For paper KHB";
            List<PsmFromTsv> calibrated = new();
            List<PsmFromTsv> unCalibrated = new();
            foreach (var file in Directory.GetDirectories(directoryPath).Where(p => p.Contains("CaliSearch") || p.Split("\\").Any(m => m.Equals("Search"))))
            {
                if (file.Contains("Cali"))
                {
                    string psmPath = SearchResultParser.FindFileInSearchFolder(file, "SearchTask", "AllProteoforms.psmtsv");
                    calibrated = PsmTsvReader.ReadTsv(psmPath, out List<string> warnings).Where(p => p.QValue <= 0.01).ToList();
                }
                else
                {
                    string psmPath = SearchResultParser.FindFileInSearchFolder(file, "SearchTask", "AllProteoforms.psmtsv");
                    unCalibrated = PsmTsvReader.ReadTsv(psmPath, out List<string> warnings).Where(p => p.QValue <= 0.01).ToList();
                }
            }
        }

        [Test]
        public static void GetAllChargeStateValuesFromMasses()
        {
            List<ProteinMassContainer> proteinMassContainers = new();
            proteinMassContainers.Add(new("Aldolase", 39211.28));
            proteinMassContainers.Add(new("Insulin", 5728.609));
            proteinMassContainers.Add(new("Albumin", 66429.09));
            proteinMassContainers.Add(new("Apomyoglobin", 16952.27));
            proteinMassContainers.Add(new("Cytochrome C", 12361.96));
            proteinMassContainers.Add(new("Follistatin", 31500));
            proteinMassContainers.Add(new("Human Beta Thrombin", 35400));
            proteinMassContainers.Add(new("Alpha Thrombin", 36700));
            proteinMassContainers.Add(new("Human C Reactive Protein", 25039));
            proteinMassContainers.Add(new("Human Growth Hormone", 22260));
            proteinMassContainers.Add(new("Serpin F2", 54566));
            proteinMassContainers.Add(new("Bovine Activated Protein C", 52650));
            proteinMassContainers.Add(new("beta lactoglobulin a", 18363));
            proteinMassContainers.Add(new("trypsinogen", 23981));
            proteinMassContainers.Add(new("Ubiquitin", 10000));
            proteinMassContainers.ForEach(p => p.FilterMzDictionary(100, 2000));

            AbsoluteTolerance tolerance = new AbsoluteTolerance(10);
            foreach (var protein in proteinMassContainers)
            {
                foreach (var mz in protein.FilteredMzByChargeDictionary.Values)
                {
                    protein.ProteinsWithSimilarMzValues = proteinMassContainers.Count(p => p.FilteredMzByChargeDictionary.Values.Any(m => tolerance.Within(mz, m)));
                    foreach (var proteinToCompare in proteinMassContainers)
                    {
                        if (protein.Name == proteinToCompare.Name)
                            break;

                        var matchedIons = proteinToCompare.FilteredMzByChargeDictionary.Values
                            .Where(p => tolerance.Within(p, mz)).ToList();
                        if (matchedIons.Any())
                        {
                            if (protein.MatchedIonsFromOtherProteinsByIon[mz].ContainsKey(proteinToCompare.Name))
                            {
                                matchedIons.ForEach(p => protein.MatchedIonsFromOtherProteinsByIon[mz][proteinToCompare.Name].AddRange(matchedIons));
                            }



                            //if (protein.ProteinsWithSimilarMzDict.ContainsKey(proteinToCompare.Name))
                            //{
                            //    matchedIons.ForEach(p => protein.ProteinsWithSimilarMzDict[proteinToCompare.Name].AddRange(matchedIons));
                            //}
                            //else
                            //{
                            //    matchedIons.ForEach(p => protein.ProteinsWithSimilarMzDict.Add(proteinToCompare.Name, matchedIons));
                            //}
                        }
                    }
                }
            }

            var temp = proteinMassContainers.Select(p => p.ProteinsWithSimilarMzValues).ToList();
        }

        [Test]
        public static void GetFragmentedMasses()
        {
            string spectraPath =
                @"R:\Nic\Chimera Validation\CaMyoUbiqCytC\221110_CaMyoUbiqCyctC_1187110_5%_Sample21_50IW.raw";
            var scans = ThermoRawFileReader.LoadAllStaticData(spectraPath).GetAllScansList()
                .Where(p => p.MsnOrder == 2);

            var fragmentedmass = scans.Select(p => Math.Round(p.SelectedIonMZ.Value, 2)).ToList();
            var distinctFragmentedmasses = fragmentedmass.Distinct().ToList();
        }
    }

    public class ProteinMassContainer
    {
        public double Mass { get; set; }
        public string Name { get; set; }
        public string Acession { get; set; }
        public Dictionary<int, double> MzByChargeDictionary { get; set; }
        public Dictionary<int, double> FilteredMzByChargeDictionary { get; set; }

        public int ProteinsWithSimilarMzValues { get; set; }
        public Dictionary<double, Dictionary<string, List<double>>> MatchedIonsFromOtherProteinsByIon { get; set; }

        public ProteinMassContainer(string name, double mass, string acession = "")
        {
            Name = name;
            Acession = acession;
            Mass = mass;
            MzByChargeDictionary = new();
            FilteredMzByChargeDictionary = new();
            ProteinsWithSimilarMzValues = 0;
            CalculateMzForAllCharges(40);
            MatchedIonsFromOtherProteinsByIon = new();
            MzByChargeDictionary.ForEach(p => MatchedIonsFromOtherProteinsByIon.Add(p.Value, new Dictionary<string, List<double>>()));
        }

        private void CalculateMzForAllCharges(int maxCharge)
        {
            for (int i = 1; i < maxCharge + 1; i++)
            {
                MzByChargeDictionary.Add(i, Mass.ToMz(i));
            }
        }

        public void FilterMzDictionary(double minMz, double maxMz)
        {
            foreach (var mz in MzByChargeDictionary)
            {
                if (mz.Value >= minMz && mz.Value <= maxMz)
                {
                    FilteredMzByChargeDictionary.Add(mz.Key, mz.Value);
                }
            }
        }
    }

}