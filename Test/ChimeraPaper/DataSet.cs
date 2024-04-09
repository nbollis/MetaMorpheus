using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Chemistry;
using Easy.Common.Extensions;
using EngineLayer;
using MassSpectrometry;
using Microsoft.WindowsAPICodePack.Shell;
using NUnit.Framework;
using Readers;
using Test.AveragingPaper;
using Test.ChimeraPaper.ResultFiles;

namespace Test.ChimeraPaper
{

    public enum BottomUpResultType
    {
        MetaMorpheus,
        MetaMorpheusNoChimeras,
        MetaMorpheusFraggerLike,
        MetaMorpheusFraggerLikeNoChimeras,
        MsFragger,
        MsFraggerDDAPlus,
    }



    /// <summary>
    /// Class representing a dataset of mass spectrometry data files and a result file.
    /// This class is specific to the file structure I have in the
    /// D:/Projects/Chimeras/Mann_11cell_analysis directory.
    /// </summary>
    public class Dataset
    {
        private string[] _dataFilePaths;
        private List<MsDataFile> _dataFiles;

        // directory paths
        internal string _psmResultPath;
        internal string _psmResultNoChimerasPath;
        internal string _peptideResultPath;
        internal string _peptideResultNoChimerasPath;
        internal string _proteinGroupsResultPath;
        internal string _proteinGroupsNoChimerasResultPath;
        internal string _directoryPath;
        internal string _searchResultDirectoryPath;
        internal string _msFraggerResultDirectoryPath;
        internal string _msFraggerResultDirectoryPathDDAPlus;
        internal string _metaMorpheusResultDirectoryPath;
        internal string _metaMorpheusNoChimeraResultDirectoryPath;
        internal string _metaMorpheusFraggerEquivalentDirectoryPath;
        internal string _metaMorpheusFraggerEquivalentNoChimerasDirectoryPath;
        internal string _prosightPDResultDirectoryPath;
        internal string _msPathFinderTResultDirectoryPath;
       internal string _chimeraMzmlDirectoryPath => Directory.GetDirectories(_directoryPath, "ChimeraMzmls").First();

        // processed result paths

        internal string _combinedMsFraggerDDAPlusPeptideResultsPath => Path.Combine(_directoryPath, $"{DatasetName}_CombinedMsFraggerResultsDDA+_peptide.tsv");
        internal string _combinedMsFraggerDDAPlusProteinResultsPath => Path.Combine(_searchResultDirectoryPath, "MsFraggerDDA+", $"combined_protein.tsv");
        internal string _combinedMsFraggerPeptideResultsPath => Path.Combine(_directoryPath, $"{DatasetName}_CombinedMsFraggerResults_peptide.tsv");
        internal string _combinedMsFraggerProteinResultsPath => Path.Combine(_searchResultDirectoryPath, "MsFragger", $"combined_protein.tsv");



        public string DatasetName { get; set; }
        public List<MsDataFile> DataFiles => _dataFiles ??= _dataFilePaths.Select(MsDataFileReader.GetDataFile).ToList();


        public Dataset(string directoryPath)
        {
            _directoryPath = directoryPath;
            _searchResultDirectoryPath = Directory.GetDirectories(_directoryPath, "SearchResults").First();
            _metaMorpheusResultDirectoryPath = Directory.GetDirectories(_searchResultDirectoryPath, "MetaMorpheusWithLibrary").First();
            _metaMorpheusNoChimeraResultDirectoryPath = Directory.GetDirectories(_searchResultDirectoryPath, "MetaMorpheusNoChimerasWithLibrary").First();
            _metaMorpheusFraggerEquivalentDirectoryPath = Directory.GetDirectories(Path.Combine(_searchResultDirectoryPath, "MetaMorpheusFraggerEquivalent"), "Task3-WithChimeras").FirstOrDefault();
            _metaMorpheusFraggerEquivalentNoChimerasDirectoryPath= Directory.GetDirectories(Path.Combine(_searchResultDirectoryPath, "MetaMorpheusFraggerEquivalent"), "Task2-NoChimeras").FirstOrDefault();
            _msFraggerResultDirectoryPath = Directory.GetDirectories(_searchResultDirectoryPath, "MsFragger").FirstOrDefault();
            _msFraggerResultDirectoryPathDDAPlus = Directory.GetDirectories(_searchResultDirectoryPath, "*DDA+").FirstOrDefault();
            _prosightPDResultDirectoryPath = Directory.GetDirectories(_searchResultDirectoryPath, "ProsightPD").FirstOrDefault();
            _msPathFinderTResultDirectoryPath = Directory.GetDirectories(_searchResultDirectoryPath, "MsPathFinderT").FirstOrDefault();

            DatasetName = Path.GetFileName(_directoryPath);
            _psmResultPath = Directory.GetFiles(_metaMorpheusResultDirectoryPath, "*PSMs.psmtsv", SearchOption.AllDirectories).First();
            _psmResultNoChimerasPath= Directory.GetFiles(_metaMorpheusNoChimeraResultDirectoryPath, "*PSMs.psmtsv", SearchOption.AllDirectories).First();
            _peptideResultPath = Directory.GetFiles(_metaMorpheusResultDirectoryPath, "*Peptides.psmtsv", SearchOption.AllDirectories).First();
            _peptideResultNoChimerasPath = Directory.GetFiles(_metaMorpheusNoChimeraResultDirectoryPath, "*Peptides.psmtsv", SearchOption.AllDirectories).First();
            _proteinGroupsResultPath = Directory.GetFiles(_metaMorpheusResultDirectoryPath, "*ProteinGroups.tsv", SearchOption.AllDirectories).First();
            _proteinGroupsNoChimerasResultPath = Directory.GetFiles(_metaMorpheusNoChimeraResultDirectoryPath, "*ProteinGroups.tsv", SearchOption.AllDirectories).First();


            // this magic is to look inside the shortcuts to the datasets to find the data files
            var dataFileShortcutPath = Directory.GetFileSystemEntries(_directoryPath, "*.lnk").FirstOrDefault();
            if (dataFileShortcutPath is not null)
            {
                var shellObject = ShellObject.FromParsingName(dataFileShortcutPath);
                var dataFilePath = shellObject.Properties.System.Link.TargetParsingPath.Value;
                if (dataFilePath.StartsWith("Z:"))
                    dataFilePath = dataFilePath.Replace("Z:", @"B:");
                _dataFilePaths = Directory.GetFiles(dataFilePath, "*.raw", SearchOption.AllDirectories);
            }
        }

        #region ProcessedResults

        internal string _chimeraCountMetaMorpheusPath => Path.Combine(_directoryPath, $"{DatasetName}_MetaMorpheus_{FileIdentifiers.ChimeraCountingFile}");
        private ChimeraCountingFile _chimeraCountingFile;
        public ChimeraCountingFile ChimeraCountingFile
        {
            get
            {
                if (File.Exists(_chimeraCountMetaMorpheusPath))
                {
                    _chimeraCountingFile ??= new ChimeraCountingFile() {FilePath = _chimeraCountMetaMorpheusPath};
                    return _chimeraCountingFile;
                }
                else
                {
                    CountMetaMorpheusChimericPsms();
                    _chimeraCountingFile ??= new ChimeraCountingFile() { FilePath = _chimeraCountMetaMorpheusPath};
                    return _chimeraCountingFile;
                }
            }
        }


        internal string _chimeraCountMsFraggerDDAPlusPath => Path.Combine(_directoryPath, $"{DatasetName}_Fragger_{FileIdentifiers.ChimeraCountingFile}");
        private ChimeraCountingFile _fraggerChimeraCountingFile;
        public ChimeraCountingFile FraggerChimeraCountingFile
        {
            get
            {
                if (File.Exists(_chimeraCountMetaMorpheusPath))
                {
                    _fraggerChimeraCountingFile ??= new ChimeraCountingFile() { FilePath = _chimeraCountMsFraggerDDAPlusPath };
                    return _chimeraCountingFile;
                }
                else
                {
                    CountMetaMorpheusChimericPsms();
                    _fraggerChimeraCountingFile ??= new ChimeraCountingFile() { FilePath = _chimeraCountMsFraggerDDAPlusPath };
                    return _fraggerChimeraCountingFile;
                }
            }
        }

        internal string _chimeraCountMetaMorpheusFraggerEquivalentPath => Path.Combine(_directoryPath, $"{DatasetName}_MetaMorpheusFraggerEquivalent_{FileIdentifiers.ChimeraCountingFile}");
        private ChimeraCountingFile _chimeraCountMetaMorpheusFraggerEquivalentFile;
        public ChimeraCountingFile ChimeraCountingMetaMorpheusFraggerEquivalentFile
        {
            get
            {
                if (File.Exists(_chimeraCountMetaMorpheusFraggerEquivalentPath))
                {
                    _chimeraCountMetaMorpheusFraggerEquivalentFile ??= new ChimeraCountingFile() { FilePath = _chimeraCountMetaMorpheusFraggerEquivalentPath };
                    return _chimeraCountMetaMorpheusFraggerEquivalentFile;
                }
                else
                {
                    CountMetaMorpheusChimericPsms();
                    _chimeraCountMetaMorpheusFraggerEquivalentFile ??= new ChimeraCountingFile() { FilePath = _chimeraCountMetaMorpheusFraggerEquivalentPath };
                    return _chimeraCountMetaMorpheusFraggerEquivalentFile;
                }
            }
        }


        internal string _combinedMsFraggerDDAPlusPSMResultsPath => Path.Combine(_directoryPath, $"{DatasetName}_CombinedMsFraggerResultsDDA+_psm.tsv");
        private MsFraggerPsmFile _combinedMsFraggerPsmFileDDAPlus;
        public MsFraggerPsmFile CombinedMsFraggerPsmFileDDAPlus
        {
            get
            {
                if (File.Exists(_combinedMsFraggerDDAPlusPSMResultsPath))
                {
                    _combinedMsFraggerPsmFileDDAPlus ??= new MsFraggerPsmFile(_combinedMsFraggerDDAPlusPSMResultsPath);
                    return _combinedMsFraggerPsmFileDDAPlus;
                }
                else
                {
                    CombineDDAPlusPSMFraggerResults();
                    _combinedMsFraggerPsmFileDDAPlus ??= new MsFraggerPsmFile(_combinedMsFraggerDDAPlusPSMResultsPath);
                    return _combinedMsFraggerPsmFileDDAPlus;
                }
            }
        }


        internal string _combinedMsFraggerPSMResultsPath => Path.Combine(_directoryPath, $"{DatasetName}_CombinedMsFraggerResults_psm.tsv");
        private MsFraggerPsmFile _combinedMsFraggerPsmFile;
        public MsFraggerPsmFile CombinedMsFraggerPsmFile
        {
            get
            {
                if (File.Exists(_combinedMsFraggerPSMResultsPath))
                {
                    _combinedMsFraggerPsmFile ??= new MsFraggerPsmFile(_combinedMsFraggerPSMResultsPath);
                    return _combinedMsFraggerPsmFile;
                }
                else
                {
                    CombineMsFraggerPSMResults();
                    _combinedMsFraggerPsmFile ??= new MsFraggerPsmFile(_combinedMsFraggerPSMResultsPath);
                    return _combinedMsFraggerPsmFile;
                }
            }
        }


        #endregion

        #region Operations

        public void CountMetaMorpheusChimericPsms(bool fraggerLike = false)
        {
            var path = fraggerLike ? _chimeraCountMetaMorpheusFraggerEquivalentPath : _chimeraCountMetaMorpheusPath;
            var resultPath = fraggerLike ? Path.Combine(_metaMorpheusFraggerEquivalentDirectoryPath,"Task3-WithChimeras", "AllPSMs.psmtsv") : _psmResultPath;
            var software = fraggerLike ? "MetaMorpheusFraggerEquivalent ": "MetaMorpheus";
            if (!File.Exists(resultPath) || File.Exists(path))
                return;

            var psms = PsmTsvReader.ReadTsv(resultPath, out _).Where(p => p.DecoyContamTarget == "T").ToList();
            var allPsmCounts = psms.GroupBy(p => p, CustomComparer<PsmFromTsv>.ChimeraComparer)
                .GroupBy(m => m.Count()).ToDictionary(p => p.Key, p => p.Count());
            var onePercentFdrPsmCounts = psms.Where(p => p.PEP_QValue <= 0.01).GroupBy(p => p, CustomComparer<PsmFromTsv>.ChimeraComparer)
                .GroupBy(m => m.Count()).ToDictionary(p => p.Key, p => p.Count());

            var results = allPsmCounts.Keys.Select(count => new ChimeraCountingResult(count, allPsmCounts[count],
                onePercentFdrPsmCounts.TryGetValue(count, out var psmCount) ? psmCount : 0, DatasetName, software)).ToList();

            _chimeraCountingFile = new ChimeraCountingFile() { FilePath = path, Results = results};
            _chimeraCountingFile.WriteResults(path);
        }

        public void CountMsFraggerChimericPsms()
        {
            if (_msFraggerResultDirectoryPathDDAPlus is not null && File.Exists(_chimeraCountMsFraggerDDAPlusPath))
                return;

            var allPSms = CombinedMsFraggerPsmFileDDAPlus.Results.GroupBy(p => p, CustomComparer<MsFraggerPsm>.MsFraggerChimeraComparer)
                .GroupBy(m => m.Count()).ToDictionary(p => p.Key, p => p.Count());
            var eValueFiltered = CombinedMsFraggerPsmFileDDAPlus.Results.Where(p => p.Expectation <= 0.01).GroupBy(p => p, CustomComparer<MsFraggerPsm>.MsFraggerChimeraComparer)
                .GroupBy(m => m.Count()).ToDictionary(p => p.Key, p => p.Count());

            var results = allPSms.Keys.Select(count => new ChimeraCountingResult(count, allPSms[count],
                eValueFiltered.TryGetValue(count, out var psmCount) ? psmCount : 0, DatasetName, "DDA+")).ToList();
            _fraggerChimeraCountingFile = new ChimeraCountingFile() { FilePath = _chimeraCountMsFraggerDDAPlusPath, Results = results};
            _fraggerChimeraCountingFile.WriteResults(_chimeraCountMsFraggerDDAPlusPath);
        }



        #region Combining Fragger Results

        public void CombineDDAPlusPSMFraggerResults()
        {
            if (_msFraggerResultDirectoryPathDDAPlus is not null && File.Exists(_combinedMsFraggerDDAPlusPSMResultsPath))
                return;

            var msFraggerResultFiles =
                Directory.GetFiles(_msFraggerResultDirectoryPathDDAPlus, "*psm.tsv", SearchOption.AllDirectories);

            var outPath = Path.Combine(_directoryPath, _combinedMsFraggerDDAPlusPSMResultsPath);
            var results = new List<MsFraggerPsm>();
            foreach (var file in msFraggerResultFiles)
            {
                var msFraggerPsmFile = new MsFraggerPsmFile(file);
                msFraggerPsmFile.LoadResults();
                results.AddRange(msFraggerPsmFile.Results);
            }

            var combinedMsFraggerPsmFile = new MsFraggerPsmFile(outPath) { Results = results };
            combinedMsFraggerPsmFile.WriteResults(outPath);
        }

        public void CombineMsFraggerPSMResults()
        {
            if (_msFraggerResultDirectoryPath is not null && File.Exists(_combinedMsFraggerPSMResultsPath))
                return;

            var msFraggerResultFiles =
                Directory.GetFiles(_msFraggerResultDirectoryPath, "*psm.tsv", SearchOption.AllDirectories);

            var outPath = Path.Combine(_directoryPath, _combinedMsFraggerPSMResultsPath);
            var results = new List<MsFraggerPsm>();
            foreach (var file in msFraggerResultFiles)
            {
                var msFraggerPsmFile = new MsFraggerPsmFile(file);
                msFraggerPsmFile.LoadResults();
                results.AddRange(msFraggerPsmFile.Results);
            }

            var combinedMsFraggerPsmFile = new MsFraggerPsmFile(outPath) { Results = results };
            combinedMsFraggerPsmFile.WriteResults(outPath);
        }

        public void CombineMsFraggerPeptideResults(bool ddaPlus = false)
        {
            string directoryPath = ddaPlus ? _msFraggerResultDirectoryPathDDAPlus : _msFraggerResultDirectoryPath;
            string combinedPeptideResultsPath = ddaPlus ? _combinedMsFraggerDDAPlusPeptideResultsPath : _combinedMsFraggerPeptideResultsPath;
            if (directoryPath is not null && File.Exists(combinedPeptideResultsPath))
                return;

            var msFraggerResultFilePaths = Directory.GetFiles(directoryPath, "*peptide.tsv", SearchOption.AllDirectories);
            List<MsFraggerPeptide> results = new List<MsFraggerPeptide>();
            foreach (var fraggerfilePath in msFraggerResultFilePaths)
            {
                var msFraggerPeptideFile = new MsFraggerPeptideFile(fraggerfilePath);
                msFraggerPeptideFile.LoadResults();
                results.AddRange(msFraggerPeptideFile.Results);
            }
            var distinct = results.GroupBy(p => p, CustomComparer<MsFraggerPeptide>.MsFraggerPeptideDistinctComparer)
                .Select(p => p.MaxBy(result => result.Probability))
                .ToList();
            var combinedMsFraggerPeptideFile = new MsFraggerPeptideFile(combinedPeptideResultsPath) { Results = distinct, FilePath = combinedPeptideResultsPath!};
            combinedMsFraggerPeptideFile.WriteResults(combinedPeptideResultsPath);
        }

        public void CreateFraggerIndividualFileOutput()
        {
            string directoryPath = true ? _msFraggerResultDirectoryPathDDAPlus : _msFraggerResultDirectoryPath;
            string filename =  $"{DatasetName}_{FileIdentifiers.IndividualFraggerFileComparison}";
            string outPath = Path.Combine(_directoryPath, filename);
            if (File.Exists(outPath))
                return;

            var msFraggerPsmResultFilePaths = Directory.GetFiles(directoryPath, "*psm.tsv", SearchOption.AllDirectories);
            var msFraggerPeptideResultFilePaths = Directory.GetFiles(directoryPath, "*peptide.tsv", SearchOption.AllDirectories);
            var msFraggerProteinResultFilePaths = Directory.GetFiles(directoryPath, "*protein.tsv", SearchOption.AllDirectories);

            List<BulkResultCountComparison> bulkResultCountComparisonFiles = new List<BulkResultCountComparison>();
            for (int i = 0; i < msFraggerPsmResultFilePaths.Length; i++)
            {
                var psms = new MsFraggerPsmFile(msFraggerPsmResultFilePaths[i]);
                psms.LoadResults();
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(psms.First().FileNameWithoutExtension);
                string condition = fileNameWithoutExtension!.Replace("interact-", "");
                  
                var peptides = new MsFraggerPeptideFile(msFraggerPeptideResultFilePaths[i]);
                peptides.LoadResults();

                int proteinCount;
                int onePercentProteinCount;
                using (var sr = new StreamReader(msFraggerProteinResultFilePaths[i]))
                {
                    var header = sr.ReadLine();
                    var headerSplit = header.Split('\t');
                    var qValueIndex = Array.IndexOf(headerSplit, "Protein Probability");
                    int count = 0;
                    int onePercentCount = 0;

                    while (!sr.EndOfStream)
                    {
                        var line = sr.ReadLine();
                        var values = line.Split('\t');
                        count++;
                        if (double.Parse(values[qValueIndex]) >= 0.99)
                            onePercentCount++;
                    }

                    proteinCount = count;
                    onePercentProteinCount = onePercentCount;
                }
                

                bulkResultCountComparisonFiles.Add(new BulkResultCountComparison()
                {
                    DatasetName = DatasetName,
                    Condition = "DDA+",
                    FileName = condition,
                    PsmCount = psms.Results.Count,
                    OnePercentPsmCount = psms.Results.Count(p => p.PeptideProphetProbability >= 0.99),
                    PeptideCount = peptides.Results.Count,
                    OnePercentPeptideCount = peptides.Results.Count(p => p.Probability >= 0.99),
                    ProteinGroupCount = proteinCount,
                    OnePercentProteinGroupCount = onePercentProteinCount
                });
            }


            directoryPath = false ? _msFraggerResultDirectoryPathDDAPlus : _msFraggerResultDirectoryPath;
            msFraggerPsmResultFilePaths = Directory.GetFiles(directoryPath, "*psm.tsv", SearchOption.AllDirectories);
            msFraggerPeptideResultFilePaths = Directory.GetFiles(directoryPath, "*peptide.tsv", SearchOption.AllDirectories);
            msFraggerProteinResultFilePaths = Directory.GetFiles(directoryPath, "*protein.tsv", SearchOption.AllDirectories);
            for (int i = 0; i < msFraggerPsmResultFilePaths.Length; i++)
            {
                var psms = new MsFraggerPsmFile(msFraggerPsmResultFilePaths[i]);
                psms.LoadResults();
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(psms.First().FileNameWithoutExtension);
                string condition = fileNameWithoutExtension!.Replace("interact-", "");

                var peptides = new MsFraggerPeptideFile(msFraggerPeptideResultFilePaths[i]);
                peptides.LoadResults();

                int proteinCount;
                int onePercentProteinCount;
                using (var sr = new StreamReader(msFraggerProteinResultFilePaths[i]))
                {
                    var header = sr.ReadLine();
                    var headerSplit = header.Split('\t');
                    var qValueIndex = Array.IndexOf(headerSplit, "Protein Probability");
                    int count = 0;
                    int onePercentCount = 0;

                    while (!sr.EndOfStream)
                    {
                        var line = sr.ReadLine();
                        var values = line.Split('\t');
                        count++;
                        if (double.Parse(values[qValueIndex]) >= 0.99)
                            onePercentCount++;
                    }

                    proteinCount = count;
                    onePercentProteinCount = onePercentCount;
                }


                bulkResultCountComparisonFiles.Add(new BulkResultCountComparison()
                {
                    DatasetName = DatasetName,
                    Condition = "DDA",
                    FileName = condition,
                    PsmCount = psms.Results.Count,
                    OnePercentPsmCount = psms.Results.Count(p => p.PeptideProphetProbability >= 0.99),
                    PeptideCount = peptides.Results.Count,
                    OnePercentPeptideCount = peptides.Results.Count(p => p.Probability >= 0.99),
                    ProteinGroupCount = proteinCount,
                    OnePercentProteinGroupCount = onePercentProteinCount
                });
            }




            var bulkComparisonFile = new BulkResultCountComparisonFile()
                { FilePath = outPath, Results = bulkResultCountComparisonFiles };
            bulkComparisonFile.WriteResults(outPath);
        }


        #endregion


        //TODO: For each set of chimeric spectra in the dataset, if the IDs that are not the peak selected for isolation are at 1% FDR or higher,
        // then take them and modify the dataset so they are the precursor mz and output that mzml with a map of the original IDs to the new spectra for validation
        public void CreateChimericMzMls()
        {
            string directoryPath = _chimeraMzmlDirectoryPath;
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            if (Directory.GetFiles(directoryPath).Length == DataFiles.Count)
                return;


            var psms = PsmTsvReader.ReadTsv(_psmResultPath, out _);
            var onePercentChimerasGroupedByFile = psms.Where(p => p.QValue <= 0.01 && !p.IsDecoy() && p.AmbiguityLevel == "1")
                .GroupBy(p => p.FileNameWithoutExtension.Replace("-calib-averaged", ""))
                .ToDictionary(p => p.Key,
                    p => p.GroupBy(m => m, CustomComparer<PsmFromTsv>.ChimeraComparer)
                                                  .Where(n => n.Count() > 1).ToDictionary(m => m.Key.Ms2ScanNumber, m => m.ToList()));

            // create the new mzmls
            foreach (var dataFileName in onePercentChimerasGroupedByFile)
            {
                var dataFilePath = _dataFilePaths.First(p => p.Contains(dataFileName.Key));
                var dataFile = MsDataFileReader.GetDataFile(dataFilePath);
                var newFilePath = Path.Combine(directoryPath, $"{dataFileName.Key}_ChimeraExpanded.mzml");
                if (File.Exists(newFilePath)) // short circuit if this file already exists
                    continue;


                var sourceFile = dataFile.GetSourceFile();
                dataFile.LoadAllStaticData();
                var chimeras = dataFileName.Value;

                // for each chimeric spectra, replace the nonselected peak mz with the chimeric ID mz
                List<MsDataScan> outputScans = new List<MsDataScan>();
                Dictionary<int, int> precursorMap = new Dictionary<int, int>();
                List<ChimeraSpecFileMap> specFileMaps = new List<ChimeraSpecFileMap>();


                string newNativeId;
                int precursorIndex = 1;
                int scanIndex = 1;
                foreach (var scan in dataFile.Scans)
                {
                    var copy = scan.Clone();
                    var nativeId = scan.NativeId;
                    switch (scan.MsnOrder)
                    {
                        // for each ms1, set the scan number and nativeID then add it to the output
                        case 1:
                            precursorIndex = scanIndex;
                            newNativeId = nativeId.Replace(nativeId.Split('=').Last(), scanIndex.ToString());
                            precursorMap.Add(scan.OneBasedScanNumber, precursorIndex);

                            copy.SetOneBasedScanNumber(precursorIndex);
                            copy.SetNativeID(newNativeId);

                            outputScans.Add(copy);
                            scanIndex++;
                            continue;
                        // for non-chimeric MS2s, update the scan number and NativeId then add it to the output
                        case 2 when !chimeras.TryGetValue(scan.OneBasedScanNumber, out psms):
                            newNativeId = nativeId.Replace(nativeId.Split('=').Last(), scanIndex.ToString());
                            var precursorScanNum = precursorMap[scan.OneBasedPrecursorScanNumber.Value];

                            copy.SetNativeID(newNativeId);
                            copy.SetOneBasedScanNumber(scanIndex);
                            copy.SetOneBasedPrecursorScanNumber(precursorScanNum);
                            outputScans.Add(copy);
                            scanIndex++;
                            continue;
                        // for chimeric MS2s, create a new ms2 scan for each chimeric Id and set the selection ion mz equal to the chimeric ID precursor mz
                        case 2:
                        {
                            foreach (var psm in psms)
                            {
                                newNativeId = nativeId.Replace(nativeId.Split('=').Last(), scanIndex.ToString());
                                var precursorScanNumber = precursorMap[scan.OneBasedPrecursorScanNumber.Value];
                                var precursorMz = psm.PrecursorMz + double.Parse(psm.MassDiffDa);
                                var precursorCharge = psm.PrecursorCharge;
                                var precursorPeakIndex = scan.MassSpectrum.GetClosestPeakIndex(psm.PrecursorMz);
                                var precursorIntensity = scan.MassSpectrum.YArray[precursorPeakIndex];


                                var newScan = scan.CloneWithNewPrecursor(precursorMz, precursorCharge, precursorIntensity);
                                newScan.SetNativeID(newNativeId);
                                newScan.SetOneBasedScanNumber(scanIndex);
                                newScan.SetOneBasedPrecursorScanNumber(precursorScanNumber);
                                outputScans.Add(newScan);
                                scanIndex++;

                                specFileMaps.Add(new ChimeraSpecFileMap()
                                {
                                    OriginalMs1ScanNumber = scan.OneBasedPrecursorScanNumber.Value,
                                    OriginalMs2ScanNumber = scan.OneBasedScanNumber,
                                    NewMs1ScanNumber = newScan.OneBasedPrecursorScanNumber.Value,
                                    NewMs2ScanNumber = newScan.OneBasedScanNumber,
                                    OriginalPeptideBaseSequence = psm.BaseSeq,
                                    OriginalPeptideAccession = psm.ProteinAccession,
                                    OriginalPeptideFullSequence = psm.FullSequence
                                });
                            }

                            break;
                        }
                    }
                }

                

                var newFile = new GenericMsDataFile( outputScans.ToArray(), sourceFile);
                newFile.ExportAsMzML(newFilePath, true);
                var mapFile = new ChimeraSpecFileMapFile() { FilePath = newFilePath.Replace(".mzml", "_Map.tsv"), Results = specFileMaps};
                mapFile.WriteResults(mapFile.FilePath);
            }
        }


       




        #endregion

        public override string ToString()
        {
            return DatasetName;
        }
    }

    public static class MsScanExtensions
    {
        public static MsDataScan CloneWithNewPrecursor(this MsDataScan scan, double precursorMz, int precursorCharge,
            double precursorIntensity)
        {
            return new MsDataScan(scan.MassSpectrum, scan.OneBasedScanNumber, scan.MsnOrder, scan.IsCentroid,
                scan.Polarity, scan.RetentionTime, scan.ScanWindowRange, scan.ScanFilter, scan.MzAnalyzer,
                scan.TotalIonCurrent, scan.InjectionTime, scan.NoiseData, scan.NativeId, precursorMz,
                precursorCharge, precursorIntensity, scan.IsolationMz, scan.IsolationWidth,
                scan.DissociationType, scan.OneBasedPrecursorScanNumber, scan.SelectedIonMonoisotopicGuessMz,
                scan.HcdEnergy);
        }

        public static MsDataScan Clone(this MsDataScan scan)
        {
            return new MsDataScan(scan.MassSpectrum, scan.OneBasedScanNumber, scan.MsnOrder, scan.IsCentroid,
                scan.Polarity, scan.RetentionTime, scan.ScanWindowRange, scan.ScanFilter, scan.MzAnalyzer,
                scan.TotalIonCurrent, scan.InjectionTime, scan.NoiseData, scan.NativeId, scan.SelectedIonMZ,
                scan.SelectedIonChargeStateGuess, scan.SelectedIonIntensity, scan.IsolationMz, scan.IsolationWidth,
                scan.DissociationType, scan.OneBasedPrecursorScanNumber, scan.SelectedIonMonoisotopicGuessMz,
                scan.HcdEnergy);
        }
    }
}
