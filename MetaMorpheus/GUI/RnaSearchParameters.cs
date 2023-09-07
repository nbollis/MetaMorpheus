using System.Collections.Generic;
using MassSpectrometry;
using MzLibUtil;

namespace MetaMorpheusGUI;

public class RnaSearchParameters
{
    public bool MatchMs1 { get; set; }
    public bool MatchMs2 { get; set; }
    public int MinScanId { get; set; }
    public int MaxScanId { get; set;}
    public bool MatchAllCharges { get; set; }
    public Tolerance FragmentIonTolerance { get; set; }

    public RnaSearchParameters(bool matchMs1 = false, bool matchMs2 = true, bool matchCharges = false, int minScanId = 1,
        int maxScanId = 100, double tolerance = 10)
    {
        MatchMs1 = matchMs1;
        MatchMs2 = matchMs2;
        MatchAllCharges = matchCharges;
        MinScanId = minScanId;
        MaxScanId = maxScanId;
        FragmentIonTolerance = new PpmTolerance(tolerance);
    }

    public IEnumerable<MsDataScan> GetFilteredScans(List<MsDataScan> scans)
    {
        foreach (var scanned in scans)
        {
            if (scanned.OneBasedScanNumber >= MinScanId && scanned.OneBasedScanNumber <= MaxScanId)
            {
                if (scanned.MsnOrder == 1 && MatchMs1)
                    yield return scanned;
                if (scanned.MsnOrder == 2 && MatchMs2) 
                    yield return scanned;
            }
        }
    }
}