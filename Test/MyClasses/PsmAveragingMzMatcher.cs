#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chemistry;
using Easy.Common.Extensions;
using EngineLayer;
using GuiFunctions;
using MassSpectrometry;
using MzLibUtil;
using Proteomics.ProteolyticDigestion;
using SpectralAveraging;
using UsefulProteomicsDatabases.Generated;

namespace Test
{
    public class PsmAveragingMzMatcher
    {
        

        private static int minChargeState = 1;
        private static int maxChargeState = 50;
        private static PpmTolerance tolerance = new(50);
        private static double snrCutoff = 3;

        private List<SpectralAveragingParameters> parameters;
        private List<MsDataScan> originalMs1Scans;
        private PsmFromTsv psm;
        private Dictionary<int, double> chargeStateAndMz;

        public List<AveragingMatcherResults> Results { get; set; }


        public PsmAveragingMzMatcher(List<SpectralAveragingParameters> parameters, List<MsDataScan> originalScans, PsmFromTsv psmFromSearch)
        {
            this.parameters = parameters;
            originalMs1Scans = originalScans.Where(p => p.MsnOrder == 1).ToList();
            psm = psmFromSearch;
            chargeStateAndMz = GetMzValues(psm);
            Results = new List<AveragingMatcherResults>();
            OriginalAveragingMatcherResults.OriginalScanCount = originalMs1Scans.Count;
            OriginalAveragingMatcherResults.OriginalAverageNoiseLevel = GetNoiseLevel(originalMs1Scans);
            OriginalAveragingMatcherResults.OriginalScansScore = ScoreChargeStates(originalMs1Scans, OriginalAveragingMatcherResults.OriginalAverageNoiseLevel);
        }

        public void ScoreAllAveragingParameters()
        {
            foreach (var averagingParameter in parameters)
            {
                Stopwatch sw = Stopwatch.StartNew();
                var averagedScans = SpectraFileAveraging.AverageSpectraFile(originalMs1Scans, averagingParameter);
                sw.Stop();
                var noise = GetNoiseLevel(averagedScans.ToList());
                var score = ScoreChargeStates(averagedScans, noise);

                Results.Add(new AveragingMatcherResults(averagingParameter, averagedScans.Length, score, sw.ElapsedMilliseconds, noise));
            }
        }


        private Dictionary<int, double> ScoreChargeStates(IReadOnlyCollection<MsDataScan> scans, double noiseLevel)
        {
            Dictionary<int, double> chargeStateScores = chargeStateAndMz.ToDictionary
                (p => p.Key, p => 0.0);
            int scanCount = scans.Count(p => p.MsnOrder == 1);

            foreach (var spectra in scans.Where(p => p.MsnOrder == 1).Select(p => p.MassSpectrum))
            {
                var yCutoff = noiseLevel * snrCutoff;
                foreach (var chargeState in chargeStateAndMz)
                {
                    var peaksWithinTolerance = spectra.XArray.Where(p =>
                        tolerance.Within(p, chargeState.Value)).ToList();

                    var peaksWithinToleranceAboveCutoff =
                        peaksWithinTolerance.Where(p => spectra.YArray[Array.IndexOf(spectra.XArray, p)] >= yCutoff).ToList();

                    if (peaksWithinToleranceAboveCutoff.Any())
                        chargeStateScores[chargeState.Key] += 1;
                }
            }

            // normalize counts to number of spectra
            for (int i = minChargeState; i < maxChargeState; i++)
            {
                chargeStateScores[i] /= scanCount;
            }

            return chargeStateScores;
        }

        private static Dictionary<int, double> GetMzValues(PsmFromTsv psm)
        {
            var pep = new PeptideWithSetModifications(psm.FullSequence, GlobalVariables.AllModsKnownDictionary);
            Dictionary<int, double> mzs = new();
            for (int i = minChargeState; i < maxChargeState; i++)
            {
                mzs.Add(i, pep.MostAbundantMonoisotopicMass.ToMz(i));
            }
            return mzs;
        }

        private double GetNoiseLevel(List<MsDataScan> scans)
        {
            List<double> noiseEstimates = new();
            foreach (var signal in scans.Select(p => p.MassSpectrum.YArray))
            {
                if (MRSNoiseEstimator.MRSNoiseEstimation(signal, 0.01, out double noiseEstimate))
                    noiseEstimates.Add(noiseEstimate);
            }

            return noiseEstimates.Average();
        }
    }


    public readonly struct AveragingParamCombo
    {
        public double[] BinSizes { get; init; }
        public int[] NumberOfScansToAverage { get; init; }
        public int[] ScanOverlap { get; init; }
        public double[] Sigmas { get; init; }
        public double[] Percentiles { get; init; }

        public AveragingParamCombo(double[] binSizes,
            int[] numberOfScansToAverage, int[] scanOverlap, double[] sigmas, double[] percentiles)
        {
            BinSizes = binSizes;
            NumberOfScansToAverage = numberOfScansToAverage;
            ScanOverlap = scanOverlap;
            Sigmas = sigmas;
            Percentiles = percentiles;
        }
    }

   

}
