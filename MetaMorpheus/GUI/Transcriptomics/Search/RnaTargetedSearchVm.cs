using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using EngineLayer;
using GuiFunctions;
using MassSpectrometry;
using MzLibUtil;
using Omics.Fragmentation;
using Omics.Modifications;
using Readers;
using TaskLayer;
using Transcriptomics;
using Transcriptomics.Digestion;

namespace MetaMorpheusGUI;

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
        CommonParameters commonParams = new(dissociationType: DissociationType.CID, deconvolutionMaxAssumedChargeState:20);
        List<OligoSpectralMatch> spectralMatches = new();
        var tolerance = new PpmTolerance(20);

        var products = new List<Product>();
        foreach (var product in PossibleProducts.Where(p => p.Use))
        {
            products.AddRange(rna.GetNeutralFragments(product.ProductType));
        }

        var ms1Products = new List<Product>();
        if (SearchParameters.MatchMs1)
        {
            ms1Products.AddRange(products);
            var mIonProduct = new Product(ProductType.M, FragmentationTerminus.None, rna.MonoisotopicMass, 0, 0,
                0);
            ms1Products.Add(mIonProduct);
        }

        var ms2WithSpecificMasses = MetaMorpheusTask.GetMs2Scans(DataFile, FilePath, commonParams)
            .ToList();
            
        foreach (var ms2WithSpecificMass in ms2WithSpecificMasses)
        {
            List<MatchedFragmentIon> matched =
                MetaMorpheusEngine.MatchFragmentIons(ms2WithSpecificMass, products, commonParams, SearchParameters.MatchAllCharges);
            if (matched.Any())
            {
                spectralMatches.Add(new OligoSpectralMatch(ms2WithSpecificMass.TheScan, rna.Parent as RNA, rna.BaseSequence, matched,
                    DataFile.FilePath));
            }
        }

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
            new(false, ProductType.b),
            new(false, ProductType.bBaseLoss),
            new(false, ProductType.bWaterLoss),
            new(true, ProductType.c),
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
            new(true, ProductType.yWaterLoss),
            new(false, ProductType.z),
            new(false, ProductType.zBaseLoss),
            new(false, ProductType.zWaterLoss),
        };
    }
}