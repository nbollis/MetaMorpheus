using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Easy.Common.Extensions;
using MathNet.Numerics;

namespace EngineLayer.FdrAnalysis
{
    /// <summary>
    /// https://prospector.ucsf.edu/prospector/html/misc/publications/2006_ASMS_1.pdf
    /// </summary>
    public static class EValueCalculator
    {
        private static int _decimalPlacesToRound = 4;

        public static void GetEValueEstimationLineByLinearCurveFit(List<double> allDecoyScores, out double slope, out double intercept, out bool useLogTransform)
        {
            // use top fraction of the scores (10%) requiring large numbers of
            // incorrect matches to model the distribution
            var orderedMatches = allDecoyScores
                .Take(Range.EndAt((int)(allDecoyScores.Count * 0.1) + 1))
                .OrderBy(p => p)
                .ToList();

            // Calculate the cumulative score distribution
            double cumulativeCount = 0;
            var scoreVsCumulativeFrequency = new Dictionary<double, double>();
            foreach (var result in orderedMatches.GroupBy(p => p.Round(_decimalPlacesToRound))
                         .OrderByDescending(p => p.Key))
            {
                cumulativeCount += result.Count();
                scoreVsCumulativeFrequency[result.Key] = cumulativeCount;
            }

            // Normalize cumulative score distribution to 1
            foreach (var key in scoreVsCumulativeFrequency.Keys.ToList())
                scoreVsCumulativeFrequency[key] /= cumulativeCount;


            // Plot log(survival) vs log(score) and use linear regression to estimate
            // p-values for a given peptide score
            var scores = scoreVsCumulativeFrequency.Keys
                .ToArray();
            var logScores = scoreVsCumulativeFrequency.Keys
                .Select(p => Math.Log10(p).Round(_decimalPlacesToRound))
                .ToArray();
            var logSurvival = scoreVsCumulativeFrequency.Values
                .Select(p => Math.Log10(p).Round(_decimalPlacesToRound))
                .ToArray();

            // Calculate both log and linear fits and use the best fit (most linear)
            var logLine = Fit.Line(logScores, logSurvival);
            var logR2 = GoodnessOfFit.RSquared(logScores.Select(p => logLine.A * p + logLine.B).ToArray(), logSurvival);

            var line = Fit.Line(scores, logSurvival);
            var r2 = GoodnessOfFit.RSquared(scores.Select(p => line.A * p + line.B).ToArray(), logSurvival);

            if (logR2 > r2)
            {
                slope = logLine.A;
                intercept = logLine.B;
                useLogTransform = true;
            }
            else
            {
                slope = line.A;
                intercept = line.B;
                useLogTransform = false;
            }
        }
    }
}
