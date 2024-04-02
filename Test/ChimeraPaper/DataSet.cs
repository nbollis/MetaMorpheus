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

namespace Test.ChimeraPaper
{

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
        private string _searchResultDirectoryPath;
        internal string _msFraggerResultDirectoryPath;
        private string _msFraggerResultDirectoryPathDDAPlus;
        private string _metaMorpheusResultDirectoryPath;
        private string _metaMorpheusNoChimeraResultDirectoryPath;
        private string _prosightPDResultDirectoryPath;
        private string _msPathFinderTResultDirectoryPath;
        private string _chimeraMzmlDirectoryPath => Directory.GetDirectories(_directoryPath, "ChimeraMzmls").First();

        // processed result paths
        private string _chimeraCountMetaMorpheusPath => Path.Combine(_directoryPath, $"{DatasetName}_MetaMorpheus_{FileIdentifiers.ChimeraCountingFile}");
        private string _chimeraCountMsFraggerDDAPlusPath => Path.Combine(_directoryPath, $"{DatasetName}_Fragger_{FileIdentifiers.ChimeraCountingFile}");
        private string _combinedMsFraggerPSMResultsPath => Path.Combine(_directoryPath, $"{DatasetName}_CombinedMsFraggerResults_psm.tsv");
        private string _combinedMsFraggerDDAPlusPSMResultsPath => Path.Combine(_directoryPath, $"{DatasetName}_CombinedMsFraggerResultsDDA+_psm.tsv");



        public string DatasetName { get; set; }
        public List<MsDataFile> DataFiles => _dataFiles ??= _dataFilePaths.Select(MsDataFileReader.GetDataFile).ToList();


        public Dataset(string directoryPath)
        {
            _directoryPath = directoryPath;
            _searchResultDirectoryPath = Directory.GetDirectories(_directoryPath, "SearchResults").First();
            _metaMorpheusResultDirectoryPath = Directory.GetDirectories(_searchResultDirectoryPath, "MetaMorpheusWithLibrary").First();
            _metaMorpheusNoChimeraResultDirectoryPath = Directory.GetDirectories(_searchResultDirectoryPath, "MetaMorpheusNoChimerasWithLibrary").First();
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
                    CombineDDAPlusMsFraggerResults();
                    _combinedMsFraggerPsmFileDDAPlus ??= new MsFraggerPsmFile(_combinedMsFraggerDDAPlusPSMResultsPath);
                    return _combinedMsFraggerPsmFileDDAPlus;
                }
            }
        }

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

        



        #endregion

        #region Operations

        public void CountMetaMorpheusChimericPsms()
        {
            if (File.Exists(_chimeraCountMetaMorpheusPath))
                return;

            var psms = PsmTsvReader.ReadTsv(_psmResultPath, out _).Where(p => p.DecoyContamTarget == "T").ToList();
            var allPsmCounts = psms.GroupBy(p => p, CustomComparer<PsmFromTsv>.ChimeraComparer)
                .GroupBy(m => m.Count()).ToDictionary(p => p.Key, p => p.Count());
            var onePercentFdrPsmCounts = psms.Where(p => p.QValue <= 0.01).GroupBy(p => p, CustomComparer<PsmFromTsv>.ChimeraComparer)
                .GroupBy(m => m.Count()).ToDictionary(p => p.Key, p => p.Count());

            var results = allPsmCounts.Keys.Select(count => new ChimeraCountingResult(count, allPsmCounts[count],
                onePercentFdrPsmCounts.TryGetValue(count, out var psmCount) ? psmCount : 0, DatasetName, Software.MetaMorpheus.ToString())).ToList();

            _chimeraCountingFile = new ChimeraCountingFile() { FilePath = _chimeraCountMetaMorpheusPath, Results = results};
            _chimeraCountingFile.WriteResults(_chimeraCountMetaMorpheusPath);
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

        public void CombineDDAPlusMsFraggerResults()
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

        public void CombineMsFraggerResults()
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
