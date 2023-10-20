using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
        private string selectedDatbase;
        public string SelectedDatabase
        {
            get => selectedDatbase;
            set { selectedDatbase = value; OnPropertyChanged(nameof(SelectedDatabase)); }
        }

        public ObservableCollection<string> SearchResultPaths { get; }
        private string selectedSearchResult;
        public string SelectedSearchResult
        {
            get => selectedSearchResult;
            set { selectedSearchResult = value; OnPropertyChanged(nameof(SelectedSearchResult)); }
        }


        private string _outputDatabasePath;
        public string OutputDatabasePath
        {
            get => _outputDatabasePath ??= DatabasePaths.FirstOrDefault()?.Replace(".xml", ".fasta") ?? null;
            set { _outputDatabasePath = value; OnPropertyChanged(nameof(OutputDatabasePath)); }
        }


        private bool _generateDecoys;
        public bool GenerateDecoys
        { 
            get => _generateDecoys; 
            set 
            { 
                _generateDecoys = value;
                if (!value)
                    SelectedDecoyType = DecoyType.None;
                OnPropertyChanged(nameof(GenerateDecoys));
            }
        }

        private bool _generateTargets;
        public bool GenerateTargets
        {
            get => _generateTargets;
            set { _generateTargets = value; OnPropertyChanged(nameof(GenerateTargets)); }
        }

        public DecoyType[] DecoyTypes { get; set; }

        private DecoyType selectedDecoyType;
        public DecoyType SelectedDecoyType
        {
            get => selectedDecoyType;
            set { selectedDecoyType = value; OnPropertyChanged(nameof(SelectedDecoyType)); }
        }

        public string[] OutputTypes { get; set; }
        private string selectedOutputType;
        public string SelectedOutputType
        {
            get => selectedOutputType;
            set { selectedOutputType = value; OnPropertyChanged(nameof(SelectedOutputType)); }
        }

        public DatabaseConverterViewModel()
        {
            DatabasePaths = new ObservableCollection<string>();
            SearchResultPaths = new ObservableCollection<string>();
            DecoyTypes = Enum.GetValues<DecoyType>()
                .Where(p => p != DecoyType.Random)
                .ToArray();
            SelectedDecoyType = DecoyType.None;
            OutputTypes = new[] { "fasta", "xml" };
            SelectedOutputType = "fasta";
            GenerateTargets = true;
            GenerateDecoys = false;

            RemoveDatabaseCommand = new RelayCommand(RemoveDatabaseFromDatabasePaths);
            RemoveSearchResultCommand = new RelayCommand(RemoveSearchResultFromSearchResultPaths);
            CreateSingleDatabaseCommand = new RelayCommand(CreateSingleDatabase);
            ClearDataCommand = new RelayCommand(Clear);
        }

        public ICommand RemoveDatabaseCommand { get; set; }
        private void RemoveDatabaseFromDatabasePaths()
        {
            DatabasePaths.Remove(SelectedDatabase);
            SelectedDatabase = DatabasePaths.FirstOrDefault();
        }

        public ICommand CreateSingleDatabaseCommand { get; set; }

        private void CreateSingleDatabase()
        {
            List<Protein> proteins = new List<Protein>();
            foreach (var database in DatabasePaths)
            {
                try
                {
                    if (database.EndsWith(".xml"))
                        proteins.AddRange(ProteinDbLoader.LoadProteinXML(database, GenerateTargets, SelectedDecoyType, GlobalVariables.AllModsKnown, false,
                            new List<string>(), out _));
                    else if (database.EndsWith(".fasta"))
                        proteins.AddRange(ProteinDbLoader.LoadProteinFasta(database, GenerateTargets, SelectedDecoyType, false, out _));
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Error Reading in Database {database}\n{e.Message}");
                }
            }

            string finalPath = GetFinalPath(OutputDatabasePath);
            WriteDatabase(proteins);
            MessageBox.Show($"New Database Outputted to {finalPath}");
        }

        public ICommand RemoveSearchResultCommand { get; set; }

        private void RemoveSearchResultFromSearchResultPaths()
        {
            SearchResultPaths.Remove(SelectedSearchResult);
            SelectedSearchResult = SearchResultPaths.FirstOrDefault();
        }

        public ICommand ClearDataCommand { get; set; }
        public void Clear()
        {
            DatabasePaths.Clear();
            SearchResultPaths.Clear();
            OutputDatabasePath = null;
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

        private void WriteDatabase(List<Protein> proteins)
        {
            string finalpath = GetFinalPath(OutputDatabasePath);

            try
            {
                switch (SelectedOutputType)
                {
                    case "fasta":
                        ProteinDbWriter.WriteFastaDatabase(proteins, finalpath, ">");
                        break;
                    case "xml":
                        var modDict = CreateModDictionary(proteins);
                        ProteinDbWriter.WriteXmlDatabase(modDict, proteins, finalpath);
                        break;
                    default:
                        throw new ArgumentException("not a valid database output type");
                }
            }
            catch (Exception e)
            {
                MessageBox.Show($"Database Writing Error: {e.Message}");
            }
            
        }

        private Dictionary<string, HashSet<Tuple<int, Modification>>> CreateModDictionary(List<Protein> proteins)
        {
            Dictionary<string, HashSet<Tuple<int, Modification>>> modDict = new();
            HashSet<Tuple<int, Modification>> mods = new();
            foreach (var protein in proteins)
            {
                mods.Clear();
                foreach (var modificationDictEntry in protein.OneBasedPossibleLocalizedModifications)
                {
                    foreach (var mod in modificationDictEntry.Value)
                    {
                        var tuple = new Tuple<int, Modification>(modificationDictEntry.Key, mod);
                        if (modDict.TryGetValue(protein.Accession, out var hash))
                            hash.Add(tuple);
                        
                        else
                            modDict[protein.Accession] = new HashSet<Tuple<int, Modification>>() { tuple };
                    }
                }
            }

            return modDict;
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
