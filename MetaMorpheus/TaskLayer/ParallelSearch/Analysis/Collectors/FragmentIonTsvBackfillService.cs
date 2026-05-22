#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Readers;

namespace TaskLayer.ParallelSearch.Analysis.Collectors;

public sealed class FragmentIonTsvBackfillService
{
    public bool BackfillIfNeeded(string outputFolder, List<TransientDatabaseMetrics> metricsList)
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

                var psmList = SpectrumMatchTsvReader.ReadTsv(psmPath, out var _, parsingParams);
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

                var peptideList = SpectrumMatchTsvReader.ReadTsv(peptidePath, out var _, parsingParams);
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
