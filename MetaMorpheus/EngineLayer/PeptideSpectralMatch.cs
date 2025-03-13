using System.Collections.Generic;
using System.Linq;
using EngineLayer.SpectrumMatch;
using Omics;
using Omics.Fragmentation;
using Omics.Modifications;

namespace EngineLayer
{
    public class PeptideSpectralMatch : SpectralMatch
    {
        public PeptideSpectralMatch(IBioPolymerWithSetMods peptide, int notch, double score, int scanIndex,
            Ms2ScanWithSpecificMass scan, CommonParameters commonParameters,
            List<MatchedFragmentIon> matchedFragmentIons, SearchLogType logType = SearchLogType.TopScoringOnly) 
            : base(peptide, notch, score, scanIndex, scan, commonParameters, matchedFragmentIons, logType)
        {

        }

        #region Silac
            
        /// <summary>
        /// This method changes the base and full sequences to reflect heavy silac labels
        /// translates SILAC sequence into the proper peptide sequence ("PEPTIDEa" into "PEPTIDEK(+8.014)")
        /// </summary>
        public void ResolveHeavySilacLabel(List<SilacLabel> labels, IReadOnlyDictionary<string, int> modsToWritePruned)
        {
            var bestMatches = SearchLog.GetTopScoringAttemptsWithSequenceInformation().ToList();

            //FullSequence
            FullSequence = PsmTsvWriter.Resolve(bestMatches.Select(b => b.SpecificBioPolymer.FullSequence)).ResolvedString; //string, not value
            FullSequence = SilacConversions.GetAmbiguousLightSequence(FullSequence, labels, false);

            //BaseSequence
            BaseSequence = PsmTsvWriter.Resolve(bestMatches.Select(b => b.SpecificBioPolymer.BaseSequence)).ResolvedString; //string, not value
            BaseSequence = SilacConversions.GetAmbiguousLightSequence(BaseSequence, labels, true);

            //EssentialSequence
            EssentialSequence = PsmTsvWriter.Resolve(bestMatches.Select(b => b.SpecificBioPolymer.EssentialSequence(modsToWritePruned))).ResolvedString; //string, not value
            EssentialSequence = SilacConversions.GetAmbiguousLightSequence(EssentialSequence, labels, false);
        }

        /// <summary>
        /// This method is used by SILAC quantification to add heavy/light psms
        /// Don't have access to the scans at that point, so a new contructor is needed
        /// </summary>
        public PeptideSpectralMatch Clone(List<SpectralMatchHypothesis> bestMatchingPeptides) => new PeptideSpectralMatch(this, bestMatchingPeptides);
        
        protected PeptideSpectralMatch(SpectralMatch psm, List<SpectralMatchHypothesis> bestMatchingPeptides) 
            : base(psm, bestMatchingPeptides)
        {
        }

        #endregion
    }
}
