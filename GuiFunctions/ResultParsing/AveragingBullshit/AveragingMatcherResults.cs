using MzLibUtil;
using SpectralAveraging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Easy.Common.Extensions;

namespace GuiFunctions
{
    public readonly struct AveragingMatcherResults : ITsv
    {

        #region Scoring Metrics

        public double TimeToAverageInMilliseconds { get; init; }
        public int NumberOfScansAfterAveraging { get; init; }
        public int NumberOfChargeStatesObserved { get; init; }
        public double NumberOfChargeStatesObservedPercentChange { get; init; }
        public double AverageNoiseLevel { get; init; }
        public double AverageNoiseLevelPercentChange { get; init; }
        public double SumOfScores { get; init; }
        public double SumOfScoresPercentChange { get; init; }
        public int FoundIn75PercentOfScans { get; init; }
        public double FoundIn75PercentChange { get; init; }
        public int FoundIn90PercentOfScans { get; init; }
        public double FoundIn90PercentChange { get; init; }

        #endregion

        public SpectralAveragingParameters Parameters { get; init; }


        public AveragingMatcherResults(SpectralAveragingParameters parameters, int numOfScans,
            Dictionary<int, double> chargeStateScores, double timeToAverageInMilliseconds, double averageNoiseLevel)
        {
            if (OriginalAveragingMatcherResults.OriginalScanCount.IsDefault())
                throw new MzLibException("Gotta initialize the original before averaging my guy");

            AverageNoiseLevel = averageNoiseLevel;
            Parameters = parameters;
            TimeToAverageInMilliseconds = timeToAverageInMilliseconds;
            NumberOfScansAfterAveraging = numOfScans;

            var observedChargeStates = chargeStateScores.Where(p => p.Value != 0).ToDictionary(p => p.Key, p => p.Value);
            NumberOfChargeStatesObserved = observedChargeStates.Count;
            FoundIn75PercentOfScans = observedChargeStates.Count(p => p.Value >= 0.75);
            FoundIn90PercentOfScans = observedChargeStates.Count(p => p.Value >= 0.9);

            SumOfScores = Math.Round(chargeStateScores.Values.Sum(), 2);
            AverageNoiseLevelPercentChange =
                Math.Round((AverageNoiseLevel - OriginalAveragingMatcherResults.OriginalAverageNoiseLevel) /
                    OriginalAveragingMatcherResults.OriginalAverageNoiseLevel * 100.00 / 2);
            SumOfScoresPercentChange =
                Math.Round(
                    (SumOfScores - OriginalAveragingMatcherResults.OriginalSumOfScores) /
                    OriginalAveragingMatcherResults.OriginalSumOfScores * 100.0, 2);
            NumberOfChargeStatesObservedPercentChange =
                Math.Round(
                    (NumberOfChargeStatesObserved - OriginalAveragingMatcherResults.OriginalNumberOfChargeStates) /
                    (double)OriginalAveragingMatcherResults.OriginalNumberOfChargeStates * 100.0, 2);
            FoundIn75PercentChange =
                Math.Round(
                    (FoundIn75PercentOfScans - OriginalAveragingMatcherResults.OriginalFoundIn75PercentOfScans) /
                    (double)OriginalAveragingMatcherResults.OriginalFoundIn75PercentOfScans * 100.0, 2);
            FoundIn90PercentChange =
                Math.Round(
                    (FoundIn90PercentOfScans - OriginalAveragingMatcherResults.OriginalFoundIn90PercentOfScans) /
                    (double)OriginalAveragingMatcherResults.OriginalFoundIn90PercentOfScans * 100.0, 2);
        }


        public string TabSeparatedHeader
        {
            get
            {
                var sb = new StringBuilder();
                // results
                sb.Append("Milliseconds To Average\t");
                sb.Append("Scan Count\t");
                sb.Append("Observed Charge States\t");
                sb.Append("Charge States % Change\t");
                sb.Append("Noise Level\t");
                sb.Append("Noise Level % Change\t");
                sb.Append("Sum of Scores\t");
                sb.Append("Sum % Change\t");
                sb.Append("75%\t");
                sb.Append("75% Change\t");
                sb.Append("90%\t");
                sb.Append("90% Change\t");

                // averaging stuff
                sb.Append("Bin Size\t");
                sb.Append("Scans To Average\t");
                sb.Append("Scan Overlap\t");
                sb.Append("Weighting Type\t");
                sb.Append("Normalization Type\t");
                sb.Append("Rejection Type\t");
                if (Parameters.OutlierRejectionType.ToString().Contains("Sigma"))
                {
                    sb.Append("Min Sigma\t");
                    sb.Append("Max Sigma\t");
                }
                else if (Parameters.OutlierRejectionType.ToString().Contains("Percentile"))
                    sb.Append("Percentile\t");

                var tsvString = sb.ToString().TrimEnd('\t');
                return tsvString;
            }
        }

        public string ToTsvString()
        {
            var sb = new StringBuilder();
            // results
            sb.Append($"{TimeToAverageInMilliseconds}\t");
            sb.Append($"{NumberOfScansAfterAveraging}\t");
            sb.Append($"{NumberOfChargeStatesObserved}\t");
            sb.Append($"{NumberOfChargeStatesObservedPercentChange}\t");
            sb.Append($"{AverageNoiseLevel}\t");
            sb.Append($"{AverageNoiseLevelPercentChange}\t");
            sb.Append($"{SumOfScores}\t");
            sb.Append($"{SumOfScoresPercentChange}\t");
            sb.Append($"{FoundIn75PercentOfScans}\t");
            sb.Append($"{FoundIn75PercentChange}\t");
            sb.Append($"{FoundIn90PercentOfScans}\t");
            sb.Append($"{FoundIn90PercentChange}\t");

            // averaging stuff
            sb.Append($"{Parameters.BinSize}\t");
            sb.Append($"{Parameters.NumberOfScansToAverage}\t");
            sb.Append($"{Parameters.ScanOverlap}\t");
            sb.Append($"{Parameters.SpectralWeightingType}\t");
            sb.Append($"{Parameters.NormalizationType}\t");
            sb.Append($"{Parameters.OutlierRejectionType}\t");
            if (Parameters.OutlierRejectionType.ToString().Contains("Sigma"))
            {
                sb.Append($"{Parameters.MinSigmaValue}\t");
                sb.Append($"{Parameters.MaxSigmaValue}\t");
            }
            else if (Parameters.OutlierRejectionType.ToString().Contains("Percentile"))
                sb.Append($"{Parameters.Percentile}\t");
            var tsvString = sb.ToString().TrimEnd('\t');
            return tsvString;
        }
    }

    
}
