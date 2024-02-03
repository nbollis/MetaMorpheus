using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Easy.Common.Interfaces;
using MassSpectrometry;
using Microsoft.FSharp.Core;
using NUnit.Framework;
using Readers;

namespace Test
{

    public static class DataFileExtensions
    {
        /// <summary>
        /// Returns the fragmentation efficiency of the MS1 scans in the data file
        /// </summary>
        /// <param name="dataFile"></param>
        /// <returns>
        /// a 2D array with one row for each MS1 scan. The columns are:
        /// Ms1 Scan Number
        /// MS1 Total Ion Current
        /// MS1 Isolated Ion Current
        /// MS1 Non-Isolated Ion Current
        /// </returns>
        public static double[][] GetFragmentationEfficiency(this MsDataFile dataFile)
        {
            if (!dataFile.CheckIfScansLoaded())
                dataFile.LoadAllStaticData();

            var ms1ScansDictionary = dataFile.Scans.Where(p => p.MsnOrder == 1)
                .ToDictionary(p => p.OneBasedScanNumber, scan => new double[4] {scan.OneBasedScanNumber, scan.TotalIonCurrent, 0, scan.TotalIonCurrent} );

            
            foreach (var ms2Scans in dataFile.Scans.Where(p => p.MsnOrder > 1)
                         .GroupBy(ms2Scan => ms2Scan.OneBasedPrecursorScanNumber))
            {

                var precursorScanNum = ms2Scans.First().OneBasedPrecursorScanNumber;
                if (!precursorScanNum.HasValue)
                    continue;

                var precursorScan = dataFile.GetOneBasedScan(precursorScanNum.Value);
                foreach (var scan in ms2Scans)
                {
                    var isolatedIntensity = precursorScan.MassSpectrum.Extract(scan.IsolationRange.Minimum, scan.IsolationRange.Maximum)
                                .Sum(peak => peak.Intensity);
                    ms1ScansDictionary[precursorScanNum.Value][2] += isolatedIntensity;
                    ms1ScansDictionary[precursorScanNum.Value][3] -= isolatedIntensity;
                }
               
            }
            return ms1ScansDictionary.Values.ToArray();
        }


        [Test]
        public static void TESTNAME2()
        {
            string path =
                @"D:\Projects\SpectralAveraging\PaperTestOutputs\ChimeraImages\ForWout\Input\02-18-20_jurkat_td_rep2_fract6-calib-averaged.mzML";
            var dataFile = MsDataFileReader.GetDataFile(path);


            string outPath =
                @"C:\Users\Nic\Downloads\fragEfficiency.csv";
            using (StreamWriter output = new StreamWriter(outPath))
            {
                output.WriteLine("MS1 Scan Number,MS1 TIC,Isolated IC,NonIoslated IC,Relative Used,Relative Unused");
                foreach (var item in dataFile.GetFragmentationEfficiency())
                {
                    output.WriteLine(string.Join(',', item) + $"{item[2] / item[1]},{item[3] / item[1]}");
                }
            }
        }
    }
}
