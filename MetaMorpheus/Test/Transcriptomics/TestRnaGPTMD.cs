using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chemistry;
using EngineLayer;
using EngineLayer.Gptmd;
using MassSpectrometry;
using MzLibUtil;
using NUnit.Framework;
using Omics.Fragmentation;
using Omics.Modifications;
using Proteomics.ProteolyticDigestion;
using Readers;
using TaskLayer;
using Transcriptomics;
using Transcriptomics.Digestion;

namespace Test.Transcriptomics
{
    internal class TestRnaGPTMD
    {
        [Test]
        [TestCase("GUACUG", 28.030300, 6)]
        [TestCase("GUACUG", 14.015650, 6)]
        public static void TestRunningGptmd_AllResiduesWithCombos(string rnaSequence, double modMassToAdd, int expectedGptmdMods)
        {
            var allResultingIdentifications = new List<SpectralMatch>();
            IEnumerable<Tuple<double, double>> combos = new List<Tuple<double, double>>()
                { new Tuple<double, double>(14.015650, 14.015650) };
            Tolerance precursorMassTolerance = new PpmTolerance(10); 
            var gptmdMods = GlobalVariables.AllRnaModsKnown.Where(p => p.IdWithMotif.Contains("Methyl"))
                .ToList();

            List<Modification> variableModifications = new List<Modification>();
            var commonParams = new CommonParameters(digestionParams: new RnaDigestionParams("top-down"),
                listOfModsVariable: new List<(string, string)>(),
                listOfModsFixed: new List<(string, string)>(),
                deconvolutionMaxAssumedChargeState: -12);
            var fsp = new List<(string fileName, CommonParameters fileSpecificParameters)>();
            fsp.Add(("filepath", commonParams));

            // run the engine where nothing will be found
            var engine = new GptmdEngine(allResultingIdentifications, gptmdMods, combos, 
                new Dictionary<string, Tolerance> { { "filepath", precursorMassTolerance } },
                new CommonParameters(), fsp, new List<string>());
            var gptmdResults = (GptmdResults)engine.Run();
            Assert.AreEqual(0, gptmdResults.Mods.Count);

            // set up an oligo 
            var rna = new RNA(rnaSequence, "", "accession", "", "");
            var digestedOligo = rna.Digest(commonParams.DigestionParams, new(), variableModifications).First();
            Ms2ScanWithSpecificMass scan = new Ms2ScanWithSpecificMass(new MsDataScan(new MzSpectrum(new double[] { 1 },
                new double[] { 1 }, false), 0, 1, true, Polarity.Positive,
                double.NaN, null, null, MZAnalyzerType.Orbitrap, double.NaN,
                null, null, "scan=1", double.NaN, null,
                null, double.NaN, null, DissociationType.AnyActivationType, 
                0, null), 
                (digestedOligo.MonoisotopicMass + modMassToAdd).ToMz(1),
            1, "filepath", new CommonParameters());

            SpectralMatch newPsm = new OligoSpectralMatch(digestedOligo, 0, 0, 0, scan, commonParams, new List<MatchedFragmentIon>());
            

            newPsm.SetFdrValues(1, 0, 0, 1, 0, 0, 0, 0);
            allResultingIdentifications.Add(newPsm);

            engine = new GptmdEngine(allResultingIdentifications, gptmdMods, combos,
                new Dictionary<string, Tolerance> { { "filepath", precursorMassTolerance } }, new CommonParameters(),
                null, new List<string>());
            gptmdResults = (GptmdResults)engine.Run();
            Assert.AreEqual(1, gptmdResults.Mods.Count);

            if (expectedGptmdMods == 0)
                Assert.That(gptmdResults.Mods.Count == 0);
            else
                Assert.AreEqual(expectedGptmdMods, gptmdResults.Mods["accession"].Count);
        }

        [Test]
        [TestCase("GUACUG", 28.030300, 2)]
        [TestCase("GUACUG", 14.015650, 2)]
        public static void TestRunningGptmd_FewResiduesWithCombos(string rnaSequence, double modMassToAdd, int expectedGptmdMods)
        {
            var allResultingIdentifications = new List<SpectralMatch>();
            IEnumerable<Tuple<double, double>> combos = new List<Tuple<double, double>>()
                { new Tuple<double, double>(14.015650, 14.015650) };
            Tolerance precursorMassTolerance = new PpmTolerance(10);
            var gptmdMods = GlobalVariables.AllRnaModsKnown.Where(p => p.IdWithMotif.Contains("Methylation on G"))
                .ToList();

            List<Modification> variableModifications = new List<Modification>();
            var commonParams = new CommonParameters(digestionParams: new RnaDigestionParams("top-down"),
                listOfModsVariable: new List<(string, string)>(),
                listOfModsFixed: new List<(string, string)>(),
                deconvolutionMaxAssumedChargeState: -12);
            var fsp = new List<(string fileName, CommonParameters fileSpecificParameters)>();
            fsp.Add(("filepath", commonParams));

            // run the engine where nothing will be found
            var engine = new GptmdEngine(allResultingIdentifications, gptmdMods, combos,
                new Dictionary<string, Tolerance> { { "filepath", precursorMassTolerance } },
                new CommonParameters(), fsp, new List<string>());
            var gptmdResults = (GptmdResults)engine.Run();
            Assert.AreEqual(0, gptmdResults.Mods.Count);

            // set up an oligo 
            var rna = new RNA(rnaSequence, "", "accession", "", "");
            var digestedOligo = rna.Digest(commonParams.DigestionParams, new(), variableModifications).First();
            Ms2ScanWithSpecificMass scan = new Ms2ScanWithSpecificMass(new MsDataScan(new MzSpectrum(new double[] { 1 },
                new double[] { 1 }, false), 0, 1, true, Polarity.Positive,
                double.NaN, null, null, MZAnalyzerType.Orbitrap, double.NaN,
                null, null, "scan=1", double.NaN, null,
                null, double.NaN, null, DissociationType.AnyActivationType,
                0, null),
                (digestedOligo.MonoisotopicMass + modMassToAdd).ToMz(1),
            1, "filepath", new CommonParameters());

            SpectralMatch newPsm = new OligoSpectralMatch(digestedOligo, 0, 0, 0, scan, commonParams, new List<MatchedFragmentIon>());


            newPsm.SetFdrValues(1, 0, 0, 1, 0, 0, 0, 0);
            allResultingIdentifications.Add(newPsm);

            engine = new GptmdEngine(allResultingIdentifications, gptmdMods, combos,
                new Dictionary<string, Tolerance> { { "filepath", precursorMassTolerance } }, new CommonParameters(),
                null, new List<string>());
            gptmdResults = (GptmdResults)engine.Run();
            Assert.AreEqual(1, gptmdResults.Mods.Count);

            if (expectedGptmdMods == 0)
                Assert.That(gptmdResults.Mods.Count == 0);
            else
                Assert.AreEqual(expectedGptmdMods, gptmdResults.Mods["accession"].Count);
        }


        [Test]
        [TestCase("GUACUGAUGAUAUAYAU", 14.015650, 1)]
        [TestCase("GUACUGAUGACUAUUAAAUA", 14.015650, 2)]
        public static void TestRunningGptmd_FewResidues_NoCombos(string rnaSequence, double modMassToAdd, int expectedGptmdMods)
        {
            var allResultingIdentifications = new List<SpectralMatch>();
            IEnumerable<Tuple<double, double>> combos = new List<Tuple<double, double>>();
            Tolerance precursorMassTolerance = new PpmTolerance(10);
            var gptmdMods = GlobalVariables.AllRnaModsKnown.Where(p => p.IdWithMotif.Contains("Methylation on C"))
                .ToList();

            List<Modification> variableModifications = new List<Modification>();
            var commonParams = new CommonParameters(digestionParams: new RnaDigestionParams("top-down"),
                listOfModsVariable: new List<(string, string)>(),
                listOfModsFixed: new List<(string, string)>(),
                deconvolutionMaxAssumedChargeState: -12);
            var fsp = new List<(string fileName, CommonParameters fileSpecificParameters)>();
            fsp.Add(("filepath", commonParams));

            // run the engine where nothing will be found
            var engine = new GptmdEngine(allResultingIdentifications, gptmdMods, combos,
                new Dictionary<string, Tolerance> { { "filepath", precursorMassTolerance } },
                new CommonParameters(), fsp, new List<string>());
            var gptmdResults = (GptmdResults)engine.Run();
            Assert.AreEqual(0, gptmdResults.Mods.Count);

            // set up an oligo 
            var rna = new RNA(rnaSequence, "", "accession", "", "");
            var digestedOligo = rna.Digest(commonParams.DigestionParams, new(), variableModifications).First();
            Ms2ScanWithSpecificMass scan = new Ms2ScanWithSpecificMass(new MsDataScan(new MzSpectrum(new double[] { 1 },
                new double[] { 1 }, false), 0, 1, true, Polarity.Positive,
                double.NaN, null, null, MZAnalyzerType.Orbitrap, double.NaN,
                null, null, "scan=1", double.NaN, null,
                null, double.NaN, null, DissociationType.AnyActivationType,
                0, null),
                (digestedOligo.MonoisotopicMass + modMassToAdd).ToMz(1),
            1, "filepath", new CommonParameters());

            SpectralMatch newPsm = new OligoSpectralMatch(digestedOligo, 0, 0, 0, scan, commonParams, new List<MatchedFragmentIon>());


            newPsm.SetFdrValues(1, 0, 0, 1, 0, 0, 0, 0);
            allResultingIdentifications.Add(newPsm);

            engine = new GptmdEngine(allResultingIdentifications, gptmdMods, combos,
                new Dictionary<string, Tolerance> { { "filepath", precursorMassTolerance } }, new CommonParameters(),
                null, new List<string>());
            gptmdResults = (GptmdResults)engine.Run();
            Assert.AreEqual(1, gptmdResults.Mods.Count);

            if (expectedGptmdMods == 0)
                Assert.That(gptmdResults.Mods.Count == 0);
            else
                Assert.AreEqual(expectedGptmdMods, gptmdResults.Mods["accession"].Count);
        }
    }
}
