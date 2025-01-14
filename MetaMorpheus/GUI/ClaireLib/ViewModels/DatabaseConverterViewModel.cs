using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Easy.Common.Extensions;
using EngineLayer;
using GuiFunctions;
using pepXML.Generated;
using Proteomics;
using Readers;
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

        #region Parameters

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

        private bool _appendSearchResults;
        public bool AppendSearchResults
        {
            get => _appendSearchResults;
            set { _appendSearchResults = value; OnPropertyChanged(nameof(AppendSearchResults)); }
        }

        

        private bool _filterToFdr;
        public bool FilterToFdr
        {
            get => _filterToFdr;
            set { _filterToFdr = value; OnPropertyChanged(nameof(FilterToFdr)); }
        }

        private double _fdrCutoff;
        public double FdrCutoff
        {
            get => _fdrCutoff;
            set { _fdrCutoff = value; OnPropertyChanged(nameof(FdrCutoff)); }
        }

        private bool _organismStratifiedDatabaseOutput;
        public bool OrganismStratifiedDatabaseOutput
        {
            get => _organismStratifiedDatabaseOutput;
            set { _organismStratifiedDatabaseOutput = value; OnPropertyChanged(nameof(OrganismStratifiedDatabaseOutput)); }
        }

        // not implemented, only fasta can be exported
        public string[] OutputTypes { get; set; }
        private string selectedOutputType;
        public string SelectedOutputType
        {
            get => selectedOutputType;
            set { selectedOutputType = value; OnPropertyChanged(nameof(SelectedOutputType)); }
        }

        #endregion



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
            AppendSearchResults = true;
            FilterToFdr = true;
            FdrCutoff = 0.01;

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

        private string GetFinalPath(string path, string organism = null)
        {
            if (!path.EndsWith(".fasta"))
                path += ".fasta";
            
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

        public ICommand CreateSingleDatabaseCommand { get; set; }

        private void CreateSingleDatabase()
        {
            List<string> readingErrors = new();

            // load proteins from input database
            List<Protein> proteins = new List<Protein>();
            foreach (var database in DatabasePaths)
            {
                try
                {
                    if (database.EndsWith(".xml"))
                    {
                        proteins.AddRange(ProteinDbLoader.LoadProteinXML(database, GenerateTargets, SelectedDecoyType,
                            GlobalVariables.AllModsKnown, false,
                            new List<string>(), out Dictionary<string, Modification> unknownMods));
                        readingErrors.AddRange(unknownMods.Select(keyValuePair =>
                            $"unknown modificaiton found on {keyValuePair.Key} with type {keyValuePair.Value.IdWithMotif}"));
                    }
                    else if (database.EndsWith(".fasta"))
                    {
                        proteins.AddRange(ProteinDbLoader.LoadProteinFasta(database, GenerateTargets, SelectedDecoyType,
                            false, out List<string> error));
                        readingErrors.AddRange(error);
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Error Reading in Database {database}\n{e.Message}");
                    return;
                }
            }

            if (!AppendSearchResults)
            {
                // write proteins to output database
                string finalPath = GetFinalPath(OutputDatabasePath);
                if (OrganismStratifiedDatabaseOutput)
                {
                    List<string> paths = new();
                    foreach (var organismGroup in proteins.GroupBy(p => p.Organism))
                    {
                        string extension = Path.GetExtension(finalPath);
                        string organismSpecificPath = finalPath.Replace(extension, $"{organismGroup.Key}.fasta");
                        WriteDatabase(organismGroup.ToList(), organismSpecificPath);
                        paths.Add(organismSpecificPath);
                    }

                    MessageBox.Show($"New Databases Outputted to {string.Join(',', paths)}");
                }
                else
                {
                    WriteDatabase(proteins, finalPath);
                    MessageBox.Show($"New Database Outputted to {finalPath}");
                }

                return;
            }

            // load in search results and collect fasta headers
            List<FastaLine> fastaLines = new(2048);
            foreach (var resultPath in SearchResultPaths)
            {
                try
                {
                    fastaLines.AddRange(GetFastaLinesFromSearchResult(resultPath));
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Error Reading in Search Result {resultPath}\n{e.Message}");
                    return;
                }
            }

            string outputPath = GetFinalPath(OutputDatabasePath);

            // append fasta lines from search results to output database
            if (OrganismStratifiedDatabaseOutput)
            {
                List<string> paths = new();
                foreach (var organismGroup in proteins.GroupBy(p => p.Organism))
                {
                    string extension = Path.GetExtension(outputPath);
                    string organismSpecificPath = outputPath.Replace(extension, $"{organismGroup.Key}.fasta");
                    WriteDatabase(organismGroup.ToList(), organismSpecificPath);
                    paths.Add(organismSpecificPath);

                    Dictionary<string, int> accessionCountDict = organismGroup.GroupBy(p => p.Accession)
                        .ToDictionary(p => p.Key, p => 0);

                    using var streamWriter = new StreamWriter(organismSpecificPath, true);
                    fastaLines.Where(p => p.AnyContains(organismGroup.Key))
                        .DistinctBy(p => (p.Sequence, p.Accession)).ForEach(m =>
                    {
                        if (accessionCountDict.TryGetValue(m.Accession, out var count))
                        {
                            accessionCountDict[m.Accession]++;
                            m.IncrementAccession(accessionCountDict[m.Accession]);
                        }
                        else
                        {
                            accessionCountDict.Add(m.Accession, 0);
                        }

                        streamWriter.WriteLine(m.ToString());
                    });
                    streamWriter.Close();
                }

                MessageBox.Show($"New Databases Outputted to {string.Join(',', paths)}");
            }
            else
            {
                WriteDatabase(proteins, outputPath);
                Dictionary<string, int> accessionCountDict = proteins.GroupBy(p => p.Accession)
                    .ToDictionary(p => p.Key, p => 0);

                using var sw = new StreamWriter(outputPath, true);
                fastaLines.DistinctBy(p => (p.Sequence, p.Accession)).ForEach(m => 
                {
                    if (accessionCountDict.TryGetValue(m.Accession, out var count))
                    {
                        accessionCountDict[m.Accession]++;
                        m.IncrementAccession(accessionCountDict[m.Accession]);
                    }
                    else
                    {
                        accessionCountDict.Add(m.Accession, 0);
                    }
                    sw.WriteLine(m.ToString());
                });
                sw.Close();

                MessageBox.Show($"New Database Outputted to {outputPath}");
            }
        }


        private void WriteDatabase(List<Protein> inputDatabaseProteins, string finalpath)
        {
            try
            {
                switch (SelectedOutputType)
                {
                    case "fasta":
                        ProteinDbWriter.WriteFastaDatabase(inputDatabaseProteins, finalpath, ">");
                        break;
                    case "xml":
                        var modDict = CreateModDictionary(inputDatabaseProteins);
                        ProteinDbWriter.WriteXmlDatabase(modDict, inputDatabaseProteins, finalpath);
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

        private IEnumerable<FastaLine> GetFastaLinesFromSearchResult(string resultPath)
        {
            if (resultPath.EndsWith(".psmtsv"))
            {
                var psms = PsmTsvReader.ReadTsv(resultPath, out List<string> warnings);
                foreach (var psm in psms)
                {
                    if (FilterToFdr && psm.QValue >= FdrCutoff) continue;
                    if (psm.DecoyContamTarget == "D") continue;
                    if (psm.AmbiguityLevel != "1") continue;

                    yield return new FastaLine(psm);
                }
            }
            else if (resultPath.ParseFileType().ToString().Contains("Toppic", StringComparison.InvariantCultureIgnoreCase))
            {

                int paramCount = 0;
                bool foundShit = false;
                int accessionIndex = 0;
                int descriptionIndex = 0;
                int eValueIndex = 0;
                int proteoformIndex = 0;

                using var reader = new StreamReader(resultPath);
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (line is null)
                        continue;
                    if (line.Contains("**** Parameters ****"))
                    {
                        paramCount++;
                        continue;
                    }

                    if (line.StartsWith("Data file name"))
                    {
                        foundShit = true;
                        var headersSplits = line.Split('\t');
                        accessionIndex = headersSplits.IndexOf("Protein accession");
                        descriptionIndex = headersSplits.IndexOf("Protein description");
                        eValueIndex = headersSplits.IndexOf("E-value");
                        proteoformIndex = headersSplits.IndexOf("Proteoform");
                        continue;
                    }

                    if (foundShit && paramCount == 2)
                    {
                        var splits = line.Split('\t');
                        if (splits.Length < 10)
                            continue;
                        if (splits.Length < Math.Max(eValueIndex, proteoformIndex))
                            continue;

                        var accession = splits[accessionIndex];
                        if (accession.Contains("DECOY"))
                            continue;

                        if (FilterToFdr && double.Parse(splits[eValueIndex]) >= FdrCutoff)
                            continue;

                        yield return new FastaLine(accession, splits[descriptionIndex], splits[proteoformIndex]);
                    }
                }
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
            // database
            if (path.EndsWith(".xml") || path.EndsWith(".fasta"))
            {
                if (DatabasePaths.Contains(path))
                    return;

                DatabasePaths.Add(path);
                OnPropertyChanged(nameof(OutputDatabasePath));
            }
            else if (path.EndsWith(".psmtsv") || path.EndsWith(".tsv"))
            {
                if (SearchResultPaths.Contains(path))
                    return;

                SearchResultPaths.Add(path);
            }
            else
            {
                MessageBox.Show($"Cannot determine file type of and ignored file: {path}");
            }
        }
        
    }

    public class FastaLine
    {
        private readonly string _accession;
        public string Sequence { get; init; }
        public string FullSequence { get; init; }
        public string Accession { get; private set; }
        public string ProteinName { get; init; }
        public string OrganismName { get; init; }
        public string GeneName { get; init; }
        public string Description { get; init; }

        public FastaLine(PsmFromTsv psm)
        {
            Sequence = psm.BaseSeq;
            FullSequence = psm.FullSequence;
            _accession = psm.ProteinAccession;
            Accession = psm.ProteinAccession;
            ProteinName = psm.ProteinName;
            OrganismName = psm.OrganismName;

            var gene = "";
            var geneName = psm.GeneName.Split(',');
            GeneName = geneName.Length > 1 ? geneName.First().Split(':')[1] : geneName.First();

            Description = $"OS={psm.OrganismName} GN={GeneName}";
        }

        public FastaLine(string accessionColumn, string descriptionColumn, string proteoformColumn)
        {
            Sequence = proteoformColumn.GetBaseSequenceFromFullSequence();
            FullSequence = proteoformColumn; // TODO: convert this to something useful in xml outputting
            var accessionSplits = accessionColumn.Split('|');
            _accession = accessionSplits[1];
            Accession = accessionSplits[1];
            ProteinName = "";
            Description = descriptionColumn;

            // Extract organism name from descriptionColumn 
            var organismMatch = Regex.Match(descriptionColumn, @"OS=([^=]+?)\s[A-Z]{2}=");
            OrganismName = organismMatch.Success ? organismMatch.Groups[1].Value : "";

            var geneMatch = Regex.Match(descriptionColumn, @"GN=([^=]+?)\s[A-Z]{2}=");
            GeneName = geneMatch.Success ? geneMatch.Groups[1].Value : "";
        }

        public override string ToString()
        {
            var formattedSequence = string.Join("\n", Regex.Matches(Sequence, ".{1,60}").Select(m => m.Value));
            return $">mz|{Accession}|{ProteinName} {Description}\n{formattedSequence}";
        }

        public void IncrementAccession(int number)
        {
            Accession = $"{_accession}-{number}";
        }

        public bool AnyContains(string toCheck)
        {
            if (OrganismName.Contains(toCheck))
                return true;
            if (ProteinName.Contains(toCheck))
                return true;
            if (Description.Contains(toCheck))
                return true;
            if (GeneName.Contains(toCheck))
                return true;
            return false;
        }
    }

    public static class IdExtensions
    {
        public static string GetUniprotHeaderFromPsmFromTsv(this PsmFromTsv psm)
        {
            var gene = "";
            var geneName = psm.GeneName.Split(',');
            gene = geneName.Length > 1 ? geneName.First().Split(':')[1] : geneName.First();
            
            var str =  $">mz|{psm.ProteinAccession}|{psm.ProteinName} OS={psm.OrganismName} GN={gene}";
            return str;
        }

        public static string GetUniprotHeaderFromToppicPrsm(this ToppicPrsm prsm)
        {
          
            var str = $">mz|{string.Join('|', prsm.ProteinAccession.Split('|')[1..])} {prsm.ProteinDescription}";
            return str;
        }

        public static string GetBaseSequenceFromFullSequence(this string FullSequence)
        {
            // Remove text within square brackets
            var text = Regex.Replace(FullSequence, @"\[[^\]]*\]", "");

            // Remove parentheses
            text = Regex.Replace(text, @"[()]", "");

            // Remove periods
            text = Regex.Replace(text, @"(^[^.]+)|(\.[^.]+$)", "")
                .Replace(".", "");
            return text;
        }
    }
}
