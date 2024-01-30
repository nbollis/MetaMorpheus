using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
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
        [STAThread]
        public static void TESTNAME()
        {
            string psmPath =
                @"D:\Projects\SpectralAveraging\PaperTestOutputs\ChimeraImages\ForWout\SelectPrSMs.psmtsv";
            List<string> dataFilePaths = new()
            {

                @"D:\Projects\SpectralAveraging\PaperTestOutputs\ChimeraImages\ForWout\Input\02-18-20_jurkat_td_rep2_fract6-calib-averaged.mzML"
            };
            var dataFiles = dataFilePaths.ToDictionary(Path.GetFileNameWithoutExtension, MsDataFileReader.GetDataFile);
            var psms = PsmTsvReader.ReadTsv(psmPath, out var warnings)
                .Where(p => dataFilePaths.Select(Path.GetFileNameWithoutExtension).Contains(p.FileNameWithoutExtension))
                .ToList();
            var vm = new ChimeraAnalysisTabViewModel(psms, dataFiles);

            var chimeraGroup = vm.ChimeraGroupViewModels.First();
            var canvas = new Canvas();
            var temp = new ChimeraDrawnSequence(canvas, chimeraGroup);


            //vm.ExportBulkCommand.Execute();

        }
    }
}
