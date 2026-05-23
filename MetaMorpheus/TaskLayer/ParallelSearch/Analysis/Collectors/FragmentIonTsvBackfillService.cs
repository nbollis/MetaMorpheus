#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Readers;

namespace TaskLayer.ParallelSearch.Analysis.Collectors;

public sealed class FragmentIonTsvBackfillService
{
    public bool BackfillIfNeeded(string outputFolder, List<TransientDatabaseMetrics> metricsList, double qValueCutoff)
    {
        bool backfilledAny = false;
        if (string.IsNullOrEmpty(outputFolder) || metricsList == null || metricsList.Count == 0)
            return backfilledAny;

        foreach (var metric in metricsList)
        {
            if (metric.Psm_FragmentPPMErrors.Length > 0)
                continue;

            var dbDir = Path.Combine(outputFolder, metric.DatabaseName);
            if (!Directory.Exists(dbDir))
                continue;

            var psmPath = Path.Combine(dbDir, $"{metric.DatabaseName}_AllPsms.psmtsv");
            var peptidePath = Path.Combine(dbDir, $"{metric.DatabaseName}_AllPeptides.psmtsv");

            try
            {
                var parsingParams = new SpectrumMatchParsingParameters
                {
                    ParseMatchedFragmentIons = true,
                    FragmentIonsHavePlaceholderForEnvelope = true
                };

                var psmList = SpectrumMatchTsvReader.ReadTsv(psmPath, out var _, parsingParams).Where(p => p.QValue <= qValueCutoff).ToList();
                metric.Psm_FragmentPPMErrors = psmList
                    .Where(p => p.MatchedIons != null)
                    .SelectMany(p => p.MatchedIons)
                    .Select(m => m.MassErrorPpm)
                    .Where(v => !double.IsNaN(v) && !double.IsInfinity(v))
                    .ToArray();
                metric.PsmBacterialTargetDeltaScores = psmList
                    .Where(p => p.DeltaScore.HasValue && p.DeltaScore.Value > 0)
                    .Select(p => p.DeltaScore.Value)
                    .ToArray();
                metric.PsmPrecursorMassErrors = psmList.Select(p =>
                {
                    if (p.MassDiffPpm.Contains('|'))
                        return p.MassDiffPpm.Split('|').Select(s =>
                        {
                            if (double.TryParse(s, out double result))
                                return result;
                            return double.NaN;
                        }).Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).Average();
                    else if (double.TryParse(p.MassDiffPpm, out double result))
                        return result;
                    else
                        return double.NaN;
                }).Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToArray();

                var peptideList = SpectrumMatchTsvReader.ReadTsv(peptidePath, out var _, parsingParams).Where(p => p.QValue <= qValueCutoff).ToList(); 
                metric.Peptide_FragmentPPMErrors = peptideList
                    .Where(p => p.MatchedIons != null)
                    .SelectMany(p => p.MatchedIons)
                    .Select(m => m.MassErrorPpm)
                    .Where(v => !double.IsNaN(v) && !double.IsInfinity(v))
                    .ToArray();
                metric.PeptideBacterialTargetDeltaScores = peptideList
                    .Where(p => p.DeltaScore.HasValue && p.DeltaScore.Value > 0)
                    .Select(p => p.DeltaScore.Value)
                    .ToArray();
                metric.PeptidePrecursorMassErrors = peptideList.Select(p =>
                {
                    if (p.MassDiffPpm.Contains('|'))
                        return p.MassDiffPpm.Split('|').Select(s =>
                        {
                            if (double.TryParse(s, out double result))
                                return result;
                            return double.NaN;
                        }).Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).Average();
                    else if (double.TryParse(p.MassDiffPpm, out double result))
                        return result;
                    else
                        return double.NaN;
                }).Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToArray();

                metric.PopulateResultsFromProperties();
                backfilledAny = true;
            }
            catch
            {
                // leave at defaults if files don't exist or parsing fails
            }
        }
        return backfilledAny;
    }
}
