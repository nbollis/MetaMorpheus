using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Easy.Common.Extensions;
using MathNet.Numerics;
using Plotly.NET;
using Plotly.NET.CSharp;
using Chart = Plotly.NET.CSharp.Chart;
using GenericChartExtensions = Plotly.NET.GenericChartExtensions;

namespace EngineLayer.FdrAnalysis
{
    /// <summary>
    /// https://prospector.ucsf.edu/prospector/html/misc/publications/2006_ASMS_1.pdf
    /// </summary>
    public static class EValueCalculator
    {
        private static int _decimalPlacesToRound = 4;



        public static void GetEValueEstimationLineByLinearCurveFitWithPlots(List<double> allDecoyScores, string title, 
            out double slope, out double intercept, out bool useLogTransform, out GenericChart combinedPlot)
        {
            // use top fraction of the scores (10%) requiring large numbers of
            // incorrect matches to model the distribution
            var orderedMatches = allDecoyScores
                .OrderByDescending(p => p)
                .Take(Range.EndAt((int)(allDecoyScores.Count * 0.1) + 1))
                .ToList();

            // Calculate the cumulative score distribution
            double cumulativeCount = 0;
            var scoreVsCumulativeFrequency = new Dictionary<double, double>();
            foreach (var result in orderedMatches.GroupBy(p => p.Round(_decimalPlacesToRound)))
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


            // Calculate the cumulative score distribution again for the whole set
            var cumulativeCount2 = 0;
            var scoreVsCumulativeFrequency2 = new Dictionary<double, double>();
            foreach (var result in allDecoyScores.GroupBy(p => p.Round(_decimalPlacesToRound))
                         .OrderByDescending(p => p.Key))
            {
                cumulativeCount2 += result.Count();
                scoreVsCumulativeFrequency2[result.Key] = cumulativeCount2;
            }
            foreach (var key in scoreVsCumulativeFrequency2.Keys.ToList())
                scoreVsCumulativeFrequency2[key] /= cumulativeCount2;

            GenericChart logScorePlot;
            GenericChart logScoreTrimmedPlot;
            string logTitle;
            if (logR2 > r2)
            {
                slope = logLine.A;
                intercept = logLine.B;
                useLogTransform = true;

                logScorePlot = Chart.Line<double, double, string>(scoreVsCumulativeFrequency2.Keys
                        .Select(p => Math.Log10(p).Round(_decimalPlacesToRound)).ToArray(),
                    scoreVsCumulativeFrequency2.Values.Select(p => Math.Log10(p).Round(_decimalPlacesToRound)).ToArray()
                    , true, "Log Score Vs Log Survival");
                logScoreTrimmedPlot = Chart.Line<double, double, string>(logScores, logSurvival, true,
                    "Log Score Vs Log Survival");
                logTitle = "Log Score Vs Log Survival";
            }
            else
            {
                slope = line.A;
                intercept = line.B;
                useLogTransform = false;

                logScorePlot = Chart.Line<double, double, string>(scoreVsCumulativeFrequency2.Keys.ToArray(),
                    scoreVsCumulativeFrequency2.Values.Select(p => Math.Log10(p).Round(_decimalPlacesToRound)).ToArray()
                    , true, "Log Score Vs Log Survival");
                logScoreTrimmedPlot = Chart.Line<double, double, string>(scores, logSurvival, true,
                    "Score Vs Log Survival");
                logTitle = "Score Vs Log Survival";
            }

            var scoreVsFrequencyChart = Chart.Histogram<double, int, string>(allDecoyScores
                    .OrderByDescending(p => p).Select(p => p).ToArray(),
                HistNorm: StyleParam.HistNorm.Percent, Name: "Score vs Frequency");
            var scoreVsCumFrequencyChart = Chart.Line<double, double, string>(scoreVsCumulativeFrequency2.Keys.ToArray(),
                scoreVsCumulativeFrequency2.Values.ToArray(), true, "Score Vs Cumulative Frequency");

            combinedPlot = Chart.Grid(
                new[] { scoreVsFrequencyChart, scoreVsCumFrequencyChart, logScorePlot, logScoreTrimmedPlot },
                2, 2, 
                new[]
                { "Score vs Frequency", "Score Vs Cumulative Frequency", logTitle, $"Top 10% {logTitle}" }
            ).WithSize(800, 800)
            .WithTitle(title)
            .WithLegendStyle(VerticalAlign: StyleParam.VerticalAlign.Bottom);
        }

















        public static void GetEValueEstimationLineByLinearCurveFit(List<double> allDecoyScores, out double slope, out double intercept, out bool useLogTransform)
        {
            // use top fraction of the scores (10%) requiring large numbers of
            // incorrect matches to model the distribution
            var orderedMatches = allDecoyScores
                .OrderByDescending(p => p)
                .Take(Range.EndAt((int)(allDecoyScores.Count * 0.1) + 1))
                .ToList();

            // Calculate the cumulative score distribution
            double cumulativeCount = 0;
            var scoreVsCumulativeFrequency = new Dictionary<double, double>();
            foreach (var result in orderedMatches.GroupBy(p => p.Round(_decimalPlacesToRound)))
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
