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
        private static double intensityCutoff = 0.2;
        
        private AveragingParamCombo averagingParamCombo;
        private List<MsDataScan> originalMs1Scans;
        private PsmFromTsv psm;
        private Dictionary<int, double> chargeStateAndMz;

        public List<AveragingMatcherResults> Results { get; set; }


        public PsmAveragingMzMatcher(AveragingParamCombo averagingParamsToGenerate, List<MsDataScan> originalScans, PsmFromTsv psmFromSearch)
        {
            averagingParamCombo = averagingParamsToGenerate;
            originalMs1Scans = originalScans.Where(p => p.MsnOrder == 1).ToList();
            psm = psmFromSearch;
            chargeStateAndMz = GetMzValues(psm);
            Results = new List<AveragingMatcherResults>();
            OriginalAveragingMatcherResults.OriginalScanCount = originalMs1Scans.Count;
        }

        public void ScoreAllAveragingParameters()
        {
            var score = ScoreChargeStates(originalMs1Scans);
            OriginalAveragingMatcherResults.OriginalScansScore = score;

            foreach (var averagingParameter in GenerateSpectralAveragingParameters(averagingParamCombo))
            {
                Stopwatch sw = Stopwatch.StartNew();
                var averagedScans = SpectraFileAveraging.AverageSpectraFile(originalMs1Scans, averagingParameter);
                sw.Stop();
                score = ScoreChargeStates(averagedScans);
                Results.Add(new AveragingMatcherResults(averagingParameter, averagedScans.Length, score, sw.ElapsedMilliseconds));
            }
        }

        internal static IEnumerable<SpectralAveragingParameters> GenerateSpectralAveragingParameters(AveragingParamCombo averagingParamsToGenerate)
        {
            List<SpectralAveragingParameters> averagingParams = new();
            foreach (var binSize in averagingParamsToGenerate.BinSizes)
            {
                foreach (var numberOfScans in averagingParamsToGenerate.NumberOfScansToAverage)
                {
                    foreach (var overlap in averagingParamsToGenerate.ScanOverlap)
                    {
                        if (overlap >= numberOfScans) break;

                        for (int i = 0; i < 2; i++)
                        {
                            foreach (var weight in Enum.GetValues<SpectraWeightingType>().Where(p => p != SpectraWeightingType.MrsNoiseEstimation))
                            {
                                foreach (var rejection in Enum.GetValues<OutlierRejectionType>())
                                {
                                    // specific rejection type stuff

                                    switch (rejection)
                                    {
                                        case OutlierRejectionType.SigmaClipping:
                                        case OutlierRejectionType.AveragedSigmaClipping:
                                        case OutlierRejectionType.WinsorizedSigmaClipping:
                                            averagingParams.AddRange(from outerSigma in averagingParamsToGenerate.Sigmas
                                                                     from innerSigma in averagingParamsToGenerate.Sigmas
                                                                     let minSigma = outerSigma
                                                                     let maxSigma = innerSigma
                                                                     select new SpectralAveragingParameters()
                                                                     {
                                                                         NormalizationType = i % 2 == 0 ? NormalizationType.RelativeToTics : NormalizationType.NoNormalization,
                                                                         SpectraFileAveragingType = SpectraFileAveragingType.AverageEverynScansWithOverlap,
                                                                         NumberOfScansToAverage = numberOfScans,
                                                                         ScanOverlap = overlap,
                                                                         OutlierRejectionType = rejection,
                                                                         SpectralWeightingType = weight,
                                                                         MinSigmaValue = minSigma,
                                                                         MaxSigmaValue = maxSigma,
                                                                         BinSize = binSize,
                                                                     });
                                            break;
                                        case OutlierRejectionType.PercentileClipping:
                                            averagingParams.AddRange(averagingParamsToGenerate.Percentiles.Select(percentile => new SpectralAveragingParameters()
                                            {
                                                NormalizationType = i % 2 == 0 ? NormalizationType.RelativeToTics : NormalizationType.NoNormalization,
                                                SpectraFileAveragingType = SpectraFileAveragingType.AverageEverynScansWithOverlap,
                                                NumberOfScansToAverage = numberOfScans,
                                                ScanOverlap = overlap,
                                                OutlierRejectionType = rejection,
                                                SpectralWeightingType = weight,
                                                Percentile = percentile,
                                                BinSize = binSize,
                                            }));
                                            break;
                                        default:
                                            averagingParams.Add(new SpectralAveragingParameters()
                                            {
                                                NormalizationType = i % 2 == 0 ? NormalizationType.RelativeToTics : NormalizationType.NoNormalization,
                                                SpectraFileAveragingType = SpectraFileAveragingType.AverageEverynScansWithOverlap,
                                                NumberOfScansToAverage = numberOfScans,
                                                ScanOverlap = overlap,
                                                OutlierRejectionType = rejection,
                                                SpectralWeightingType = weight,
                                                BinSize = binSize,
                                            });
                                            break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return averagingParams;
        }

        private Dictionary<int, double> ScoreChargeStates(IReadOnlyCollection<MsDataScan> scans)
        {
            Dictionary<int, double> chargeStateScores = chargeStateAndMz.ToDictionary
                (p => p.Key, p => 0.0);
            int scanCount = scans.Count(p => p.MsnOrder == 1);

            foreach (var spectra in scans.Where(p => p.MsnOrder == 1).Select(p => p.MassSpectrum))
            {
                var yCutoff = spectra.YArray.Max() * intensityCutoff;
                OriginalAveragingMatcherResults.OriginalAverageNoiseLevel = yCutoff;
                foreach (var chargeState in chargeStateAndMz)
                {
                    // relative intensity
                    // TODO: change relative intensity to noise level
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
