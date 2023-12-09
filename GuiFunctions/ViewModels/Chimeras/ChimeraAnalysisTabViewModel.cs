using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            ChimeraGroupViewModels = ConstructChimericPsms(allPsms, dataFiles).ToList();
        }

        private IEnumerable<ChimeraGroupViewModel> ConstructChimericPsms(List<PsmFromTsv> psms, Dictionary<string, MsDataFile> dataFiles)
        {
            foreach (var group in psms.Where(p => p.QValue <= 0.01 && p.DecoyContamTarget == "T")
                         .GroupBy(p => p, ChimeraAnalysisTabViewModel.ChimeraComparer)
                         .Where(p => p.Count() >= MetaDrawSettings.MinChimera)
                         .OrderByDescending(p => p.Count()))
            {
                // get the scan
                if (!dataFiles.TryGetValue(group.First().FileNameWithoutExtension, out MsDataFile spectraFile))
                    continue;

                var ms1Scan = spectraFile.GetOneBasedScanFromDynamicConnection(group.First().PrecursorScanNum);
                var ms2Scan = spectraFile.GetOneBasedScanFromDynamicConnection(group.First().Ms2ScanNumber);

                if (ms1Scan == null || ms2Scan == null)
                    continue;
                var groupVm = new ChimeraGroupViewModel(ms2Scan, ms1Scan, group.ToList());
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
