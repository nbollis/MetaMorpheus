using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EngineLayer;
using GuiFunctions;
using MassSpectrometry;
using Omics.Fragmentation;
using Omics.Fragmentation.Oligo;
using Omics.Modifications;
using OxyPlot.Wpf;
using Readers;
using TaskLayer;
using Transcriptomics;
using Transcriptomics.Digestion;

namespace MetaMorpheusGUI;

public class RnaVisualizationVm : BaseViewModel
{
    public MetaDrawSettingsViewModel SettingsView;
    private string _dataFilePath;
    public string DataFilePath
    {
        get => _dataFilePath;
        set
        {
            _dataFilePath = value;
            OnPropertyChanged(nameof(DataFilePath));
        }
    }

    private string _OsmPath;
    public string OsmPath
    {
        get => _OsmPath;
        set
        {
            _OsmPath = value;
            OnPropertyChanged(nameof(OsmPath));
        }
    }

    private ObservableCollection<OsmFromTsv> _spectralMatches;
    public ObservableCollection<OsmFromTsv> SpectralMatches
    {
        get => _spectralMatches;
        set
        {
            _spectralMatches = value;
            OnPropertyChanged(nameof(SpectralMatches));
        }
    }

    private MsDataFile _dataFile;
    public MsDataFile DataFile
    {
        get => _dataFile;
        set
        {
            _dataFile = value;
            OnPropertyChanged(nameof(DataFile));
        }
    }

    private OsmFromTsv _selectedMatch;
    public OsmFromTsv SelectedMatch
    {
        get => _selectedMatch;
        set { _selectedMatch = value; OnPropertyChanged(nameof(SelectedMatch)); }
    }

    private DummyPlot _model;
    public DummyPlot Model
    {
        get => _model;
        set { _model = value; OnPropertyChanged(nameof(Model)); }
    }

    private bool mirror;
    public bool Mirror
    {
        get => mirror;
        set { mirror = value; OnPropertyChanged(nameof(Mirror)); }
    }

    private DrawnSequence _drawnSequence;
    public DrawnSequence DrawnSequence
    {
        get => _drawnSequence;
        set { _drawnSequence = value; OnPropertyChanged(nameof(DrawnSequence)); }
    }

    private ObservableCollection<RnaFragmentVm> _possibleProducts;
    public ObservableCollection<RnaFragmentVm> PossibleProducts
    {
        get => _possibleProducts;
        set { _possibleProducts = value; OnPropertyChanged(nameof(PossibleProducts)); }
    }

    private RnaSearchParameters _searchParameters;
    public RnaSearchParameters SearchParameters
    {
        get => _searchParameters;
        set { _searchParameters = value; OnPropertyChanged(nameof(SearchParameters)); }
    }

    private bool _searchPersists;
    public bool SearchPersists
    {
        get => _searchPersists;
        set { _searchPersists = value; OnPropertyChanged(nameof(SearchPersists)); }
    }

    public RnaVisualizationVm()
    {
        SettingsView = new();
        PossibleProducts = new();
        SearchParameters = new();

        LoadDataCommand = new RelayCommand(LoadData);
        ExportPlotCommand = new RelayCommand(ExportPlot);
        ClearDataCommand = new RelayCommand(ClearData);
    }

    public RnaVisualizationVm(MsDataFile dataFile, List<OsmFromTsv> matches)
    {
        DataFile = dataFile;
        SelectedMatch = SpectralMatches.First();
        DataFilePath = DataFile.FilePath;
        OsmPath = SpectralMatches.First().FileNameWithoutExtension;
        SpectralMatches = new ObservableCollection<OsmFromTsv>(matches.OrderByDescending(p => p.Score));

        PossibleProducts = new();
        SettingsView = new();
        SearchParameters = new();

        LoadDataCommand = new RelayCommand(LoadData);
        ExportPlotCommand = new RelayCommand(ExportPlot);
        ClearDataCommand = new RelayCommand(ClearData);

        MetaDrawSettings.DrawNumbersUnderStationary = false;
    }

    #region Commands

    public ICommand LoadDataCommand { get; set; }
    private void LoadData()
    {
        try
        {
            SpectralMatches = new ObservableCollection<OsmFromTsv>(SpectrumMatchTsvReader.ReadOsmTsv(OsmPath, out List<string> warnings));
            DataFile = MsDataFileReader.GetDataFile(DataFilePath);
            DataFile.InitiateDynamicConnection();
            ParsePossibleProducts();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ruh Roh Raggy");
        }
    }

    public ICommand ClearDataCommand { get; set; }
    private void ClearData()
    {
        try
        {
            PossibleProducts.Clear();
            DataFile.CloseDynamicConnection();
            DataFile = null;
            SpectralMatches = new ObservableCollection<OsmFromTsv>();
            DataFilePath = "";
            OsmPath = "";
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message, "Ruh Roh Raggy");
        }
    }

    public ICommand ExportPlotCommand { get; set; }
    private void ExportPlot()
    {
        try
        {
            string drawnDirectory = Path.Combine(Path.GetDirectoryName(DataFile.FilePath), "RnaMetaDraw");
            if (!Directory.Exists(drawnDirectory))
                Directory.CreateDirectory(drawnDirectory);

            string outDirectory = Path.Combine(drawnDirectory, DateTime.Now.ToString("yy-MM-dd"));
            if (!Directory.Exists(outDirectory))
                Directory.CreateDirectory(outDirectory);

            string outPath = Path.Combine(outDirectory,
                $"{SelectedMatch.Ms2ScanNumber}_{SelectedMatch.BaseSeq.Substring(0, Math.Min(SelectedMatch.BaseSeq.Length, 20))}.png");
            Model.ExportToPng(outPath);

            MessageBox.Show($"Exported plot to {outPath}");
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message, "Ruh Roh Raggy");
        }

    }


    public void TargetedSearch(PlotView plotView, Canvas canvas)
    {
        // load info
        var rna = new RNA(SelectedMatch.BaseSeq).Digest(new RnaDigestionParams(), new List<Modification>(), new List<Modification>())
            .First() as OligoWithSetMods ?? throw new NullReferenceException();

        var products = new List<Product>();
        foreach (var product in PossibleProducts.Where(p => p.Use))
        {
            products.AddRange(rna.GetNeutralFragments(product.ProductType));
        }

        var scan = DataFile.GetOneBasedScanFromDynamicConnection(SelectedMatch.Ms2ScanNumber);
        var deconParams = new ClassicDeconvolutionParameters(-20, -1, 20, 3, Polarity.Negative);
        var commonParams = new CommonParameters(
            dissociationType: scan.DissociationType ?? DissociationType.HCD,
            deconParams: deconParams
        );

        var specificMass = new Ms2ScanWithSpecificMass(scan, scan.IsolationMz.Value,
            -scan.SelectedIonChargeStateGuess.Value, DataFile.FilePath, commonParams);

        // search
        var matched = MetaMorpheusEngine.MatchFragmentIons(specificMass, products, commonParams,
            false);

        //var osm = new OligoSpectralMatch(rna, 0, matched.Count,
        //    specificMass.TheScan.OneBasedScanNumber, specificMass, commonParams, matched);
        SelectedMatch.GetType().GetProperty("MatchedIons").SetValue(SelectedMatch, matched);
        
        DisplaySelected(plotView, canvas);

    }

    public void ParseDroppedFile(string[] files)
    {
        try
        {
            foreach (var file in files)
            {
                string extension = Path.GetExtension(file);

                if (extension.Equals(".osmtsv"))
                {
                    OsmPath = file;
                }
                if (extension.Equals(".raw") || extension.Equals(".mzML", StringComparison.InvariantCultureIgnoreCase))
                {
                    DataFilePath = file;
                }
            }
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message, "Ruh Roh Raggy");
        }
    }

    public void DisplaySelected(PlotView plotView, Canvas canvas)
    {
        try
        {
            if (SelectedMatch is null) return;
            var scan = DataFile.GetOneBasedScanFromDynamicConnection(SelectedMatch.Ms2ScanNumber);
            if (scan == null)
                throw new Exception("Scan not found");

            Model = new DummyPlot(scan,
                SelectedMatch.MatchedIons, plotView, Mirror ? SpectralMatches.MaxBy(p => p.Score) : null);
            OnPropertyChanged(nameof(Model));
            DrawnSequence = new DrawnSequence(canvas, SelectedMatch, false, false);
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message, "Ruh Roh Raggy");
        }
    }

    /// <summary>
    /// Only run this after the SpectralMatches are loaded
    /// </summary>
    private void ParsePossibleProducts()
    {
        PossibleProducts.Clear();
        var possibilities = Enum.GetValues<ProductType>().ToList();
        possibilities.Remove(ProductType.D);
        possibilities.Remove(ProductType.zPlusOne);
        possibilities.Remove(ProductType.zDot);
        possibilities.Remove(ProductType.Y);
        possibilities.Remove(ProductType.Ycore);
        possibilities.Remove(ProductType.aStar);
        possibilities.Remove(ProductType.yAmmoniaLoss);
        possibilities.Remove(ProductType.aDegree);
        possibilities.Remove(ProductType.bAmmoniaLoss);

        var productsPresent = SpectralMatches.SelectMany(p =>
                p.MatchedIons.Select(m => m.NeutralTheoreticalProduct.ProductType).Distinct())
            .Distinct().ToArray();
        foreach (var possibility in possibilities)
        {
            PossibleProducts.Add(productsPresent.Contains(possibility)
                ? new RnaFragmentVm(true, possibility)
                : new RnaFragmentVm(false, possibility));
        }

    }

    #endregion
}