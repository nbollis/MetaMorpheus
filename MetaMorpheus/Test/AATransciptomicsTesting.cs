using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MassSpectrometry;
using Nett;
using NUnit.Framework;
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
            @"D:\Projects\RNA\TestData\6merMs2Subset_H2O_HFIPandTEA_Centroided.mzML";

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


        [Test]
        public static void RunThingy()
        {
            MsDataFile sixMerFile = MsDataFileReader.GetDataFile(SixmerSpecPath);
            var scans = sixMerFile.GetAllScansList();

        }
    }
}
