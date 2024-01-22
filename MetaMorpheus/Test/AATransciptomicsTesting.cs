using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Chemistry;
using EngineLayer;
using GuiFunctions;
using MassSpectrometry;
using MzLibUtil;
using Nett;
using NUnit.Framework;
using OxyPlot.Wpf;
using Readers;
using ThermoFisher.CommonCore.Data;
using Transcriptomics;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using TaskLayer;
using Path = System.IO.Path;
using UsefulProteomicsDatabases;
using UsefulProteomicsDatabases.Transcriptomics;
using Easy.Common.Extensions;
using MathNet.Numerics;
using Omics.Fragmentation;
using Omics.Modifications;
using Transcriptomics.Digestion;

namespace Test
{
    [TestFixture]
    [NUnit.Framework.Ignore("Development")]
    internal class AATransciptomicsTesting
    {
        public static RNA Sixmer;
        public static RNA Twentymer;
        public static RNA TwentymerIsobar;
        public static RNA TwentymerWithMods;
        public static RNA Fiftymer;
        public static RNA FiftymerWithMods;

        public static string SixmerSpecPath =>
            @"B:\Users\Whitworth\Raw Mass Spec Data\Mass Spec Data 2023\Direct Injection RNA\H2O_HFIPandTEA.raw";

        public static string SixmerTheoreticalSpecpath =>
            @"D:\Projects\RNA\WorkingThroughCode\MatchingFragmentIons\sixMerTheroetical.mzML";

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            Sixmer = new RNA("GUACUG");
            Twentymer = new RNA("GUACUGCCUCUAGUGAAGCA");
            Fiftymer = new RNA("CUCCGUAAUGCGAAAUACGCUAUGCUGCCUCUAGUGACUGCAUGACACAA");
        }

        #region Set Up Stuff

        [Test]
        public static void GetDataSubset()
        {
            string filePath =
                @"B:\Users\Whitworth\Raw Mass Spec Data\Mass Spec Data 2023\Direct Injection RNA\H2O_HFIPandTEA.raw";
            MsDataFile sixMerFile = MsDataFileReader.GetDataFile(filePath).LoadAllStaticData();
            var scansOfInterest = sixMerFile.Scans.Where(p => p.OneBasedScanNumber is >= 750 and <= 900).ToArray();

            string tomlPath = @"D:\Projects\RNA\TestData\SixMerMs2Scans.toml";
            Toml.WriteFile<MsDataScan[]>(scansOfInterest, tomlPath);

            var readInScans = Toml.ReadFile<MsDataScan[]>(tomlPath);

            for (var index = 0; index < readInScans.Length; index++)
            {
                var scan = readInScans[index];
                var og = scansOfInterest[index];

                Assert.That(scan.Equals(og));
                Assert.That(scan.MassSpectrum.Equals(og.MassSpectrum));
            }
        }

        #endregion




        #region Creating Theoretical Spectra

        [Test]
        public static void Runner()
        {
            string outPath = @"D:\Projects\RNA\WorkingThroughCode\MatchingFragmentIons\sixMerTheroetical.mzML";
            var scan = GenerateTheoreticalSpectrum(Sixmer);
            var sourceFile = new SourceFile("no nativeID format", "mzML format", null, null, filePath: outPath, null);


            var file = new GenericMsDataFile(new MsDataScan[] { scan.Ms1, scan.Ms2 }, sourceFile);
            file.ExportAsMzML(outPath, true);
        }

        public static (MsDataScan Ms1, MsDataScan Ms2) GenerateTheoreticalSpectrum(NucleicAcid rna)
        {
            List<Product> fragments = new();
            rna.Digest(new RnaDigestionParams(), new List<Modification>(), new List<Modification>()).First()
                .Fragment(DissociationType.CID, FragmentationTerminus.Both, fragments);

            var mzs = fragments.Select(p => p.NeutralMass.ToMz(-1)).ToArray();
            var intensites = Enumerable.Repeat(1.0, mzs.Length).ToArray();

            var spectrum = new MzSpectrum(mzs, intensites, false);

            var ms1 = new MsDataScan(new MzSpectrum(new[] { rna.MonoisotopicMass.ToMz(-3) }, new[] { 1e6 }, false), 1,
                1, true, Polarity.Negative, 0.9, new MzRange(50, 2000), "",
                MZAnalyzerType.Orbitrap, 1e6, 0.5, new double[1, 1], "controllerType=0 controllerNumber=1 scan=1");
            var ms2 = new MsDataScan(spectrum, 2, 2, true, Polarity.Negative, 1, new MzRange(50, 2000), "",
                MZAnalyzerType.Orbitrap, spectrum.SumOfAllY, 0.5, new double[1, 1],
                "controllerType=0 controllerNumber=1 scan=2",
                rna.MonoisotopicMass.ToMz(3), -3, 1e6, rna.MonoisotopicMass.ToMz(3), 4, DissociationType.CID,
                1, rna.MonoisotopicMass.ToMz(3), "30");
            return (ms1, ms2);
        }

        #endregion

        #region Matching Fragment Ions

        [Test]
        public static void MatchFragmentIonsFromRealScan()
        {
            CommonParameters commonParams = new(dissociationType: DissociationType.CID);

            List<OligoSpectralMatch> spectralMatches = new();
            MsDataFile sixMerFile = MsDataFileReader.GetDataFile(SixmerSpecPath);
            sixMerFile.InitiateDynamicConnection();
            for (int i = 750; i < 900; i++)
            {
                var scan = sixMerFile.GetOneBasedScanFromDynamicConnection(i);
                var ms2WithMass = new Ms2ScanWithSpecificMass(scan, scan.IsolationMz.Value,
                    scan.SelectedIonChargeStateGuess.Value, SixmerSpecPath, commonParams);
                var products = new List<Product>();
                Sixmer.Digest(new RnaDigestionParams(), new List<Modification>(), new List<Modification>()).First()
                    .Fragment(DissociationType.CID, FragmentationTerminus.Both, products);


                var matched = MetaMorpheusEngine.MatchFragmentIons(ms2WithMass, products, commonParams);
                if (matched.Any())
                    spectralMatches.Add(new OligoSpectralMatch(scan, Sixmer, Sixmer.BaseSequence, matched,
                        SixmerSpecPath));
            }

            sixMerFile.CloseDynamicConnection();

            //string outPath =
            //   @"D:\Projects\RNA\WorkingThroughCode\MatchingFragmentIons\SixMer_750-900_Attempt3.tsv";
            // OligoSpectralMatch.Export(spectralMatches, outPath);
            var mostMatched = GetMostMatched(spectralMatches);
            foreach (var oligoSpectralMatch in mostMatched)
            {
                ExportPlot(oligoSpectralMatch);
            }

        }

        [Test]
        public static void ReadOsmTsv()
        {
            string path =
                @"B:\Users\Whitworth\Raw Mass Spec Data\Mass Spec Data 2023\Direct Injection RNA\H2O_HFIPandTEA.raw_730-900.osmtsv";

            var temp = OligoSpectralMatch.Import(path, out List<string> warnings);
        }

        [Test]
        public static void MatchIonsFromTheoreticalScan()
        {
            CommonParameters commonParams = new(dissociationType: DissociationType.CID);
            var ms2Scan = MsDataFileReader.GetDataFile(SixmerTheoreticalSpecpath).GetAllScansList()[1];

            var matched = MatchFragments(ms2Scan, Sixmer, commonParams);
            Assert.That(matched.Count, Is.EqualTo(20));
        }


        private static List<MatchedFragmentIon> MatchFragments(MsDataScan scan, NucleicAcid nucleicAcid,
            CommonParameters commonParams)
        {
            var ms2WithMass = new Ms2ScanWithSpecificMass(scan, scan.IsolationMz.Value,
                scan.SelectedIonChargeStateGuess.Value, SixmerSpecPath, commonParams);
            var products = new List<Product>();
            nucleicAcid.Digest(new RnaDigestionParams(), new List<Modification>(), new List<Modification>()).First()
                .Fragment(DissociationType.CID, FragmentationTerminus.Both, products);

            return MetaMorpheusEngine.MatchFragmentIons(ms2WithMass, products, commonParams);
        }

        public static void ExportPlot(OligoSpectralMatch match)
        {
            var t = new Thread(o =>
            {
                string outPath =
                    @$"D:\Projects\RNA\WorkingThroughCode\MatchingFragmentIons\SixMerPics\{match.BaseSequence}_{match.ScanNumber}.png";
                DummyPlot plot = new DummyPlot(match.MsDataScan, match.MatchedFragmentIons, new PlotView());
                plot.ExportToPng(outPath);
                System.Windows.Threading.Dispatcher.Run();
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        public static IEnumerable<OligoSpectralMatch> GetMostMatched(List<OligoSpectralMatch> matches)
        {
            int mostMatched = matches.Max(p => p.MatchedFragmentIons.Count);
            return matches.Where(p => p.MatchedFragmentIons.Count >= mostMatched);
        }

        #endregion

        #region Sid Exploration

        // 2545 -> 2620
        public static string SixMerOsmPath =
            @"D:\Projects\RNA\TestData\ISF\230627_RNA_withISF_1ul_GUACUG_2545-2620.osmtsv";

        // 2927 -> 3049
        public static string TwentyMerOsmPath =
            @"D:\Projects\RNA\TestData\ISF\230627_RNA_withISF_1ul_GUACUGCCUCUAGUGAAGCA_2927-3049.osmtsv";

        // 3050 -> 3120
        public static string FiftyMerOsmpath =
            @"D:\Projects\RNA\TestData\ISF\230627_RNA_withISF_1ul_CUCCGUAAUGCGAAAUACGC_3050-3120.osmtsv";


        [Test]
        public void ExploreSidData()
        {

            var header = $"Msn Order,Sid Energy,Length,ScanNum,Matched Ion Count,";
            header += string.Join(',', Enum.GetNames<ProductType>());

            string outpath = @"D:\Projects\RNA\TestData\ISF\ISFfragments_6,20,50_10PercentMinIntensity.xlsx";
            using StreamWriter sw = new StreamWriter(File.Create(outpath));
            sw.WriteLine(header);
            sw.Write(SidTest(Sixmer, SixMerOsmPath));
            sw.Write(SidTest(Twentymer, TwentyMerOsmPath));
            sw.Write(SidTest(Fiftymer, FiftyMerOsmpath));


        }



        private string SidTest(RNA rna, string osmPath)
        {

            var osms = OligoSpectralMatch.Import(osmPath, out List<string> warnings);
            Assert.That(warnings.IsNullOrEmpty());

            var filteredOsms = osms.Where(p => p.MatchedFragmentIons.Count >= 10).ToList();

            var header = $"Msn Order,Sid Energy,Length,ScanNum,Matched Ion Count,";
            header += string.Join(',', Enum.GetNames<ProductType>());

            var resultBuilder = new StringBuilder();
            foreach (var osm in filteredOsms)
            {
                var startString =
                    $"MSn {osm.MsnOrder},SID {osm.SidEnergy},{osm.BaseSequence.Length},{osm.ScanNumber},{osm.MatchedFragmentIons.Count},";
                var ionCounts = GetIonCount(osm);
                var resultString = startString + ionCounts;

                resultBuilder.AppendLine(resultString);
            }

            return resultBuilder.ToString();
        }

        private string GetIonCount(OligoSpectralMatch osm)
        {
            var ionDict = Enum.GetValues<ProductType>().ToDictionary(p => p,
                p => osm.MatchedFragmentIons.Count(ion => ion.NeutralTheoreticalProduct.ProductType == p));


            var results = string.Join(",", ionDict.Values);
            return results;
        }

        #endregion

        #region Framentation Exploration


        [Test]
        public static void CombineSearchAndDataResultsFromEngine()
        {
            Dictionary<string, (string dataFile, string database)> dataFiles = new()
            {
                {
                    "6mer",
                    (@"B:\Users\Whitworth\Raw Mass Spec Data\Mass Spec Data 2023\Direct Injection RNA\231023\231025_ITW_6mer_5050_CIDTesting.raw",
                        @"D:\Projects\RNA\TestData\Databases\6mer.fasta")
                },
                {
                    "20mer1",
                    (@"B:\Users\Whitworth\Raw Mass Spec Data\Mass Spec Data 2023\Direct Injection RNA\231023\231025_ITW_20mer1_5050_CIDTesting.raw",
                        @"D:\Projects\RNA\TestData\Databases\20mer1.fasta")
                },
                {
                    "20mer2",
                    (@"B:\Users\Whitworth\Raw Mass Spec Data\Mass Spec Data 2023\Direct Injection RNA\231023\231025_ITW_20mer2_5050_CIDTesting.raw",
                        @"D:\Projects\RNA\TestData\Databases\20mer2.fasta")
                },
                {
                    "20mer3",
                    (@"B:\Users\Whitworth\Raw Mass Spec Data\Mass Spec Data 2023\Direct Injection RNA\231023\231025_ITW_20mer3_5050_CIDTesting.raw",
                        @"D:\Projects\RNA\TestData\Databases\20mer3.fasta")
                },
                {
                    "20mer4",
                    (@"B:\Users\Whitworth\Raw Mass Spec Data\Mass Spec Data 2023\Direct Injection RNA\231023\231025_ITW_20mer4_5050_CIDTesting.raw",
                        @"D:\Projects\RNA\TestData\Databases\20mer4.fasta")
                },
                {
                    "75mer",
                    (@"B:\Users\Whitworth\Raw Mass Spec Data\Mass Spec Data 2023\Direct Injection RNA\231023\231025_ITW_75mer_5050_CIDTesting.raw",
                        @"D:\Projects\RNA\TestData\Databases\75mer.fasta")
                },
            };

            var toUse = dataFiles.Where(p => p.Key == "20mer3").First();
            var dataFile = MsDataFileReader.GetDataFile(toUse.Value.dataFile).LoadAllStaticData();
            var oligos = RnaDbLoader.LoadRnaFasta(toUse.Value.database, true, DecoyType.None, false,
                out List<string> errors);
            string modFile = Path.Combine(GlobalVariables.DataDir, "Mods", "RnaMods.txt");
            var allMods = PtmListLoader.ReadModsFromFile(modFile, out var errorMods)
                .ToDictionary(p => p.IdWithMotif, p => p);
            var metals = allMods.Values.Where(p => p.ModificationType == "Metal").ToList();

            if (toUse.Key == "20mer2")
            {

                Dictionary<int, List<Modification>> twentyMerTwoModDict = new()
                {
                    { 2, new List<Modification> { allMods["Methylation on U"] } },
                    { 8, new List<Modification> { allMods["Methylation on C"] } },
                    { 13, new List<Modification> { allMods["Methylation on G"] } },
                    { 18, new List<Modification> { allMods["Methylation on G"] } },
                };

                oligos = new List<RNA>
                {
                    new RNA(oligos.First().BaseSequence, oligos.First().FivePrimeTerminus,
                        oligos.First().ThreePrimeTerminus, twentyMerTwoModDict)
                };
            }
            else if (toUse.Key == "20mer4")
            {
                Dictionary<int, List<Modification>> twentyMerFourModDict = new()
                {
                    { 3, new List<Modification> { allMods["Methylation on A"] } }, // methyl
                    { 5, new List<Modification> { allMods["DeoxyFluoronation on U"] } }, // fluoro
                    { 7, new List<Modification> { allMods["Deoxylnosine on H"] } }, // deoxylnosine
                    { 8, new List<Modification> { allMods["Methylation on C"] } }, // methyl
                    { 10, new List<Modification> { allMods["DeoxyFluoronation on C"] } }, // fluoro
                    { 14, new List<Modification> { allMods["Deoxylnosine on H"] } }, // deoxylnosine
                    { 16, new List<Modification> { allMods["MethoxyEthoxylation on A"] } }, // methoxy
                    { 17, new List<Modification> { allMods["Methylation on G"] } }, // methyl
                };
                oligos = new List<RNA>
                {
                    new RNA(oligos.First().BaseSequence, oligos.First().FivePrimeTerminus,
                        oligos.First().ThreePrimeTerminus, twentyMerFourModDict)
                };
            }

            CommonParameters commonParams = new
            (
                dissociationType: DissociationType.CID,
                deconvolutionMaxAssumedChargeState: -20,
                deconvolutionIntensityRatio: 3,
                deconvolutionMassTolerance: new PpmTolerance(20),
                scoreCutoff: 5,
                totalPartitions: 1,
                maxThreadsToUsePerFile: 1,
                doPrecursorDeconvolution: true,
                useProvidedPrecursorInfo: false
            );



            RnaSearchParameters searchParams = new()
            {
                DisposeOfFileWhenDone = true,
                FragmentIonTolerance = new PpmTolerance(20),
                MatchAllCharges = true,
                MatchAllScans = true,
                MatchMs1 = false,
                MatchMs2 = true,
                MassDiffAcceptorType = MassDiffAcceptorType.Custom,
                CustomMdac = "Custom interval [-10,10]",
                DigestionParams = new(
                    maxMods: 6,
                    maxModificationIsoforms: 2048
                )
            };


            List<Modification> fixedMods = new();
            List<Modification> variableMods = metals; /*new List<Modification>();*/
            MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(searchParams.PrecursorMassTolerance,
                searchParams.MassDiffAcceptorType, searchParams.CustomMdac);
            Ms2ScanWithSpecificMass[] ms2WithSpecificMasses = MetaMorpheusTask
                .GetMs2Scans(dataFile, dataFile.FilePath, commonParams)
                .OrderBy(b => b.PrecursorMass)
                .ToArray();
            var osms = new OligoSpectralMatch[ms2WithSpecificMasses.Length];
            var engine = new RnaSearchEngine(osms, oligos, ms2WithSpecificMasses, commonParams,
                massDiffAcceptor,
                searchParams.DigestionParams, variableMods, fixedMods,
                new List<(string FileName, CommonParameters Parameters)>(),
                new List<string>());
            var results = engine.Run();
            var oligoSpectralMatches = osms.Where(p => p != null).OrderByDescending(p => p.Score).ToList();
            string specific = "+-10_WithAdducts_Max6";
            string outPath = @$"B:\Users\Nic\RNA\CidExperiments\{toUse.Key}_{specific}.osmtsv";
            OligoSpectralMatch.Export(oligoSpectralMatches, outPath);


            //ScoreAllScansFromUnAdductedPrecursor(dataFile, oligos.Last(), commonParams, digestionParams, toUse.Key, -15, -5);
        }

        public static void ScoreAllScansFromUnAdductedPrecursor(MsDataFile dataFile, RNA rna,
            CommonParameters commonParams, RnaDigestionParams digestionParams, string rnaName = "", int minCharge = -20,
            int maxCharge = -1)
        {
            AbsoluteTolerance tolerance = new AbsoluteTolerance(1);
            List<Ms2ScanWithSpecificMass> ms2WithSpecificMasses = MetaMorpheusTask
                .GetMs2Scans(dataFile, dataFile.FilePath, commonParams)
                .OrderBy(b => b.PrecursorMass)
                .ToList();

            List<Product> theoreticalProducts = new();
            var oligo = rna.Digest(digestionParams, new List<Modification>(), new List<Modification>()).Last();
            oligo.Fragment(DissociationType.CID, FragmentationTerminus.Both, theoreticalProducts);

            // testing ms2withspecific mass getter
            var ms2ScanNums = dataFile.Where(p => p.MsnOrder == 2).Select(p => p.OneBasedScanNumber).Distinct()
                .ToList();
            var ms2WithMassScanNums =
                ms2WithSpecificMasses.Select(p => p.TheScan.OneBasedScanNumber).Distinct().ToList();
            var temp = ms2ScanNums.Except(ms2WithMassScanNums).ToList();

            // foreach ms2 scan, if the precursor is within the tolerance of an esi series, score it
            List<FragmentationExplorationResult> results = new();
            List<double> esiSeries = new();
            for (var i = minCharge; i < maxCharge; i++)
                esiSeries.Add(oligo.MonoisotopicMass.ToMz(i));


            foreach (var ms2Scan in dataFile.Where(p => p.MsnOrder == 2))
            {
                foreach (var theoreticalMz in esiSeries)
                {
                    if (tolerance.Within(ms2Scan.IsolationMz ?? 0, theoreticalMz))
                    {
                        var ms2ScanWithMass = ms2WithSpecificMasses.FirstOrDefault(p =>
                            p.TheScan.OneBasedScanNumber == ms2Scan.OneBasedScanNumber);
                        if (ms2ScanWithMass == null) continue;

                        var matchedIons =
                            MetaMorpheusEngine.MatchFragmentIons(ms2ScanWithMass, theoreticalProducts, commonParams);
                        var score = MetaMorpheusEngine.CalculatePeptideScore(ms2Scan, matchedIons).Round(4);
                        var osm = new OligoSpectralMatch(oligo, 0, score, 0, ms2ScanWithMass, commonParams, matchedIons,
                            digestionParams);
                        results.Add(new FragmentationExplorationResult(ms2Scan, osm));
                    }
                }
            }

            string outPath = @$"B:\Users\Nic\RNA\CidExperiments\{rnaName}FragExplorationResults.csv";
            FragmentationExplorationResultFile file = new();
            file.Results = results;
            file.WriteResults(outPath);
        }

        #endregion

        #region Engine Testing

        [Test]
        public static void TestEngine()
        {
            CommonParameters commonParams = new
            (
                dissociationType: DissociationType.CID,
                deconvolutionMaxAssumedChargeState: -20,
                deconvolutionIntensityRatio: 3,
                deconvolutionMassTolerance: new PpmTolerance(20),
                scoreCutoff: 5,
                totalPartitions: 1,
                maxThreadsToUsePerFile: 1,
                doPrecursorDeconvolution: true,
                useProvidedPrecursorInfo: false
            );
            RnaSearchParameters searchParams = new()
            {
                DisposeOfFileWhenDone = true,
                FragmentIonTolerance = new PpmTolerance(20),
                MatchAllCharges = true,
                MatchAllScans = true,
                MatchMs1 = false,
                MatchMs2 = true,
                MassDiffAcceptorType = MassDiffAcceptorType.Custom,
                CustomMdac = "Custom interval [-5,5]"
            };

            string modFile = Path.Combine(GlobalVariables.DataDir, "Mods", "RnaMods.txt");
            var allMods = PtmListLoader.ReadModsFromFile(modFile, out var errorMods);

            RnaDigestionParams digestionParams = new RnaDigestionParams(maxMods: 4, maxModificationIsoforms: 2048);
            List<Modification> fixedMods = new();
            List<Modification> variableMods = /*allMods.ToList();*/ new List<Modification>();
            MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(searchParams.PrecursorMassTolerance,
                searchParams.MassDiffAcceptorType, searchParams.CustomMdac);

            string path = @"B:\Users\Nic\RNA\CidExperiments\231025_ITW_6mer_5050_CIDTesting.raw";
            var dataFile = MsDataFileReader.GetDataFile(path);
            var ms2Scans = MetaMorpheusTask.GetMs2Scans(dataFile, path, commonParams)
                .OrderBy(b => b.PrecursorMass)
                .ToArray();

            var temp = ms2Scans.Select(p => p.PrecursorMass).Distinct().ToList();
            var osms = new OligoSpectralMatch[ms2Scans.Count()];

            List<RNA> targets = new()
            {
                new RNA("GUACUG"),
            };


            var engine = new RnaSearchEngine(osms, targets, ms2Scans, commonParams, massDiffAcceptor,
                digestionParams, variableMods, fixedMods, new List<(string FileName, CommonParameters Parameters)>(),
                new List<string>());
            var results = engine.Run();


            var oligoSpectralMatches = osms.Where(p => p != null).OrderByDescending(p => p.Score).ToList();


            string specific = "+-5NoMods_new";
            string outPath = @$"B:\Users\Nic\RNA\CidExperiments\231025_ITW_6mer_5050_CIDTesting_{specific}.osmtsv";
            OligoSpectralMatch.Export(oligoSpectralMatches, outPath);
        }

        #endregion

        #region TaskTesting

        [Test]
        public static void TestTask()
        {
            #region Setup

            Dictionary<string, (string dataFile, string database)> dataFiles = new()
            {
                {
                    "6mer",
                    (@"B:\Users\Whitworth\Raw Mass Spec Data\Mass Spec Data 2023\Direct Injection RNA\231023\231025_ITW_6mer_5050_CIDTesting.raw",
                        @"D:\Projects\RNA\TestData\Databases\6mer.fasta")
                },
                {
                    "20mer1",
                    (@"B:\Users\Whitworth\Raw Mass Spec Data\Mass Spec Data 2023\Direct Injection RNA\231023\231025_ITW_20mer1_5050_CIDTesting.raw",
                        @"D:\Projects\RNA\TestData\Databases\20mer1.fasta")
                },
                {
                    "20mer2",
                    (@"B:\Users\Whitworth\Raw Mass Spec Data\Mass Spec Data 2023\Direct Injection RNA\231023\231025_ITW_20mer2_5050_CIDTesting.raw",
                        @"D:\Projects\RNA\TestData\Databases\20mer2.fasta")
                },
                {
                    "20mer3",
                    (@"B:\Users\Whitworth\Raw Mass Spec Data\Mass Spec Data 2023\Direct Injection RNA\231023\231025_ITW_20mer3_5050_CIDTesting.raw",
                        @"D:\Projects\RNA\TestData\Databases\20mer3.fasta")
                },
                {
                    "20mer4",
                    (@"B:\Users\Whitworth\Raw Mass Spec Data\Mass Spec Data 2023\Direct Injection RNA\231023\231025_ITW_20mer4_5050_CIDTesting.raw",
                        @"D:\Projects\RNA\TestData\Databases\20mer4.fasta")
                },
                {
                    "75mer",
                    (@"B:\Users\Whitworth\Raw Mass Spec Data\Mass Spec Data 2023\Direct Injection RNA\231023\231025_ITW_75mer_5050_CIDTesting.raw",
                        @"D:\Projects\RNA\TestData\Databases\75mer.fasta")
                },
            };

            //db and spectra
            List<DbForTask> dbForTasks = new();
            dataFiles.Select(p => p.Value.database).ForEach(db => dbForTasks.Add(new DbForTask(db, false)));
            List<string> spectraList = dataFiles.Select(p => p.Value.dataFile).ToList();

            // mods
            string modFile = Path.Combine(GlobalVariables.DataDir, "Mods", "RnaMods.txt");
            var allMods = PtmListLoader.ReadModsFromFile(modFile, out var errorMods)
                .ToDictionary(p => p.IdWithMotif, p => p);
            var metals = allMods.Values.Where(p => p.ModificationType == "Metal").ToList();

            CommonParameters commonParams = new
            (
                dissociationType: DissociationType.CID,
                deconvolutionMaxAssumedChargeState: -20,
                deconvolutionIntensityRatio: 3,
                deconvolutionMassTolerance: new PpmTolerance(20),
                scoreCutoff: 5,
                totalPartitions: 1,
                maxThreadsToUsePerFile: 1,
                doPrecursorDeconvolution: true,
                useProvidedPrecursorInfo: false,
                listOfModsFixed: new List<(string, string)>(),
                listOfModsVariable: new List<(string, string)>()
            );

            RnaSearchParameters searchParams = new()
            {
                DisposeOfFileWhenDone = true,
                FragmentIonTolerance = new PpmTolerance(20),
                MatchAllCharges = true,
                MatchAllScans = true,
                MatchMs1 = false,
                MatchMs2 = true,
                MassDiffAcceptorType = MassDiffAcceptorType.Custom,
                CustomMdac = "Custom interval [-10,10]",
                DigestionParams = new(
                    maxMods: 6,
                    maxModificationIsoforms: 2048
                )
            };


            string outputFolder = @"B:\Users\Nic\RNA\CidExperiments\TaskTest";

            #endregion

            RnaSearchTask searchTask = new RnaSearchTask()
            {
                CommonParameters = commonParams,
                RnaSearchParameters = searchParams,
            };

            var taskList = new List<(string, MetaMorpheusTask)> { ("SearchTask", searchTask) };
            var runner = new EverythingRunnerEngine(taskList, spectraList, dbForTasks, outputFolder);
            runner.Run();
        }

        [Test]
        public static void Test6MerEverythingEngine()
        {
            string modFile = Path.Combine(GlobalVariables.DataDir, "Mods", "RnaMods.txt");
            var allMods = PtmListLoader.ReadModsFromFile(modFile, out var errorMods)
                .ToDictionary(p => p.IdWithMotif, p => p);
            var metals = allMods.Values.Where(p => p.ModificationType == "Metal").ToList();
            var metalStrings = metals.Select(p => (p.ModificationType, p.IdWithMotif)).ToList();


            CommonParameters commonParams = new
            (
                dissociationType: DissociationType.CID,
                deconvolutionMaxAssumedChargeState: -20,
                deconvolutionIntensityRatio: 3,
                deconvolutionMassTolerance: new PpmTolerance(20),
                scoreCutoff: 5,
                totalPartitions: 1,
                maxThreadsToUsePerFile: 1,
                doPrecursorDeconvolution: true,
                useProvidedPrecursorInfo: false,
                listOfModsFixed: new List<(string, string)>(),
                listOfModsVariable: metalStrings
            );

            RnaSearchParameters searchParams = new()
            {
                DisposeOfFileWhenDone = true,
                FragmentIonTolerance = new PpmTolerance(20),
                MatchAllCharges = true,
                MatchAllScans = true,
                MatchMs1 = false,
                MatchMs2 = true,
                MassDiffAcceptorType = MassDiffAcceptorType.Custom,
                CustomMdac = "Custom interval [-10,10]",
                DigestionParams = new(
                    maxMods: 6,
                    maxModificationIsoforms: 2048
                )
            };



            List<DbForTask> dbForTasks = new()
            {
                new DbForTask(@"D:\Projects\RNA\TestData\Databases\6mer.fasta", false)
            };


            List<string> spectraFileList = new()
            {
                @"B:\Users\Whitworth\Raw Mass Spec Data\Mass Spec Data 2023\Direct Injection RNA\231023\231025_ITW_6mer_5050_CIDTesting.raw"
            };

            string outputFolder = @"B:\Users\Nic\RNA\CidExperiments\TaskTest3";

            RnaSearchTask searchTask = new RnaSearchTask()
            {
                CommonParameters = commonParams,
                RnaSearchParameters = searchParams,
            };

            var taskList = new List<(string, MetaMorpheusTask)> { ("SearchTask", searchTask) };
            var runner = new EverythingRunnerEngine(taskList, spectraFileList, dbForTasks, outputFolder);
            runner.Run();
        }


      

        #endregion

        internal static string GetFinalPath(string path)
        {
            // check if a file with this name already exists, if so add a number to the end within parenthesis. If that file still exists, increment the number by one and try again
            string end = Path.GetFileName(path);
            int fileCount = 1;
            string finalPath = path;
            while (System.IO.File.Exists(finalPath) || Directory.Exists(finalPath))
            {
                finalPath = path.Replace(end, $"{end}({fileCount})");
                fileCount++;
            }
            return finalPath;
        }
    }



    internal class FragmentationExplorationResult
    {
        public static CsvConfiguration CsvConfiguration => new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
            HasHeaderRecord = true,
            Encoding = Encoding.UTF8,
        };

        public int NCE { get; set; }
        public int ReactionTime { get; set; }
        public double MathieuQ { get; set; }
        public int ScanNumber { get; set; }
        public double MS2TIC { get; set; }
        public int SequenceLength { get; set; }
        public int TotalIonCount { get; set; }
        public double PrecursorMz { get; set; }

        [Optional]
        public int? MatchedIonCount { get; set; }
        [Optional]
        public double? PercentTicMatched { get; set; }
        [Optional]
        public int? ChargeStateFragmented { get; set; }
        [Optional]
        public double? SequenceCoverage { get; set; }

        [CsvHelper.Configuration.Attributes.Ignore]
        public OligoSpectralMatch? SpectralMatch { get; set; }

        public FragmentationExplorationResult(int nce, int reactionTime, double mathieuQ, int scanNumber, double ms2Tic,
            int sequenceLength, int totalIonCount, double precursorMz, OligoSpectralMatch match = null)
        {
            NCE = nce;
            ReactionTime = reactionTime;
            MathieuQ = mathieuQ;
            ScanNumber = scanNumber;
            MS2TIC = ms2Tic;
            SequenceLength = sequenceLength;
            TotalIonCount = totalIonCount;
            PrecursorMz = precursorMz;

            if (match == null) return;
            MatchedIonCount = match.MatchedFragmentIons.Count;
            ChargeStateFragmented = match.ScanPrecursorCharge;
            PercentTicMatched = Math.Round((match.Score - Math.Round(match.Score, 0)) * 100, 2);
            SpectralMatch = match;
            SequenceCoverage = match.SequenceCoverage;
        }

        public FragmentationExplorationResult(MsDataScan scan, OligoSpectralMatch osm)
        {
            NCE = int.Parse(scan.ScanDescription.Substring(0, 2));
            ReactionTime = int.Parse(scan.ScanDescription.Substring(2, 2));
            MathieuQ = double.Parse(scan.ScanDescription.Substring(4, 2));
            ScanNumber = scan.OneBasedScanNumber;
            MS2TIC = scan.TotalIonCurrent;
            SequenceLength = osm.BaseSequence.Length;
            TotalIonCount = scan.MassSpectrum.XArray.Length;
            PrecursorMz = scan.IsolationMz.Value;

            MatchedIonCount = osm.MatchedFragmentIons.Count;
            ChargeStateFragmented = osm.ScanPrecursorCharge;
            PercentTicMatched = Math.Round((osm.Score - Math.Round(osm.Score, 0)) * 100, 2);
            SpectralMatch = osm;
            SequenceCoverage = osm.SequenceCoverage;
        }
    }

    internal class FragmentationExplorationResultFile : ResultFile<FragmentationExplorationResult>
    {
        public override SupportedFileType FileType { get; } 
        public override Software Software { get; set; }


        /// <summary>
        /// Constructor used to initialize from the factory method
        /// </summary>
        public FragmentationExplorationResultFile() : base() { }

        public FragmentationExplorationResultFile(string filePath, List<FragmentationExplorationResult> results) : base(
            filePath, Software.Unspecified)
        {
            Results = results;
        }


        public override void LoadResults()
        {
            using var csv = new CsvReader(new StreamReader(FilePath), FragmentationExplorationResult.CsvConfiguration);
            Results = csv.GetRecords<FragmentationExplorationResult>().ToList();
        }

        public override void WriteResults(string outputPath)
        {
            using var csv = new CsvWriter(new StreamWriter(outputPath), FragmentationExplorationResult.CsvConfiguration);
            csv.WriteHeader<FragmentationExplorationResult>();
            csv.NextRecord();
            csv.WriteRecords(Results);
        }

    }
}
