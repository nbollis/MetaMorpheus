using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using GuiFunctions;
using IO.ThermoRawFileReader;
using MassSpectrometry;
using NUnit.Framework;
using Proteomics;
using SpectralAveraging;
using UsefulProteomicsDatabases;

namespace Test
{
    [TestFixture]
    public class ChimeraAnalysisTests
    {

        #region Constants

        private const string caOnlyPath = @"R:\Nic\Chimera Validation\SingleStandards\221110_CaOnly_.raw";
        private const string myoOnlyPath = @"";
        private const string ubiqOnlyPath = @"R:\Nic\Chimera Validation\SingleStandards\221110_UbiqOnly_50IW.raw";
        private const string hghOnlyPath = @"R:\Nic\Chimera Validation\SingleStandards\221110_HGHOnly_50IW.raw";
        private const string cytoOnlyPath = @"R:\Nic\Chimera Validation\SingleStandards\221110_CytoOnly.raw";
        private const string trypOnlyPath = @"R:\Nic\Chimera Validation\SingleStandards\221110_TrypOnly";

        private const string caAccession = "P00921";
        private const string myoAccession = "P68082";
        private const string ubiqAccession = "P0CH28";
        private const string hghAccession = "P01241";
        private const string cytoAccession = "P00004";
        private const string trypAccession = "";

        private const string allIsolatedStandardsSearchedTogether = @"R:\Nic\Chimera Validation\SingleStandards\AllStandardsSearchedTogetherBellsAndWhistles\Task1-SearchTask\AllProteoforms.psmtsv";
        private const string bulkCaliOrAverageFirstSearchedTogether = @"D:\Projects\Top Down MetaMorpheus\ChimeraValidation\CaMyoUbiqCytCHgh\Bulk_CaliOrAverageFirstBellsAndWhistles\Task1-SearchTask\AllProteoforms.psmtsv";

        #endregion

        // Big run 1 = new[] { 0.001, 0.01, 0.05, 0.1 }, new[] { 5, 10, 20, 35, 50 },
        // new[] { 2, 3, 4, 10, 15, 25 }, new[] { 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0 }, new[] { 0.5, 0.6, 0.7, 0.8, 0.9 }

        [Test]
        public static void PsmAveragingMzMatherRunner()
        {
            var proteoforms = PsmTsvReader.ReadTsv(allIsolatedStandardsSearchedTogether, out List<string> warnings).Concat(PsmTsvReader.ReadTsv(bulkCaliOrAverageFirstSearchedTogether, out warnings)).ToList();
            
                                                                                // Change this \/ here for a different protein
            var representativePsm = proteoforms.Where(p => p.ProteinAccession == caAccession && p.AmbiguityLevel == "1")
                .MaxBy(p => p.PsmCount);
            var scans = ThermoRawFileReader.LoadAllStaticData(caOnlyPath).GetAllScansList();
            var averagingParamGenerator = new AveragingParamCombo(new[] { 0.001, 0.01, 0.05, 0.1 }, new[] { 5, 10, 20, 35, 50 },
                new[] { 2, 3, 4, 10, 15, 25 }, new[] { 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0 }, new[] { 0.5, 0.6, 0.7, 0.8, 0.9 });
            PsmAveragingMzMatcher matcher = new(averagingParamGenerator, scans, representativePsm);
            matcher.ScoreAllAveragingParameters();

            string outDirectory = @"D:\Projects\SpectralAveraging\ScoringBasedUponPeaksFound";
            string fileName = "FirstLargeScaleTest";
            string outPath = Path.Combine(outDirectory, fileName);

            var allResults = new List<ITsv>() { new OriginalAveragingMatcherResults() };
            allResults.AddRange(matcher.Results.Select(p => p as ITsv));
            allResults.ExportAsTsv(outPath);
        }




        [Test]
        [TestCase(new[] { 0.1 }, new[] { 5 },
            new[] { 0 }, new[] { 1.3 }, new[] { 0.8 })]
        [TestCase(new[] { 0.01, 0.1 }, new[] { 5, 10, 20 },
            new[] { 2, 3, 4 }, new[] { 1.0, 2.0, 3.0 }, new[] { 0.5, 0.7, 0.9 })]
        [TestCase(new[] { 0.01, 0.1 }, new[] { 5, 10, 20 },
            new[] { 2, 3, 4, 10, 15 }, new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 0.5, 0.7, 0.9 })]
        public static void TestGenerateSpectralAveragingParameters(double[] binSizes,
            int[] numberOfScansToAverage, int[] scanOverlap, double[] sigmas, double[] percentiles)
        {
            int rejectionTypes = Enum.GetNames<OutlierRejectionType>()
                .Count(p => !p.Contains("Sigma") && !p.Contains("Percent"));
            int weightingTypes = Enum.GetValues<SpectraWeightingType>().Length;
            int normalizationTypes = 2;
            int sigmaTypes = (int)Math.Pow(sigmas.Length, 2) *
                             Enum.GetNames<OutlierRejectionType>().Count(p => p.Contains("Sigma"));
            int percentileTypes = percentiles.Length;
            int binSizeCount = binSizes.Length;

            int scanToAverageCount = 0;
            foreach (var scanCount in numberOfScansToAverage)
            {
                foreach (var overlap in scanOverlap)
                {
                    if (overlap < scanCount)
                        scanToAverageCount++;
                }
            }

            var averagingParamCount =
                weightingTypes * normalizationTypes * (sigmaTypes + percentileTypes + rejectionTypes) * binSizeCount * scanToAverageCount;

            AveragingParamCombo combo = new(binSizes, numberOfScansToAverage, scanOverlap, sigmas, percentiles);
            var result = PsmAveragingMzMatcher.GenerateSpectralAveragingParameters(combo);
            Assert.That(result.Count, Is.EqualTo(averagingParamCount));
        }

    }
}
