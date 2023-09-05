using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EngineLayer;
using GuiFunctions;
using MassSpectrometry;
using OxyPlot.Wpf;
using Readers;
using TaskLayer;

namespace MetaMorpheusGUI
{
    public class RnaVisualizationVm : BaseViewModel
    {
        public SettingsViewModel SettingsView;
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

        private ObservableCollection<OligoSpectralMatch> _spectralMatches;
        public ObservableCollection<OligoSpectralMatch> SpectralMatches
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

        private OligoSpectralMatch _selectedMatch;
        public OligoSpectralMatch SelectedMatch
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

        public RnaVisualizationVm()
        {
            SettingsView = new();

            LoadDataCommand = new RelayCommand(LoadData);
            ExportPlotCommand = new RelayCommand(ExportPlot);
            ClearDataCommand = new RelayCommand(ClearData);
        }

        public RnaVisualizationVm(MsDataFile dataFile, List<OligoSpectralMatch> matches)
        {
            DataFile = dataFile;
            DataFile.InitiateDynamicConnection();
            SpectralMatches = new ObservableCollection<OligoSpectralMatch>(matches.OrderByDescending(p => p.Score));
            SelectedMatch = SpectralMatches.First();
            DataFilePath = DataFile.FilePath;
            OsmPath = SpectralMatches.First().FilePath;

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
                SpectralMatches =
                    new ObservableCollection<OligoSpectralMatch>(
                        OligoSpectralMatch.Import(OsmPath, out List<string> warnings));
                DataFile = MsDataFileReader.GetDataFile(DataFilePath);
                DataFile.InitiateDynamicConnection();
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
                DataFile.CloseDynamicConnection();
                DataFile = null;
                SpectralMatches = new ObservableCollection<OligoSpectralMatch>();
                DataFilePath = "";
                OsmPath = "";
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Ruh Roh Raggy");
            }
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
                    $"{SelectedMatch.ScanNumber}_{SelectedMatch.BaseSequence.Substring(0, Math.Min(SelectedMatch.BaseSequence.Length, 20))}.png");
                Model.ExportToPng(outPath);

                MessageBox.Show($"Exported plot to {outPath}");
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
                Model = new DummyPlot(DataFile.GetOneBasedScanFromDynamicConnection(SelectedMatch.ScanNumber),
                    SelectedMatch.MatchedFragmentIons, plotView, Mirror ? SpectralMatches.MaxBy(p => p.Score) : null);
                OnPropertyChanged(nameof(Model));
                DrawnSequence = new DrawnSequence(canvas, SelectedMatch, false, false);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Ruh Roh Raggy");
            }
        }

        #endregion
    }

    public class RnaVisualizationModel : RnaVisualizationVm
    {
        public static RnaVisualizationModel Instance => new RnaVisualizationModel();
        public RnaVisualizationModel() { }
    }
}
