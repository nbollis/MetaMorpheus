using System;
using System.Text;
using GuiFunctions;

namespace Test;

internal readonly record struct IndividualPeakResult : ITsv
{
    internal int ScanNumber { get; init; }
    internal double TheoreticalMz { get; init; }
    internal int TheoreticalCharge { get; init; }
    internal double ExperimentalMz { get; init; }
    internal int ExperimentalCharge { get; init; }
    internal double ExperimentalIntensity { get; init; }
    internal bool FoundAboveCutoff { get; init; }
    internal bool ChargeStateResolvable { get; init; }
    internal double MzPpmError { get; init; }
    internal int NumberOfIsotopicPeaks { get; init; }

    internal IndividualPeakResult(int scanNum, int charge, double mz, int expCharge, double expMz, double experimentalIntensity,
        bool found, bool resolvable, int numberOfIsotopicPeaks)
    {
        ScanNumber = scanNum;
        TheoreticalCharge = charge;
        TheoreticalMz = mz;
        ExperimentalCharge = expCharge;
        ExperimentalMz = expMz;
        ExperimentalIntensity = experimentalIntensity;
        FoundAboveCutoff = found;
        ChargeStateResolvable = resolvable;
        NumberOfIsotopicPeaks = numberOfIsotopicPeaks;
        MzPpmError = (TheoreticalMz - ExperimentalMz) / Math.Pow(10, 6);
    }

    internal IndividualPeakResult(string tsvLine)
    {
        var splits = tsvLine.Split('\t');
        ScanNumber = int.Parse(splits[0]);
        TheoreticalMz = double.Parse(splits[1]);
        TheoreticalCharge = int.Parse(splits[2]);
        ExperimentalMz = double.Parse(splits[3]);
        ExperimentalCharge = int.Parse(splits[4]);
        ExperimentalIntensity = double.Parse(splits[5]);
        FoundAboveCutoff = bool.Parse(splits[6]);
        ChargeStateResolvable = bool.Parse(splits[7]);
        MzPpmError = double.Parse(splits[8]);
        NumberOfIsotopicPeaks = int.Parse(splits[9]);
    }

    public string TabSeparatedHeader
    {
        get
        {
            var sb = new StringBuilder();

            sb.Append("ScanNumber\t");
            sb.Append("TheoreticalMz\t");
            sb.Append("TheoreticalCharge\t");
            sb.Append("ExperimentalMz\t");
            sb.Append("ExperimentalCharge\t");
            sb.Append("Intensity\t");
            sb.Append("FoundAboveCutoff\t");
            sb.Append("ChargeStateResolvable\t");
            sb.Append("MzPpmError\t");
            sb.Append("NumberOfIsotopicPeaks\t");

            var tsvString = sb.ToString().TrimEnd('\t');
            return tsvString;
        }

    }

    public string ToTsvString()
    {
        var sb = new StringBuilder();

        sb.Append($"{ScanNumber}\t");
        sb.Append($"{TheoreticalMz}\t");
        sb.Append($"{TheoreticalCharge}\t");
        sb.Append($"{ExperimentalMz}\t");
        sb.Append($"{ExperimentalCharge}\t");
        sb.Append($"{ExperimentalIntensity}\t");
        sb.Append($"{FoundAboveCutoff}\t");
        sb.Append($"{ChargeStateResolvable}\t");
        sb.Append($"{MzPpmError}\t");
        sb.Append($"{NumberOfIsotopicPeaks}\t");

        var tsvString = sb.ToString().TrimEnd('\t');
        return tsvString;
    }
}