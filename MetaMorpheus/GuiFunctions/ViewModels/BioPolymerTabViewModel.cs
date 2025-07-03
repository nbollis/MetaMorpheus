using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using MathNet.Numerics;
using MetaMorpheusGUI;
using Omics;
using Omics.SpectrumMatch;
using TaskLayer;
using MzLibUtil;
using OxyPlot;
using Readers;
using UsefulProteomicsDatabases;

namespace GuiFunctions
{
    public class BioPolymerTabViewModel : BaseViewModel
    {
        private List<IBioPolymer> _allBioPolymers;
        private readonly MetaDrawLogic _metaDrawLogic;

        public BioPolymerTabViewModel(MetaDrawLogic metaDrawLogic, MetaDrawSettingsViewModel settingsViewModel)
        {
            _metaDrawLogic = metaDrawLogic;
            SettingsViewModel = settingsViewModel;

            IsDatabaseLoaded = false;
            _allBioPolymers = new List<IBioPolymer>();
            AllGroups = new ObservableCollection<BioPolymerGroupViewModel>();

            LoadDatabaseCommand = new RelayCommand(LoadDatabase);
            ResetDatabaseCommand = new RelayCommand(ResetDatabase);
            GroupBioPolymersCommand = new RelayCommand(GroupBioPolymers);
            ExportImageCommand = new RelayCommand(ExportImage);
            FilterResultsCommand = new RelayCommand(FilterResults);
        }


        #region Database Loading Handling

        private bool _isDatabaseLoaded;
        public bool IsDatabaseLoaded
        {
            get => _isDatabaseLoaded;
            set
            {
                _isDatabaseLoaded = value; OnPropertyChanged(nameof(IsDatabaseLoaded));
            }
        }

        private string _databasePath;
        public string DatabasePath
        {
            get => _databasePath;
            set
            {
                _databasePath = value; OnPropertyChanged(nameof(DatabasePath));
                DatabaseName =
                    PeriodTolerantFilenameWithoutExtension.GetPeriodTolerantFilenameWithoutExtension(_databasePath);
            }
        }

        private string _databaseName;
        public string DatabaseName
        {
            get => _databaseName;
            set
            {
                _databaseName = value; OnPropertyChanged(nameof(DatabaseName));
            }
        }

        #endregion

        public ObservableCollection<BioPolymerGroupViewModel> AllGroups { get; set; }

        private BioPolymerGroupViewModel _selectedGroup;
        public BioPolymerGroupViewModel SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                _selectedGroup = value; 
                OnPropertyChanged(nameof(SelectedGroup));
            }
        }

        private MetaDrawSettingsViewModel _settingsViewModel;

        /// <summary>
        /// The same instance as is housed in MetaDraw.xaml.cs
        /// </summary>
        public MetaDrawSettingsViewModel SettingsViewModel
        {
            get => _settingsViewModel;
            set
            {
                _settingsViewModel = value; 
                OnPropertyChanged(nameof(SettingsViewModel));
            }
        }

        public ICommand LoadDatabaseCommand { get; set; }
        public ICommand ResetDatabaseCommand { get; set; }
        public ICommand GroupBioPolymersCommand { get; set; }
        public ICommand ExportImageCommand { get; set; }
        public ICommand FilterResultsCommand { get; set; }

        #region Command Methods

        private void LoadDatabase()
        {
            if (DatabasePath is null or "")
                return;
            try
            {
                _allBioPolymers = new SearchTask().LoadBioPolymers("", new()
                    { new DbForTask(DatabasePath, false) }, true, DecoyType.None, new(), new());
                if (_allBioPolymers.Count != 0)
                {
                    IsDatabaseLoaded = true;
                    GroupBioPolymers();   
                }
            }
            catch (Exception e)
            {
                // do nothing
            }
        }

        private void ResetDatabase()
        {
            DatabasePath = "";
            _allBioPolymers.Clear();
            AllGroups.Clear();
        }

        private void GroupBioPolymers()
        {
            if (_metaDrawLogic.FilteredListOfPsms.Count == 0 || !IsDatabaseLoaded)
                return;

            var spectralMatches = _metaDrawLogic.FilteredListOfPsms;

            // group the spectral matches by accession and pull the sequence of the biopolymer it belongs to  
            var groupedMatches = new Dictionary<string, List<SpectrumMatchFromTsv>>();

            foreach (var match in spectralMatches)
            {
                if (!groupedMatches.ContainsKey(match.Accession))
                {
                    groupedMatches[match.Accession] = new List<SpectrumMatchFromTsv>();
                }
                groupedMatches[match.Accession].Add(match);
            }

            AllGroups.Clear();
            foreach (var group in groupedMatches)
            {
                var bioPolymer = _allBioPolymers.FirstOrDefault(bp => bp.Accession == group.Key);
                if (bioPolymer != null)
                {
                    double seqCov = CalculateSequenceCoverage(bioPolymer.BaseSequence, group.Value).Round(2);
                    var groupViewModel = new BioPolymerGroupViewModel(group.Key,  bioPolymer.BaseSequence, group.Value, seqCov);
                    AllGroups.Add(groupViewModel);
                }
            }
        }

        private void FilterResults()
        {
            _metaDrawLogic.FilterPsms();
            GroupBioPolymers();
        }

        private void ExportImage()
        {

        }

        #endregion

        private double CalculateSequenceCoverage(string sequence, List<SpectrumMatchFromTsv> matches)
        {
            var coveredPositions = new HashSet<int>();
            foreach (var match in matches.DistinctBy(p => p.StartAndEndResiduesInParentSequence))
            {
                foreach (var positions in match.GetStartAndEndPosition())
                {
                    for (var i = positions.Start; i <= positions.End; i++)
                    {
                        coveredPositions.Add(i);
                    }
                }
            }
            return (double)coveredPositions.Count / sequence.Length * 100;
        }
    }

    public class BioPolymerGroupViewModel : BaseViewModel
    {
        public string GroupName { get; set; }
        public int GroupCount { get; set; }
        public double SequenceCoverage { get; set; }
        public string BaseSequence { get; set; }
        public int Length => BaseSequence.Length;
        public ObservableCollection<SpectrumMatchFromTsv> AllSpectrumMatches { get; set; }

        public BioPolymerGroupViewModel(string accession, string baseSequence, List<SpectrumMatchFromTsv> allMatches, double sequenceCoverage)
        {
            BaseSequence = baseSequence;
            GroupName = accession;
            GroupCount = allMatches.Count;
            SequenceCoverage = sequenceCoverage;
            AllSpectrumMatches = new ObservableCollection<SpectrumMatchFromTsv>(allMatches);
        }
    }
}
