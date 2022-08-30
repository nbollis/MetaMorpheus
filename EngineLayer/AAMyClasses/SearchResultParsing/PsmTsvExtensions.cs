using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using MassSpectrometry;
using MathNet.Numerics;
using MathNet.Numerics.Statistics;

namespace EngineLayer
{
    public static class PsmTsvExtensions
    {
        public static double GetSpectraSignalToNoise(this PsmFromTsv psmFromTsv)
        {
            double maxMatchedIonHeight = psmFromTsv.MatchedIons.Max(p => p.Intensity);
            double medianMatchedIonHeight = psmFromTsv.MatchedIons.Select(p => p.Intensity).Median();
            return maxMatchedIonHeight / medianMatchedIonHeight;
        }

        public static double GetNoiseLevel(this MsDataScan scan, double snrCutoff)
        {
            double[] sortedIntensities = scan.MassSpectrum.YArray.OrderBy(p => p).ToArray();

            int i = 1;
            double delta = 0.5;
            double iHati;
            for (; i < sortedIntensities.Length; i++)
            {
                if (i == 1)
                {
                    iHati = (1 + delta) * sortedIntensities[i - 1];
                }
                else
                {
                    double[] peakIntensities = sortedIntensities.Take(i).ToArray();
                    double[] peakNum = Enumerable.Range(1, i).Select(p => (double)p).ToArray();
                    // Item1 is intercept, Item2 is slope
                    Tuple<double, double> linearRegression = Fit.Line(peakNum, peakIntensities);
                    iHati = linearRegression.Item2 * i + linearRegression.Item1;
                }

                double signalToNoise = sortedIntensities[i - 1] / iHati;
                if (signalToNoise > snrCutoff)
                    break;
            }

            return sortedIntensities[i - 1];
        }
    }
}
