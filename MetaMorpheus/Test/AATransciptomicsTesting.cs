global using FragmentationTerminus = MassSpectrometry.FragmentationTerminus;
global using ProductType = MassSpectrometry.ProductType;
using System;
using System.Collections.Generic;
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
using MassSpectrometry;
using MzLibUtil;
using Nett;
using NUnit.Framework;
using OxyPlot;
using OxyPlot.Wpf;
using Proteomics.Fragmentation;
using Readers;
using Transcriptomics;

namespace Test
{
    [TestFixture]
    internal class AATransciptomicsTesting
    {
        public static RNA Sixmer;
        public static RNA Twentymer;
        public static RNA TwentymerIsobar;
        public static RNA TwentymerWithMods;
        public static RNA FiftymerWithMods;

        public static string SixmerSpecPath =>
            @"B:\Users\Whitworth\Raw Mass Spec Data\Mass Spec Data 2023\Direct Injection RNA\H2O_HFIPandTEA.raw";

        public static string SixmerTheoreticalSpecpath =>
            @"D:\Projects\RNA\WorkingThroughCode\MatchingFragmentIons\sixMerTheroetical.mzML";

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            Sixmer = new RNA("GUACUG");
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
            rna.Fragment(DissociationType.CID, FragmentationTerminus.Both, fragments);

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
                Sixmer.Fragment(DissociationType.CID, FragmentationTerminus.Both, products);


                var matched = MetaMorpheusEngine.MatchFragmentIons(ms2WithMass, products, commonParams);
                if (matched.Any())
                    spectralMatches.Add(new OligoSpectralMatch(scan, Sixmer.BaseSequence, matched, SixmerSpecPath));
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
            nucleicAcid.Fragment(DissociationType.CID, FragmentationTerminus.Both, products);

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





    }
}
