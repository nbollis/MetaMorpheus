#nullable enable
using Chemistry;
using EngineLayer;
using Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaskLayer.ParallelSearch.Util;

namespace TaskLayer.ParallelSearch.Analysis.Collectors;

public sealed class PrecursorDeconTsvBackfillService
{
    public bool BackfillIfNeeded(
        string outputFolder,
        List<TransientDatabaseMetrics> metricsList,
        List<string> currentRawFileList,
        FileSpecificParameters[] fileSettingsList,
        CommonParameters commonParameters,
        double qValueCutoff,
        bool disposeOfFileWhenDone)
    {
        if (!commonParameters.DoPrecursorDeconvolution
            || string.IsNullOrEmpty(outputFolder)
            || metricsList == null
            || metricsList.Count == 0
            || currentRawFileList == null
            || currentRawFileList.Count == 0)
        {
            return false;
        }

        var pendingMetrics = metricsList.Where(NeedsBackfill).ToList();
        if (pendingMetrics.Count == 0)
            return false;

        var scanLookup = BuildScanLookup(currentRawFileList, fileSettingsList, commonParameters, disposeOfFileWhenDone);
        if (scanLookup.Count == 0)
            return false;

        bool backfilledAny = false;
        foreach (var metric in pendingMetrics)
        {
            var dbDir = Path.Combine(outputFolder, metric.DatabaseName);
            if (!Directory.Exists(dbDir))
                continue;

            var psmPath = Path.Combine(dbDir, $"{metric.DatabaseName}_AllPsms.psmtsv");
            var peptidePath = Path.Combine(dbDir, $"{metric.DatabaseName}_AllPeptides.psmtsv");

            if (!File.Exists(psmPath) || !File.Exists(peptidePath))
                continue;

            try
            {
                var parsingParams = new SpectrumMatchParsingParameters();

                var psmRows = SpectrumMatchTsvReader.ReadTsv(psmPath, out var _, parsingParams)
                    .Where(p => p.QValue <= qValueCutoff)
                    .ToList();
                var peptideRows = SpectrumMatchTsvReader.ReadTsv(peptidePath, out var _, parsingParams)
                    .Where(p => p.QValue <= qValueCutoff)
                    .ToList();

                var psmData = BuildBackfillData(
                    psmRows,
                    scanLookup,
                    row => row.FileNameWithoutExtension,
                    row => row.Ms2ScanNumber,
                    row => row.PrecursorCharge,
                    row => row.PrecursorMass,
                    row => row.DeltaScore,
                    row => row.MassDiffPpm);

                var peptideData = BuildBackfillData(
                    peptideRows,
                    scanLookup,
                    row => row.FileNameWithoutExtension,
                    row => row.Ms2ScanNumber,
                    row => row.PrecursorCharge,
                    row => row.PrecursorMass,
                    row => row.DeltaScore,
                    row => row.MassDiffPpm);

                if (metric.PsmBacterialTargetDeltaScores.Length == 0)
                    metric.PsmBacterialTargetDeltaScores = psmData.DeltaScores;
                if (metric.PsmPrecursorMassErrors.Length == 0)
                    metric.PsmPrecursorMassErrors = psmData.PrecursorMassErrors;
                if (metric.PsmPrecursorDeconScores.Length == 0)
                    metric.PsmPrecursorDeconScores = psmData.PrecursorDeconScores;
                if (metric.PsmPrecursorEnvelopePeakCounts.Length == 0)
                    metric.PsmPrecursorEnvelopePeakCounts = psmData.EnvelopePeakCounts;
                if (metric.PsmPrecursorFractionalIntensities.Length == 0)
                    metric.PsmPrecursorFractionalIntensities = psmData.FractionalIntensities;

                if (metric.PeptideBacterialTargetDeltaScores.Length == 0)
                    metric.PeptideBacterialTargetDeltaScores = peptideData.DeltaScores;
                if (metric.PeptidePrecursorMassErrors.Length == 0)
                    metric.PeptidePrecursorMassErrors = peptideData.PrecursorMassErrors;
                if (metric.PeptidePrecursorDeconScores.Length == 0)
                    metric.PeptidePrecursorDeconScores = peptideData.PrecursorDeconScores;
                if (metric.PeptidePrecursorEnvelopePeakCounts.Length == 0)
                    metric.PeptidePrecursorEnvelopePeakCounts = peptideData.EnvelopePeakCounts;
                if (metric.PeptidePrecursorFractionalIntensities.Length == 0)
                    metric.PeptidePrecursorFractionalIntensities = peptideData.FractionalIntensities;

                metric.PopulateResultsFromProperties();
                backfilledAny = true;
            }
            catch
            {
                // Leave defaults if loading/parsing/alignment fails.
            }
        }

        return backfilledAny;
    }

    private static bool NeedsBackfill(TransientDatabaseMetrics metric)
    {
        return metric.TargetPsmsFromTransientDbAtQValueThreshold > 0 &&
            metric.PsmBacterialTargetDeltaScores.Length == 0
               || metric.PsmPrecursorMassErrors.Length == 0
               || metric.PsmPrecursorDeconScores.Length == 0
               || metric.PsmPrecursorEnvelopePeakCounts.Length == 0
               || metric.PsmPrecursorFractionalIntensities.Length == 0
               || metric.PeptideBacterialTargetDeltaScores.Length == 0
               || metric.PeptidePrecursorMassErrors.Length == 0
               || metric.PeptidePrecursorDeconScores.Length == 0
               || metric.PeptidePrecursorEnvelopePeakCounts.Length == 0
               || metric.PeptidePrecursorFractionalIntensities.Length == 0;
    }

    private static Dictionary<string, Dictionary<int, List<Ms2ScanWithSpecificMass>>> BuildScanLookup(
        List<string> currentRawFileList,
        FileSpecificParameters[] fileSettingsList,
        CommonParameters commonParameters,
        bool disposeOfFileWhenDone)
    {
        var scanLookup = new Dictionary<string, Dictionary<int, List<Ms2ScanWithSpecificMass>>>(StringComparer.OrdinalIgnoreCase);
        var fileManager = new MyFileManager(disposeOfFileWhenDone);

        for (int i = 0; i < currentRawFileList.Count; i++)
        {
            var rawFile = currentRawFileList[i];
            if (IsNeutralMassFile(rawFile))
                continue;

            var fileSpecific = i < fileSettingsList.Length ? fileSettingsList[i] : null;
            var fileParams = MetaMorpheusTask.SetAllFileSpecificCommonParams(commonParameters, fileSpecific);
            var msDataFile = fileManager.LoadFile(rawFile, fileParams);
            var scans = MetaMorpheusTask.GetMs2Scans(msDataFile, rawFile, fileParams).ToList();

            scanLookup[Path.GetFileNameWithoutExtension(rawFile)] = scans
                .GroupBy(p => p.OneBasedScanNumber)
                .ToDictionary(g => g.Key, g => g.ToList());

            fileManager.DoneWithFile(rawFile);
        }

        return scanLookup;
    }

    private static bool IsNeutralMassFile(string filePath)
    {
        return filePath.EndsWith(".mgf", StringComparison.OrdinalIgnoreCase)
               || filePath.EndsWith(".msAlign", StringComparison.OrdinalIgnoreCase);
    }

    private static BackfillData BuildBackfillData<TRow>(
        List<TRow> rows,
        Dictionary<string, Dictionary<int, List<Ms2ScanWithSpecificMass>>> scanLookup,
        Func<TRow, string> fileStemSelector,
        Func<TRow, int> scanNumberSelector,
        Func<TRow, int> chargeSelector,
        Func<TRow, double> precursorMassSelector,
        Func<TRow, double?> deltaScoreSelector,
        Func<TRow, string> massDiffPpmSelector)
    {
        var deltaScores = rows
            .Select(deltaScoreSelector)
            .Where(v => v.HasValue && v.Value > 0)
            .Select(v => v!.Value)
            .ToArray();

        var precursorMassErrors = rows
            .Select(massDiffPpmSelector)
            .Select(ParseMeanMassDiffPpm)
            .Where(v => !double.IsNaN(v) && !double.IsInfinity(v))
            .ToArray();

        var deconScores = new List<double>();
        var envelopePeakCounts = new List<double>();
        var fractionalIntensities = new List<double>();

        foreach (var group in rows.GroupBy(r => (File: fileStemSelector(r), Scan: scanNumberSelector(r))))
        {
            if (!scanLookup.TryGetValue(group.Key.File, out var fileLookup))
                continue;
            if (!fileLookup.TryGetValue(group.Key.Scan, out var candidates) || candidates.Count == 0)
                continue;

            var groupedRows = group.ToList();
            var matchedCandidates = MatchRowsToCandidates(groupedRows, candidates, chargeSelector, precursorMassSelector);
            foreach (var candidate in matchedCandidates)
            {
                deconScores.Add(candidate.PrecursorDeconvolutionScore);
                envelopePeakCounts.Add(candidate.PrecursorEnvelopePeakCount);
                fractionalIntensities.Add(candidate.PrecursorFractionalIntensity);
            }
        }

        return new BackfillData(deltaScores, precursorMassErrors, deconScores.ToArray(), envelopePeakCounts.ToArray(), fractionalIntensities.ToArray());
    }

    private static List<Ms2ScanWithSpecificMass> MatchRowsToCandidates<TRow>(
        List<TRow> rows,
        List<Ms2ScanWithSpecificMass> candidates,
        Func<TRow, int> chargeSelector,
        Func<TRow, double> precursorMassSelector)
    {
        if (rows.Count == 0 || candidates.Count == 0)
            return [];

        if (rows.Count == 1 || candidates.Count == 1)
        {
            var row = rows[0];
            var best = candidates
                .OrderBy(c => ComputeAssignmentCost(chargeSelector(row), precursorMassSelector(row), c))
                .FirstOrDefault();
            return best is null ? [] : [best];
        }

        double[,] costMatrix = new double[rows.Count, candidates.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            for (int j = 0; j < candidates.Count; j++)
            {
                costMatrix[i, j] = ComputeAssignmentCost(chargeSelector(row), precursorMassSelector(row), candidates[j]);
            }
        }

        var assignment = HungarianAlgorithm.FindAssignments(costMatrix);
        var matched = new List<Ms2ScanWithSpecificMass>();
        for (int i = 0; i < assignment.Length; i++)
        {
            int j = assignment[i];
            if (j >= 0 && j < candidates.Count)
                matched.Add(candidates[j]);
        }

        return matched;
    }

    private static double ComputeAssignmentCost(int precursorCharge, double precursorMass, Ms2ScanWithSpecificMass candidate)
    {
        if (double.IsNaN(precursorMass) || precursorMass <= 0)
            return double.MaxValue / 4;

        double cost = 0.0;
        if (precursorCharge != candidate.PrecursorCharge)
            cost += 1000.0;

        cost += Math.Abs(precursorMass - candidate.PrecursorMass);

        if (precursorCharge > 0)
        {
            cost += Math.Abs(precursorMass.ToMz(precursorCharge) - candidate.PrecursorMonoisotopicPeakMz);
        }

        return cost;
    }

    private static double ParseMeanMassDiffPpm(string? massDiffPpm)
    {
        if (string.IsNullOrWhiteSpace(massDiffPpm))
            return double.NaN;

        if (massDiffPpm.Contains('|'))
        {
            var parsed = massDiffPpm.Split('|')
                .Select(s => double.TryParse(s, out var result) ? result : double.NaN)
                .Where(v => !double.IsNaN(v) && !double.IsInfinity(v))
                .ToList();

            return parsed.Count > 0 ? parsed.Average() : double.NaN;
        }

        return double.TryParse(massDiffPpm, out var single) ? single : double.NaN;
    }

    private sealed record BackfillData(
        double[] DeltaScores,
        double[] PrecursorMassErrors,
        double[] PrecursorDeconScores,
        double[] EnvelopePeakCounts,
        double[] FractionalIntensities);
}
