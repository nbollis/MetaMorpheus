using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GuiFunctions;
using MathNet.Numerics.Statistics;
using SpectralAveraging;
using Svg;
using ThermoFisher.CommonCore.Data.Business;
using TopDownProteomics;
using static iText.IO.Image.Jpeg2000ImageData;

namespace Test;

internal record FileMassAccuracyResults : ITsv
{
    internal List<ScanMassAccuracyResults> AllResults { get; init; }
    internal SpectralAveragingParameters Parameters { get; init; }
    internal double ScanCount { get; init; }
    internal double AverageMedOverStandardDevOfPeaks { get; init; }
    internal double StdMedOverStandardDeviationOfPeaks { get; init; }
    internal double AverageMzFoundPerScan { get; init; }
    internal double StdMzFoundPerScan { get; init; }
    internal double AverageChargeStateResolvablePerScan { get; init; }
    internal double StdChargeStateResolvablePerScan { get; init; }
    internal double AverageMzPpmError { get; init; }
    internal double StdMzPpmError { get; init; }
    internal double AverageIsotopicPeakCount {get; init; }
    internal double StdIsotopicPeakCount { get; init; }

    internal double PsmCount { get; set; }


    public FileMassAccuracyResults(List<ScanMassAccuracyResults> allResults, 
        SpectralAveragingParameters averagingParameters)
    {
        Parameters = averagingParameters;
        AllResults = allResults;
        ScanCount = AllResults.Count;

        var allMedOverDeviation = AllResults.Select(p => p.MedianOverStdOfPeaks);
        AverageMedOverStandardDevOfPeaks = allMedOverDeviation.Average();
        StdMedOverStandardDeviationOfPeaks = allMedOverDeviation.StandardDeviation();

        var allMzFoundPerScan = AllResults.Select(p => (double) p.MzFound);
        AverageMzFoundPerScan = allMzFoundPerScan.Average();
        StdMzFoundPerScan = allMzFoundPerScan.StandardDeviation();

        var allChargeStateResolvablePerScan = AllResults.Select(p => (double) p.ChargeResolved);
        AverageChargeStateResolvablePerScan = allChargeStateResolvablePerScan.Average();
        StdChargeStateResolvablePerScan = allChargeStateResolvablePerScan.StandardDeviation();

        var allPpmError = AllResults
            .Select(m => m.MzPpmError)
            .ToArray();
        AverageMzPpmError = allPpmError.Average();
        StdMzPpmError = allPpmError.StandardDeviation();

        var isotopicPeakCount = AllResults.Select(p => p.AverageIsotopicPeaksPerEnvelope);
        AverageIsotopicPeakCount = isotopicPeakCount.Average();
        StdIsotopicPeakCount = isotopicPeakCount.StandardDeviation();
    }

    public FileMassAccuracyResults(string tsvLine, SpectralAveragingParameters parameters)
    {
        Parameters = parameters;
        var splits = tsvLine.Split('\t');
        ScanCount = double.Parse(splits[0]);
        PsmCount = double.Parse(splits[1]);
        AverageMedOverStandardDevOfPeaks = double.Parse(splits[2]);
        StdMedOverStandardDeviationOfPeaks = double.Parse(splits[3]);
        AverageMzFoundPerScan = double.Parse(splits[4]);
        StdMzFoundPerScan = double.Parse(splits[5]);
        AverageChargeStateResolvablePerScan = double.Parse(splits[6]);
        StdChargeStateResolvablePerScan = double.Parse(splits[7]);
        AverageMzPpmError = double.Parse(splits[8]);
        StdMzPpmError = double.Parse(splits[9]);
        AverageIsotopicPeakCount = double.Parse(splits[10]);
        StdIsotopicPeakCount = double.Parse(splits[11]);
    }

    public FileMassAccuracyResults(string tsvLine)
    {
        var splits = tsvLine.Split('\t');
        ScanCount = double.Parse(splits[0]);
        PsmCount = double.Parse(splits[1]);
        AverageMedOverStandardDevOfPeaks = double.Parse(splits[2]);
        StdMedOverStandardDeviationOfPeaks = double.Parse(splits[3]);
        AverageMzFoundPerScan = double.Parse(splits[4]);
        StdMzFoundPerScan = double.Parse(splits[5]);
        AverageChargeStateResolvablePerScan = double.Parse(splits[6]);
        StdChargeStateResolvablePerScan = double.Parse(splits[7]);
        AverageMzPpmError = double.Parse(splits[8]);
        StdMzPpmError = double.Parse(splits[9]);
        AverageIsotopicPeakCount = double.Parse(splits[10]);
        StdIsotopicPeakCount = double.Parse(splits[11]);

        Parameters = new SpectralAveragingParameters()
        {
            BinSize = double.Parse(splits[12]),
            NumberOfScansToAverage = int.Parse(splits[13]),
            NormalizationType = Enum.Parse<NormalizationType>(splits[14]),
            SpectralWeightingType = Enum.Parse<SpectraWeightingType>(splits[15]),
            OutlierRejectionType = Enum.Parse<OutlierRejectionType>(splits[16]),
            MinSigmaValue = double.Parse(splits[17]),
            MaxSigmaValue = double.Parse(splits[18]),
            Percentile = double.Parse(splits[19]),
        };
    }

    public static IEnumerable<FileMassAccuracyResults> LoadResultFromTsv(string tsvPath)
    {
        var scansLines = File.ReadAllLines(tsvPath);
        List<ScanMassAccuracyResults> scanResults = new();
        for (int i = 1; i < scansLines.Length; i++)
        {
            yield return new FileMassAccuracyResults(scansLines[i]);
        }
    }

    public string TabSeparatedHeader 
    {
        get
        {
            var sb = new StringBuilder();

            sb.Append("ScanCount\t");
            sb.Append("PsmCount\t");
            sb.Append("AverageMedOverStandardDevOfPeaks\t");
            sb.Append("StdMedOverStandardDeviationOfPeaks\t");
            sb.Append("AverageMzFoundPerScan\t");
            sb.Append("StdMzFoundPerScan\t");
            sb.Append("AverageChargeStateResolvablePerScan\t");
            sb.Append("StdChargeStateResolvablePerScan\t");
            sb.Append("AverageMzPpmError\t");
            sb.Append("StdMzPpmError\t");
            sb.Append("AverageIsotopicPeakCount\t");
            sb.Append("StdIsotopicPeakCount\t");

            sb.Append("BinSize\t");
            sb.Append("ScansAveraged\t");
            sb.Append("Normalization\t");
            sb.Append("Weighting\t");
            sb.Append("OutlierRejection\t");
            sb.Append("MinSigma\t");
            sb.Append("MaxSigma\t");
            sb.Append("Percentile\t");

            var tsvString = sb.ToString().TrimEnd('\t');
            return tsvString;
        }
    }
    public string ToTsvString()
    {
        var sb = new StringBuilder();

        sb.Append($"{ScanCount}\t");
        sb.Append($"{PsmCount}\t");
        sb.Append($"{AverageMedOverStandardDevOfPeaks}\t");
        sb.Append($"{StdMedOverStandardDeviationOfPeaks}\t");
        sb.Append($"{AverageMzFoundPerScan}\t");
        sb.Append($"{StdMzFoundPerScan}\t");
        sb.Append($"{AverageChargeStateResolvablePerScan}\t");
        sb.Append($"{StdChargeStateResolvablePerScan}\t");
        sb.Append($"{AverageMzPpmError}\t");
        sb.Append($"{StdMzPpmError}\t");
        sb.Append($"{AverageIsotopicPeakCount}\t");
        sb.Append($"{StdIsotopicPeakCount}\t");

        sb.Append($"{Parameters.BinSize}\t");
        sb.Append($"{Parameters.NumberOfScansToAverage}\t");
        sb.Append($"{Parameters.NormalizationType}\t");
        sb.Append($"{Parameters.SpectralWeightingType}\t");
        sb.Append($"{Parameters.OutlierRejectionType}\t");

        if (Parameters.OutlierRejectionType == OutlierRejectionType.AveragedSigmaClipping ||
            Parameters.OutlierRejectionType == OutlierRejectionType.SigmaClipping ||
            Parameters.OutlierRejectionType == OutlierRejectionType.WinsorizedSigmaClipping)
        {
            sb.Append($"{Parameters.MinSigmaValue}\t");
            sb.Append($"{Parameters.MaxSigmaValue}\t");
        }
        else
        {
            sb.Append($"0\t");
            sb.Append($"0\t");
        }

        if (Parameters.OutlierRejectionType == OutlierRejectionType.PercentileClipping)
        {
            sb.Append($"{Parameters.Percentile}\t");
        }
        else
        {
            sb.Append($"0\t");
        }

        var tsvString = sb.ToString().TrimEnd('\t');
        return tsvString;
    }
}

