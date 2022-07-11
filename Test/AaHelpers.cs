using EngineLayer;
using IO.MzML;
using MassSpectrometry;
using NUnit.Framework;
using Proteomics;
using Proteomics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulProteomicsDatabases;

namespace Test
{
    [TestFixture]

    public  class AaHelpers
    {

        [Test]
        public static void FindNumberOfMods()
        {
            int count = 0;
            string filepath = @"C:\Users\Nic\source\repos\MetaMorpheus\EngineLayer\Mods\Mods.txt";
            foreach (var line in File.ReadAllLines(filepath))
            {
                if (line.StartsWith("ID"))
                    count++;
            }

            Assert.AreEqual(count, 0);
        }

        [Test]
        public static void FindSizeOfDatabase()
        {
            string search = "AllMods";
            string filepathGPTMDAllMod = @"C:\Users\Nic\Desktop\FileAccessFolder\Top Down MetaMorpheus\For paper KHB\Cali_PhosphoAcetylGPTMD_Search\Task1-GPTMDTask\uniprot-proteome_UP000005640_HumanRevPlusUnrev_012422GPTMD.xml";
            string filepathGPTMDPhosphoAcetyl = @"C:\Users\Nic\Desktop\FileAccessFolder\Top Down MetaMorpheus\For paper KHB\Cali_PhosphoAcetylGPTMD_Search\Task1-GPTMDTask\uniprot-proteome_UP000005640_HumanRevPlusUnrev_012422GPTMD.xml";
            string filepathVariable = @"C:\Users\Nic\Desktop\FileAccessFolder\Top Down MetaMorpheus\For paper KHB\uniprot-proteome_UP000005640_HumanRevPlusUnrev_012422.xml";
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
            string folderPath = @"C:\Users\Nic\Desktop\FileAccessFolder\Top Down MetaMorpheus\ForPaperNBReplicate";
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
            string folderPath = @"C:\Users\Nic\Desktop\FileAccessFolder\Top Down MetaMorpheus\NBReplicate";
            string filesSearched = "KHB Jurkat fxns 3-12 rep 1";
            //string[] results = AAASearchResultParser.GetSpecificSearchFolderInfo(folderPath, "KHBStyle", filesSearched);
            string[] results = SearchResultParser.GetSpecificSearchFolderInfo(folderPath, "Ambiguity");
            string outputPath = @"C:\Users\Nic\Desktop\FileAccessFolder\Top Down MetaMorpheus\ReranAmbiguityResults.txt";
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
            string folderPath = @"C:\Users\Nic\Desktop\FileAccessFolder\Top Down MetaMorpheus\NBReplicate";
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
        public static void PlayWithMods()
        {
            PeptideWithSetModifications sodium = new("PKRKVSSAEGAAKEEPKRRSARLSAKPPAKVEAKPKKAAAKDKSSDKKVQTKGKRGAKGKQAEVANQETKEDLPAE[Metal:Sodium on E]NGETKTEESPASDEAGEKEAKSD", GlobalVariables.AllModsKnownDictionary);
            PeptideWithSetModifications magnesium = new("PKRKVSSAEGAAKEEPKRRSARLSAKPPAKVEAKPKKAAAKDKSSDKKVQTKGKRGAKGKQAEVANQETKEDLPAE[Metal:Magnesium on E]NGETKTEESPASDEAGEKEAKSD", GlobalVariables.AllModsKnownDictionary);

        }

    }
}
