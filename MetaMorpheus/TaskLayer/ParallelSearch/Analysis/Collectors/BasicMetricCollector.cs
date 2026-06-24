#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskLayer.ParallelSearch.Analysis.Collectors;

/// <summary>
/// Analyzer for basic search metrics (PSM counts, protein counts, etc.)
/// This replaces the hardcoded metrics currently in TransientDatabaseSearchResults
/// </summary>
public class BasicMetricCollector : IMetricCollector
{
    // Column Names
    public const string TotalProteins = "TotalProteins";
    public const string TransientProteinCount = "TransientProteinCount";
    public const string TransientPeptideCount = "TransientPeptideCount";
    public const string TargetPsmsAtQValueThreshold = "TargetPsmsAtQValueThreshold";
    public const string TargetPsmsFromTransientDb = "TargetPsmsFromTransientDb";
    public const string TargetPsmsFromTransientDbAtQValueThreshold = "TargetPsmsFromTransientDbAtQValueThreshold";
    public const string TargetPeptidesAtQValueThreshold = "TargetPeptidesAtQValueThreshold";
    public const string TargetPeptidesFromTransientDb = "TargetPeptidesFromTransientDb";
    public const string TargetPeptidesFromTransientDbAtQValueThreshold = "TargetPeptidesFromTransientDbAtQValueThreshold";

    // PEP-based confident counts, reported at BOTH 1% and 5% PEP_QValue. PEP_QValue is the PEP-ranked q-value
    // assigned by mapping each match's model PEP onto the background curve (see PepAnalysisEngine); it is a
    // different confidence axis from the score-based QValue above, so both are reported side by side.
    public const string TargetPsmsFromTransientDbAtPepQ01 = "TargetPsmsFromTransientDbAtPepQ01";
    public const string TargetPsmsFromTransientDbAtPepQ05 = "TargetPsmsFromTransientDbAtPepQ05";
    public const string TargetPeptidesFromTransientDbAtPepQ01 = "TargetPeptidesFromTransientDbAtPepQ01";
    public const string TargetPeptidesFromTransientDbAtPepQ05 = "TargetPeptidesFromTransientDbAtPepQ05";

    public string CollectorName => "ResultCount";

    public IEnumerable<string> GetOutputColumns()
    {
        yield return TotalProteins;
        yield return TransientProteinCount;
        yield return TransientPeptideCount;
        yield return TargetPsmsAtQValueThreshold;
        yield return TargetPsmsFromTransientDb;
        yield return TargetPsmsFromTransientDbAtQValueThreshold;
        yield return TargetPeptidesAtQValueThreshold;
        yield return TargetPeptidesFromTransientDb;
        yield return TargetPeptidesFromTransientDbAtQValueThreshold;
        yield return TargetPsmsFromTransientDbAtPepQ01;
        yield return TargetPsmsFromTransientDbAtPepQ05;
        yield return TargetPeptidesFromTransientDbAtPepQ01;
        yield return TargetPeptidesFromTransientDbAtPepQ05;
    }

    public bool CanCollectData(TransientDatabaseContext context)
    {
        return context.AllPsms != null
            && context.TransientPsms != null
            && context.AllPeptides != null
            && context.TransientPeptides != null;
    }

    public Dictionary<string, object> CollectData(TransientDatabaseContext context)
    {
        double qValueThreshold = Math.Min(context.CommonParameters.QValueThreshold, context.CommonParameters.PepQValueThreshold);

        return new Dictionary<string, object>
        {
            [TotalProteins] = context.TotalProteins,
            [TransientProteinCount] = context.TransientProteinAccessions.Count,
            [TransientPeptideCount] = context.TransientPeptideCount,

            [TargetPsmsAtQValueThreshold] = context.AllPsms.Count(p => !p.IsDecoy && p.GetFdrInfo(false)?.QValue <= qValueThreshold),
            [TargetPsmsFromTransientDb] = context.TransientPsms.Count(p => !p.IsDecoy),
            [TargetPsmsFromTransientDbAtQValueThreshold] = context.TransientPsms.Count(p => !p.IsDecoy && p.GetFdrInfo(false)?.QValue <= qValueThreshold),

            [TargetPeptidesAtQValueThreshold] = context.AllPeptides.Count(p => !p.IsDecoy && p.PeptideFdrInfo?.QValue <= qValueThreshold),
            [TargetPeptidesFromTransientDb] = context.TransientPeptides.Count(p => !p.IsDecoy),
            [TargetPeptidesFromTransientDbAtQValueThreshold] = context.TransientPeptides.Count(p => !p.IsDecoy && p.PeptideFdrInfo?.QValue <= qValueThreshold),

            // PEP-based confident counts at 1% and 5%. PEP_QValue is only meaningful once a PEP model has been
            // trained (transient matches default to the sentinel 2 otherwise), so these read 0 when PEP is off.
            [TargetPsmsFromTransientDbAtPepQ01] = context.TransientPsms.Count(p => !p.IsDecoy && p.GetFdrInfo(false)?.PEP_QValue < 0.01),
            [TargetPsmsFromTransientDbAtPepQ05] = context.TransientPsms.Count(p => !p.IsDecoy && p.GetFdrInfo(false)?.PEP_QValue < 0.05),
            [TargetPeptidesFromTransientDbAtPepQ01] = context.TransientPeptides.Count(p => !p.IsDecoy && p.PeptideFdrInfo?.PEP_QValue < 0.01),
            [TargetPeptidesFromTransientDbAtPepQ05] = context.TransientPeptides.Count(p => !p.IsDecoy && p.PeptideFdrInfo?.PEP_QValue < 0.05)
        };
    }
}