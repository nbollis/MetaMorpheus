using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Easy.Common.Extensions;
using EngineLayer;
using GuiFunctions;
using MassSpectrometry;
using MzLibUtil;
using Omics.Fragmentation;
using Omics.Fragmentation.Oligo;
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
            set {
                _fragmentationType = value;
                PopulatePossibleProducts();
                OnPropertyChanged("FragmentationType");
            }
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

        public ICollectionView FilteredDigestionProducts { get; private set; }
        private string _filterText;

        public string FilterText
        {
            get => _filterText;
            set
            {
                _filterText = value;
                OnPropertyChanged("FilterText");
                if (FilteredDigestionProducts == null)
                {
                    return;
                }

                FilteredDigestionProducts.Filter = o =>
                {
                    if (string.IsNullOrEmpty(FilterText))
                    {
                        return true;
                    }

                    if (o is OligoWithSetMods oligo)
                    {
                        return oligo.FullSequence.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
                    }

                    return false;
                };
            }
        }

        public ObservableCollection<ModTypeForTreeViewModel> VariableModTypeForTreeViewObservableCollection { get; private set; }

        public ObservableCollection<ModTypeForTreeViewModel> FixedModTypeForTreeViewObservableCollection { get; private set; }


        #endregion

        #region Fragmentation

        public ObservableCollection<RnaFragmentViewModel> FragmentationTypes { get; private set; }

        private int _minCharge;
        public int MinCharge
        {
            get => _minCharge;
            set { _minCharge = value; OnPropertyChanged("MinCharge"); }
        }

        private int _maxCharge;
        public int MaxCharge
        {
            get => _maxCharge;
            set { _maxCharge = value; OnPropertyChanged("MaxCharge"); }
        }

        private double _targetMz;

        public double TargetMz
        {
            get => _targetMz;
            set { _targetMz = value; OnPropertyChanged("TargetMz"); }
        }

        private double _tolerance;

        public double Tolerance
        {
            get => _tolerance;
            set
            {
                _tolerance = value; 
                OnPropertyChanged("Tolerance");
                PpmTolerance = new PpmTolerance(value);
            }
        }

        internal PpmTolerance PpmTolerance { get; private set; }

        public ObservableCollection<MassResult> MassResults { get; set; }

        #endregion


        public DigestionFragmentationViewModel()
        {
            AllRnases = new ObservableCollection<Rnase>(RnaseDictionary.Dictionary.Values);
            _rnaDigestionParams = new RnaDigestionParams("RNase T1");
            Rnase = _rnaDigestionParams.Rnase;
            DigestionProducts = new ObservableCollection<OligoWithSetMods>();
            FilteredDigestionProducts = CollectionViewSource.GetDefaultView(DigestionProducts);

            _fragmentationType = DissociationType.HCD;
            FragmentationTypes = new ObservableCollection<RnaFragmentViewModel>();
            PopulatePossibleProducts();

            MinCharge = -4;
            MaxCharge = -1;
            Tolerance = 50;
            MassResults = new ObservableCollection<MassResult>();

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

            MassResults.Clear();
            foreach (var product in FragmentationTypes.Where(p => p.Use)
                         .SelectMany(p => SelectedOligo.GetNeutralFragments(p.ProductType)))
            {
                MassResults.Add(new MassResult(product, MinCharge, MaxCharge));
            }
        }

    
        #endregion

        #region Helpers

        private void PopulatePossibleProducts()
        {
            // populate the possible products
            FragmentationTypes.Clear();
            var products = FragmentationType.GetRnaProductTypesFromDissociationType();
            foreach (var productType in Enum.GetValues(typeof(ProductType)).Cast<ProductType>())
            {

                switch (productType)
                {
                    case ProductType.a:
                    case ProductType.aWaterLoss:
                    case ProductType.aBaseLoss:
                    case ProductType.b:
                    case ProductType.bWaterLoss:
                    case ProductType.bBaseLoss:
                    case ProductType.c:
                    case ProductType.cWaterLoss:
                    case ProductType.cBaseLoss:
                    case ProductType.d:
                    case ProductType.dWaterLoss:
                    case ProductType.dBaseLoss:
                    case ProductType.w:
                    case ProductType.wWaterLoss:
                    case ProductType.wBaseLoss:
                    case ProductType.x:
                    case ProductType.xWaterLoss:
                    case ProductType.xBaseLoss:
                    case ProductType.y:
                    case ProductType.yWaterLoss:
                    case ProductType.yBaseLoss:
                    case ProductType.z:
                    case ProductType.zWaterLoss:
                    case ProductType.zBaseLoss:
                    case ProductType.M:
                        FragmentationTypes.Add(new RnaFragmentViewModel(products.Contains(productType), productType));
                        break;

                    case ProductType.zPlusOne:
                    case ProductType.bAmmoniaLoss:
                    case ProductType.zDot:
                    case ProductType.aStar:
                    case ProductType.aDegree:
                    case ProductType.D:
                    case ProductType.Ycore:
                    case ProductType.Y:
                    case ProductType.yAmmoniaLoss:
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
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
