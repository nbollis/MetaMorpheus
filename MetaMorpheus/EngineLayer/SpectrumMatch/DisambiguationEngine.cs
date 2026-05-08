using EngineLayer.FdrAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;

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
    /// <returns>Count of hypotheses removed by disambiguation</returns>
    private int DisambiguateByUnmodifiedFullSequenceAndParentModificationState()
    {
        int removed = 0;

        var groupedHypotheses = new Dictionary<string, List<SpectralMatchHypothesis>>();
        var toRemove = new List<SpectralMatchHypothesis>();

        foreach (var psm in _allSpectralMatches)
        {
            if (psm.BestMatchingBioPolymersWithSetMods.Count() <= 1)
                continue;

            groupedHypotheses.Clear();

            foreach (var hypothesis in psm.BestMatchingBioPolymersWithSetMods)
            {
                string fullSequence = hypothesis.SpecificBioPolymer.FullSequence;
                if (!groupedHypotheses.TryGetValue(fullSequence, out var hypothesesForSequence))
                {
                    hypothesesForSequence = new List<SpectralMatchHypothesis>();
                    groupedHypotheses.Add(fullSequence, hypothesesForSequence);
                }

                hypothesesForSequence.Add(hypothesis);
            }

            toRemove.Clear();

            foreach (var hypothesesForSequence in groupedHypotheses.Values)
            {
                // The full sequence has multiple hypotheses
                if (hypothesesForSequence.Count < 2)
                    continue;

                bool hasUnmodifiedParent = false;
                bool hasModifiedParent = false;

                foreach (var hypothesis in hypothesesForSequence)
                {
                    // We only consider unmodified sequences in this disambiguation
                    // TODO: Consider how to generalize this to a proper mod/proteoform parsimony. 
                    if (hypothesis.SpecificBioPolymer.FullSequence != hypothesis.SpecificBioPolymer.BaseSequence)
                        continue;

                    if (hypothesis.SpecificBioPolymer.Parent.OneBasedPossibleLocalizedModifications.Any())
                        hasModifiedParent = true;
                    else
                        hasUnmodifiedParent = true;
                }

                // We only prefer the unmodified parent when both parent states explain the same unmodified child.
                // Broader "prefer unmodified" behavior would erase legitimate ambiguity that this rule is not meant to solve.
                if (!hasUnmodifiedParent || !hasModifiedParent)
                    continue;

                foreach (var hypothesis in hypothesesForSequence)
                {
                    if (hypothesis.SpecificBioPolymer.Parent.OneBasedPossibleLocalizedModifications.Any())
                        toRemove.Add(hypothesis);
                }
            }

            foreach (var hypothesis in toRemove)
            {
                psm.RemoveThisAmbiguousPeptide(hypothesis);
                removed++;
            }
        }

        return removed;
    }
}
