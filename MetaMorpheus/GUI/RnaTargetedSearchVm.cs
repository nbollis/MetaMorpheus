using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EngineLayer;
using GuiFunctions;
using MassSpectrometry;
using Proteomics.Fragmentation;
using Readers;
using Transcriptomics;

namespace MetaMorpheusGUI
{
    public class RnaTargetedSearchVm : BaseViewModel
    {
        private string _targetSequence;
        public string TargetSequence
        {
            get => _targetSequence;
            set { _targetSequence = value; OnPropertyChanged(nameof(TargetSequence)); }
        }

        private string _filePath;
        public string FilePath
        {
            get => _filePath;
            set { _filePath = value; OnPropertyChanged(nameof(FilePath)); }
        }

        private ObservableCollection<RnaFragmentVm> _possibleProducts;
        public ObservableCollection<RnaFragmentVm> PossibleProducts
        {
            get => _possibleProducts;
            set { _possibleProducts = value; OnPropertyChanged(nameof(PossibleProducts)); }
        }

        private RnaSearchParametersVm _searchParameters;
        public RnaSearchParametersVm SearchParameters
        {
            get => _searchParameters;
            set { _searchParameters = value; OnPropertyChanged(nameof(SearchParameters)); }
        }

        private List<OligoSpectralMatch> _spectralMatches;
        public List<OligoSpectralMatch> SpectralMatches
        {
            get => _spectralMatches;
            set
            {
                _spectralMatches = value;
                OnPropertyChanged(nameof(SpectralMatches));
            }
        }

        public MsDataFile DataFile { get; set; }
        public RnaTargetedSearchVm()
        {
            PossibleProducts = GenerateProductCollection();
            SearchParameters = new();
            
            MatchIonsCommand = new RelayCommand(MatchIons);
            RnaseDictionary.LoadRnaseDictionary(
                @"C:\Users\Nic\source\repos\MetaMorpheus\MetaMorpheus\EngineLayer\ProteolyticDigestion\rnases.tsv");
        }


        #region Commands

        public ICommand MatchIonsCommand { get; set; }
        private void MatchIons()
        {
            var rna = new RNA(TargetSequence)
                .Digest(new RnaDigestionParams(), new List<Modification>(), new List<Modification>())
                .First() as OligoWithSetMods;
            CommonParameters commonParams = new(dissociationType: DissociationType.CID);
            List<OligoSpectralMatch> spectralMatches = new();

            var products = new List<IProduct>();
            foreach (var product in PossibleProducts.Where(p => p.Use))
            {
                products.AddRange(rna.GetNeutralFragments(product.ProductType));
            }

            var ms1Products = new List<IProduct>();
            if (SearchParameters.MatchMs1)
            {
                ms1Products.AddRange(products);
                var mIonProduct = new RnaProduct(ProductType.M, FragmentationTerminus.None, rna.MonoisotopicMass, 0, 0,
                    0);
                ms1Products.Add(mIonProduct);
            }

            DataFile.InitiateDynamicConnection();
            for (int i = SearchParameters.MinScanId; i < SearchParameters.MaxScanId; i++)
            {
                var scan = DataFile.GetOneBasedScanFromDynamicConnection(i);
                List<MatchedFragmentIon> matched = new();
                if (SearchParameters.MatchMs1 && scan.MsnOrder == 1)
                {
                    var ms1ms2WithMas = new Ms2ScanWithSpecificMass(scan, 100, 1, DataFile.FilePath, commonParams);
                    matched =
                        MetaMorpheusEngine.MatchFragmentIons(ms1ms2WithMas, ms1Products, commonParams, SearchParameters.MatchAllCharges);
                }
                else if (SearchParameters.MatchMs2 && scan.MsnOrder == 2)
                {
                    var ms2WithMass = new Ms2ScanWithSpecificMass(scan, scan.IsolationMz.Value,
                        scan.SelectedIonChargeStateGuess.Value, DataFile.FilePath, commonParams);
                    matched =
                        MetaMorpheusEngine.MatchFragmentIons(ms2WithMass, products, commonParams, SearchParameters.MatchAllCharges);
                }

                if (matched.Any())
                    spectralMatches.Add(new OligoSpectralMatch(scan, rna.Parent as RNA,  rna.BaseSequence, matched, DataFile.FilePath));
            }
            DataFile.CloseDynamicConnection();
            SpectralMatches = spectralMatches;

            string outDirectory = Path.GetDirectoryName(DataFile.FilePath);
            string fileName = Path.GetFileNameWithoutExtension(DataFile.FilePath);
            string outPath = Path.Combine(outDirectory, $"{fileName}_{TargetSequence.Substring(0, Math.Min(TargetSequence.Length, 20))}_{SearchParameters.MinScanId}-{SearchParameters.MaxScanId}.osmtsv");

            OligoSpectralMatch.Export(spectralMatches.OrderByDescending(p => p.Score).ToList(), outPath);
            MessageBox.Show($"Results Outputted to {outPath}");
        }

        #endregion

        public void ParseDroppedFile(string[] files)
        {
            try
            {
                foreach (var file in files)
                {
                    string extension = Path.GetExtension(file);
                    if (extension.Equals(".raw") || extension.Equals(".mzML", StringComparison.InvariantCultureIgnoreCase))
                    {
                        FilePath = file;
                        DataFile = MsDataFileReader.GetDataFile(FilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        
        private ObservableCollection<RnaFragmentVm> GenerateProductCollection()
        {
            return new ObservableCollection<RnaFragmentVm>()
            {
                new(true, ProductType.a),
                new(true, ProductType.aBaseLoss),
                new(false, ProductType.aWaterLoss),
                new(true, ProductType.b),
                new(false, ProductType.bBaseLoss),
                new(false, ProductType.bWaterLoss),
                new(false, ProductType.c),
                new(false, ProductType.cBaseLoss),
                new(false, ProductType.cWaterLoss),
                new(true, ProductType.d),
                new(false, ProductType.dBaseLoss),
                new(true, ProductType.dWaterLoss),
                new(true, ProductType.w),
                new(false, ProductType.wBaseLoss),
                new(false, ProductType.wWaterLoss),
                new(false, ProductType.x),
                new(false, ProductType.xBaseLoss),
                new(false, ProductType.xWaterLoss),
                new(true, ProductType.y),
                new(false, ProductType.yBaseLoss),
                new(false, ProductType.yWaterLoss),
                new(false, ProductType.z),
                new(false, ProductType.zBaseLoss),
                new(false, ProductType.zWaterLoss),
            };
        }
    }

    public class RnaBigVm : BaseViewModel
    {
        private RnaTargetedSearchVm searchVm;

        public RnaTargetedSearchVm SearchVm
        {
            get => searchVm;
            set { searchVm = value; OnPropertyChanged(nameof(SearchVm)); }
        }

        private RnaVisualizationVm visualizationVm;

        public RnaVisualizationVm VisualizationVm
        {
            get => visualizationVm;
            set { visualizationVm = value; OnPropertyChanged(nameof(VisualizationVm));}
        }

        public RnaBigVm()
        {
            VisualizationVm = new();
            SearchVm = new();
        }
    }

    public class RnaPageModel : RnaTargetedSearchVm
    {

        public static RnaPageModel Instance => new RnaPageModel();
        public RnaPageModel()
        {
            TargetSequence = "GUACUG";
            SearchParameters = new();
            PossibleProducts = new ObservableCollection<RnaFragmentVm>()
            {
                new(false, ProductType.a),
                new(true, ProductType.aBaseLoss),
                new(false, ProductType.aWaterLoss),
                new(false, ProductType.b),
                new(false, ProductType.bBaseLoss),
                new(false, ProductType.bWaterLoss),
                new(true, ProductType.c),
                new(false, ProductType.cBaseLoss),
                new(false, ProductType.cWaterLoss),
                new(false, ProductType.d),
                new(false, ProductType.dBaseLoss),
                new(true, ProductType.dWaterLoss),
                new(false, ProductType.w),
                new(false, ProductType.wBaseLoss),
                new(false, ProductType.wWaterLoss),
                new(false, ProductType.x),
                new(false, ProductType.xBaseLoss),
                new(false, ProductType.xWaterLoss),
                new(true, ProductType.y),
                new(false, ProductType.yBaseLoss),
                new(false, ProductType.yWaterLoss),
                new(false, ProductType.z),
                new(false, ProductType.zBaseLoss),
                new(false, ProductType.zWaterLoss),
            };
        }
    }
}
