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
        }


        #region Commands

        public ICommand MatchIonsCommand { get; set; }
        private void MatchIons()
        {
            RNA rna = new(TargetSequence);
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
                    spectralMatches.Add(new OligoSpectralMatch(scan, rna.BaseSequence, matched, DataFile.FilePath));
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
                new(false, ProductType.a),
                new(true, ProductType.aBase),
                new(false, ProductType.aWaterLoss),
                new(false, ProductType.b),
                new(false, ProductType.bBase),
                new(false, ProductType.bWaterLoss),
                new(true, ProductType.c),
                new(false, ProductType.cBase),
                new(false, ProductType.cWaterLoss),
                new(false, ProductType.d),
                new(false, ProductType.dBase),
                new(true, ProductType.dWaterLoss),
                new(false, ProductType.w),
                new(false, ProductType.wBase),
                new(false, ProductType.wWaterLoss),
                new(false, ProductType.x),
                new(false, ProductType.xBase),
                new(false, ProductType.xWaterLoss),
                new(true, ProductType.y),
                new(false, ProductType.yBase),
                new(false, ProductType.yWaterLoss),
                new(false, ProductType.z),
                new(false, ProductType.zBase),
                new(false, ProductType.zWaterLoss),
            };
        }
    }

    public class RnaFragmentVm : BaseViewModel
    {
        public RnaFragmentVm(bool use, ProductType type)
        {
            Use = use;
            ProductType = type;
        }

        public ProductType ProductType { get; }
        public string TypeString => ProductType.ToString();

        private bool use;

        public bool Use
        {
            get => use;
            set { use = value; OnPropertyChanged(nameof(Use)); }
        }
    }

    public class RnaSearchParametersVm : BaseViewModel
    {
        private RnaSearchParameters parameters;

        public bool MatchMs1
        {
            get => parameters.MatchMs1;
            set { parameters.MatchMs1 = value; OnPropertyChanged(nameof(MatchMs1));}
        }
        public bool MatchMs2
        {
            get => parameters.MatchMs2;
            set { parameters.MatchMs2 = value; OnPropertyChanged(nameof(MatchMs2));}
        }
        public int MinScanId
        {
            get => parameters.MinScanId;
            set { parameters.MinScanId = value; OnPropertyChanged(nameof(MinScanId));}
        }
        public int MaxScanId
        {
            get => parameters.MaxScanId;
            set { parameters.MaxScanId = value; OnPropertyChanged(nameof(MaxScanId));}
        }

        public bool MatchAllCharges
        {
            get => parameters.MatchAllCharges;
            set { parameters.MatchAllCharges = value; OnPropertyChanged(nameof(MatchAllCharges)); }
        }

        public RnaSearchParametersVm()
        {
            parameters = new();
        }
    }

    public class RnaSearchParameters
    {
        public bool MatchMs1 { get; set; }
        public bool MatchMs2 { get; set; }
        public int MinScanId { get; set; }
        public int MaxScanId { get; set;}
        public bool MatchAllCharges { get; set; }

        public RnaSearchParameters(bool matchMs1 = false, bool matchMs2 = true, bool matchCharges = false, int minScanId = 1,
            int maxScanId = 100)
        {
            MatchMs1 = matchMs1;
            MatchMs2 = matchMs2;
            MatchAllCharges = matchCharges;
            MinScanId = minScanId;
            MaxScanId = maxScanId;
        }

        public IEnumerable<MsDataScan> GetFilteredScans(List<MsDataScan> scans)
        {
            foreach (var scanned in scans)
            {
                if (scanned.OneBasedScanNumber >= MinScanId && scanned.OneBasedScanNumber <= MaxScanId)
                {
                    if (scanned.MsnOrder == 1 && MatchMs1)
                        yield return scanned;
                    if (scanned.MsnOrder == 2 && MatchMs2) 
                        yield return scanned;
                }
            }
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
                new(true, ProductType.aBase),
                new(false, ProductType.aWaterLoss),
                new(false, ProductType.b),
                new(false, ProductType.bBase),
                new(false, ProductType.bWaterLoss),
                new(true, ProductType.c),
                new(false, ProductType.cBase),
                new(false, ProductType.cWaterLoss),
                new(false, ProductType.d),
                new(false, ProductType.dBase),
                new(true, ProductType.dWaterLoss),
                new(false, ProductType.w),
                new(false, ProductType.wBase),
                new(false, ProductType.wWaterLoss),
                new(false, ProductType.x),
                new(false, ProductType.xBase),
                new(false, ProductType.xWaterLoss),
                new(true, ProductType.y),
                new(false, ProductType.yBase),
                new(false, ProductType.yWaterLoss),
                new(false, ProductType.z),
                new(false, ProductType.zBase),
                new(false, ProductType.zWaterLoss),
            };
        }
    }
}
