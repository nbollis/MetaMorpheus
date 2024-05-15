using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Easy.Common.Extensions;
using EngineLayer;
using MassSpectrometry;
using Omics;
using Omics.Fragmentation;
using Omics.Modifications;
using Omics.SpectrumMatch;
using OxyPlot.Wpf;
using Proteomics;
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
            set { _dataFiles = value; OnPropertyChanged(nameof(DataFiles)); }
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
            set { _dataScans = value; OnPropertyChanged(nameof(MsnDataScans)); }
        }

        private MsDataScan _selectedScan;
        public MsDataScan SelectedScan
        {
            get => _selectedScan;
            set
            {
                _selectedScan = value;
                if (!PrimarySequence.IsNullOrEmpty())
                {
                    var fixedModsToUse = ModsToUse.Where(p => p.Use).ToList();
                    if (fixedModsToUse.Any())
                    {
                        var motifs = fixedModsToUse.Select(p => p.ModName.Split("on").Last().Trim().Last()).Distinct().ToList();
                        int modCount = PrimarySequence.Count(p => p.Equals('['));
                        int motifCount = Regex.Replace(PrimarySequence, @"\[.*?\]", "").Count(p => motifs.Any(motif => motif.Equals(p)));
                        if (motifCount > modCount)
                        {
                            var digestionparams = new DigestionParams("top-down", maxModsForPeptides: 20);
                            var baseSeq = IBioPolymerWithSetMods.GetBaseSequenceFromFullSequence(PrimarySequence);
                            var pwsm = new PeptideWithSetModifications(PrimarySequence,
                                GlobalVariables.AllModsKnownDictionary, motifCount, digestionparams, new Protein(baseSeq, "tacos"), 1,
                                baseSeq.Length);
                            List<Modification> fixedMods = GlobalVariables.AllModsKnown.Where(p =>
                                p.ModificationType == "Common Fixed" &&
                                fixedModsToUse.Select(fm => fm.ModName).Contains(p.IdWithMotif)).ToList();
                            var peps = pwsm.GetModifiedPeptides(fixedMods,
                                digestionparams, new List<Modification>());
                            var seq = peps.First().FullSequence;
                            PrimarySequence = seq;
                        }
                    }
                    SpectrumMatch = new PsmFromTsv(PrimarySequence, SelectedScan, new List<MatchedFragmentIon>());
                    var ions = FragmentationReanalysisViewModel.MatchIonsWithNewTypes(SelectedScan, SpectrumMatch);
                    SpectrumMatch.MatchedIons = ions;
                }
                OnPropertyChanged(nameof(SelectedScan));
            }
        }

        #endregion

        #region Protein Sequence and Digestion Information

        private string _primatrySequence;
        public string PrimarySequence
        {
            get => _primatrySequence;
            set { _primatrySequence = value; OnPropertyChanged(nameof(PrimarySequence)); }
        }

        private PsmFromTsv _spectrumMatch;
        public PsmFromTsv SpectrumMatch
        {
            get => _spectrumMatch;
            set { _spectrumMatch = value; OnPropertyChanged(nameof(SpectrumMatch)); }
        }

        private ObservableCollection<ModForTreeViewModel> _modsToUse;
        public ObservableCollection<ModForTreeViewModel> ModsToUse
        {
            get => _modsToUse;
            set { _modsToUse = value; OnPropertyChanged(nameof(ModsToUse)); }
        }

        #endregion

        private FragmentationReanalysisViewModel _fragmentationReanalysisViewModel;

        public FragmentationReanalysisViewModel FragmentationReanalysisViewModel
        {
            get => _fragmentationReanalysisViewModel;
            set { _fragmentationReanalysisViewModel = value; OnPropertyChanged(nameof(FragmentationReanalysisViewModel)); }
        }

        public FragmentExplorerTabViewModel(FragmentationReanalysisViewModel fragmentVm)
        {
            FragmentationReanalysisViewModel = fragmentVm;
            ModsToUse = new();
            GlobalVariables.AllModsKnown.Where(p => p.ModificationType == "Common Fixed").ForEach(mod =>
                ModsToUse.Add(new ModForTreeViewModel(mod.ToString(), false, mod.IdWithMotif, false, null)));
        }

        public void RefreshMetaDrawLogic(MetaDrawLogic metaDrawLogic)
        {
            DataFiles = new ObservableCollection<MsDataFile>(metaDrawLogic.MsDataFiles.Values);

        }
    }

    /// <summary>
    /// Class only exists to inform the tab item what view model to use and properties available to it
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class FragmentExplorerTabModel : FragmentExplorerTabViewModel
    {
        public static FragmentExplorerTabModel Instance => new FragmentExplorerTabModel();

        public FragmentExplorerTabModel() : base(new FragmentationReanalysisViewModel())
        {
        }
    }
}
