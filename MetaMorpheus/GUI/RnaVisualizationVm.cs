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
using OxyPlot.Wpf;
using Readers;
using TaskLayer;

namespace MetaMorpheusGUI
{
    public class RnaVisualizationVm : BaseViewModel
    {
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

        public RnaVisualizationVm()
        {
            LoadDataCommand = new RelayCommand(LoadData);
            ExportPlotCommand = new RelayCommand(ExportPlot);
        }

        public RnaVisualizationVm(MsDataFile dataFile, List<OligoSpectralMatch> matches)
        {
            DataFile = dataFile;
            SpectralMatches = new ObservableCollection<OligoSpectralMatch>(matches);
            SelectedMatch = SpectralMatches.First();
            DataFilePath = DataFile.FilePath;
            OsmPath = SpectralMatches.First().FilePath;
            LoadDataCommand = new RelayCommand(LoadData);
            ExportPlotCommand = new RelayCommand(ExportPlot);
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
                DataFile = MsDataFileReader.GetDataFile(DataFilePath).LoadAllStaticData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public void ParseDroppedFile(string[] files)
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

        public ICommand ExportPlotCommand { get; set; }

        private void ExportPlot()
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

        public void DisplaySelected(PlotView plotView)
        {

                Model = new DummyPlot(DataFile.GetOneBasedScan(SelectedMatch.ScanNumber),
                    SelectedMatch.MatchedFragmentIons, plotView, Mirror ? SpectralMatches.MaxBy(p => p.Score) : null);
            OnPropertyChanged(nameof(Model));
        }

        #endregion




    }

    public class RnaVisualizationModel : RnaVisualizationVm
    {
        public static RnaVisualizationModel Instance => new RnaVisualizationModel();
        public RnaVisualizationModel() { }
    }
}
