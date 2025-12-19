using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using MathNet.Numerics.Statistics;

namespace TaskLayer.ParallelSearchTask.Analysis.Analyzers;
public class FragmentIonAnalyzer : ITransientDatabaseAnalyzer
{
    public string AnalyzerName => "FragmentIons";
    public IEnumerable<string> GetOutputColumns()
    {
        yield return "PSM_LongestIonSeriesBidirectionalTargets";
        yield return "PSM_ComplementaryIonCountTargets";
        yield return "PSM_SequenceCoverageFractionTargets";
        yield return "PSM_LongestIonSeriesBidirectionalDecoys";
        yield return "PSM_ComplementaryIonCountDecoys";
        yield return "PSM_SequenceCoverageFractionDecoys";
        yield return "PSM_LongestIonSeriesBidirectional_AllTargets";
        yield return "PSM_ComplementaryIonCount_AllTargets";
        yield return "PSM_SequenceCoverageFraction_AllTargets";
        yield return "PSM_LongestIonSeriesBidirectional_AllDecoys";
        yield return "PSM_ComplementaryIonCount_AllDecoys";
        yield return "PSM_SequenceCoverageFraction_AllDecoys";

        yield return "Peptide_LongestIonSeriesBidirectionalTargets";
        yield return "Peptide_ComplementaryIonCountTargets";
        yield return "Peptide_SequenceCoverageFractionTargets";
        yield return "Peptide_LongestIonSeriesBidirectionalDecoys";
        yield return "Peptide_ComplementaryIonCountDecoys";
        yield return "Peptide_SequenceCoverageFractionDecoys";
        yield return "Peptide_LongestIonSeriesBidirectional_AllTargets";
        yield return "Peptide_ComplementaryIonCount_AllTargets";
        yield return "Peptide_SequenceCoverageFraction_AllTargets";
        yield return "Peptide_LongestIonSeriesBidirectional_AllDecoys";
        yield return "Peptide_ComplementaryIonCount_AllDecoys";
        yield return "Peptide_SequenceCoverageFraction_AllDecoys";
    }

    public Dictionary<string, object> Analyze(TransientDatabaseAnalysisContext context)
    {
        // Psms
        var confidentPsms = context.TransientPsms
            .Where(p => p.FdrInfo.QValue <= ITransientDatabaseAnalyzer.QCutoff)
            .ToList();

        foreach (var psm in confidentPsms)
            psm.GetAminoAcidCoverage();

        var psmBidirectional = confidentPsms
            .Select(p => (p.IsDecoy,
                (double)SpectralMatch.GetLongestIonSeriesBidirectional(p.BestMatchingBioPolymersWithSetMods.First())))
            .ToList();

        var psmComplementaryCounts = confidentPsms
            .Select(p => (p.IsDecoy,
                (double)SpectralMatch.GetCountComplementaryIons(p.BestMatchingBioPolymersWithSetMods.First())))
            .ToList();

        var psmSequenceCoverage = confidentPsms
            .Select(p => (p.IsDecoy,
                p.FragmentCoveragePositionInPeptide.Count(p => p != -1) / (double)p.BaseSequence.Length))
            .ToList();

        // peptides
        var confidentPeptides = context.TransientPeptides
            .Where(p => p.FdrInfo.QValue <= ITransientDatabaseAnalyzer.QCutoff)
            .ToList();

        foreach (var psm in confidentPeptides)
            psm.GetAminoAcidCoverage();

        var peptidesBidirectional = confidentPeptides
            .Select(p => (p.IsDecoy,
                (double)SpectralMatch.GetLongestIonSeriesBidirectional(p.BestMatchingBioPolymersWithSetMods.First())))
            .ToList();

        var peptidesComplementaryCounts = confidentPeptides
            .Select(p => (p.IsDecoy,
                (double)SpectralMatch.GetCountComplementaryIons(p.BestMatchingBioPolymersWithSetMods.First())))
            .ToList();

        var peptidesSequenceCoverage = confidentPeptides
            .Select(p => (p.IsDecoy,
                p.FragmentCoveragePositionInPeptide.Count(p => p != -1) / (double)p.BaseSequence.Length))
            .ToList();

        return new Dictionary<string, object>
        {
            ["PSM_LongestIonSeriesBidirectionalTargets"] = psmBidirectional.Where(p => !p.Item1).Select(p => p.Item2).Median(),
            ["PSM_ComplementaryIonCountTargets"] = psmComplementaryCounts.Where(p => !p.Item1).Select(p => p.Item2).Median(),
            ["PSM_SequenceCoverageFractionTargets"] = psmSequenceCoverage.Where(p => !p.Item1).Select(p => p.Item2).Median(),
            ["PSM_LongestIonSeriesBidirectionalDecoys"] = psmBidirectional.Where(p => p.Item1).Select(p => p.Item2).Median(),
            ["PSM_ComplementaryIonCountDecoys"] = psmComplementaryCounts.Where(p => p.Item1).Select(p => p.Item2).Median(),
            ["PSM_SequenceCoverageFractionDecoys"] = psmSequenceCoverage.Where(p => p.Item1).Select(p => p.Item2).Median(),
            ["PSM_LongestIonSeriesBidirectional_AllTargets"] = psmBidirectional.Where(p => !p.Item1).Select(p => p.Item2).ToArray(),
            ["PSM_ComplementaryIonCount_AllTargets"] = psmComplementaryCounts.Where(p => !p.Item1).Select(p => p.Item2).ToArray(),
            ["PSM_SequenceCoverageFraction_AllTargets"] = psmSequenceCoverage.Where(p => !p.Item1).Select(p => p.Item2).ToArray(),
            ["PSM_LongestIonSeriesBidirectional_AllDecoys"] = psmBidirectional.Where(p => p.Item1).Select(p => p.Item2).ToArray(),
            ["PSM_ComplementaryIonCount_AllDecoys"] = psmComplementaryCounts.Where(p => p.Item1).Select(p => p.Item2).ToArray(),
            ["PSM_SequenceCoverageFraction_AllDecoys"] = psmSequenceCoverage.Where(p => p.Item1).Select(p => p.Item2).ToArray(),

            ["Peptide_LongestIonSeriesBidirectionalTargets"] = peptidesBidirectional.Where(p => !p.Item1).Select(p => p.Item2).Median(),
            ["Peptide_ComplementaryIonCountTargets"] = peptidesComplementaryCounts.Where(p => !p.Item1).Select(p => p.Item2).Median(),
            ["Peptide_SequenceCoverageFractionTargets"] = peptidesSequenceCoverage.Where(p => !p.Item1).Select(p => p.Item2).Median(),
            ["Peptide_LongestIonSeriesBidirectionalDecoys"] = peptidesBidirectional.Where(p => p.Item1).Select(p => p.Item2).Median(),
            ["Peptide_ComplementaryIonCountDecoys"] = peptidesComplementaryCounts.Where(p => p.Item1).Select(p => p.Item2).Median(),
            ["Peptide_SequenceCoverageFractionDecoys"] = peptidesSequenceCoverage.Where(p => p.Item1).Select(p => p.Item2).Median(),
            ["Peptide_LongestIonSeriesBidirectional_AllTargets"] = peptidesBidirectional.Where(p => !p.Item1).Select(p => p.Item2).ToArray(),
            ["Peptide_ComplementaryIonCount_AllTargets"] = peptidesComplementaryCounts.Where(p => !p.Item1).Select(p => p.Item2).ToArray(),
            ["Peptide_SequenceCoverageFraction_AllTargets"] = peptidesSequenceCoverage.Where(p => !p.Item1).Select(p => p.Item2).ToArray(),
            ["Peptide_LongestIonSeriesBidirectional_AllDecoys"] = peptidesBidirectional.Where(p => p.Item1).Select(p => p.Item2).ToArray(),
            ["Peptide_ComplementaryIonCount_AllDecoys"] = peptidesComplementaryCounts.Where(p => p.Item1).Select(p => p.Item2).ToArray(),
            ["Peptide_SequenceCoverageFraction_AllDecoys"] = peptidesSequenceCoverage.Where(p => p.Item1).Select(p => p.Item2).ToArray(),
        };
    }

    public bool CanAnalyze(TransientDatabaseAnalysisContext context)
    {
        return context.TransientPeptides != null && context.TransientPsms != null;
    }
}
