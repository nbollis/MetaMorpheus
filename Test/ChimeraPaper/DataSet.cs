using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    internal class Dataset
    {
        private string[] _dataFilePaths;
        private List<MsDataFile> _dataFiles;
        private string _psmResultPath;
        private string _peptideResultPath;
        private string _directoryPath;

        public string DatasetName { get; set; }

        public List<MsDataFile> DataFiles =>
            _dataFiles ??= _dataFilePaths.Select(MsDataFileReader.GetDataFile).ToList();


        public Dataset(string directoryPath)
        {
            _directoryPath = directoryPath;
            DatasetName = Path.GetFileName(_directoryPath);
            _psmResultPath = Directory.GetFiles(_directoryPath, "*PSMs.psmtsv").First();
            _peptideResultPath = Directory.GetFiles(_directoryPath, "*Peptides.psmtsv").First();

            // this magic is to look inside the shortcuts to the datasets to find the data files
            var dataFileShortcutPath = Directory.GetFileSystemEntries(_directoryPath, "*.lnk").FirstOrDefault();
            if (dataFileShortcutPath is not null)
            {
                var shellObject = ShellObject.FromParsingName(dataFileShortcutPath);
                var dataFilePath = shellObject.Properties.System.Link.TargetParsingPath.Value;
                _dataFilePaths = Directory.GetFiles(dataFilePath, "*.raw", SearchOption.AllDirectories);
            }
        }

        #region ProcessedResults

        private string _chimeraCountPath => Path.Combine(_directoryPath, $"{DatasetName}_{ChimeraCountingFile.FileIdentifier}");
        private ChimeraCountingFile _chimeraCountingFile;
        public ChimeraCountingFile ChimeraCountingFile
        {
            get
            {
                if (File.Exists(_chimeraCountPath))
                {
                    _chimeraCountingFile ??= new ChimeraCountingFile() {FilePath = _chimeraCountPath};
                    return _chimeraCountingFile;
                }
                else
                {
                    CountChimericPsms();
                    _chimeraCountingFile ??= new ChimeraCountingFile() { FilePath = _chimeraCountPath};
                    return _chimeraCountingFile;
                }
            }
        }

        #endregion

        #region Operations

        public void CountChimericPsms()
        {
            if (File.Exists(_chimeraCountPath))
                return;

            var psms = PsmTsvReader.ReadTsv(_psmResultPath, out _);
            var allPsmCounts = psms.GroupBy(p => p, CustomComparer<PsmFromTsv>.ChimeraComparer)
                .GroupBy(m => m.Count()).ToDictionary(p => p.Key, p => p.Count());
            var onePercentFdrPsmCounts = psms.Where(p => p.QValue <= 0.01).GroupBy(p => p, CustomComparer<PsmFromTsv>.ChimeraComparer)
                .GroupBy(m => m.Count()).ToDictionary(p => p.Key, p => p.Count());

            var results = allPsmCounts.Keys.Select(count => new ChimeraCountingResult(count, allPsmCounts[count],
                onePercentFdrPsmCounts.TryGetValue(count, out var psmCount) ? psmCount : 0)).ToList();

            _chimeraCountingFile = new ChimeraCountingFile() { FilePath = _chimeraCountPath, Results = results};
            _chimeraCountingFile.WriteResults(_chimeraCountPath);
        }

        //TODO: For each set of chimeric spectra in the dataset, if the IDs that are not the peak selected for isolation are at 1% FDR or higher,
        // then take them and modify the dataset so they are the precursor mz and output that mzml with a map of the original IDs to the new spectra for validation


        #endregion
    }
}
