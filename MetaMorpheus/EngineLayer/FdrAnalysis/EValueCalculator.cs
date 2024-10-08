using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Easy.Common.Extensions;
using MathNet.Numerics;

namespace EngineLayer.FdrAnalysis
{
    public enum EValueMethod
    {
        LinearTailFit,
        MethodOfMoments,
        MaximumLikelihood
    }
    /// <summary>
    /// https://prospector.ucsf.edu/prospector/html/misc/publications/2006_ASMS_1.pdf
    /// </summary>
    public static class EValueCalculator
    {
        private static int _decimalPlacesToRound = 4;


        public static void CalculateAndSetEValues(List<SpectralMatch> allMatches, EValueMethod method = EValueMethod.LinearTailFit)
        {
            
            switch (method)
            {
                case EValueMethod.LinearTailFit:
                    var decoyScores = allMatches.Where(p => p.IsDecoy)
                        .Select(p => p.Score)
                        .ToList();
                    var lineToFit = CalculateEValueLinearTailFit(decoyScores);
                    foreach (var match in allMatches)
                    {
                        if (lineToFit.LogTransform)
                        {
                            match.FdrInfo.EValue = Math.Pow(10, lineToFit.Slope * Math.Log10(match.Score) + lineToFit.Intersect);
                        }
                        else
                        {
                            match.FdrInfo.EValue = Math.Pow(10, lineToFit.Slope * match.Score + lineToFit.Intersect);
                        }
                    }


                    break;

                case EValueMethod.MaximumLikelihood:
                case EValueMethod.MethodOfMoments:
                    default:
                        throw new NotImplementedException("EValue methods not yet implemented");
            }


        }

        public static (double Slope, double Intersect, bool LogTransform) CalculateEValueLinearTailFit(List<double> allDecoyScores)
        {
            // use top fraction of the scores (10%) requiring large numbers of
            // incorrect matches to model the distribution
            var orderedMatches = allDecoyScores
                .Take(Range.EndAt((int)(allDecoyScores.Count * 0.1) + 1))
                .OrderBy(p => p)
                .ToList();

            // Plot log(survival) vs log(score) and use linear regression to estimate
            // p-values for a given peptide score
            var uniqueScores = orderedMatches.Select(p => p.Round(_decimalPlacesToRound))
                .Distinct()
                .ToArray();

                // Calculate the cumulative score distribution
            double cumulativeCount = 0;
            var scoreVsCumulativeFrequency = uniqueScores.ToDictionary(p => p, p => 0.0);
            foreach (var result in orderedMatches.GroupBy(p => p.Round(_decimalPlacesToRound))
                         .OrderByDescending(p => p.Key))
            {
                cumulativeCount += result.Count();
                scoreVsCumulativeFrequency[result.Key] = cumulativeCount;
            }
                // normalize cumulative score distribution to 1
            scoreVsCumulativeFrequency.ForEach(p => scoreVsCumulativeFrequency[p.Key] /= cumulativeCount);

            var scores = scoreVsCumulativeFrequency.Keys
                .ToArray();
            var logScores = scoreVsCumulativeFrequency.Keys
                .Select(p => Math.Log10(p).Round(_decimalPlacesToRound))
                .ToArray();
            var logSurvival = scoreVsCumulativeFrequency.Values
                .Select(p => Math.Log10(p).Round(_decimalPlacesToRound))
                .ToArray();

            // log( survival ) vs log( score ) not always linear; for Prospector,
            // log(survival) vs score is more linear. So, we will calculate both and use the best fit (most linear)

            var logLine = Fit.Line(logScores, logSurvival);
            var logModeledValues = logScores.Select(p => logLine.A * p + logLine.B).ToArray();
            var logR2 = GoodnessOfFit.RSquared(logModeledValues, logSurvival);

            var line = Fit.Line(scores, logSurvival);
            var modeledValues = scores.Select(p => line.A * p + line.B).ToArray();
            var r2 = GoodnessOfFit.RSquared(modeledValues, logSurvival);

            return logR2 > r2 ? (logLine.A, logLine.B, true) : (line.A, line.B, false);
        }
    }
}
