using EngineLayer;
using MassSpectrometry;
using NUnit.Framework; using Assert = NUnit.Framework.Legacy.ClassicAssert;
using System;
using System.Collections.Generic;
using System.IO;
using TaskLayer;

namespace Test
{
    [TestFixture]
    public static class MsDataFileTest
    {
        private static MsDataScan scanB;
        private static IsotopicEnvelope[] experimentalFragments;

        [OneTimeSetUp]
        public static void Setup()
        {
            Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;
            scanB = new MsDataScan(
                    new MzSpectrum(Array.Empty<double>(), Array.Empty<double>(), false),
                    2, 1, true, Polarity.Positive, double.NaN, null, null, MZAnalyzerType.Orbitrap, double.NaN, null, null, "scan=1", double.NaN, null, null, double.NaN, null, DissociationType.AnyActivationType, 1, null);
            experimentalFragments = new IsotopicEnvelope[]
            {
                new IsotopicEnvelope(new List<(double mz, double intensity)> { (100, 1) }, 100, 1, 1, 0),
                new IsotopicEnvelope(new List<(double mz, double intensity)> { (200, 1) }, 200, 1, 1, 0),
                new IsotopicEnvelope(new List<(double mz, double intensity)> { (300, 1) }, 300, 1, 1, 0)
            };

        }

        [Test]
        public static void TestLoadAndRunMgf()
        {
            //The purpose of this test is to ensure that mgfs can be run without crashing.
            //Whenever a new feature is added that may require things an mgf does not have,
            //there should be a check that prevents mgfs from using that feature.
            string mgfName = @"TestData\ok.mgf";
            string xmlName = @"TestData\okk.xml";
            string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestLoadAndRunMgf");

            SearchTask task1 = new()
            {
                SearchParameters = new SearchParameters
                {
                    DoParsimony = true,
                    DoLabelFreeQuantification = true
                }
            };
            List<(string, MetaMorpheusTask)> taskList = new()
            {
                ("task1", task1),
            };
            //run!

            var engine = new EverythingRunnerEngine(taskList, new List<string> { mgfName }, new List<DbForTask> { new DbForTask(xmlName, false) }, outputFolder);
            engine.Run();
            //Just don't crash! There should also be at least one psm at 1% FDR, but can't check for that.
            Directory.Delete(outputFolder, true);
        }

        [Test]
        public static void TestCompressionDecompression()
        {
            string testInputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, @"CompressionTest");
            DirectoryInfo testDirectory = new(testInputFolder);
            MyFileManager.CompressDirectory(testDirectory);

            foreach (FileInfo file in testDirectory.GetFiles())
            {
                Assert.AreEqual(".gz", file.Extension);
            }

            MyFileManager.DecompressDirectory(testDirectory);

            foreach (FileInfo file in testDirectory.GetFiles())
            {
                Assert.AreNotEqual(".gz", file.Extension);
            }
        }

        [Test]
        public static void TestMs2ScanWithSpecificMass()
        {
            var ms2Scan = new Ms2ScanWithSpecificMass(scanB, 100, 1, "testPath", new CommonParameters(), new IsotopicEnvelope[0]);
            var closestExperimentalMassB = ms2Scan.GetClosestExperimentalIsotopicEnvelope(10);

            Assert.IsNull(closestExperimentalMassB);
        }

        [Test]
        [TestCase(200, 1)]
        [TestCase(50, 0)]
        [TestCase(350, 2)]
        [TestCase(150, 0)]
        [TestCase(250, 1)]
        public static void TestGetClosestFragmentMassIndex(double mass, int expectedIndex)
        {
            // Arrange
            var commonParams = new CommonParameters();
            var ms2Scan = new Ms2ScanWithSpecificMass(scanB, 100, 1, "testPath", commonParams, experimentalFragments);

            // Act
            var closestIndex = ms2Scan.GetType().GetMethod("GetClosestFragmentMassIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(ms2Scan, new object[] { mass });

            // Assert
            Assert.That(closestIndex, Is.EqualTo(expectedIndex));
        }

        [Test]
        [TestCase(50, 150, new double[] { 100 })]
        [TestCase(150, 250, new double[] { 200 })]
        [TestCase(50, 350, new double[] { 100, 200, 300 })]
        [TestCase(250, 350, new double[] { 300 })]
        [TestCase(0, 50, new double[] { })]
        public static void TestGetClosestExperimentalIsotopicEnvelopeList(double minMass, double maxMass, double[] expectedMasses)
        {
            // Arrange
            var commonParams = new CommonParameters();
            var ms2Scan = new Ms2ScanWithSpecificMass(scanB, 100, 1, "testPath", commonParams, experimentalFragments);

            // Act
            var closestEnvelopes = ms2Scan.GetClosestExperimentalIsotopicEnvelopeList(minMass, maxMass);

            // Assert
            if (expectedMasses.Length == 0)
            {
                Assert.IsNull(closestEnvelopes);
            }
            else
            {
                Assert.IsNotNull(closestEnvelopes);
                Assert.AreEqual(expectedMasses.Length, closestEnvelopes.Length);
                for (int i = 0; i < expectedMasses.Length; i++)
                {
                    Assert.AreEqual(expectedMasses[i], closestEnvelopes[i].MonoisotopicMass);
                }
            }
        }
    }
}