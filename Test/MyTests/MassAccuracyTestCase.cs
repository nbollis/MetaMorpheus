using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using IO.MzML;
using MassSpectrometry;
using Nett;
using SpectralAveraging;
using TaskLayer;

namespace Test
{
    internal class MassAccuracyTestCase
    {

        public MassAccuracyTestCase(string originalFilePath)
        {

        }

        #region Private Properties

        private SpectralAveragingParameters averagingParameters;
        private List<MsDataScan> averagedSpectra;
        private List<PsmFromTsv> psms;
        private List<PsmFromTsv> proteoforms;
        private FileMassAccuracyResults fileResults;
        private List<ScanMassAccuracyResults> scanResults;

        #endregion

        #region Paths
        public string SpecificDirectory { get; }
        public string OriginalFilePath { get; }
        public string AveragingTomlPath { get; }
        public string AveragedSpectraPath { get; }
        public string IndividualPeakTsvPath { get; }
        public string ScanMassAccuracyResultTsvPath { get; }
        public string SearchOutputDirectory { get; }
        public string SearchPsmsPath { get; }
        public string SearchProteoformsPath { get; }

        #endregion

        #region Public Properties
        public SpectralAveragingParameters AveragingParameters
            => averagingParameters ??= Toml.ReadFile<SpectralAveragingParameters>(AveragingTomlPath, MetaMorpheusTask.tomlConfig);

        public List<MsDataScan> AveragedSpectra
            => averagedSpectra ??= Mzml.LoadAllStaticData(AveragedSpectraPath).GetAllScansList();

        public List<PsmFromTsv> AllPsms
        {
            get
            {
                var warnings = new List<string>();
                return psms ??= PsmTsvReader.ReadTsv(SearchPsmsPath, out warnings);
            }
        }

        public List<PsmFromTsv> AllProteoforms
        {
            get
            {
                var warnings = new List<string>();
                return psms ??= PsmTsvReader.ReadTsv(SearchProteoformsPath, out warnings);
            }
        }

        public FileMassAccuracyResults FileResults
        {
            get
            {
                if (fileResults is not null) return fileResults;
                fileResults = new FileMassAccuracyResults(ScanResults, AveragingParameters);
                return fileResults;
            }
        }

        public List<ScanMassAccuracyResults> ScanResults
        {
            get
            {
                if (scanResults is not null) return scanResults;
                var results = new List<ScanMassAccuracyResults>();
                var lines = File.ReadAllLines(ScanMassAccuracyResultTsvPath);
                for (var i = 1; i < lines.Length; i++)
                {
                    results.Add(new ScanMassAccuracyResults(lines[i]));
                }
                scanResults = results;
                return scanResults;
            }
        }

        #endregion

    }
}
