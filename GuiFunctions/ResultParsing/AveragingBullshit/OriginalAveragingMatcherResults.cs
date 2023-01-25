using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuiFunctions
{
    public readonly struct OriginalAveragingMatcherResults : ITsv
    {
        private static Dictionary<int, double> originalScansScore;
        public static Dictionary<int, double> OriginalScansScore
        {
            get => originalScansScore;
            set
            {
                originalScansScore = value;
                var observedChargeStates =
                    originalScansScore.Where(p => p.Value != 0).ToDictionary(p => p.Key, p => p.Value);
                OriginalNumberOfChargeStates = observedChargeStates.Count;
                OriginalFoundIn75PercentOfScans = observedChargeStates.Count(p => p.Value >= 0.75);
                OriginalFoundIn90PercentOfScans = observedChargeStates.Count(p => p.Value >= 0.9);
                OriginalSumOfScores = Math.Round(observedChargeStates.Values.Sum(), 2);
            }
        }
        public static int OriginalScanCount { get; set; }
        public static int OriginalNumberOfChargeStates { get; private set; }
        public static double OriginalAverageNoiseLevel { get; set; }
        public static double OriginalSumOfScores { get; private set; }
        public static int OriginalFoundIn75PercentOfScans { get; private set; }
        public static int OriginalFoundIn90PercentOfScans { get; private set; }

        public string TabSeparatedHeader
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append("Milliseconds To Average\t");
                sb.Append("Scan Count\t");
                sb.Append("Observed Charge States\t");
                sb.Append("Noise Level\t");
                sb.Append("Sum of Scores\t");
                sb.Append("75%\t");
                sb.Append("90%\t");
                var tsvString = sb.ToString().TrimEnd('\t');
                return tsvString;
            }
        }

        public string ToTsvString()
        {
            var sb = new StringBuilder();
            sb.Append($"Not Averaged\t");
            sb.Append($"{OriginalScanCount}\t");
            sb.Append($"{OriginalNumberOfChargeStates}\t");
            sb.Append($"{OriginalAverageNoiseLevel}\t");
            sb.Append($"{OriginalSumOfScores}\t");
            sb.Append($"{OriginalFoundIn75PercentOfScans}\t");
            sb.Append($"{OriginalFoundIn90PercentOfScans}\t");
            var tsvString = sb.ToString().TrimEnd('\t');
            return tsvString;
        }
    }
}
