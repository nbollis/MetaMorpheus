#nullable enable
using System;
using System.Collections.Generic;
using EngineLayer;

namespace TaskLayer.ParallelSearch.Analysis.Analyzers;

/// <summary>
/// Analyzer for organism-specific metrics (bacterial vs human ambiguity)
/// This replaces the AnalyzeSpectralMatches logic currently in ParallelSearchTask
/// </summary>
public class PsmPeptideSearchCollector : IMetricCollector
{
    // PSM Column Names
    public const string PsmTargets = "PsmTargets";
    public const string PsmDecoys = "PsmDecoys";
    public const string PsmBacterialTargets = "PsmBacterialTargets";
    public const string PsmBacterialDecoys = "PsmBacterialDecoys";
    public const string PsmBacterialAmbiguous = "PsmBacterialAmbiguous";
    public const string PsmBacterialUnambiguousTargets = "PsmBacterialUnambiguousTargets";
    public const string PsmBacterialUnambiguousDecoys = "PsmBacterialUnambiguousDecoys";
    public const string PsmBacterialUnambiguousTargetScores = "PsmBacterialUnambiguousTargetScores";
    public const string PsmBacterialUnambiguousDecoyScores = "PsmBacterialUnambiguousDecoyScores";

    // Peptide Column Names
    public const string PeptideTargets = "PeptideTargets";
    public const string PeptideDecoys = "PeptideDecoys";
    public const string PeptideBacterialTargets = "PeptideBacterialTargets";
    public const string PeptideBacterialDecoys = "PeptideBacterialDecoys";
    public const string PeptideBacterialAmbiguous = "PeptideBacterialAmbiguous";
    public const string PeptideBacterialUnambiguousTargets = "PeptideBacterialUnambiguousTargets";
    public const string PeptideBacterialUnambiguousDecoys = "PeptideBacterialUnambiguousDecoys";
    public const string PeptideBacterialUnambiguousTargetScores = "PeptideBacterialUnambiguousTargetScores";
    public const string PeptideBacterialUnambiguousDecoyScores = "PeptideBacterialUnambiguousDecoyScores";

    private readonly string _targetOrganism;

    public PsmPeptideSearchCollector(string targetOrganism = "Homo sapiens")
    {
        _targetOrganism = targetOrganism;
    }

    public string AnalyzerName => "OrganismSpecificity";

    public IEnumerable<string> GetOutputColumns()
    {
        // PSM metrics
        yield return PsmTargets;
        yield return PsmDecoys;
        yield return PsmBacterialTargets;
        yield return PsmBacterialDecoys;
        yield return PsmBacterialAmbiguous;
        yield return PsmBacterialUnambiguousTargets;
        yield return PsmBacterialUnambiguousDecoys;
        yield return PsmBacterialUnambiguousTargetScores;
        yield return PsmBacterialUnambiguousDecoyScores;
        // Peptide metrics
        yield return PeptideTargets;
        yield return PeptideDecoys;
        yield return PeptideBacterialTargets;
        yield return PeptideBacterialDecoys;
        yield return PeptideBacterialAmbiguous;
        yield return PeptideBacterialUnambiguousTargets;
        yield return PeptideBacterialUnambiguousDecoys;
        yield return PeptideBacterialUnambiguousTargetScores;
        yield return PeptideBacterialUnambiguousDecoyScores;
    }

    public bool CanAnalyze(TransientDatabaseContext context)
    {
        return context.AllPsms != null
               && context.TransientPsms != null
               && context.AllPeptides != null
               && context.TransientPeptides != null;
    }

    public Dictionary<string, object> Analyze(TransientDatabaseContext context)
    {
        double qValueThreshold = Math.Min(context.CommonParameters.QValueThreshold, context.CommonParameters.PepQValueThreshold);
        var globalPsmMetrics = AnalyzeSpectralMatches(context.AllPsms, qValueThreshold);
        var globalPeptideMetrics = AnalyzeSpectralMatches(context.AllPeptides, qValueThreshold, isPeptideLevel: true);
        var psmMetrics = AnalyzeSpectralMatches(context.TransientPsms, qValueThreshold);
        var peptideMetrics = AnalyzeSpectralMatches(context.TransientPeptides, qValueThreshold, isPeptideLevel: true);

        return new Dictionary<string, object>
        {
            [PsmTargets] = globalPsmMetrics.Targets,
            [PsmDecoys] = globalPsmMetrics.Decoys,
            [PsmBacterialTargets] = psmMetrics.Targets,
            [PsmBacterialDecoys] = psmMetrics.Decoys,
            [PsmBacterialAmbiguous] = psmMetrics.Targets - psmMetrics.UnambiguousTargets,
            [PsmBacterialUnambiguousTargets] = psmMetrics.UnambiguousTargets,
            [PsmBacterialUnambiguousDecoys] = psmMetrics.UnambiguousDecoys,
            [PsmBacterialUnambiguousTargetScores] = psmMetrics.TargetScores.ToArray(),
            [PsmBacterialUnambiguousDecoyScores] = psmMetrics.DecoyScores.ToArray(),

            [PeptideTargets] = globalPeptideMetrics.Targets,
            [PeptideDecoys] = globalPeptideMetrics.Decoys,
            [PeptideBacterialTargets] = peptideMetrics.Targets,
            [PeptideBacterialDecoys] = peptideMetrics.Decoys,
            [PeptideBacterialAmbiguous] = peptideMetrics.Targets - peptideMetrics.UnambiguousTargets,
            [PeptideBacterialUnambiguousTargets] = peptideMetrics.UnambiguousTargets,
            [PeptideBacterialUnambiguousDecoys] = peptideMetrics.UnambiguousDecoys,
            [PeptideBacterialUnambiguousTargetScores] = peptideMetrics.TargetScores.ToArray(),
            [PeptideBacterialUnambiguousDecoyScores] = peptideMetrics.DecoyScores.ToArray(),
        };
    }

    private (int Targets, int Decoys, int UnambiguousTargets, int UnambiguousDecoys,
        List<double> TargetScores, List<double> DecoyScores)
        AnalyzeSpectralMatches(List<SpectralMatch> spectralMatches, double qValueThreshold, bool isPeptideLevel = false)
    {
        int targets = 0, decoys = 0, unambiguousTargets = 0, unambiguousDecoys = 0;
        List<double> targetScores = [];
        List<double> decoyScores = [];

        foreach (var psm in spectralMatches)
        {
            double qValue = isPeptideLevel
                ? psm.PeptideFdrInfo?.QValue ?? 1.0
                : psm.GetFdrInfo(false)?.QValue ?? 1.0;

            if (qValue > qValueThreshold) continue;

            bool isOrganismAmbiguous = false;
            foreach (var match in psm.BestMatchingBioPolymersWithSetMods)
            {
                string? organism = match.SpecificBioPolymer.Parent.Organism;
                if (organism != null && organism.Contains(_targetOrganism, StringComparison.Ordinal))
                {
                    isOrganismAmbiguous = true;
                    break;
                }
            }

            if (psm.IsDecoy)
            {
                decoys++;
                if (!isOrganismAmbiguous)
                {
                    unambiguousDecoys++;
                    decoyScores.Add(psm.Score);
                }
            }
            else
            {
                targets++;
                if (!isOrganismAmbiguous)
                {
                    unambiguousTargets++;
                    targetScores.Add(psm.Score);
                }
            }
        }

        return (targets, decoys, unambiguousTargets, unambiguousDecoys, targetScores, decoyScores);
    }
}