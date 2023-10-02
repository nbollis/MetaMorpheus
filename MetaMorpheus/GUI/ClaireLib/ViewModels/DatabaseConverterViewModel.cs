using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EngineLayer;
using GuiFunctions;
using Proteomics;
using TopDownProteomics.IO.MzIdentMl;
using UsefulProteomicsDatabases;

namespace MetaMorpheusGUI
{
    public class DatabaseConverterViewModel : BaseViewModel
    {
        public ObservableCollection<string> DatabasePaths { get; }

        private string _outputDatabasePath;
        public string OutputDatabasePath
        {
            get => _outputDatabasePath ??= DatabasePaths.FirstOrDefault()?.Replace(".xml", ".fasta") ?? null;
            set { _outputDatabasePath = value; OnPropertyChanged(nameof(OutputDatabasePath)); }
        }

        private string selectedDatbase;

        public string SelectedDatabase
        {
            get => selectedDatbase;
            set { selectedDatbase = value; OnPropertyChanged(nameof(SelectedDatabase)); }
        }

        public DatabaseConverterViewModel()
        {
            DatabasePaths = new ObservableCollection<string>();

            RemoveDatabaseCommand = new RelayCommand(RemoveDatabaseFromDatabasePaths);
            CreateFastaCommand = new RelayCommand(CreateFasta);
            ClearDataCommand = new RelayCommand(Clear);
        }

        public ICommand RemoveDatabaseCommand { get; set; }
        private void RemoveDatabaseFromDatabasePaths()
        {
            DatabasePaths.Remove(SelectedDatabase);
            SelectedDatabase = DatabasePaths.FirstOrDefault();
        }

        public ICommand CreateFastaCommand { get; set; }

        private void CreateFasta()
        {
            List<Protein> proteins = new List<Protein>();
            foreach (var database in DatabasePaths)
            {
                if (database.EndsWith(".xml"))
                    proteins.AddRange(ProteinDbLoader.LoadProteinXML(database, true, DecoyType.None, GlobalVariables.AllModsKnown, false,
                                               new List<string>(), out _));
                else if (database.EndsWith(".fasta"))
                    proteins.AddRange(ProteinDbLoader.LoadProteinFasta(database, true, DecoyType.None, false, out _));
            }

            string finalPath = GetFinalPath(OutputDatabasePath);
            ProteinDbWriter.WriteFastaDatabase(proteins, finalPath, ">");

            MessageBox.Show($"New Database Outputted to {finalPath}");
        }

        private string GetFinalPath(string path)
        {
            // check if a file with this name already exists, if so add a number to the end within parenthesis. If that file still exists, increment the number by one and try again
            int fileCount = 1;
            string finalPath = path;
            while (System.IO.File.Exists(finalPath))
            {
                finalPath = path.Replace(".fasta", $"({fileCount}).fasta");
                fileCount++;
            }
            return finalPath;
        }

        public ICommand ClearDataCommand { get; set; }

        public void Clear()
        {
            DatabasePaths.Clear();
            OutputDatabasePath = null;
        }

        internal void FileDropped(string path)
        {
            if (!path.EndsWith(".xml") && !path.EndsWith(".fasta"))
                return;

            if (DatabasePaths.Contains(path))
                return;

            DatabasePaths.Add(path);
            OnPropertyChanged(nameof(OutputDatabasePath));
        }
    }
}
