using EngineLayer.FdrAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EngineLayer.SpectrumMatch;

/// <summary>
/// Engine designed to disambiguate spectral matches. 
/// <remarks>
/// This is currently done in several locations and should be consolidated to this engine. 
/// Places in which disambiguation occurs: 
/// SearchTask -> Internal Ions
/// PEPAnalysisEngine -> By PEP
/// ProteinParsimonyEngine -> remove non-parsimonious peptides. 
/// </remarks>
/// </summary>
public class DisambiguationEngine : MetaMorpheusEngine
{
    private readonly double _qvalueNotchDisambiguationThreshold = 0.05;
    private readonly List<SpectralMatch> _allSpectralMatches;

    public DisambiguationEngine(List<SpectralMatch> allPsms, CommonParameters commonParameters, List<(string FileName, CommonParameters Parameters)> fileSpecificParameters, List<string> nestedIds)
        : base(commonParameters, fileSpecificParameters, nestedIds)
    {
        _allSpectralMatches = allPsms;
    }

    protected override MetaMorpheusEngineResults RunSpecific()
    {
        Status("Running Disambiguation Engine...");

        // Remove ambiguous PSMs by various methods.
        int removedQValueNotch = DisambiguateByQValueNotch();
        int removedByParentModificationPreference = DisambiguateByUnmodifiedFullSequenceAndParentModificationState();

        if (removedQValueNotch > 0 || removedByParentModificationPreference > 0)
        {        
            // Resolve all remaining ambiguities
            foreach (var psm in _allSpectralMatches)
                psm.ResolveAllAmbiguities();

            // Recalculate Q-Values 
            FdrAnalysisEngine.DoFalseDiscoveryRateAnalysis(_allSpectralMatches, false, FileSpecificParameters, null, null, CommonParameters);
        }

        Status("Done.");
        return new DisambiguationEngineResults(this)
        {
            RemovedByQValueNotch = removedQValueNotch,
            RemovedByParentModificationPreference = removedByParentModificationPreference,
        };
    }

    private int DisambiguateByQValueNotch()
    {
        int removed = 0;
        foreach (var psm in _allSpectralMatches.Where(p => p.Notch == null && p.BestMatchingBioPolymersWithSetMods.Count() > 1))
        {
            if (psm.BestMatchingBioPolymersWithSetMods.Any(b => !b.QValueNotch.HasValue))
                continue; // can't disambiguate by q-value if we don't have q-values for all of them. 
            var bestQValue = psm.BestMatchingBioPolymersWithSetMods.Min(b => b.QValueNotch!.Value);
            var toRemove = psm.BestMatchingBioPolymersWithSetMods.Where(b => Math.Abs(b.QValueNotch!.Value - bestQValue) > _qvalueNotchDisambiguationThreshold);

            foreach (var remove in toRemove)
            {
                psm.RemoveThisAmbiguousPeptide(remove);
                removed++;
            }
        }
        return removed;
    }


    /// <summary>
    /// A modification parsimonious disambiguation method: if we have multiple matches with the same unmodified full sequence AND one of those matches has an unmodified parent while the others have modified parents, we will prefer the unmodified parent(s). 
    /// </summary>
    /// <returns></returns>
    private int DisambiguateByUnmodifiedFullSequenceAndParentModificationState()
    {
        int removed = 0;

        foreach (var psm in _allSpectralMatches.Where(p => p.BestMatchingBioPolymersWithSetMods.Count() > 1))
        {
            var toRemove = psm.BestMatchingBioPolymersWithSetMods
                .ToList()
                // Key on the realized child form so this only breaks ties between the same matched sequence,
                // not between different localizations or different modified children that happen to share a base sequence.
                .GroupBy(h => h.SpecificBioPolymer.FullSequence)
                .Where(g => g.Count() > 1 && g.All(h => h.SpecificBioPolymer.FullSequence == h.SpecificBioPolymer.BaseSequence))
                .SelectMany(g =>
                {
                    bool hasUnmodifiedParent = g.Any(h => !h.SpecificBioPolymer.Parent.OneBasedPossibleLocalizedModifications.Any());
                    bool hasModifiedParent = g.Any(h => h.SpecificBioPolymer.Parent.OneBasedPossibleLocalizedModifications.Any());

                    // We only prefer the unmodified parent when both parent states explain the same unmodified child.
                    // Broader "prefer unmodified" behavior would erase legitimate ambiguity that this rule is not meant to solve.
                    return hasUnmodifiedParent && hasModifiedParent
                        ? g.Where(h => h.SpecificBioPolymer.Parent.OneBasedPossibleLocalizedModifications.Any())
                        : [];
                })
                .ToList();

            foreach (var hypothesis in toRemove)
            {
                psm.RemoveThisAmbiguousPeptide(hypothesis);
                removed++;
            }
        }

        return removed;
    }
}

public class DisambiguationEngineResults : MetaMorpheusEngineResults
{
    //public int RemovedByPEP { get; set; }
    public int RemovedByQValueNotch { get; set; }
    public int RemovedByParentModificationPreference { get; set; }
    //public int RemovedByInternalIonCount { get; set; }

    public DisambiguationEngineResults(DisambiguationEngine s) : base(s)
    {
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine(base.ToString());
        //sb.AppendLine($"Ambiguous {GlobalVariables.AnalyteType.GetUniqueFormLabel()}s removed by PEP: {RemovedByPEP}");
        sb.AppendLine($"Ambiguous {GlobalVariables.AnalyteType.GetUniqueFormLabel()}s removed QValueNotch: {RemovedByQValueNotch}");
        sb.AppendLine($"Ambiguous {GlobalVariables.AnalyteType.GetUniqueFormLabel()}s removed by parent modification preference: {RemovedByParentModificationPreference}");
        //sb.AppendLine($"Ambiguous {GlobalVariables.AnalyteType.GetUniqueFormLabel()}s removed Internal Ion Count: {RemovedByInternalIonCount}");
        return sb.ToString();
    }
}
