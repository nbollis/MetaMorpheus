﻿using Chemistry;
using EngineLayer.FdrAnalysis;
using EngineLayer.ModernSearch;
using Proteomics;
using Omics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using System;
using MassSpectrometry;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MzLibUtil;
using Omics.Digestion;
using Omics.Fragmentation.Peptide;
using Omics.Modifications;

namespace EngineLayer.NonSpecificEnzymeSearch
{
    public class NonSpecificEnzymeSearchEngine : ModernSearchEngine
    {
        private static readonly double WaterMonoisotopicMass = PeriodicTable.GetElement("H").PrincipalIsotope.AtomicMass * 2 + PeriodicTable.GetElement("O").PrincipalIsotope.AtomicMass;

        private readonly List<int>[] PrecursorIndex;
        private readonly int MinimumPeptideLength;
        readonly SpectralMatch[][] GlobalCategorySpecificPsms;
        readonly CommonParameters ModifiedParametersNoComp;
        readonly List<ProductType> ProductTypesToSearch;
        readonly List<Modification> VariableTerminalModifications;
        readonly List<int>[] CoisolationIndex;

        public NonSpecificEnzymeSearchEngine(SpectralMatch[][] globalPsms, Ms2ScanWithSpecificMass[] listOfSortedms2Scans, List<int>[] coisolationIndex,
            List<PeptideWithSetModifications> peptideIndex, List<int>[] fragmentIndex, List<int>[] precursorIndex, int currentPartition,
            CommonParameters commonParameters, List<(string fileName, CommonParameters fileSpecificParameters)> fileSpecificParameters, List<Modification> variableModifications, MassDiffAcceptor massDiffAcceptor, double maximumMassThatFragmentIonScoreIsDoubled, List<string> nestedIds)
            : base(null, listOfSortedms2Scans, peptideIndex, fragmentIndex, currentPartition, commonParameters, fileSpecificParameters, massDiffAcceptor, maximumMassThatFragmentIonScoreIsDoubled, nestedIds)
        {
            CoisolationIndex = coisolationIndex;
            PrecursorIndex = precursorIndex;
            MinimumPeptideLength = commonParameters.DigestionParams.MinLength;
            GlobalCategorySpecificPsms = globalPsms;
            ModifiedParametersNoComp = commonParameters.CloneWithNewTerminus(addCompIons: false);
            ProductTypesToSearch = DissociationTypeCollection.ProductsFromDissociationType[commonParameters.DissociationType].Intersect(TerminusSpecificProductTypes.ProductIonTypesFromSpecifiedTerminus[commonParameters.DigestionParams.FragmentationTerminus]).ToList();
            VariableTerminalModifications = GetVariableTerminalMods(commonParameters.DigestionParams.FragmentationTerminus, variableModifications);
        }

        protected override MetaMorpheusEngineResults RunSpecific()
        {
            bool semiSpecificSearch = CommonParameters.DigestionParams.SearchModeType == CleavageSpecificity.Semi;

            double progress = 0;
            int oldPercentProgress = 0;
            ReportProgress(new ProgressEventArgs(oldPercentProgress, "Performing nonspecific search... " + CurrentPartition + "/" + CommonParameters.TotalPartitions, NestedIds));

            //check that the dissociationType is supported
            if (CommonParameters.AddCompIons && !complementaryIonConversionDictionary.ContainsKey(CommonParameters.DissociationType))
            {
                throw new NotImplementedException();
            }

            int maxThreadsPerFile = CommonParameters.MaxThreadsToUsePerFile;
            int[] threads = Enumerable.Range(0, maxThreadsPerFile).ToArray();
            Parallel.ForEach(threads, (i) =>
            {
                byte[] scoringTable = new byte[PeptideIndex.Count];

                List<Product> peptideTheorProducts = new List<Product>();
                List<int> idsOfPeptidesPossiblyObserved = new List<int>();

                for (; i < CoisolationIndex.Length; i += maxThreadsPerFile)
                {
                    // Stop loop if canceled
                    if (GlobalVariables.StopLoops) { return; }

                    // empty the scoring table to score the new scan (conserves memory compared to allocating a new array)
                    Array.Clear(scoringTable, 0, scoringTable.Length);
                    idsOfPeptidesPossiblyObserved.Clear();
                    List<int> coisolatedIndexes = CoisolationIndex[i];
                    Ms2ScanWithSpecificMass scan = ListOfSortedMs2Scans[coisolatedIndexes[(coisolatedIndexes.Count - 1) / 2]]; //get first scan; all scans should be identical

                    //do first pass scoring
                    SnesIndexedScoring(scan, FragmentIndex, scoringTable, PeptideIndex, DissociationType);

                    for (int j = 0; j < coisolatedIndexes.Count; j++)
                    {
                        int ms2ArrayIndex = coisolatedIndexes[j];
                        scan = ListOfSortedMs2Scans[ms2ArrayIndex];

                        //populate ids of possibly observed with those containing allowed precursor masses
                        List<AllowedIntervalWithNotch> validIntervals = MassDiffAcceptor.GetAllowedPrecursorMassIntervalsFromObservedMass(scan.PrecursorMass).ToList(); //get all valid notches
                        foreach (AllowedIntervalWithNotch interval in validIntervals)
                        {
                            int obsPrecursorFloorMz = (int)Math.Floor(interval.Minimum * FragmentBinsPerDalton);
                            int obsPrecursorCeilingMz = (int)Math.Ceiling(interval.Maximum * FragmentBinsPerDalton);

                            foreach (ProductType pt in ProductTypesToSearch)
                            {
                                int dissociationBinShift = (int)Math.Round((WaterMonoisotopicMass - DissociationTypeCollection.GetMassShiftFromProductType(pt)) * FragmentBinsPerDalton);
                                int lowestBin = obsPrecursorFloorMz - dissociationBinShift;
                                int highestBin = obsPrecursorCeilingMz - dissociationBinShift;
                                for (int bin = lowestBin; bin <= highestBin; bin++)
                                {
                                    if (bin < FragmentIndex.Length && FragmentIndex[bin] != null)
                                    {
                                        FragmentIndex[bin].ForEach(id => idsOfPeptidesPossiblyObserved.Add(id));
                                    }
                                }
                            }

                            for (int bin = obsPrecursorFloorMz; bin <= obsPrecursorCeilingMz; bin++) //no bin shift, since they're precursor masses
                            {
                                if (bin < PrecursorIndex.Length && PrecursorIndex[bin] != null)
                                {
                                    PrecursorIndex[bin].ForEach(id => idsOfPeptidesPossiblyObserved.Add(id));
                                }
                            }
                        }

                        // done with initial scoring and precursor matching; refine scores and create PSMs
                        if (idsOfPeptidesPossiblyObserved.Any())
                        {
                            int maxInitialScore = idsOfPeptidesPossiblyObserved.Max(id => scoringTable[id]) + 1;
                            while (maxInitialScore + 4 > CommonParameters.ScoreCutoff) //go through all until we hit the end. +4 is arbitrary to negate initial scoring's removal of peaks below 350 Da
                            {
                                maxInitialScore--;
                                foreach (int id in idsOfPeptidesPossiblyObserved.Where(id => scoringTable[id] == maxInitialScore))
                                {
                                    PeptideWithSetModifications peptide = PeptideIndex[id];
                                    peptide.Fragment(CommonParameters.DissociationType, CommonParameters.DigestionParams.FragmentationTerminus, peptideTheorProducts);
                                    Tuple<int, PeptideWithSetModifications> notchAndUpdatedPeptide = Accepts(peptideTheorProducts, scan.PrecursorMass, peptide, CommonParameters.DigestionParams.FragmentationTerminus, MassDiffAcceptor, semiSpecificSearch);
                                    int notch = notchAndUpdatedPeptide.Item1;
                                    if (notch >= 0)
                                    {
                                        peptide = notchAndUpdatedPeptide.Item2;
                                        peptide.Fragment(CommonParameters.DissociationType, FragmentationTerminus.Both, peptideTheorProducts);
                                        List<MatchedFragmentIon> matchedIons = MatchFragmentIons(scan, peptideTheorProducts, ModifiedParametersNoComp);

                                        double thisScore = CalculatePeptideScore(scan.TheScan, matchedIons);
                                        if (thisScore > CommonParameters.ScoreCutoff)
                                        {
                                            SpectralMatch[] localPeptideSpectralMatches = GlobalCategorySpecificPsms[(int)FdrClassifier.GetCleavageSpecificityCategory(peptide.CleavageSpecificityForFdrCategory)];
                                            if (localPeptideSpectralMatches[ms2ArrayIndex] == null)
                                            {
                                                localPeptideSpectralMatches[ms2ArrayIndex] = new PeptideSpectralMatch(peptide, notch, thisScore, ms2ArrayIndex, scan, CommonParameters, matchedIons);
                                            }
                                            else
                                            {
                                                localPeptideSpectralMatches[ms2ArrayIndex].AddOrReplace(peptide, thisScore, notch, CommonParameters.ReportAllAmbiguity, matchedIons);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // report search progress
                    progress++;
                    int percentProgress = (int)((progress / CoisolationIndex.Length) * 100);

                    if (percentProgress > oldPercentProgress)
                    {
                        oldPercentProgress = percentProgress;
                        ReportProgress(new ProgressEventArgs(percentProgress, "Performing nonspecific search... " + CurrentPartition + "/" + CommonParameters.TotalPartitions, NestedIds));
                    }
                }
            });
            return new MetaMorpheusEngineResults(this);
        }

        private void SnesIndexedScoring(Ms2ScanWithSpecificMass scan, List<int>[] FragmentIndex, byte[] scoringTable, List<PeptideWithSetModifications> peptideIndex, DissociationType dissociationType)
        {
            int obsPreviousFragmentCeilingMz = 0;

            if (dissociationType == DissociationType.LowCID)
            {
                double[] masses = scan.TheScan.MassSpectrum.XArray;
                double[] intensities = scan.TheScan.MassSpectrum.YArray;

                for (int i = 0; i < masses.Length; i++)
                {
                    //convert to an int since we're in discrete 1.0005...
                    int fragmentBin = (int)(Math.Round(masses[i].ToMass(1) / 1.0005079) * 1.0005079 * FragmentBinsPerDalton);
                    List<int> bin = FragmentIndex[fragmentBin];

                    //score
                    if (bin != null)
                    {
                        for (int pep = 0; pep < bin.Count; pep++)
                        {
                            scoringTable[bin[pep]]++;
                        }
                    }

                    // add complementary ions
                    if (CommonParameters.AddCompIons)
                    {
                        if (complementaryIonConversionDictionary.ContainsKey(CommonParameters.DissociationType))
                        {
                            foreach (double massShift in complementaryIonConversionDictionary[CommonParameters.DissociationType])
                            {
                                double protonMassShift = massShift.ToMass(1);
                                protonMassShift = Chemistry.ClassExtensions.ToMass(protonMassShift, 1);
                                fragmentBin = (int)Math.Round((scan.PrecursorMass + protonMassShift - masses[i]) / 1.0005079);

                                if(fragmentBin >= 0) //if the fragments contain the precursor than this code would break
                                {
                                    bin = FragmentIndex[fragmentBin];

                                    //score
                                    if (bin != null)
                                    {
                                        for (int pep = 0; pep < bin.Count; pep++)
                                        {
                                            scoringTable[bin[pep]]++;
                                        }
                                    }
                                }
                                
                            }
                        }
                    }
                }
            }
            else
            {
                IsotopicEnvelope[] experimentalFragments = scan.ExperimentalFragments;
                Tolerance productTolerance = CommonParameters.ProductMassTolerance;
                for (int envelopeIndex = 0; envelopeIndex < experimentalFragments.Length; envelopeIndex++)
                {
                    // assume charge state 1 to calculate mass tolerance
                    double experimentalFragmentMass = experimentalFragments[envelopeIndex].MonoisotopicMass;

                    // get theoretical fragment bins within mass tolerance
                    int obsFragmentFloorMass = (int)Math.Floor((productTolerance.GetMinimumValue(experimentalFragmentMass)) * FragmentBinsPerDalton);
                    int obsFragmentCeilingMass = (int)Math.Ceiling((productTolerance.GetMaximumValue(experimentalFragmentMass)) * FragmentBinsPerDalton);
                    if (obsFragmentCeilingMass > 350000)
                    {
                        // prevents double-counting peaks close in m/z and lower-bound out of range exceptions
                        if (obsFragmentFloorMass < obsPreviousFragmentCeilingMz)
                        {
                            obsFragmentFloorMass = obsPreviousFragmentCeilingMz;
                        }
                        obsPreviousFragmentCeilingMz = obsFragmentCeilingMass + 1;

                        // prevent upper-bound index out of bounds errors;
                        // lower-bound is handled by the previous "if (obsFragmentFloorMass < obsPreviousFragmentCeilingMz)" statement
                        if (obsFragmentCeilingMass >= FragmentIndex.Length)
                        {
                            obsFragmentCeilingMass = FragmentIndex.Length - 1;
                            if (obsFragmentFloorMass >= FragmentIndex.Length)
                            {
                                obsFragmentFloorMass = FragmentIndex.Length - 1;
                            }
                        }

                        // search mass bins within a tolerance
                        for (int fragmentBin = obsFragmentFloorMass; fragmentBin <= obsFragmentCeilingMass; fragmentBin++)
                        {
                            List<int> bin = FragmentIndex[fragmentBin];

                            //score
                            if (bin != null)
                            {
                                for (int pep = 0; pep < bin.Count; pep++)
                                {
                                    scoringTable[bin[pep]]++;
                                }
                            }
                        }
                    }

                    // add complementary ions
                    if (CommonParameters.AddCompIons)
                    {
                        if (complementaryIonConversionDictionary.ContainsKey(dissociationType))
                        {
                            foreach (double massShift in complementaryIonConversionDictionary[dissociationType])
                            {
                                double protonMassShift = massShift.ToMass(1);
                                int compFragmentFloorMass = (int)Math.Round(((scan.PrecursorMass + protonMassShift) * FragmentBinsPerDalton)) - obsFragmentCeilingMass;
                                int compFragmentCeilingMass = (int)Math.Round(((scan.PrecursorMass + protonMassShift) * FragmentBinsPerDalton)) - obsFragmentFloorMass;
                                if (compFragmentCeilingMass > 350000)
                                {
                                    // prevent index out of bounds errors
                                    if (compFragmentCeilingMass >= FragmentIndex.Length)
                                    {
                                        compFragmentCeilingMass = FragmentIndex.Length - 1;
                                        if (compFragmentFloorMass >= FragmentIndex.Length)
                                        {
                                            compFragmentFloorMass = FragmentIndex.Length - 1;
                                        }
                                    }
                                    if (compFragmentFloorMass < 0)
                                    {
                                        compFragmentFloorMass = 0;
                                    }

                                    for (int fragmentBin = compFragmentFloorMass; fragmentBin <= compFragmentCeilingMass; fragmentBin++)
                                    {
                                        List<int> bin = FragmentIndex[fragmentBin];

                                        //score
                                        if (bin != null)
                                        {
                                            for (int pep = 0; pep < bin.Count; pep++)
                                            {
                                                scoringTable[bin[pep]]++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        
                    }
                }
            }
        }

        private Tuple<int, PeptideWithSetModifications> Accepts(List<Product> fragments, double scanPrecursorMass, PeptideWithSetModifications peptide, FragmentationTerminus fragmentationTerminus, MassDiffAcceptor searchMode, bool semiSpecificSearch)
        {
            int localminPeptideLength = CommonParameters.DigestionParams.MinLength;

            //Get terminal modifications, if any
            Dictionary<int, List<Modification>> databaseAnnotatedMods = semiSpecificSearch ? null : GetTerminalModPositions(peptide, CommonParameters.DigestionParams, VariableTerminalModifications);

            for (int i = localminPeptideLength - 1; i < fragments.Count; i++) //minus one start, because fragment 1 is at index 0
            {
                Product fragment = fragments[i];
                double theoMass = fragment.NeutralMass - DissociationTypeCollection.GetMassShiftFromProductType(fragment.ProductType) + WaterMonoisotopicMass;
                int notch = searchMode.Accepts(scanPrecursorMass, theoMass);

                //check for terminal mods that might reach the observed mass
                Modification terminalMod = null;
                if (!semiSpecificSearch && notch < 0 && databaseAnnotatedMods.TryGetValue(i + 1, out List<Modification> terminalModsAtThisIndex)) //look for i+1, because the mod might exist at the terminus
                {
                    foreach (Modification mod in terminalModsAtThisIndex)
                    {
                        notch = searchMode.Accepts(scanPrecursorMass, theoMass + mod.MonoisotopicMass.Value); //overwrite the notch, since the other notch wasn't accepted
                        if (notch >= 0)
                        {
                            terminalMod = mod;
                            break;
                        }
                    }
                }

                if (notch >= 0)
                {
                    PeptideWithSetModifications updatedPwsm = null;
                    if (fragmentationTerminus == FragmentationTerminus.N)
                    {
                        int endResidue = peptide.OneBasedStartResidue + fragment.FragmentNumber - 1; //-1 for one based index
                        Dictionary<int, Modification> updatedMods = new Dictionary<int, Modification>();
                        foreach (KeyValuePair<int, Modification> mod in peptide.AllModsOneIsNterminus)
                        {
                            if (mod.Key < endResidue - peptide.OneBasedStartResidue + 3) //check if we cleaved it off, +1 for N-terminus being mod 1 and first residue being mod 2, +1 again for the -1 on end residue for one based index, +1 (again) for the one-based start residue
                            {
                                updatedMods.Add(mod.Key, mod.Value);
                            }
                        }
                        if (terminalMod != null)
                        {
                            updatedMods.Add(endResidue, terminalMod);
                        }
                        updatedPwsm = new PeptideWithSetModifications(peptide.Protein, peptide.DigestionParams, peptide.OneBasedStartResidue, endResidue, CleavageSpecificity.Unknown, "", 0, updatedMods, 0);
                    }
                    else //if C terminal ions, shave off the n-terminus
                    {
                        int startResidue = peptide.OneBasedEndResidue - fragment.FragmentNumber + 1; //plus one for one based index
                        Dictionary<int, Modification> updatedMods = new Dictionary<int, Modification>();  //updateMods
                        int indexShift = startResidue - peptide.OneBasedStartResidue;
                        foreach (KeyValuePair<int, Modification> mod in peptide.AllModsOneIsNterminus)
                        {
                            if (mod.Key > indexShift + 1) //check if we cleaved it off, +1 for N-terminus being mod 1 and first residue being 2
                            {
                                int key = mod.Key - indexShift;
                                updatedMods.Add(key, mod.Value);
                            }
                        }
                        if (terminalMod != null && !updatedMods.Keys.Contains(startResidue - 1))
                        {
                            updatedMods.Add(startResidue - 1, terminalMod);
                        }
                        updatedPwsm = new PeptideWithSetModifications(peptide.Protein, peptide.DigestionParams, startResidue, peptide.OneBasedEndResidue, CleavageSpecificity.Unknown, "", 0, updatedMods, 0);
                    }
                    return new Tuple<int, PeptideWithSetModifications>(notch, updatedPwsm);
                }
                else if (theoMass > scanPrecursorMass)
                {
                    break;
                }
            }

            //if the theoretical and experimental have the same mass or a terminal mod exists
            if (peptide.BaseSequence.Length >= localminPeptideLength)
            {
                double totalMass = peptide.MonoisotopicMass;// + Constants.ProtonMass;
                int notch = searchMode.Accepts(scanPrecursorMass, totalMass);
                if (notch >= 0)
                {
                    //need to update so that the cleavage specificity is recorded
                    PeptideWithSetModifications updatedPwsm = new PeptideWithSetModifications(peptide.Protein, peptide.DigestionParams, peptide.OneBasedStartResidue, peptide.OneBasedEndResidue, CleavageSpecificity.Unknown, "", 0, peptide.AllModsOneIsNterminus, peptide.NumFixedMods);
                    return new Tuple<int, PeptideWithSetModifications>(notch, updatedPwsm);
                }
                else //try a terminal mod (if it exists)
                {
                    if (!semiSpecificSearch && databaseAnnotatedMods.TryGetValue(peptide.Length, out List<Modification> terminalModsAtThisIndex))
                    {
                        foreach (Modification terminalMod in terminalModsAtThisIndex)
                        {
                            notch = searchMode.Accepts(scanPrecursorMass, totalMass + terminalMod.MonoisotopicMass.Value); //overwrite the notch, since the other notch wasn't accepted
                            if (notch >= 0)
                            {
                                //need to update the mod dictionary and don't want to overwrite the peptide incase it's in other scans
                                Dictionary<int, Modification> updatedMods = new Dictionary<int, Modification>();  //updateMods
                                foreach (KeyValuePair<int, Modification> mod in peptide.AllModsOneIsNterminus)
                                {
                                    updatedMods.Add(mod.Key, mod.Value);
                                }

                                //add the terminal mod
                                if (fragmentationTerminus == FragmentationTerminus.N)
                                {
                                    updatedMods[peptide.OneBasedEndResidue] = terminalMod;
                                }
                                else
                                {
                                    updatedMods[peptide.OneBasedStartResidue - 1] = terminalMod;
                                }

                                PeptideWithSetModifications updatedPwsm = new PeptideWithSetModifications(peptide.Protein, peptide.DigestionParams, peptide.OneBasedStartResidue, peptide.OneBasedEndResidue, CleavageSpecificity.Unknown, "", 0, updatedMods, peptide.NumFixedMods);
                                return new Tuple<int, PeptideWithSetModifications>(notch, updatedPwsm);
                            }
                        }
                    }
                }
            }
            return new Tuple<int, PeptideWithSetModifications>(-1, null);
        }

        public static List<SpectralMatch> ResolveFdrCategorySpecificPsms(List<SpectralMatch>[] AllPsms, int numNotches, string taskId, CommonParameters commonParameters, List<(string fileName, CommonParameters fileSpecificParameters)> fileSpecificParameters)
        {
            //update all psms with peptide info
            AllPsms.ToList()
                .Where(psmArray => psmArray != null).ToList()
                .ForEach(psmArray => psmArray.Where(psm => psm != null).ToList()
                .ForEach(psm => psm.ResolveAllAmbiguities()));

            foreach (List<SpectralMatch> psmsArray in AllPsms)
            {
                if (psmsArray != null)
                {
                    List<SpectralMatch> cleanedPsmsArray = psmsArray.Where(b => b != null).OrderByDescending(b => b.Score)
                       .ThenBy(b => b.BioPolymerWithSetModsMonoisotopicMass.HasValue ? Math.Abs(b.ScanPrecursorMass - b.BioPolymerWithSetModsMonoisotopicMass.Value) : double.MaxValue)
                       .GroupBy(b => (b.FullFilePath, b.ScanNumber, b.BioPolymerWithSetModsMonoisotopicMass)).Select(b => b.First()).ToList();

                    new FdrAnalysisEngine(cleanedPsmsArray, numNotches, commonParameters, fileSpecificParameters, new List<string> { taskId }).Run();

                    for (int i = 0; i < psmsArray.Count; i++)
                    {
                        if (psmsArray[i] != null)
                        {
                            if (psmsArray[i].FdrInfo == null) //if it was grouped in the cleanedPsmsArray
                            {
                                psmsArray[i] = null;
                            }
                        }
                    }
                }
            }

            int[] ranking = new int[AllPsms.Length]; //high int is good ranking
            List<int> indexesOfInterest = new List<int>();
            for (int i = 0; i < ranking.Length; i++)
            {
                if (AllPsms[i] != null)
                {
                    ranking[i] = AllPsms[i].Where(x => x != null).Count(x => x.PsmFdrInfo.QValue <= 0.01); //set ranking as number of psms above 1% FDR
                    indexesOfInterest.Add(i);
                }
            }

            //get the index of the category with the highest ranking
            int majorCategoryIndex = indexesOfInterest[0];
            for (int i = 1; i < indexesOfInterest.Count; i++)
            {
                int currentCategoryIndex = indexesOfInterest[i];
                if (ranking[currentCategoryIndex] > ranking[majorCategoryIndex])
                {
                    majorCategoryIndex = currentCategoryIndex;
                }
            }

            //update other category q-values
            //There's a chance of weird categories getting a random decoy before a random target, but we don't want to give that target a q value of zero.
            //We can't just take the q of the first decoy, because if the target wasn't random (score = 40), but there are no other targets before the decoy (score = 5), then we're incorrectly dinging the target
            //The current solution is such that if a minor category has a lower q value than it's corresponding score in the major category, then its q-value is changed to what it would be in the major category
            List<SpectralMatch> majorCategoryPsms = AllPsms[majorCategoryIndex].Where(x => x != null).OrderByDescending(x => x.Score).ToList(); //get sorted major category
            for (int i = 0; i < indexesOfInterest.Count; i++)
            {
                int minorCategoryIndex = indexesOfInterest[i];
                if (minorCategoryIndex != majorCategoryIndex)
                {
                    List<SpectralMatch> minorCategoryPsms = AllPsms[minorCategoryIndex].Where(x => x != null).OrderByDescending(x => x.Score).ToList(); //get sorted minor category
                    int minorPsmIndex = 0;
                    int majorPsmIndex = 0;
                    while (minorPsmIndex < minorCategoryPsms.Count && majorPsmIndex < majorCategoryPsms.Count) //while in the lists
                    {
                        SpectralMatch majorPsm = majorCategoryPsms[majorPsmIndex];
                        SpectralMatch minorPsm = minorCategoryPsms[minorPsmIndex];
                        //major needs to be a lower score than the minor
                        if (majorPsm.Score > minorPsm.Score)
                        {
                            majorPsmIndex++;
                        }
                        else
                        {
                            if (majorPsm.PsmFdrInfo.QValue > minorPsm.PsmFdrInfo.QValue)
                            {
                                minorPsm.PsmFdrInfo.QValue = majorPsm.PsmFdrInfo.QValue;
                            }
                            minorPsmIndex++;
                        }
                    }
                    //wrap up if we hit the end of the major category
                    while (minorPsmIndex < minorCategoryPsms.Count)
                    {
                        SpectralMatch majorPsm = majorCategoryPsms[majorPsmIndex - 1]; //-1 because it's out of index right now
                        SpectralMatch minorPsm = minorCategoryPsms[minorPsmIndex];
                        if (majorPsm.PsmFdrInfo.QValue > minorPsm.PsmFdrInfo.QValue)
                        {
                            minorPsm.PsmFdrInfo.QValue = majorPsm.PsmFdrInfo.QValue;
                        }
                        minorPsmIndex++;
                    }
                }
            }

            int numTotalSpectraWithPrecursors = AllPsms[indexesOfInterest[0]].Count;
            List<SpectralMatch> bestPsmsList = new List<SpectralMatch>();
            for (int i = 0; i < numTotalSpectraWithPrecursors; i++)
            {
                SpectralMatch bestPsm = null;
                double lowestQ = double.MaxValue;
                int bestIndex = -1;
                foreach (int index in indexesOfInterest) //foreach category
                {
                    SpectralMatch currentPsm = AllPsms[index][i];
                    if (currentPsm != null)
                    {
                        double currentQValue = currentPsm.PsmFdrInfo.QValue;
                        if (currentQValue < lowestQ //if the new one is better
                            || (currentQValue == lowestQ && currentPsm.Score > bestPsm.Score))
                        {
                            if (bestIndex != -1)
                            {
                                //remove the old one so we don't use it for fdr later
                                AllPsms[bestIndex][i] = null;
                            }
                            bestPsm = currentPsm;
                            lowestQ = currentQValue;
                            bestIndex = index;
                        }
                        else //remove the old one so we don't use it for fdr later
                        {
                            AllPsms[index][i] = null;
                        }
                    }
                }
                if (bestPsm != null)
                {
                    bestPsmsList.Add(bestPsm);
                }
            }

            //It's probable that psms from some categories were removed by psms from other categories.
            //however, the fdr is still affected by their presence, since it was calculated before their removal.
            foreach (List<SpectralMatch> psmsArray in AllPsms)
            {
                if (psmsArray != null)
                {
                    List<SpectralMatch> cleanedPsmsArray = psmsArray.Where(b => b != null).OrderByDescending(b => b.Score)
                       .ThenBy(b => b.BioPolymerWithSetModsMonoisotopicMass.HasValue ? Math.Abs(b.ScanPrecursorMass - b.BioPolymerWithSetModsMonoisotopicMass.Value) : double.MaxValue)
                       .ToList();

                    new FdrAnalysisEngine(cleanedPsmsArray, numNotches, commonParameters, fileSpecificParameters, new List<string> { taskId }).Run();
                }
            }

            return bestPsmsList.OrderBy(b => b.PsmFdrInfo.QValue).ThenByDescending(b => b.Score).ToList();
        }

        public static List<Modification> GetVariableTerminalMods(FragmentationTerminus fragmentationTerminus, List<Modification> variableModifications)
        {
            string terminalStringToFind = fragmentationTerminus == FragmentationTerminus.N ? "C-terminal" : "N-terminal"; //if singleN, want to find c-terminal mods and vice-versa
            return variableModifications == null ?
                new List<Modification>() :
                variableModifications.Where(x => x.LocationRestriction.Contains(terminalStringToFind)).ToList();
        }

        public static Dictionary<int, List<Modification>> GetTerminalModPositions(PeptideWithSetModifications peptide, IDigestionParams digestionParams, List<Modification> variableMods)
        {
            Dictionary<int, List<Modification>> annotatedTerminalModDictionary = new Dictionary<int, List<Modification>>();
            bool nTerminus = digestionParams.FragmentationTerminus == FragmentationTerminus.N; //is this the singleN or singleC search?

            //determine the start and end index ranges when considering the minimum peptide length
            int startResidue = nTerminus ?
                peptide.OneBasedStartResidue + digestionParams.MinLength - 1 :
                peptide.OneBasedStartResidue;
            int endResidue = nTerminus ?
                peptide.OneBasedEndResidue :
                peptide.OneBasedEndResidue - digestionParams.MinLength + 1;
            string terminalStringToFind = nTerminus ? "C-terminal" : "N-terminal"; //if singleN, want to find c-terminal mods and vice-versa

            //get all the mods for this protein
            IDictionary<int, List<Modification>> annotatedModsForThisProtein = peptide.Protein.OneBasedPossibleLocalizedModifications;
            //get the possible annotated mods for this peptide
            List<int> annotatedMods = annotatedModsForThisProtein.Keys.Where(x => x >= startResidue && x <= endResidue).ToList();

            foreach (int index in annotatedMods)
            {
                //see which mods are terminal (if any)
                List<Modification> terminalModsHere = annotatedModsForThisProtein[index].Where(x => x.LocationRestriction.Contains(terminalStringToFind) && x.MonoisotopicMass.HasValue).ToList();

                //if there were terminal mods, record where in the peptide they were and which mods
                if (terminalModsHere.Count != 0)
                {
                    if (nTerminus)
                    {
                        annotatedTerminalModDictionary.Add(index - peptide.OneBasedStartResidue + 1, terminalModsHere);
                    }
                    else
                    {
                        annotatedTerminalModDictionary.Add(peptide.OneBasedEndResidue - index + 1, terminalModsHere);
                    }
                }
            }

            //add variable modifications
            foreach (Modification mod in variableMods)
            {
                string modAminoAcid = mod.Target.ToString();
                int index = peptide.BaseSequence.IndexOf(modAminoAcid);
                while (index != -1)
                {
                    index++;//if index == 0, length is 1
                    if (nTerminus)
                    {
                        //if singleN, then we're looking at C-terminal
                        if (index >= digestionParams.MinLength)
                        {
                            if (annotatedTerminalModDictionary.ContainsKey(index))
                            {
                                annotatedTerminalModDictionary[index].Add(mod);
                            }
                            else
                            {
                                annotatedTerminalModDictionary.Add(index, new List<Modification> { mod });
                            }
                        }
                    }
                    else
                    {
                        int fragmentIndex = peptide.BaseSequence.Length - index + 1; //if index == 0, length should be the peptide length
                        //if singleC, then we're looking at N-terminal
                        if (fragmentIndex >= digestionParams.MinLength)
                        {
                            if (annotatedTerminalModDictionary.ContainsKey(fragmentIndex))
                            {
                                annotatedTerminalModDictionary[fragmentIndex].Add(mod);
                            }
                            else
                            {
                                annotatedTerminalModDictionary.Add(fragmentIndex, new List<Modification> { mod });
                            }
                        }
                        //see if there are more motifs
                    }
                    //see if there are more motifs
                    int subIndex = peptide.BaseSequence.Substring(index).IndexOf(modAminoAcid);
                    index = subIndex == -1 ? -1 : index + subIndex;
                }
            }

            return annotatedTerminalModDictionary;
        }
    }
}