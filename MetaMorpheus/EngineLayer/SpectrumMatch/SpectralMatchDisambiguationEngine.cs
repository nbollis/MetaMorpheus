using Omics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineLayer.SpectrumMatch;

public class SpectralMatchDisambiguationEngine : MetaMorpheusEngine
{
    private readonly double _pepDisambiguaitonThreshold = 0.05;
    private readonly double _qvalueNotchDisambiguationThreshold = 0.05;
    private readonly int _internalIonCountForDisambiguation;
    private readonly List<SpectralMatch> _allSpectralMatches;

    public SpectralMatchDisambiguationEngine(List<SpectralMatch> allPsms, int internalIonDeltaForDisambiguation, CommonParameters commonParameters, List<(string FileName, CommonParameters Parameters)> fileSpecificParameters, List<string> nestedIds) 
        : base(commonParameters, fileSpecificParameters, nestedIds)
    {
        _allSpectralMatches = allPsms;
        _internalIonCountForDisambiguation = internalIonDeltaForDisambiguation;
    }

    // TO think. In the case where PEP is better by 0.06, and the q value is better by 0.4, we would be keeping the one with a marginally better pep but way worse q notch. 
    protected override MetaMorpheusEngineResults RunSpecific()
    {
        int removedPep = DisambiguateByPep();
        int removedQValueNotch = DisambiguateByQValueNotch();
        int removedInternalIonCount = DisambiguateByInternalIonCount();

        return new DisambiguationEngineResults(this)
        {
            RemovedByPEP = removedPep,
            RemovedByQValueNotch = removedQValueNotch,
            RemovedByInternalIonCount = removedInternalIonCount
        };
    }

    private int DisambiguateByPep()
    {
        // TODO: Fix this so we do peptide first, then psm level. 
        int removed = 0;
        foreach (var psm in _allSpectralMatches.Where(p => p.BestMatchingBioPolymersWithSetMods.Count() > 1))
        {
            // Try to use PeptideQValueNotch first for disambiguation
            if (psm.BestMatchingBioPolymersWithSetMods.All(b => b.PeptideQValueNotch.HasValue))
            {
                var bestPeptideQValue = psm.BestMatchingBioPolymersWithSetMods.Min(b => b.PeptideQValueNotch.Value);
                var toRemove = psm.BestMatchingBioPolymersWithSetMods
                    .Where(b => Math.Abs(b.PeptideQValueNotch.Value - bestPeptideQValue) > _pepDisambiguaitonThreshold);

                foreach (var remove in toRemove)
                {
                    psm.RemoveThisAmbiguousPeptide(remove);
                    removed++;
                }
            }
            // Fallback to Score if PeptideQValueNotch is not available for all
            else
            {
                var bestPep = psm.BestMatchingBioPolymersWithSetMods.First().SpecificBioPolymer.FullSequence;
                var toRemove = psm.BestMatchingBioPolymersWithSetMods
                    .Where(b => b.SpecificBioPolymer.FullSequence != bestPep && Math.Abs(b.Score - psm.BestMatchingBioPolymersWithSetMods.First().Score) > _pepDisambiguaitonThreshold);

                foreach (var remove in toRemove)
                {
                    psm.RemoveThisAmbiguousPeptide(remove);
                    removed++;
                }
            }
        }
        return removed;
    }

    private int DisambiguateByQValueNotch()
    {
        int removed = 0;
        foreach (var psm in _allSpectralMatches.Where(p => p.BestMatchingBioPolymersWithSetMods.Count() > 1))
        {
            if (psm.BestMatchingBioPolymersWithSetMods.Any(b => !b.QValueNotch.HasValue))
                continue; // can't disambiguate by q-value if we don't have q-values for all of them. 
            var bestQValue = psm.BestMatchingBioPolymersWithSetMods.Min(b => b.QValueNotch.Value);
            var toRemove = psm.BestMatchingBioPolymersWithSetMods.Where(b => Math.Abs(b.QValueNotch.Value - bestQValue) > _qvalueNotchDisambiguationThreshold);

            foreach (var remove in toRemove)
            {
                psm.RemoveThisAmbiguousPeptide(remove);
                removed++;
            }
        }
        return removed;
    }

    private int DisambiguateByInternalIonCount()
    {
        int removed = 0;
        foreach (var psm in _allSpectralMatches.Where(p => p.BestMatchingBioPolymersWithSetMods.Count() > 1))
        {
            var bestInternalIonCount = psm.BestMatchingBioPolymersWithSetMods
                .Max(b => b.MatchedIons.Count(ion => ion.IsInternalFragment));

            var toRemove = psm.BestMatchingBioPolymersWithSetMods.Where(b => Math.Abs(b.MatchedIons.Count(ion => ion.IsInternalFragment) - bestInternalIonCount) > _internalIonCountForDisambiguation);

            foreach (var remove in toRemove)
            {
                psm.RemoveThisAmbiguousPeptide(remove);
                removed++;
            }
        }
        return removed;
    }
}


public class DisambiguationEngineResults : MetaMorpheusEngineResults
{
    public int RemovedByPEP { get; set; }
    public int RemovedByQValueNotch { get; set; }
    public int RemovedByInternalIonCount { get; set; }

    public DisambiguationEngineResults(SpectralMatchDisambiguationEngine s) : base(s)
    {
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine(base.ToString());
        sb.AppendLine($"Ambiguous {GlobalVariables.AnalyteType.GetUniqueFormLabel()}s removed by PEP: {RemovedByPEP}");
        sb.AppendLine($"Ambiguous {GlobalVariables.AnalyteType.GetUniqueFormLabel()}s removed QValueNotch: {RemovedByQValueNotch}");
        sb.AppendLine($"Ambiguous {GlobalVariables.AnalyteType.GetUniqueFormLabel()}s removed Internal Ion Count: {RemovedByInternalIonCount}");
        return sb.ToString();
    }
}