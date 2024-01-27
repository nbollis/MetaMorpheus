using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using EngineLayer;
using GuiFunctions.MetaDraw.SpectrumMatch;
using MassSpectrometry;
using Nett;
using TaskLayer;

namespace GuiFunctions
{
    public class ChimeraAnalysisTabViewModel : BaseViewModel
    {
        public ChimeraSpectrumMatchPlot ChimeraSpectrumMatchPlot { get; set; }
        public Ms1ChimeraPlot Ms1ChimeraPlot { get; set; }
        public List<ChimeraGroupViewModel> ChimeraGroupViewModels { get; set; }

        private ChimeraGroupViewModel _selectedChimeraGroup;
        public ChimeraGroupViewModel SelectedChimeraGroup
        {
            get => _selectedChimeraGroup;
            set
            {
                _selectedChimeraGroup = value;
                ChimeraLegendViewModel.ChimeraLegendItems = value.LegendItems;
                OnPropertyChanged(nameof(SelectedChimeraGroup));
            }
        }

        private ChimeraLegendViewModel _chimeraLegendViewModel;
        public ChimeraLegendViewModel ChimeraLegendViewModel
        {
            get => _chimeraLegendViewModel;
            set
            {
                _chimeraLegendViewModel = value;
                OnPropertyChanged(nameof(ChimeraLegendViewModel));
            }
        }

        public ChimeraAnalysisTabViewModel(List<PsmFromTsv> allPsms, Dictionary<string, MsDataFile> dataFiles)
        {
            ChimeraGroupViewModels = ConstructChimericPsms(allPsms, dataFiles)
                .OrderByDescending(p => p.Count)
                .ThenByDescending(p => p.ChimeraScore)
                .ToList();
            ChimeraLegendViewModel = new ChimeraLegendViewModel();
            ExportAsSvgCommand = new RelayCommand(ExportAsSvg);
            new Thread(() => BackgroundLoader()) { IsBackground = true }.Start();
        }


        public ICommand ExportAsSvgCommand { get; set; }
        private static string _directory = @"D:\Projects\SpectralAveraging\PaperTestOutputs\ChimeraImages";
        private void ExportAsSvg()
        {
            string path = Path.Combine(_directory, 
                $"{SelectedChimeraGroup.FileNameWithoutExtension}_{SelectedChimeraGroup.Ms1Scan.OneBasedScanNumber}_{SelectedChimeraGroup.Count}.svg");
            Ms1ChimeraPlot.ExportToSvg(path, (int)Ms1ChimeraPlot.Model.Width, (int)Ms1ChimeraPlot.Model.Height);
        }

        private async Task BackgroundLoader()
        {
            foreach (var chimeraGroup in ChimeraGroupViewModels)
            {
                var temp = chimeraGroup.PrecursorIonsByColor;
            }
        }

        private IEnumerable<ChimeraGroupViewModel> ConstructChimericPsms(List<PsmFromTsv> psms, Dictionary<string, MsDataFile> dataFiles)
        {
            //// TODO: Delet this when done with stuffs
            //int[] acceptableScans = new[] { 2461, 2477, 2513, 2984, 3089, 2367, 2227, 2477, 2461, 2477, 2516, 3008 };

            foreach (var group in psms.Where(p => p.QValue <= 0.01 && p.DecoyContamTarget == "T")
                         .GroupBy(p => p, ChimeraComparer)
                         .Where(p => p.Count() >= MetaDrawSettings.MinChimera /*|| acceptableScans.Any(m => p.First().PrecursorScanNum == m)*/)
                         .OrderByDescending(p => p.Count()))
            {
                // get the scan
                if (!dataFiles.TryGetValue(group.First().FileNameWithoutExtension, out MsDataFile spectraFile))
                    continue;

                var ms1Scan = spectraFile.GetOneBasedScanFromDynamicConnection(group.First().PrecursorScanNum);
                var ms2Scan = spectraFile.GetOneBasedScanFromDynamicConnection(group.First().Ms2ScanNumber);

                if (ms1Scan == null || ms2Scan == null)
                    continue;
                var groupVm = new ChimeraGroupViewModel(ms2Scan, ms1Scan, group.OrderBy(p => p.PrecursorMz).ToList());
                if (groupVm.ChimericPsms.Count > 0)
                    yield return groupVm;
            }
        }


        #region Static

        public static Func<PsmFromTsv, object>[] ChimeraSelector =
        {
            psm => psm.PrecursorScanNum,
            psm => psm.Ms2ScanNumber,
            psm => psm.FileNameWithoutExtension.Replace("-averaged", "")
        };
        public static CustomComparer<PsmFromTsv> ChimeraComparer = new CustomComparer<PsmFromTsv>(ChimeraSelector);

        private static CommonParameters _commonParameters { get; set; }
        public static CommonParameters CommonParameters => _commonParameters ?? Toml
            .ReadFile<SearchTask>(
                @"C:\Users\Nic\OneDrive - UW-Madison\AUSTIN V CARR - AUSTIN V CARR's files\SpectralAveragingPaper\Supplemental Information\Tasks\SupplementalFile4_Task4-SearchTaskconfig.toml",
                MetaMorpheusTask.tomlConfig).CommonParameters;

        #endregion
    }

    
}
