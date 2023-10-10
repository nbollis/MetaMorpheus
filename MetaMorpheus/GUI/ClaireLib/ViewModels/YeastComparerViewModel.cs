using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using GuiFunctions;

namespace MetaMorpheusGUI
{
    public class FileAndSearchComparerViewModel : BaseViewModel, IFileDragDropTarget
    {
        private ObservableCollection<DataViewModel> _dataViewModels;
        public ObservableCollection<DataViewModel> DataViewModels
        {
            get => _dataViewModels;
            set
            {
                _dataViewModels = value;
                OnPropertyChanged(nameof(DataViewModels));
            }
        }

        private DataViewModel selectedDataViewModel;
        public DataViewModel SelectedDataViewModel
        {
            get => selectedDataViewModel;
            set
            {
                selectedDataViewModel = value; OnPropertyChanged(nameof(SelectedDataViewModel));
            }
        }

        public FileAndSearchComparerViewModel()
        {
            DataViewModels = new();

            AddDataCommand = new RelayCommand(AddData);
            RemoveDataCommand = new RelayCommand(RemoveData);
            ClearDataCommand = new RelayCommand(ClearData);
            RunCommand = new RelayCommand(Run);


        }



        public ICommand ClearDataCommand { get; set; }

        private void ClearData()
        {
            DataViewModels = new();
        }

        public ICommand AddDataCommand { get; set; }
        private void AddData()
        {
            DataViewModels.Add(new DataViewModel());
        }

        public ICommand RemoveDataCommand { get; set; }
        private void RemoveData()
        {
            DataViewModels.Remove(SelectedDataViewModel);
        }

        public ICommand RunCommand { get; set; }
        private void Run()
        {

        }

        public void OnFileDrop(string[] filepaths)
        {
            List<string> warnings = new();
            foreach (var path in filepaths)
            {
                bool isSpectra = false;
                var extension = Path.GetExtension(path);
                if (extension == ".psmtsv" || extension == ".raw")
                {
               
                }
                else
                {
                    warnings.Add($"File {path} is not a .psmtsv or .raw file");
                    continue;
                }


                if (Path.GetExtension(path) == ".psmtsv")
                {
                    
                }
                else if (Path.GetExtension(path) == ".raw")
                {
                    
                }
                
            }
        }
    }


    public class DataViewModel : BaseViewModel, IFileDragDropTarget
    {
        private DataModel _dataModel;

        public DataModel DataModel
        {
            get => _dataModel;
            set
            {
                _dataModel = value;
                OnPropertyChanged(nameof(DataModel));
            }
        }

        public string PsmPath
        {
            get => DataModel.PsmPath ?? "Drop Psms Here";
            set
            {
                DataModel.PsmPath = value;
                OnPropertyChanged(nameof(PsmPath));
            }
        }

        //public string PsmsShortPath => string.Join('\\', DataModel.PsmPath.Split('\\')[..3]);
        //public string SpectraShortPath => Path.GetFileNameWithoutExtension(SpectraPath);

        public string SpectraPath
        {
            get => DataModel.SpectraPath ?? "Drop Spectra File";
            set
            {
                DataModel.SpectraPath = value;
                OnPropertyChanged(nameof(SpectraPath));
            }
        }

        public bool IsControl
        {
            get => DataModel.IsControl;
            set
            {
                DataModel.IsControl = value;
                OnPropertyChanged(nameof(IsControl));
            }
        }

        public DataViewModel()
        {
            DataModel = new();
        }

        public void OnFileDrop(string[] filepaths)
        {
            foreach (var filepath in filepaths)
            {
                throw new NotImplementedException();
            }
        }
    }


    public class FileAndSearchComparerModel : FileAndSearchComparerViewModel
    {
        public static FileAndSearchComparerModel Instance { get; } = new FileAndSearchComparerModel();

        public FileAndSearchComparerModel()
        {
        }
    }

    public class DataModel 
    {
        public string PsmPath { get; set; }
        public string SpectraPath { get; set; }
        public bool IsControl { get; set; }
        public DataModel()
        {
           
        }
    }

}
