using EngineLayer;
using MassSpectrometry;
using MzLibUtil;
using NUnit.Framework;
using Omics.Modifications;
using Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskLayer;
using Transcriptomics.Digestion;
using Transcriptomics;
using UsefulProteomicsDatabases;

namespace Test.Transcriptomics
{
    internal class TestRnaSearchEngine
    {

        public static RnaSearchParameters SearchParameters;
        public static CommonParameters CommonParameters;
        public static string SixmerFilePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Transcriptomics", "TestData", "GUACUG_NegativeMode_Sliced.mzML");

        [OneTimeSetUp]
        public static void Setup()
        {
            SearchParameters = new RnaSearchParameters
            {
                DecoyType = DecoyType.Reverse,
                MassDiffAcceptorType = MassDiffAcceptorType.Custom,
                CustomMdac = "Custom interval [-5,5]",
                DisposeOfFileWhenDone = true
            };
            CommonParameters = new CommonParameters
            (
                dissociationType: DissociationType.CID,
                deconvolutionMaxAssumedChargeState: -20,
                deconvolutionIntensityRatio: 3,
                deconvolutionMassTolerance: new PpmTolerance(20),
                precursorMassTolerance: new PpmTolerance(10),
                productMassTolerance: new PpmTolerance(20),
                scoreCutoff: 5,
                totalPartitions: 1,
                maxThreadsToUsePerFile: 1,
                doPrecursorDeconvolution: true,
                useProvidedPrecursorInfo: false,
                digestionParams: new RnaDigestionParams()
            );
        }

        [Test]
        public static void FindsSimpleSixmer()
        {
            List<Modification> fixedMods = new();
            List<Modification> variableMods = new();
            var dataFile = MsDataFileReader.GetDataFile(SixmerFilePath);
            var ms2Scans = MetaMorpheusTask.GetMs2Scans(dataFile, SixmerFilePath, CommonParameters)
                .OrderBy(b => b.PrecursorMass)
                .ToArray();
            MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(CommonParameters.PrecursorMassTolerance,
                SearchParameters.MassDiffAcceptorType, SearchParameters.CustomMdac);
            var osms = new OligoSpectralMatch[ms2Scans.Length];

            List<RNA> targets = new() { new RNA("GUACUG"), };
            var engine = new RnaSearchEngine(osms, targets, ms2Scans, CommonParameters, massDiffAcceptor,
                 variableMods, fixedMods, new List<(string FileName, CommonParameters Parameters)>(),
                new List<string>());
            var results = engine.Run();

        }


        [Test]
        public static void TestEngine_TwoSpectraFile()
        {
            CommonParameters commonParams = new
            (
                dissociationType: DissociationType.CID,
                deconvolutionMaxAssumedChargeState: -20,
                deconvolutionIntensityRatio: 3,
                deconvolutionMassTolerance: new PpmTolerance(20),
                precursorMassTolerance: new PpmTolerance(10),
                productMassTolerance: new PpmTolerance(20),
                scoreCutoff: 5,
                totalPartitions: 1,
                maxThreadsToUsePerFile: 1,
                doPrecursorDeconvolution: true,
                useProvidedPrecursorInfo: false,
                digestionParams: new RnaDigestionParams()
            );
            RnaSearchParameters searchParams = new()
            {
                DisposeOfFileWhenDone = true,
                MassDiffAcceptorType = MassDiffAcceptorType.Custom,
                CustomMdac = "Custom interval [-5,5]",
                DecoyType = DecoyType.Reverse
            };

            string filePath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"Transcriptomics\TestData",
                "GUACUG_NegativeMode_Sliced.mzML");
            var dataFile = MsDataFileReader.GetDataFile(filePath);
            var ms2Scans = MetaMorpheusTask.GetMs2Scans(dataFile, filePath, commonParams)
                .OrderBy(b => b.PrecursorMass)
                .ToArray();

            List<Modification> fixedMods = new();
            List<Modification> variableMods = new ();
            MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(commonParams.PrecursorMassTolerance,
                searchParams.MassDiffAcceptorType, searchParams.CustomMdac);
            var osms = new OligoSpectralMatch[ms2Scans.Length];
            List<RNA> targets = new() { new RNA("GUACUG"), };

            var engine = new RnaSearchEngine(osms, targets, ms2Scans, commonParams, massDiffAcceptor,
                 variableMods, fixedMods, new List<(string FileName, CommonParameters Parameters)>(),
                new List<string>());
            var results = engine.Run();


            var oligoSpectralMatches = osms.Where(p => p != null)
                .OrderByDescending(p => p.Score).ToList();
            var match = oligoSpectralMatches.First();
            Assert.That(match.BaseSequence, Is.EqualTo("GUACUG"));
            Assert.That(match.Score, Is.EqualTo(21.370).Within(0.001));
            Assert.That(match.MatchedFragmentIons.Count, Is.EqualTo(29));
        }
    }
}
