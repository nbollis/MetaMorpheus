using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using MetaMorpheusGUI;
using Omics;
using Omics.SpectrumMatch;
using TaskLayer;
using MzLibUtil;
using UsefulProteomicsDatabases;

namespace GuiFunctions
{
    public class BioPolymerTabViewModel : BaseViewModel
    {
        private List<IBioPolymer> _allBioPolymers;
        private readonly MetaDrawLogic _metaDrawLogic;

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

        public ObservableCollection<BioPolymerGroupViewModel> AllGroups { get; set; }

        private BioPolymerGroupViewModel _selectedGroup;
        public BioPolymerGroupViewModel SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                _selectedGroup = value; OnPropertyChanged(nameof(SelectedGroup));
            }
        }

        public BioPolymerTabViewModel(MetaDrawLogic metaDrawLogic) : this()
        {
            _metaDrawLogic = metaDrawLogic;
        }

        public BioPolymerTabViewModel()
        {
            IsDatabaseLoaded = false;
            _allBioPolymers = new List<IBioPolymer>();
            AllGroups = new ObservableCollection<BioPolymerGroupViewModel>();

            LoadDatabaseCommand = new RelayCommand(LoadDatabase);
            ResetDatabaseCommand = new RelayCommand(ResetDatabase);
            GroupBioPolymersCommand = new RelayCommand(GroupBioPolymers);
            ExportImageCommand = new RelayCommand(ExportImage);
        }

        public ICommand LoadDatabaseCommand { get; set; }
        public ICommand ResetDatabaseCommand { get; set; }
        public ICommand GroupBioPolymersCommand { get; set; }
        public ICommand ExportImageCommand { get; set; }

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
        }

        private void GroupBioPolymers()
        {
            var spectralMatches = _metaDrawLogic.FilteredListOfPsms;

        }

        private void ExportImage()
        {

        }

        #region GUI Interaction Parameters

        private bool _isDatabaseLoaded;
        public bool IsDatabaseLoaded
        {
            get => _isDatabaseLoaded;
            set
            {
                _isDatabaseLoaded = value; OnPropertyChanged(nameof(IsDatabaseLoaded));
            }
        }

        #endregion




    }

    public class BioPolymerGroupViewModel : BaseViewModel
    {
        public string GroupName { get; set; }
        public int GroupCount { get; set; }
        public double SequenceCoverage { get; set; }
        public int Length { get; set; }
        public ObservableCollection<SpectrumMatchFromTsv> AllSpectrumMatches { get; set; }
    }
}
