using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Easy.Common.Extensions;
using EngineLayer;
using MassSpectrometry;
using Omics;
using Omics.Fragmentation;
using Omics.SpectrumMatch;
using OxyPlot.Wpf;
using Proteomics.ProteolyticDigestion;

namespace GuiFunctions
{
    public class FragmentExplorerTabViewModel : BaseViewModel
    {

        #region Data File Selection Properties

        private ObservableCollection<MsDataFile> _dataFiles;
        public ObservableCollection<MsDataFile> DataFiles
        {
            get => _dataFiles;
            set
            {
                _dataFiles = value;
                OnPropertyChanged(nameof(DataFiles));
            }
        }

        private MsDataFile _selectedDataFile;
        public MsDataFile SelectedDataFile
        {
            get => _selectedDataFile;
            set
            {
                _selectedDataFile = value;
                if (SelectedDataFile.Scans == null) ;
                    SelectedDataFile.LoadAllStaticData();
                MsnDataScans = new ObservableCollection<MsDataScan>(SelectedDataFile.Scans!.Where(p => p.MsnOrder > 1));
                OnPropertyChanged(nameof(SelectedDataFile));
            }
        }

        private ObservableCollection<MsDataScan> _dataScans;

        public ObservableCollection<MsDataScan> MsnDataScans
        {
            get => _dataScans;
            set
            {
                _dataScans = value;
                OnPropertyChanged(nameof(MsnDataScans));
            }
        }

        private MsDataScan _selectedScan;

        public MsDataScan SelectedScan
        {
            get => _selectedScan;
            set
            {
                _selectedScan = value;
                SpectrumMatch = new PsmFromTsv(PrimarySequence, SelectedScan, new List<MatchedFragmentIon>());
                var ions = FragmentationReanalysisViewModel.MatchIonsWithNewTypes(SelectedScan, SpectrumMatch);
                SpectrumMatch.MatchedIons = ions;
                OnPropertyChanged(nameof(SelectedScan));
            }
        }

        #endregion

        #region Protein Sequence Information

        private string _primatrySequence;

        public string PrimarySequence
        {
            get => _primatrySequence;
            set
            {
                _primatrySequence = value;
                OnPropertyChanged(nameof(PrimarySequence));
            }
        }

        private PsmFromTsv _spectrumMatch;
        public PsmFromTsv SpectrumMatch
        {
            get => _spectrumMatch;
            set
            {
                _spectrumMatch = value;
                OnPropertyChanged(nameof(SpectrumMatch));
            }
        }

        #endregion

        private FragmentationReanalysisViewModel _fragmentationReanalysisViewModel;

        public FragmentationReanalysisViewModel FragmentationReanalysisViewModel
        {
            get => _fragmentationReanalysisViewModel;
            set
            {
                _fragmentationReanalysisViewModel = value;
                OnPropertyChanged(nameof(FragmentationReanalysisViewModel));
            }
        }

        public FragmentExplorerTabViewModel(FragmentationReanalysisViewModel fragmentVm)
        {
            FragmentationReanalysisViewModel = fragmentVm;
        }

        public void RefreshMetaDrawLogic(MetaDrawLogic metaDrawLogic)
        {
            DataFiles = new ObservableCollection<MsDataFile>(metaDrawLogic.MsDataFiles.Values);
        }
    }

    public class FragmentExplorerTabModel : FragmentExplorerTabViewModel
    {
        public static FragmentExplorerTabModel Instance => new FragmentExplorerTabModel();

        public FragmentExplorerTabModel() : base(new FragmentationReanalysisViewModel())
        {
        }
    }
}
