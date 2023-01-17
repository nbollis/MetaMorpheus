using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using EngineLayer;
using iText.Kernel.Geom;
using Path = System.IO.Path;

namespace GuiFunctions
{
    public class AnalyzeTabViewModel : BaseViewModel
    {
        #region Private Properties

        private DataView dataTable;
        private string outpath;
        #endregion

        #region Public Properties

        public ObservableCollection<MetaMorpheusRun> MMRuns { get; set; }
        //public MultiResultAnalyzer Analyzer { get; set; }

        public DataView DataTable
        {
            get => dataTable;
            set { dataTable = value; OnPropertyChanged(nameof(DataTable)); }
        }

        public string OutputPath
        {
            get => outpath;
            set { outpath = value; OnPropertyChanged(nameof(OutputPath)); }
        }

        public ICommand OutputDataTableCommand {get; set; }
        public ICommand RunAllAnalysisCommand { get; set; }
        public ICommand RunBasicAnalysisCommand { get; set; }
        public ICommand RunChimeraAnalysisCommand { get; set; }
        public ICommand RunAmbiguityAnalysisCommand { get; set; }

        #endregion

        #region Constructor

        public AnalyzeTabViewModel()
        {
            MMRuns = new();
            OutputPath = Directory.GetCurrentDirectory();

            OutputDataTableCommand = new RelayCommand(OutputDataTable);
            RunAllAnalysisCommand = new RelayCommand(RunAllAnalysis);
            RunBasicAnalysisCommand = new RelayCommand(RunBasicAnalysis);
            RunChimeraAnalysisCommand = new RelayCommand(RunChimeraAnalysis);
            RunAmbiguityAnalysisCommand = new RelayCommand(RunAmbiguityAnalysis);
        }

        #endregion

        #region Command Methods

        private void OutputDataTable()
        {
            int index = 1;
            var outPath = OutputPath;
            while (File.Exists(outPath))
            {
                int insertionIndex = outPath.IndexOf(".csv", StringComparison.Ordinal);
                if (index == 1)
                {
                    outPath = outPath.Insert(insertionIndex, $"_{index}");
                }
                else
                {
                    outPath = outPath.Replace($"{index - 1}", $"{index}");
                }
                index++;
            }
            OutputPath = outPath;
            using (StreamWriter writer = new StreamWriter(File.Create(outpath)))
            {
               // writer.Write(ResultAnalyzer.OutputDataTable(Analyzer.TotalTable));
            }
        }

        private void RunAllAnalysis()
        {
            
        }

        private void RunBasicAnalysis()
        {
            
        }

        private void RunChimeraAnalysis()
        {
            
        }

        private void RunAmbiguityAnalysis()
        {
            
        }

        #endregion

        #region Helpers

        public void InputSearchFolder(string folderPath)
        {
            var run = new MetaMorpheusRun(folderPath);
            MMRuns.Add(run);
            //Analyzer.AddSearchResult(run);

            var paths = MMRuns.Select(p => Path.GetDirectoryName(p.DirectoryPath));
            var mostAbundantFolder = paths.GroupBy(p => p).OrderByDescending(p => p.Key).First();

            string outPath = Path.Combine(mostAbundantFolder.First(), "AnalysisTable.csv");
            OutputPath = outPath;
        }

        #endregion
    }
}
