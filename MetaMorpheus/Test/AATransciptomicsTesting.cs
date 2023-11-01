global using FragmentationTerminus = MassSpectrometry.FragmentationTerminus;
global using ProductType = MassSpectrometry.ProductType;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.TextFormatting;
using Chemistry;
using EngineLayer;
using FlashLFQ;
using GuiFunctions;
using iText.Kernel.Geom;
using iText.Svg.Renderers.Impl;
using MassSpectrometry;
using MzLibUtil;
using Nett;
using NUnit.Framework;
using OxyPlot;
using OxyPlot.Wpf;
using Proteomics.Fragmentation;
using Readers;
using ThermoFisher.CommonCore.Data;
using Transcriptomics;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Plotly.NET.CSharp;
using TaskLayer;
using Path = System.IO.Path;
using System.Reflection;
using UsefulProteomicsDatabases;

namespace Test
{
    [TestFixture]
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
            List<IProduct> fragments = new();
            rna.Digest(new RnaDigestionParams(), new List<Modification>(), new List<Modification>()).First()
                .Fragment(DissociationType.CID, FragmentationTerminus.Both, fragments);

            var mzs = fragments.Select(p => p.NeutralMass.ToMz(-1)).ToArray();
            var intensites = Enumerable.Repeat(1.0, mzs.Length).ToArray();

            var spectrum = new MzSpectrum(mzs, intensites, false);

            var ms1 = new MsDataScan(new MzSpectrum(new[] { rna.MonoisotopicMass.ToMz(-3) }, new[] { 1e6 }, false), 1, 1, true, Polarity.Negative, 0.9, new MzRange(50, 2000), "",
                MZAnalyzerType.Orbitrap, 1e6, 0.5, new double[1, 1], "controllerType=0 controllerNumber=1 scan=1");
            var ms2 = new MsDataScan(spectrum, 2, 2, true, Polarity.Negative, 1, new MzRange(50, 2000), "",
                MZAnalyzerType.Orbitrap, spectrum.SumOfAllY, 0.5, new double[1, 1], "controllerType=0 controllerNumber=1 scan=2",
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
                var ms2WithMass = new Ms2ScanWithSpecificMass(scan, scan.IsolationMz.Value, scan.SelectedIonChargeStateGuess.Value, SixmerSpecPath, commonParams);
                var products = new List<IProduct>();
                Sixmer.Digest(new RnaDigestionParams(), new List<Modification>(), new List<Modification>()).First()
                    .Fragment(DissociationType.CID, FragmentationTerminus.Both, products);


                var matched = MetaMorpheusEngine.MatchFragmentIons(ms2WithMass, products, commonParams);
                if (matched.Any())
                    spectralMatches.Add(new OligoSpectralMatch(scan, Sixmer, Sixmer.BaseSequence, matched, SixmerSpecPath));
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

       
        private static List<MatchedFragmentIon> MatchFragments(MsDataScan scan, NucleicAcid nucleicAcid, CommonParameters commonParams)
        {
            var ms2WithMass = new Ms2ScanWithSpecificMass(scan, scan.IsolationMz.Value, scan.SelectedIonChargeStateGuess.Value, SixmerSpecPath, commonParams);
            var products = new List<IProduct>();
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
        public static string FiftyMerOsmpath = @"D:\Projects\RNA\TestData\ISF\230627_RNA_withISF_1ul_CUCCGUAAUGCGAAAUACGC_3050-3120.osmtsv";


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
        public static void TESTNAME()
        {
            string path = @"B:\Users\Nic\RNA\CidExperiments\231025_ITW_6mer_5050_CIDTesting.raw";
            var dataFile = MsDataFileReader.GetDataFile(path).LoadAllStaticData();
            string osmPath = @"B:\Users\Nic\RNA\CidExperiments\231025_ITW_6mer_5050_CIDTesting_GUACUG_1-100.osmtsv";
            var osms = OligoSpectralMatch.Import(osmPath, out List<string> warnings);
            var oligo = new RNA("GUACUG");
            var esi = oligo.GetElectrospraySeries(-5, 0);

            var results = PullOutScanInfo(dataFile);

            foreach (var result in results)
            {
                var osm = osms.FirstOrDefault(p => p.ScanNumber == result.ScanNumber);
                if (osm == null)
                    continue;

                result.MatchedIonCount = osm.MatchedFragmentIons.Count;
                result.ChargeStateFragmented = osm.ScanPrecursorCharge;
                result.PercentTicMatched = Math.Round((osm.Score - Math.Round(osm.Score, 0)) * 100,2);
            }
            var temp = results.Count(p => p.MatchedIonCount != null);
         }

        [Test]
        public static void PullOutCidData()
        {
            CommonParameters commonParams = new
            (
                dissociationType: DissociationType.CID,
                deconvolutionMaxAssumedChargeState: -20,
                deconvolutionIntensityRatio: 3,
                deconvolutionMassTolerance: new PpmTolerance(20),
                scoreCutoff: 2,
                totalPartitions: 1,
                maxThreadsToUsePerFile: 1,
                doPrecursorDeconvolution: true
            );

            string path = @"B:\Users\Nic\RNA\CidExperiments\231025_ITW_6mer_5050_CIDTesting.raw";
            var dataFile = MsDataFileReader.GetDataFile(path).LoadAllStaticData();
            
            var rna = new RNA("GUACUG");
            List<IProduct> products = new List<IProduct>();
            var oligo = rna.Digest(new RnaDigestionParams(), new List<Modification>(), new List<Modification>())
                .First() as OligoWithSetMods; 
            oligo!.Fragment(DissociationType.CID, FragmentationTerminus.Both, products);


            List<OligoSpectralMatch> spectralMatches = new();
            List<FragmentationExplorationResult> results = new();
            foreach (var scan in MetaMorpheusTask.GetMs2Scans(dataFile, dataFile.FilePath, commonParams))
            {
                if (scan.TheScan.MsnOrder != 2) continue;

                // prep scan description variables
                var description = scan.TheScan.ScanDescription;
                // nce is the first two digits in descritpion, time is the next two, and mathieu q is the last two
                int nce = int.Parse(description.Substring(0, 2));
                int reactionTime = int.Parse(description.Substring(2, 2));
                double mathieuQ = double.Parse(description.Substring(4, 2));
                int scanNumber = scan.OneBasedScanNumber;
                double ms2Tic = scan.TotalIonCurrent;
                int sequenceLength = oligo.Length;
                int totalIonCount = scan.NumPeaks;
                double precursorMz = scan.PrecursorMonoisotopicPeakMz;

                List<MatchedFragmentIon> matchedIons = MetaMorpheusEngine.MatchFragmentIons(scan, products,
                    commonParams, true);

                if (matchedIons.Count == 0) continue;

                var match = new OligoSpectralMatch(scan.TheScan, rna, oligo.BaseSequence, matchedIons, path);
                spectralMatches.Add(match);

                results.Add(new FragmentationExplorationResult(nce, reactionTime, mathieuQ, scanNumber, ms2Tic, sequenceLength, totalIonCount, precursorMz, match));
            }

            var outpath = path.Replace(".raw", "ms2WithSpecific_1.csv");
            FragmentationExplorationResultFile file = new(outpath, results.OrderByDescending(p => p.MatchedIonCount).ToList());
            file.WriteResults(outpath);

            Thread.Sleep(1000);


        }
        
        public static List<FragmentationExplorationResult> PullOutScanInfo(MsDataFile dataFile)
        {
            List<FragmentationExplorationResult> results = new();
            foreach (var scan in dataFile.GetAllScansList())
            {
                if (scan.MsnOrder == 2)
                {
                    var description = scan.ScanDescription;
                    // nce is the first two digits in descritpion, time is the next two, and mathieu q is the last two
                    int nce = int.Parse(description.Substring(0, 2));
                    int reactionTime = int.Parse(description.Substring(2, 2));
                    double mathieuQ = double.Parse(description.Substring(4, 2));
                    int scanNumber = scan.OneBasedScanNumber;
                    double ms2Tic = scan.TotalIonCurrent;
                    int sequenceLength = 6;
                    int totalIonCount = scan.MassSpectrum.XArray.Length;
                    double precursorMz = scan.IsolationMz.Value;

                    results.Add(new FragmentationExplorationResult(nce, reactionTime, mathieuQ, scanNumber, ms2Tic, sequenceLength, totalIonCount, precursorMz));
                }
            }

            return results;
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
                scoreCutoff: 2,
                totalPartitions: 1,
                maxThreadsToUsePerFile: 1,
                doPrecursorDeconvolution: true
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
                CustomMdac = "Custom interval [-50,50]"
            };

            string modFile = Path.Combine(GlobalVariables.DataDir,"Mods", "RnaMods.txt");
            var allMods = PtmListLoader.ReadModsFromFile(modFile, out var errorMods);

            RnaDigestionParams digestionParams = new RnaDigestionParams(maxMods:4, maxModificationIsoforms:2048);
            List<Modification> fixedMods = new();
            List<Modification> variableMods = allMods.ToList();
            MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(searchParams.PrecursorMassTolerance,
                searchParams.MassDiffAcceptorType, searchParams.CustomMdac);

            string path = @"B:\Users\Nic\RNA\CidExperiments\231025_ITW_6mer_5050_CIDTesting.raw";
            var dataFile = MsDataFileReader.GetDataFile(path);
            var ms2Scans = MetaMorpheusTask.GetMs2Scans(dataFile, path, commonParams)
                .OrderBy(b => b.PrecursorMass)
                .ToArray();
            var osms = new OligoSpectralMatch[ms2Scans.Count()];

            List<RNA> targets = new()
            {
                new RNA("GUACUG"),
            };


            var engine = new RnaSearchEngine(osms, targets, ms2Scans, commonParams, massDiffAcceptor,
                digestionParams, variableMods, fixedMods, new List<(string FileName, CommonParameters Parameters)>(),
                new List<string>() );
            var results = engine.Run();

            var oligoSpectralMatches = osms.Where(p => p != null).ToList();

        }

        #endregion

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
