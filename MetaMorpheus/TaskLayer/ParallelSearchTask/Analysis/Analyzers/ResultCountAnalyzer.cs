#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace TaskLayer.ParallelSearchTask.Analysis.Analyzers;

/// <summary>
/// Analyzer for basic search metrics (PSM counts, protein counts, etc.)
/// This replaces the hardcoded metrics currently in TransientDatabaseSearchResults
/// </summary>
public class ResultCountAnalyzer : ITransientDatabaseAnalyzer
{
    public string AnalyzerName => "ResultCount";

    public IEnumerable<string> GetOutputColumns()
    {
        yield return "TotalProteins";
        yield return "TransientProteinCount";
        yield return "TransientPeptideCount";
        yield return "TargetPsmsAtQValueThreshold";
        yield return "TargetPsmsFromTransientDb";
        yield return "TargetPsmsFromTransientDbAtQValueThreshold";
        yield return "TargetPeptidesAtQValueThreshold";
        yield return "TargetPeptidesFromTransientDb";
        yield return "TargetPeptidesFromTransientDbAtQValueThreshold";
    }

    public bool CanAnalyze(TransientDatabaseAnalysisContext context)
    {
        return context.AllPsms != null
            && context.TransientPsms != null
            && context.AllPeptides != null
            && context.TransientPeptides != null;
    }

    public Dictionary<string, object> Analyze(TransientDatabaseAnalysisContext context)
    {
        double qValueThreshold = context.CommonParameters.QValueThreshold;

        return new Dictionary<string, object>
        {
            ["TotalProteins"] = context.TotalProteins,
            ["TransientProteinCount"] = context.TransientProteinAccessions.Count,
            ["TransientPeptideCount"] = context.TransientPeptideCount,

            ["TargetPsmsAtQValueThreshold"] = context.AllPsms.Count(p => !p.IsDecoy && p.GetFdrInfo(false)?.QValue <= qValueThreshold),
            ["TargetPsmsFromTransientDb"] = context.TransientPsms.Count(p => !p.IsDecoy),
            ["TargetPsmsFromTransientDbAtQValueThreshold"] = context.TransientPsms.Count(p => !p.IsDecoy && p.GetFdrInfo(false)?.QValue <= qValueThreshold),

            ["TargetPeptidesAtQValueThreshold"] = context.AllPeptides.Count(p => !p.IsDecoy && p.PeptideFdrInfo?.QValue <= qValueThreshold),
            ["TargetPeptidesFromTransientDb"] = context.TransientPeptides.Count(p => !p.IsDecoy),
            ["TargetPeptidesFromTransientDbAtQValueThreshold"] = context.TransientPeptides.Count(p => !p.IsDecoy && p.PeptideFdrInfo?.QValue <= qValueThreshold)
        };
    }
}