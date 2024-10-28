using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using GuiFunctions;
using Omics;
using Omics.SpectrumMatch;
using TaskLayer;
using UsefulProteomicsDatabases;

namespace GuiFunctions
{
    public class BioPolymerTabViewModel : BaseViewModel
    {
        private string _databasePath;

        public string DatabasePath
        {
            get => _databasePath;
            set
            {
                _databasePath = value; OnPropertyChanged(nameof(DatabasePath));
            }
        }
        private List<IBioPolymer> AllBioPolymers;

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

        public BioPolymerTabViewModel()
        {
            AllBioPolymers = new List<IBioPolymer>();
            AllGroups = new ObservableCollection<BioPolymerGroupViewModel>();
        }

        //public ICommand LoadDatabaseCommand => new RelayCommand(LoadDatabase);

        private void LoadDatabase()
        {
            if (DatabasePath is null or "")
                return;

            AllBioPolymers = new SearchTask().LoadBioPolymers("", new()
                { new DbForTask(DatabasePath, false) }, true, DecoyType.None, new(), new());
        }
    }

    public class BioPolymerGroupViewModel : BaseViewModel
    {
        public string GroupName { get; set; }
        public int GroupCount { get; set; }
        public ObservableCollection<SpectrumMatchFromTsv> AllSpectrumMatches { get; set; }
    }
}
