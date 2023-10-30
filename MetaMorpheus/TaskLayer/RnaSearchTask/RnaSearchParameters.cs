using System.Collections.Generic;
using MassSpectrometry;
using MzLibUtil;

namespace TaskLayer;

public class RnaSearchParameters
{
    public bool MatchMs1 { get; set; }
    public bool MatchMs2 { get; set; }
    public int MinScanId { get; set; }
    public int MaxScanId { get; set; }
    public bool MatchAllCharges { get; set; }
    public bool MatchAllScans { get; set; }
    public Tolerance FragmentIonTolerance { get; set; }
    public Tolerance PrecursorMassTolerance { get; set; }


    #region SearchTask Build Stuff

    public bool DisposeOfFileWhenDone { get; set; } = true;
    public MassDiffAcceptorType MassDiffAcceptorType { get; set; } = MassDiffAcceptorType.OneMM;
    public string CustomMdac { get; set; } 

    #endregion

    public RnaSearchParameters(bool matchMs1 = false, bool matchMs2 = true, bool matchCharges = false, int minScanId = 1,
        int maxScanId = 100, double fragmentTolerance = 20, double precursorTolerance = 20, bool matchAllScans = true)
    {
        MatchMs1 = matchMs1;
        MatchMs2 = matchMs2;
        MatchAllCharges = matchCharges;
        MinScanId = minScanId;
        MaxScanId = maxScanId;
        FragmentIonTolerance = new PpmTolerance(fragmentTolerance);
        PrecursorMassTolerance = new PpmTolerance(precursorTolerance);
        MatchAllScans = matchAllScans;
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