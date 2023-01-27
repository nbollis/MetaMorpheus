using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

            List<SpectralAveragingParameters> averagingParams = new();
            var outlierRejection = Enum.GetValues<OutlierRejectionType>();
            var weighting = Enum.GetValues<SpectraWeightingType>();
            var normalization = Enum.GetValues<NormalizationType>().Where(p => p != NormalizationType.AbsoluteToTic).ToArray();

            foreach (var averagingparam in SpectralAveragingParameters.GenerateSpectralAveragingParameters(new[] { 0.01 }, new[] { 5, 10, 15, 20, 25 },
             new[] { 2, 4, 5, 9, 10, 20 }, new[] { 1.0, 1.5, 2.0, 2.5, 3.0, 3.5 }, new[] {0.5, 0.7, 0.8, 0.9}, weighting, outlierRejection, normalization ))
            {
                averagingparam.MaxThreadsToUsePerFile = 15;
                averagingParams.Add(averagingparam);
            }

            string outDirectory = @"D:\Projects\SpectralAveraging\ScoringBasedUponPeaksFound\BigTest";
            PerformMatchingBasedOnRejectionType(averagingParams, scans, representativePsm, outDirectory);
        }

        private static void PerformMatchingBasedOnRejectionType(List<SpectralAveragingParameters> parameters, List<MsDataScan> scans, PsmFromTsv representativePsm, string outDirectory)
        {
            foreach (var rejectionType in parameters.Select(p => p.OutlierRejectionType).Distinct())
            {
                var parametersOfInterest = parameters.Where(p => p.OutlierRejectionType == rejectionType).ToList();
                PsmAveragingMzMatcher matcher = new PsmAveragingMzMatcher(parametersOfInterest, scans, representativePsm);
                
                matcher.ScoreAllAveragingParameters();
                string runName = "FirstBigTest";
                string outPath = Path.Combine(outDirectory, rejectionType.ToString() + $"_{runName}");
                OutputResults(outPath, matcher.Results);

            }
        }


        private static void OutputResults(string outPath, List<AveragingMatcherResults> results)
        {
            var allResults = new List<ITsv>() { new OriginalAveragingMatcherResults() };
            allResults.AddRange(results.Select(p => p as ITsv));
            allResults.ExportAsTsv(outPath);
        }

    }
}
