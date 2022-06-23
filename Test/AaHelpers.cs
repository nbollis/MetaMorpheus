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

    internal static class AaHelpers
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
            string filepath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\SequenceCoverageTestPSM.psmtsv");
            List<PsmFromTsv> psms = PsmTsvReader.ReadTsv(filepath, out List<string> errors);
            Assert.That(errors.Count() == 0);

            List<MatchedFragmentIon> both = new();
            List<MatchedFragmentIon> n = new();
            List<MatchedFragmentIon> c = new();
            List<MatchedFragmentIon> none = new();
            foreach (var psm in psms)
            {
                both.AddRange(psm.MatchedIons.Where(p => p.NeutralTheoreticalProduct.Terminus == FragmentationTerminus.Both));
                n.AddRange(psm.MatchedIons.Where(p => p.NeutralTheoreticalProduct.Terminus == FragmentationTerminus.N));
                c.AddRange(psm.MatchedIons.Where(p => p.NeutralTheoreticalProduct.Terminus == FragmentationTerminus.C));
                none.AddRange(psm.MatchedIons.Where(p => p.NeutralTheoreticalProduct.Terminus == FragmentationTerminus.None));
            }
        }

    }
}
