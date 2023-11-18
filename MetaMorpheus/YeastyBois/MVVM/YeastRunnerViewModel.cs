using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nett;
using TaskLayer;
using System.Windows;
using System.Windows.Input;
using EngineLayer;
using MassSpectrometry;
using YeastyBois.Models;

namespace YeastyBois.MVVM
{
    public class YeastRunnerViewModel : BaseViewModel
    {
        public string SearchTomlPath
        {
            get => YeastyBoiGlobalContext.SearchTomlPath;
            set
            {
                try
                {
                    var task = Toml.ReadFile<SearchTask>(value);
                    if (task is not null)
                    {
                        YeastyBoiGlobalContext.SearchTomlPath = value;
                        OnPropertyChanged(nameof(SearchTomlPath));
                    }
                    else
                    {
                        throw new MetaMorpheusException("Search Task did not load Properly");
                    }
                }
                catch (Exception e)
                {
                    return;
                }
            }
        }

        public string SearchDatabasePath
        {
            get => YeastyBoiGlobalContext.DatabasePath;
            set
            {
                YeastyBoiGlobalContext.DatabasePath = value;
                OnPropertyChanged(nameof(SearchDatabasePath));
            }
        }


        private ObservableCollection<string> inputSpectra;

        public ObservableCollection<string> InputSpectra
        {
            get => inputSpectra;
            set { inputSpectra = value; OnPropertyChanged(nameof(InputSpectra)); }
        }

        public YeastRunnerViewModel()
        {
            InputSpectra = new();

        }

        public ICommand RunCommand { get; set; }

        private void Run()
        {
            // create new dataset
            int datasetId = YeastyBoiData.AllDataSets.Value.Max(p => p.DataSetId) + 1;
            DataSet set = new() { DataSetId = datasetId, Date = DateTime.Now, };

            foreach (var filePath in InputSpectra)
            {
                string outPath = Path.Combine(YeastyBoiGlobalContext.SearchResultDirectory, Path.GetFileName(filePath));
                RunIndividualDataFile(filePath, outPath);


                Results results = ParseResults(outPath);
            }
        }

        private void RunIndividualDataFile(string filePath, string outPath)
        {
            List<(string, MetaMorpheusTask)> taskList = new List<(string, MetaMorpheusTask)>
            {
                ("Task1-SearchTask", Toml.ReadFile<SearchTask>(SearchTomlPath))
            };
            List<DbForTask> dbForTasks = new List<DbForTask>()
            {
                new DbForTask(SearchDatabasePath, false),
            };
            
            EverythingRunnerEngine engine = new EverythingRunnerEngine(taskList, new List<string>() { filePath }, dbForTasks, outPath);
            engine.Run();
        }

        private Results ParseResults(string outPath)
        {
            return null;
        }

    }
}
