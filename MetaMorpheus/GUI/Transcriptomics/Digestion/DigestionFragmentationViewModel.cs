using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EngineLayer;
using GuiFunctions;
using MassSpectrometry;
using Omics.Modifications;
using Transcriptomics;
using Transcriptomics.Digestion;

namespace MetaMorpheusGUI
{
    public class DigestionFragmentationViewModel : BaseViewModel
    {
        private string _sequence;
        public string Sequence
        {
            get => _sequence;
            set { _sequence = value; OnPropertyChanged("Sequence"); }
        }

        private DissociationType _fragmentationType;
        public DissociationType FragmentationType
        {
            get => _fragmentationType;
            set { _fragmentationType = value; OnPropertyChanged("FragmentationType"); }
        }

        #region Digestion Params

        private RnaDigestionParams _rnaDigestionParams;

        public int MaxMissedCleavages
        {
            get => _rnaDigestionParams.MaxMissedCleavages;
            set { _rnaDigestionParams.MaxMissedCleavages = value; OnPropertyChanged("MaxMissedCleavages"); }
        }

        public int MinLength
        {
            get => _rnaDigestionParams.MinLength;
            set { _rnaDigestionParams.MinLength = value; OnPropertyChanged("MinLength"); }
        }

        public int MaxLength
        {
            get => _rnaDigestionParams.MaxLength;
            set { _rnaDigestionParams.MaxLength = value; OnPropertyChanged("MaxLength"); }
        }

        public int MaxModificationIsoforms
        {
            get => _rnaDigestionParams.MaxModificationIsoforms;
            set { _rnaDigestionParams.MaxModificationIsoforms = value; OnPropertyChanged("MaxModificationIsoforms"); }
        }

        public int MaxMods
        {
            get => _rnaDigestionParams.MaxMods;
            set { _rnaDigestionParams.MaxMods = value; OnPropertyChanged("MaxMods"); }
        }

        public Rnase Rnase
        {
            get => _rnaDigestionParams.Rnase;
            set
            {
                _rnaDigestionParams.GetType().GetProperty("Rnase")?.SetValue(_rnaDigestionParams, value);
                OnPropertyChanged("Rnase");
            }
        }

        public ObservableCollection<Rnase> AllRnases { get; }

        #endregion

        #region Digestion Operation

        private RNA _rna;
        public RNA Rna
        {
            get => _rna;
            set { _rna = value; OnPropertyChanged("Rna"); }
        }

        public ObservableCollection<OligoWithSetMods> DigestionProducts { get; private set; }
        private OligoWithSetMods _selectedOligo;
        public OligoWithSetMods SelectedOligo
        {
            get => _selectedOligo;
            set { _selectedOligo = value; OnPropertyChanged("SelectedOligo"); }
        }

        public ObservableCollection<ModTypeForTreeViewModel> VariableModTypeForTreeViewObservableCollection { get; private set; }

        public ObservableCollection<ModTypeForTreeViewModel> FixedModTypeForTreeViewObservableCollection { get; private set; }


        #endregion

        

        public DigestionFragmentationViewModel()
        {
            AllRnases = new ObservableCollection<Rnase>(RnaseDictionary.Dictionary.Values);
            _rnaDigestionParams = new RnaDigestionParams("RNase T1");
            Rnase = _rnaDigestionParams.Rnase;
            FragmentationType = DissociationType.HCD;

            DigestionProducts = new ObservableCollection<OligoWithSetMods>();

            // modifications
            FixedModTypeForTreeViewObservableCollection = new ObservableCollection<ModTypeForTreeViewModel>();
            foreach (var hm in GlobalVariables.AllRnaModsKnown.Where(b => b.ValidModification == true).GroupBy(b => b.ModificationType))
            {
                var theModType = new ModTypeForTreeViewModel(hm.Key, false);
                FixedModTypeForTreeViewObservableCollection.Add(theModType);
                foreach (var uah in hm)
                {
                    theModType.Children.Add(new ModForTreeViewModel(uah.ToString(), false, uah.IdWithMotif, false, theModType));
                }
                theModType.VerifyCheckState();
            }

            VariableModTypeForTreeViewObservableCollection = new ObservableCollection<ModTypeForTreeViewModel>();
            foreach (var hm in GlobalVariables.AllRnaModsKnown.Where(b => b.ValidModification == true).GroupBy(b => b.ModificationType))
            {
                var theModType = new ModTypeForTreeViewModel(hm.Key, false);
                VariableModTypeForTreeViewObservableCollection.Add(theModType);
                foreach (var uah in hm)
                {
                    theModType.Children.Add(new ModForTreeViewModel(uah.ToString(), false, uah.IdWithMotif, false, theModType));
                }
                theModType.VerifyCheckState();
            }

            DigestCommand = new RelayCommand(Digest);
            FragmentCommand = new RelayCommand(Fragment);
        }


        #region Commands

        public ICommand DigestCommand { get; set; }
        public ICommand FragmentCommand { get; set; }

        #endregion

        #region Command Methods

        private void Digest()
        {
            // load in RNA
            try
            {
                var rna = new RNA(Sequence.ToUpper());
                Rna = rna;
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error loading RNA with message: {e.Message}");
            }

            // modifications
            var listOfModsVariable = (from modType in VariableModTypeForTreeViewObservableCollection from mod in modType.Children where mod.Use select GlobalVariables.AllRnaModsKnown.First(b => b.IdWithMotif == mod.ModName)).ToList();
            var listOfModsFixed = (from modType in FixedModTypeForTreeViewObservableCollection from mod in modType.Children where mod.Use select GlobalVariables.AllRnaModsKnown.First(b => b.IdWithMotif == mod.ModName)).ToList();

            SelectedOligo = null;
            DigestionProducts.Clear();
            foreach (var digestionProduct in Rna.Digest(_rnaDigestionParams, listOfModsFixed, listOfModsVariable))
            {
                DigestionProducts.Add(digestionProduct);
            }
        }

        private void Fragment()
        {
            if (SelectedOligo == null)
            {
                MessageBox.Show("Please select an oligo to fragment");
                return;
            }


        }
        #endregion

    }

    public class DigestionFragmentationCalculatorModel : DigestionFragmentationViewModel
    {
        public static DigestionFragmentationCalculatorModel Instance => new DigestionFragmentationCalculatorModel();
        public DigestionFragmentationCalculatorModel() : base()
        {
            Sequence = "GUACUGAUG";
            DigestCommand.Execute(null);
        }
    }
}
