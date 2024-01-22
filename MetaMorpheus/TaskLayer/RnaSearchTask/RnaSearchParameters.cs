using System.Collections.Generic;
using MassSpectrometry;
using MzLibUtil;
using Transcriptomics;
using Transcriptomics.Digestion;
using UsefulProteomicsDatabases;
using IHasChemicalFormula = Chemistry.IHasChemicalFormula;

namespace TaskLayer;

public class RnaSearchParameters
{

    #region Remove Eventually
    public bool MatchMs1 { get; set; }
    public bool MatchMs2 { get; set; }
    public int MinScanId { get; set; }
    public int MaxScanId { get; set; }
    public bool MatchAllCharges { get; set; }
    public bool MatchAllScans { get; set; }

    #endregion

    #region SearchTask Build Stuff

    public bool DisposeOfFileWhenDone { get; set; } 
    public MassDiffAcceptorType MassDiffAcceptorType { get; set; }
    public string CustomMdac { get; set; }
    public PpmTolerance FragmentIonTolerance { get; set; }
    public PpmTolerance PrecursorMassTolerance { get; set; }
    public DecoyType DecoyType { get; set; }
    public RnaDigestionParams DigestionParams { get; set; }

    //public IHasChemicalFormula CustomThreePrimeCapForDatabaseReading { get; set; }
    //public IHasChemicalFormula CustomFivePrimeCapForDatabaseReading { get; set; }

    #endregion

    #region Output

    public bool WriteDecoys { get; set; }
    public bool WriteContaminants { get; set; }
    public bool WriteAmbiguous { get; set; }
    public bool WriteIndividualFiles { get; set; }

    public Dictionary<string, int> ModsToWriteSelection { get; set; }

    #endregion

    public RnaSearchParameters(bool matchMs1 = false, bool matchMs2 = true, bool matchCharges = false, int minScanId = 1,
        int maxScanId = 100, double fragmentTolerance = 20, double precursorTolerance = 20, bool matchAllScans = true, Dictionary<string, int> modsToWrite = null)
    {
        DisposeOfFileWhenDone = true;
        DecoyType = DecoyType.None;


        MatchMs1 = matchMs1;
        MatchMs2 = matchMs2;
        MatchAllCharges = matchCharges;
        MinScanId = minScanId;
        MaxScanId = maxScanId;
        FragmentIonTolerance = new PpmTolerance(fragmentTolerance);
        PrecursorMassTolerance = new PpmTolerance(precursorTolerance);
        MatchAllScans = matchAllScans;
        WriteDecoys = true;
        WriteContaminants = true;
        WriteAmbiguous = true;
        WriteIndividualFiles = true;
        ModsToWriteSelection = modsToWrite ?? new Dictionary<string, int>();
    }


    public IEnumerable<MsDataScan> GetFilteredScans(List<MsDataScan> scans)
    {
        foreach (var scanned in scans)
        {
            if (MatchAllScans)
            {
                switch (scanned.MsnOrder)
                {
                    case 1 when MatchMs1:
                        yield return scanned;
                        break;
                    case 2 when MatchMs2:
                        yield return scanned;
                        break;
                }
            }
            else if (scanned.OneBasedScanNumber >= MinScanId && scanned.OneBasedScanNumber <= MaxScanId)
            {
                switch (scanned.MsnOrder)
                {
                    case 1 when MatchMs1:
                        yield return scanned;
                        break;
                    case 2 when MatchMs2:
                        yield return scanned;
                        break;
                }
            }
        }
    }
}