using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Easy.Common.Extensions;
using EngineLayer;
using GuiFunctions;
using GuiFunctions.MetaDraw.SpectrumMatch;
using NUnit.Framework;
using OxyPlot.Wpf;
using Readers;

namespace Test.AveragingPaper
{
    internal class TestChimeraGroupVM
    {
        [Test]
        [Apartment(ApartmentState.STA)]
        public static void TESTNAME()
        {
            string psmPath =
                @"D:\Projects\SpectralAveraging\PaperTestOutputs\ChimeraImages\ForWout\Input\SI Workbook 3_PrSMs_CalibAveraged.psmtsv";
            List<string> dataFilePaths = new()
            {

                @"D:\Projects\SpectralAveraging\PaperTestOutputs\ChimeraImages\ForWout\Input\02-18-20_jurkat_td_rep2_fract6-calib-averaged.mzML"
            };
            var dataFiles = dataFilePaths.ToDictionary(Path.GetFileNameWithoutExtension, MsDataFileReader.GetDataFile);
            dataFiles.ForEach(p => p.Value.InitiateDynamicConnection());

            var ms1Scan = dataFiles.First().Value.GetOneBasedScan(2568);
            var ms2Scan = dataFiles.First().Value.GetOneBasedScan(2571);
            var psmsForScan = PsmTsvReader.ReadTsv(psmPath, out var warnings)
                .Where(p => 
                    p.PrecursorScanNum == 2568 
                    && p.Ms2ScanNumber == 2571 
                    && p.QValue <= 0.01
                    && p.DecoyContamTarget == "T"
                    && dataFilePaths.Select(Path.GetFileNameWithoutExtension).Contains(p.FileNameWithoutExtension))
                .ToList();

            var temp = new ChimeraGroupViewModel(ms2Scan, ms1Scan, psmsForScan);

            var test = new ChimeraDrawnSequence(new Canvas(), temp);
            test.Export(@"D:\Projects\SpectralAveraging\PaperTestOutputs\ChimeraImages\test.png");


            //vm.ExportBulkCommand.Execute();

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
