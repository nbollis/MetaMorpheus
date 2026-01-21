#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using EngineLayer;

namespace TaskLayer.ParallelSearch.Analysis.Collectors;

/// <summary>
/// Analyzer for protein group metrics
/// Only runs if parsimony was performed
/// </summary>
public class ProteinGroupCollector : IMetricCollector
{
    // Output Column Names
    public const string TargetProteinGroupsAtQValueThreshold = "TargetProteinGroupsAtQValueThreshold";
    public const string TargetProteinGroupsFromTransientDb = "TargetProteinGroupsFromTransientDb";
    public const string TargetProteinGroupsFromTransientDbAtQValueThreshold = "TargetProteinGroupsFromTransientDbAtQValueThreshold";
    public const string ProteinGroupBacterialTargets = "ProteinGroupBacterialTargets";
    public const string ProteinGroupBacterialDecoys = "ProteinGroupBacterialDecoys";
    public const string ProteinGroupBacterialUnambiguousTargets = "ProteinGroupBacterialUnambiguousTargets";
    public const string ProteinGroupBacterialUnambiguousDecoys = "ProteinGroupBacterialUnambiguousDecoys";
    public const string ProteinGroupTargets = "ProteinGroupTargets";
    public const string ProteinGroupDecoys = "ProteinGroupDecoys";

    private readonly string _targetOrganism;

    public ProteinGroupCollector(string targetOrganism = "Homo sapiens")
    {
        _targetOrganism = targetOrganism;
    }

    public string AnalyzerName => "ProteinGroups";

    public IEnumerable<string> GetOutputColumns()
    {
        yield return ProteinGroupTargets;
        yield return ProteinGroupDecoys;
        yield return TargetProteinGroupsAtQValueThreshold;
        yield return TargetProteinGroupsFromTransientDb;
        yield return TargetProteinGroupsFromTransientDbAtQValueThreshold;
        yield return ProteinGroupBacterialTargets;
        yield return ProteinGroupBacterialDecoys;
        yield return ProteinGroupBacterialUnambiguousTargets;
        yield return ProteinGroupBacterialUnambiguousDecoys;
    }

    public bool CanCollectData(TransientDatabaseContext context)
    {
        return context.ProteinGroups != null && context.TransientProteinGroups != null;
    }

    public Dictionary<string, object> CollectData(TransientDatabaseContext context)
    {
        double qValueThreshold = Math.Min(context.CommonParameters.QValueThreshold, context.CommonParameters.PepQValueThreshold);
        var totalTargets = context.ProteinGroups!.Count(p => !p.IsDecoy);
        var totalDecoys = context.ProteinGroups!.Count(p => p.IsDecoy);
        var totalTransientTargets = context.TransientProteinGroups!.Count(p => !p.IsDecoy);


        var (targetsGlobal, decoysGlobal, unambiguousTargetsGlobal, unambiguousDecoysGlobal) =
            AnalyzeProteinGroups(context.ProteinGroups!, qValueThreshold);

        var (bacterialTargets, bacterialDecoys, unambiguousTargets, unambiguousDecoys) =
            AnalyzeProteinGroups(context.TransientProteinGroups!, qValueThreshold);

        return new Dictionary<string, object>
        {
            [ProteinGroupTargets] = targetsGlobal,
            [ProteinGroupDecoys] = decoysGlobal,
            [TargetProteinGroupsAtQValueThreshold] = targetsGlobal,
            [TargetProteinGroupsFromTransientDb] = totalTransientTargets,
            [TargetProteinGroupsFromTransientDbAtQValueThreshold] = bacterialTargets,
            [ProteinGroupBacterialTargets] = bacterialTargets,
            [ProteinGroupBacterialDecoys] = bacterialDecoys,
            [ProteinGroupBacterialUnambiguousTargets] = unambiguousTargets,
            [ProteinGroupBacterialUnambiguousDecoys] = unambiguousDecoys
        };
    }

    private (int Targets, int Decoys, int UnambiguousTargets, int UnambiguousDecoys)
        AnalyzeProteinGroups(List<ProteinGroup> proteinGroups, double qValueThreshold)
    {
        int targets = 0, decoys = 0, unambiguousTargets = 0, unambiguousDecoys = 0;

        foreach (var pg in proteinGroups)
        {
            if (pg.QValue > qValueThreshold || pg.AllPeptides.Count < 2)
                continue;

            bool isOrganismAmbiguous = false;
            foreach (var protein in pg.Proteins)
            {
                string? organism = protein.Organism;
                if (organism != null && organism.Contains(_targetOrganism, StringComparison.Ordinal))
                {
                    isOrganismAmbiguous = true;
                    break;
                }
            }

            if (pg.IsDecoy)
            {
                decoys++;
                if (!isOrganismAmbiguous)
                    unambiguousDecoys++;
            }
            else
            {
                targets++;
                if (!isOrganismAmbiguous)
                    unambiguousTargets++;
            }
        }

        return (targets, decoys, unambiguousTargets, unambiguousDecoys);
    }
}