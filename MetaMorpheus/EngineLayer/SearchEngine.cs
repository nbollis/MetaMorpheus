using System.Collections.Generic;
using EngineLayer.Util;
using Omics;
using Omics.Fragmentation;

namespace EngineLayer;

public abstract class SearchEngine : MetaMorpheusEngine
{
    protected readonly object[] Locks;
    protected readonly SpectralMatch[] SpectralMatches;
    protected readonly Ms2ScanWithSpecificMass[] ArrayOfSortedMS2Scans;
    protected SearchEngine(SpectralMatch[] globalPsms, Ms2ScanWithSpecificMass[] arrayOfSortedms2Scans, CommonParameters commonParameters, List<(string FileName, CommonParameters Parameters)> fileSpecificParameters, List<string> nestedIds) : base(commonParameters, fileSpecificParameters, nestedIds)
    {
        SpectralMatches = globalPsms;
        ArrayOfSortedMS2Scans = arrayOfSortedms2Scans;

        // Create one lock for each PSM to ensure thread safety
        Locks = new object[SpectralMatches.Length];
        for (int i = 0; i < Locks.Length; i++)
        {
            Locks[i] = new object();
        }
    }

    protected void AddPeptideCandidateToPsm(int scanIndex, int notch, double thisScore, IBioPolymerWithSetMods peptide, List<MatchedFragmentIon> matchedIons)
    {
        bool meetsScoreCutoff = thisScore >= CommonParameters.ScoreCutoff;

        // this is thread-safe because even if the score improves from another thread writing to this PSM,
        // the lock combined with AddOrReplace method will ensure thread safety
        if (meetsScoreCutoff)
        {
            // valid hit (met the cutoff score); lock the scan to prevent other threads from accessing it
            lock (Locks[scanIndex])
            {
                bool scoreImprovement = SpectralMatches[scanIndex] == null || (thisScore - SpectralMatches[scanIndex].RunnerUpScore) > -SpectralMatch.ToleranceForScoreDifferentiation;

                if (!scoreImprovement)
                    return;

                // if the PSM is null, create a new one; otherwise, add or replace the peptide
                if (SpectralMatches[scanIndex] == null)
                    if (GlobalVariables.AnalyteType == AnalyteType.Oligo)
                        SpectralMatches[scanIndex] = new OligoSpectralMatch(peptide, notch, thisScore, scanIndex, ArrayOfSortedMS2Scans[scanIndex], CommonParameters, matchedIons);
                    else
                        SpectralMatches[scanIndex] = new PeptideSpectralMatch(peptide, notch, thisScore, scanIndex, ArrayOfSortedMS2Scans[scanIndex], CommonParameters, matchedIons);
                else
                    SpectralMatches[scanIndex].AddOrReplace(peptide, thisScore, notch, CommonParameters.ReportAllAmbiguity, matchedIons);
            }
        }
    }

    protected void AddPeptideCandidateToPsm(ScanWithIndexAndNotchInfo scan, double thisScore, IBioPolymerWithSetMods peptide, List<MatchedFragmentIon> matchedIons) 
        => AddPeptideCandidateToPsm(scan.ScanIndex, scan.Notch, thisScore, peptide, matchedIons);

}