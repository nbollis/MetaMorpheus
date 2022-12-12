using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IO.MzML;
using MassSpectrometry;
using NUnit.Framework;

namespace Test
{
    public static class FigureOutWhatISGoingOn
    {
        [Test]
        public static void ModernSearchOnMyCAShit()
        {
            string spectraPath = @"D:\Projects\Top Down MetaMorpheus\ChimeraValidation\CaMyoUbiqCytCHgh\Sample28_Avg(20)CaliOpenModern\Task2-CalibrateTask\221110_CaMyoUbiqCytCHgh_130541641_5%_Sample28_25IW_-averaged-calib.mzML";
            MsDataFile myMSDataFile = Mzml.LoadAllStaticData(spectraPath);
            var msNScans = myMSDataFile.GetAllScansList().Where(x => x.MsnOrder > 1).ToArray();
            var ms2Scans = msNScans.Where(p => p.MsnOrder == 2).ToArray();
            var ms3Scans = msNScans.Where(p => p.MsnOrder == 3).ToArray();

            var ms2Scan = myMSDataFile.GetOneBasedScan(12);
            var precursorScan = myMSDataFile.GetOneBasedScan(ms2Scan.OneBasedPrecursorScanNumber.Value);


            ms2Scan.RefineSelectedMzAndIntensity(precursorScan.MassSpectrum);

            var envelopes = ms2Scan.GetIsolatedMassesAndCharges(precursorScan.MassSpectrum, 1, 60, 4, 3);

            var temp = envelopes.GroupBy(p => p.Charge).ToDictionary(p => p.Key, p => p.Count());
            

        }
    }
}
