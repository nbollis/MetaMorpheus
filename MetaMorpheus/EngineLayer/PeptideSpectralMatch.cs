using EngineLayer.FdrAnalysis;
using MassSpectrometry;
using Proteomics;
using Proteomics.ProteolyticDigestion;
using System.Collections.Generic;
using System.Linq;
using Omics;
using Omics.Fragmentation;
using Omics.Modifications;

namespace EngineLayer
{
    public class PeptideSpectralMatch : SpectralMatch
    {

        public PeptideSpectralMatch(PeptideWithSetModifications peptide, int notch, double score, int scanIndex, Ms2ScanWithSpecificMass scan,
            CommonParameters commonParameters, List<MatchedFragmentIon> matchedFragmentIons, double xcorr = 0) : 
            base(scanIndex, scan, commonParameters, xcorr)
        {
            _BestMatchingPeptides = new List<(int, PeptideWithSetModifications)>();
            DigestionParams = commonParameters.DigestionParams;
            PeptidesToMatchingFragments = new Dictionary<PeptideWithSetModifications, List<MatchedFragmentIon>>();
            AddOrReplace(peptide, score, notch, true, matchedFragmentIons, xcorr);
        }

        public PeptideSpectralMatch(IBioPolymerWithSetMods peptide, int notch, double score, int scanIndex,
            Ms2ScanWithSpecificMass scan, CommonParameters commonParameters,
            List<MatchedFragmentIon> matchedFragmentIons, double xcorr = 0)
            : this(peptide as PeptideWithSetModifications, notch, score, scanIndex, scan, commonParameters, matchedFragmentIons, xcorr)
        {
        }

        public int? PeptideLength { get; private set; } 
        public double? PeptideMonisotopicMass { get; private set; }
        public int? ProteinLength { get; private set; }
        public string ProteinAccession { get; private set; }
       
        public List<double> LocalizedScores { get; internal set; }
       
        public int NumDifferentMatchingPeptides { get { return _BestMatchingPeptides.Count; } }
    
        public PsmData PsmData_forPEPandPercolator { get; set; }

        

        public DigestionParams DigestionParams { get; }
       


        #region Search

        public Dictionary<PeptideWithSetModifications, List<MatchedFragmentIon>> PeptidesToMatchingFragments { get; private set; }

        protected List<(int Notch, PeptideWithSetModifications Pwsm)> _BestMatchingPeptides;
        public IEnumerable<(int Notch, PeptideWithSetModifications Peptide)> BestMatchingPeptides
        {
            get
            {
                return _BestMatchingPeptides.OrderBy(p => p.Pwsm.FullSequence)
                    .ThenBy(p => p.Pwsm.Protein.Accession)
                    .ThenBy(p => p.Pwsm.OneBasedStartResidue);
            }
        }

        /// <summary>
        /// This method saves properties of this PSM for internal use. It is NOT used for any output.
        /// These resolved fields are (usually) null if there is more than one option.
        /// e.g., if this PSM can be explained by more than one base sequence, the BaseSequence property will be null
        /// </summary>
        public override void ResolveAllAmbiguities()
        {
            IsDecoy = _BestMatchingPeptides.Any(p => p.Pwsm.Protein.IsDecoy);
            IsContaminant = _BestMatchingPeptides.Any(p => p.Pwsm.Protein.IsContaminant);
            FullSequence = PsmTsvWriter.Resolve(_BestMatchingPeptides.Select(b => b.Pwsm.FullSequence)).ResolvedValue;
            BaseSequence = PsmTsvWriter.Resolve(_BestMatchingPeptides.Select(b => b.Pwsm.BaseSequence)).ResolvedValue;
            PeptideLength = PsmTsvWriter.Resolve(_BestMatchingPeptides.Select(b => b.Pwsm.Length)).ResolvedValue;
            OneBasedStartResidue = PsmTsvWriter.Resolve(_BestMatchingPeptides.Select(b => b.Pwsm.OneBasedStartResidue)).ResolvedValue;
            OneBasedEndResidue = PsmTsvWriter.Resolve(_BestMatchingPeptides.Select(b => b.Pwsm.OneBasedEndResidue)).ResolvedValue;
            ProteinLength = PsmTsvWriter.Resolve(_BestMatchingPeptides.Select(b => b.Pwsm.Protein.Length)).ResolvedValue;
            PeptideMonisotopicMass = PsmTsvWriter.Resolve(_BestMatchingPeptides.Select(b => b.Pwsm.MonoisotopicMass)).ResolvedValue;
            ProteinAccession = PsmTsvWriter.Resolve(_BestMatchingPeptides.Select(b => b.Pwsm.Protein.Accession)).ResolvedValue;
            Organism = PsmTsvWriter.Resolve(_BestMatchingPeptides.Select(b => b.Pwsm.Protein.Organism)).ResolvedValue;
            ModsIdentified = PsmTsvWriter.Resolve(_BestMatchingPeptides.Select(b => b.Pwsm.AllModsOneIsNterminus)).ResolvedValue;
            ModsChemicalFormula = PsmTsvWriter.Resolve(_BestMatchingPeptides.Select(b => b.Pwsm.AllModsOneIsNterminus.Select(c => (c.Value)))).ResolvedValue;
            Notch = PsmTsvWriter.Resolve(_BestMatchingPeptides.Select(b => b.Notch)).ResolvedValue;

            // if the PSM matches a target and a decoy and they are the SAME SEQUENCE, remove the decoy
            if (IsDecoy)
            {
                bool removedPeptides = false;
                var hits = _BestMatchingPeptides.GroupBy(p => p.Pwsm.FullSequence);

                foreach (var hit in hits)
                {
                    if (hit.Any(p => p.Pwsm.Protein.IsDecoy) && hit.Any(p => !p.Pwsm.Protein.IsDecoy))
                    {
                        // at least one peptide with this sequence is a target and at least one is a decoy
                        // remove the decoys with this sequence
                        var pwsmToRemove = _BestMatchingPeptides.Where(p => p.Pwsm.FullSequence == hit.Key && p.Pwsm.Protein.IsDecoy).ToList();
                        _BestMatchingPeptides.RemoveAll(p => p.Pwsm.FullSequence == hit.Key && p.Pwsm.Protein.IsDecoy);
                        foreach ((int, PeptideWithSetModifications) pwsm in pwsmToRemove)
                        {
                            PeptidesToMatchingFragments.Remove(pwsm.Item2);
                        }

                        removedPeptides = true;
                    }
                }

                if (removedPeptides)
                {
                    ResolveAllAmbiguities();
                }
            }

            // TODO: technically, different peptide options for this PSM can have different matched ions
            // we can write a Resolve method for this if we want...
            MatchedFragmentIons = PeptidesToMatchingFragments.First().Value;
        }

        public override void AddOrReplace(IBioPolymerWithSetMods owsm, double newScore, int notch, bool reportAllAmbiguity,
            List<MatchedFragmentIon> matchedFragmentIons, double newXcorr)
            => AddOrReplace(owsm as PeptideWithSetModifications, newScore, notch, reportAllAmbiguity, matchedFragmentIons, newXcorr);

        public void AddOrReplace(PeptideWithSetModifications pwsm, double newScore, int notch, bool reportAllAmbiguity, List<MatchedFragmentIon> matchedFragmentIons, double newXcorr)
        {
            if (newScore - Score > ToleranceForScoreDifferentiation) //if new score beat the old score, overwrite it
            {
                _BestMatchingPeptides.Clear();
                _BestMatchingPeptides.Add((notch, pwsm));

                if (Score - RunnerUpScore > ToleranceForScoreDifferentiation)
                {
                    RunnerUpScore = Score;
                }

                Score = newScore;
                Xcorr = newXcorr;

                PeptidesToMatchingFragments.Clear();
                PeptidesToMatchingFragments.Add(pwsm, matchedFragmentIons);
            }
            else if (newScore - Score > -ToleranceForScoreDifferentiation && reportAllAmbiguity) //else if the same score and ambiguity is allowed
            {
                _BestMatchingPeptides.Add((notch, pwsm));

                if (!PeptidesToMatchingFragments.ContainsKey(pwsm))
                {
                    PeptidesToMatchingFragments.Add(pwsm, matchedFragmentIons);
                }
            }
            else if (newScore - RunnerUpScore > ToleranceForScoreDifferentiation)
            {
                RunnerUpScore = newScore;
            }
        }

        #endregion

        #region IO


        public static string GetTabSeparatedHeader()
        {
            return string.Join("\t", DataDictionary(null, null).Keys);
        }

        public override string ToString()
        {
            return ToString(new Dictionary<string, int>());
        }

        public string ToString(IReadOnlyDictionary<string, int> ModstoWritePruned)
        {
            return string.Join("\t", DataDictionary(this, ModstoWritePruned).Values);
        }

        public static Dictionary<string, string> DataDictionary(PeptideSpectralMatch psm, IReadOnlyDictionary<string, int> ModsToWritePruned)
        {
            Dictionary<string, string> s = new Dictionary<string, string>();
            PsmTsvWriter.AddBasicMatchData(s, psm);
            PsmTsvWriter.AddPeptideSequenceData(s, psm, ModsToWritePruned);
            PsmTsvWriter.AddMatchedIonsData(s, psm?.MatchedFragmentIons);
            PsmTsvWriter.AddMatchScoreData(s, psm);
            return s;
        }

        #endregion

        //PEP-Value analysis identifies ambiguous peptides with lower probability. These are removed from the bestmatchingpeptides dictionary, which lowers ambiguity.
        public void RemoveThisAmbiguousPeptide(int notch, PeptideWithSetModifications pwsm)
        {
            _BestMatchingPeptides.Remove((notch, pwsm));
            if (!_BestMatchingPeptides.Any(x => x.Pwsm.Equals(pwsm)))
            {
                PeptidesToMatchingFragments.Remove(pwsm);
            }
            this.ResolveAllAmbiguities();
        }

        public static int GetLongestIonSeriesBidirectional(Dictionary<PeptideWithSetModifications, List<MatchedFragmentIon>> PeptidesToMatchingFragments, PeptideWithSetModifications peptide)
        {
            List<int> maxDiffs = new List<int> { 1 };
            if (PeptidesToMatchingFragments != null && PeptidesToMatchingFragments.TryGetValue(peptide, out var matchedFragments) && matchedFragments != null && matchedFragments.Any())
            {
                var jointSeries = matchedFragments.Select(p => p.NeutralTheoreticalProduct.AminoAcidPosition).Distinct().ToList();

                if (jointSeries.Count > 0)
                {
                    jointSeries.Sort();

                    List<int> aminoAcidPostionsThatCouldBeObserved = Enumerable.Range(1, peptide.BaseSequence.Length).ToList();

                    List<int> missing = aminoAcidPostionsThatCouldBeObserved.Except(jointSeries).ToList();

                    int localMaxDiff = 0;
                    for (int i = 0; i < aminoAcidPostionsThatCouldBeObserved.Count; i++)
                    {
                        if (!missing.Contains(aminoAcidPostionsThatCouldBeObserved[i]))
                        {
                            localMaxDiff++;
                        }
                        else
                        {
                            maxDiffs.Add(localMaxDiff);
                            localMaxDiff = 0;
                        }
                    }
                    maxDiffs.Add(localMaxDiff);
                }
            }

            return maxDiffs.Max();
        }

        /// <summary>
        /// Determine the Fragment Coverage the PSM
        /// Assigns fragment coverage indices for the PSM and the protein based on Amino Acid Position in Matched Ion Fragments
        /// </summary>
        public void GetAminoAcidCoverage()
        {
            if (string.IsNullOrEmpty(this.BaseSequence) ||
                !this.MatchedFragmentIons.Any()) return;
            //Pull C terminal and N terminal Fragments and amino acid numbers
            var nTermFragmentAAPositions = this.MatchedFragmentIons.Where(p =>
                    p.NeutralTheoreticalProduct.Terminus == FragmentationTerminus.N)
                .Select(j => j.NeutralTheoreticalProduct.AminoAcidPosition).Distinct().ToList();

            var cTermFragmentAAPositions = this.MatchedFragmentIons.Where(p =>
                    p.NeutralTheoreticalProduct.Terminus == FragmentationTerminus.C)
                .Select(j => j.NeutralTheoreticalProduct.AminoAcidPosition).Distinct().ToList();


            GetCoverage(nTermFragmentAAPositions, cTermFragmentAAPositions);
        }

        public static int GetCountComplementaryIons(Dictionary<PeptideWithSetModifications, List<MatchedFragmentIon>> PeptidesToMatchingFragments, PeptideWithSetModifications peptide)
        {
            if (PeptidesToMatchingFragments != null && PeptidesToMatchingFragments.TryGetValue(peptide, out var matchedFragments) && matchedFragments != null && matchedFragments.Any())
            {
                List<int> nIons = matchedFragments.Where(f => f.NeutralTheoreticalProduct.Terminus == FragmentationTerminus.N).Select(f => f.NeutralTheoreticalProduct.FragmentNumber).ToList();
                List<int> cIons = matchedFragments.Where(f => f.NeutralTheoreticalProduct.Terminus == FragmentationTerminus.C).Select(f => (peptide.BaseSequence.Length - f.NeutralTheoreticalProduct.FragmentNumber)).ToList();
                if (nIons.Any() && cIons.Any())
                {
                    return nIons.Intersect(cIons).Count();
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// This method changes the base and full sequences to reflect heavy silac labels
        /// translates SILAC sequence into the proper peptide sequence ("PEPTIDEa" into "PEPTIDEK(+8.014)")
        /// </summary>
        public void ResolveHeavySilacLabel(List<SilacLabel> labels, IReadOnlyDictionary<string, int> modsToWritePruned)
        {
            //FullSequence
            FullSequence = PsmTsvWriter.Resolve(_BestMatchingPeptides.Select(b => b.Pwsm.FullSequence)).ResolvedString; //string, not value
            FullSequence = SilacConversions.GetAmbiguousLightSequence(FullSequence, labels, false);

            //BaseSequence
            BaseSequence = PsmTsvWriter.Resolve(_BestMatchingPeptides.Select(b => b.Pwsm.BaseSequence)).ResolvedString; //string, not value
            BaseSequence = SilacConversions.GetAmbiguousLightSequence(BaseSequence, labels, true);

            //EssentialSequence
            EssentialSequence = PsmTsvWriter.Resolve(_BestMatchingPeptides.Select(b => b.Pwsm.EssentialSequence(modsToWritePruned))).ResolvedString; //string, not value
            EssentialSequence = SilacConversions.GetAmbiguousLightSequence(EssentialSequence, labels, false);
        }

        /// <summary>
        /// This method is used by protein parsimony to remove PeptideWithSetModifications objects that have non-parsimonious protein associations
        /// </summary>
        public void TrimProteinMatches(HashSet<Protein> parsimoniousProteins)
        {
            if (IsDecoy)
            {
                if (_BestMatchingPeptides.Any(p => parsimoniousProteins.Contains(p.Pwsm.Protein) && p.Pwsm.Protein.IsDecoy))
                {
                    _BestMatchingPeptides.RemoveAll(p => !parsimoniousProteins.Contains(p.Pwsm.Protein));
                }
                // else do nothing
            }
            else
            {
                _BestMatchingPeptides.RemoveAll(p => !parsimoniousProteins.Contains(p.Pwsm.Protein));
            }

            ResolveAllAmbiguities();
        }

        /// <summary>
        /// This method is used by protein parsimony to add PeptideWithSetModifications objects for modification-agnostic parsimony
        /// </summary>
        public void AddProteinMatch((int, PeptideWithSetModifications) peptideWithNotch, List<MatchedFragmentIon> mfi)
        {
            if (!_BestMatchingPeptides.Select(p => p.Pwsm).Contains(peptideWithNotch.Item2))
            {
                _BestMatchingPeptides.Add(peptideWithNotch);
                if (!PeptidesToMatchingFragments.ContainsKey(peptideWithNotch.Item2))
                {
                    PeptidesToMatchingFragments.Add(peptideWithNotch.Item2, mfi);
                }
                ResolveAllAmbiguities();
            }
        }

        /// <summary>
        /// This method is used by SILAC quantification to add heavy/light psms
        /// Don't have access to the scans at that point, so a new contructor is needed
        /// </summary>
        public PeptideSpectralMatch Clone(List<(int Notch, PeptideWithSetModifications Peptide)> bestMatchingPeptides)
        {
            return new PeptideSpectralMatch(this, bestMatchingPeptides);
        }

        protected PeptideSpectralMatch(PeptideSpectralMatch psm, List<(int Notch, PeptideWithSetModifications Peptide)> bestMatchingPeptides)
        {
            _BestMatchingPeptides = bestMatchingPeptides;
            BaseSequence = PsmTsvWriter.Resolve(bestMatchingPeptides.Select(b => b.Peptide.BaseSequence)).ResolvedValue;
            FullSequence = PsmTsvWriter.Resolve(bestMatchingPeptides.Select(b => b.Peptide.FullSequence)).ResolvedValue;

            ModsChemicalFormula = psm.ModsChemicalFormula;
            Notch = psm.Notch;
            PeptideLength = psm.PeptideLength;
            OneBasedStartResidue = psm.OneBasedStartResidue;
            OneBasedEndResidue = psm.OneBasedEndResidue;
            PeptideMonisotopicMass = psm.PeptideMonisotopicMass;
            ProteinLength = psm.ProteinLength;
            ProteinAccession = psm.ProteinAccession;
            Organism = psm.Organism;
            MatchedFragmentIons = psm.MatchedFragmentIons;
            PsmCount = psm.PsmCount;
            ModsIdentified = psm.ModsIdentified;
            LocalizedScores = psm.LocalizedScores;
            ScanNumber = psm.ScanNumber;
            PrecursorScanNumber = psm.PrecursorScanNumber;
            ScanRetentionTime = psm.ScanRetentionTime;
            ScanExperimentalPeaks = psm.ScanExperimentalPeaks;
            TotalIonCurrent = psm.TotalIonCurrent;
            ScanPrecursorCharge = psm.ScanPrecursorCharge;
            ScanPrecursorMonoisotopicPeakMz = psm.ScanPrecursorMonoisotopicPeakMz;
            ScanPrecursorMass = psm.ScanPrecursorMass;
            FullFilePath = psm.FullFilePath;
            ScanIndex = psm.ScanIndex;
            FdrInfo = psm.FdrInfo;
            Score = psm.Score;
            Xcorr = psm.Xcorr;
            RunnerUpScore = psm.RunnerUpScore;
            IsDecoy = psm.IsDecoy;
            IsContaminant = psm.IsContaminant;
            DigestionParams = psm.DigestionParams;
            PeptidesToMatchingFragments = psm.PeptidesToMatchingFragments;
            SpectralAngle = psm.SpectralAngle;
        }

    }
}