using MassSpectrometry;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Easy.Common.Extensions;
using IO.MzML;
using IO.ThermoRawFileReader;
using MathNet.Numerics.Statistics;
using Microsoft.ML.Data;
using mzIdentML110.Generated;
using MzLibUtil;
using Proteomics;
using Proteomics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using UsefulProteomicsDatabases;

namespace EngineLayer
{
    public class SearchResultAnalyzer : ResultAnalyzer
    {
        #region Private Properties


        #endregion

        #region Loaded Files

        public List<PsmFromTsv> AllPsms { get; set; }
        public List<PsmFromTsv> FilteredPsms { get; set; }
        public Dictionary<string, List<PsmFromTsv>> FilteredPsmsByFileDict { get; set; }
        public List<PsmFromTsv> AllProteoforms { get; set; }
        public List<PsmFromTsv> FilteredProteoforms { get; set; }
        public Dictionary<string, List<PsmFromTsv>> FilteredProteoformsByFileDict { get; set; }
        public Dictionary<string, List<MsDataScan>> AllScansByFileDict { get; set; }
        public string DatabasePath { get; set; }

        #endregion

        #region Filters

        public double QValueFilter { get; set; } = 0.01;
        public double PepFilter { get; set; } = 0.5;
        public double SignalToNoiseLimit { get; set; } = 2;

        #endregion

        public DataTable DataTable { get; private set; }


        #region Constructors

        public SearchResultAnalyzer(string proteoformPath, string psmTsvPath = "", string databasePath = "")
        {
            // value initialization
            AllPsms = new();
            AllProteoforms = new();
            DataTable = new();
            AllScansByFileDict = new();
            FilteredProteoformsByFileDict = new();
            FilteredPsmsByFileDict = new();
            FileNameIndex = new();
            this.DatabasePath = databasePath;

            // load in proteoforms and psms if present
            AllProteoforms = PsmTsvReader.ReadTsv(proteoformPath, out List<string> warnings);
            string[] spectraPaths;
            if (psmTsvPath != "")
            {
                AllPsms = PsmTsvReader.ReadTsv(psmTsvPath, out warnings);
                spectraPaths = AllPsms.Select(p => p.FileNameWithoutExtension).Distinct().ToArray();
            }
            else
            {
                spectraPaths = AllProteoforms.Select(p => p.FileNameWithoutExtension).Distinct().ToArray();
            }


            FilteredProteoforms = AllProteoforms.Where(p => p.QValue <= QValueFilter /*&& p.PEP <= PepFilter*/).ToList();
            if (!AllProteoforms.Select(p => p.FileNameWithoutExtension).Distinct().OrderBy(p => p)
                    .SequenceEqual(spectraPaths.Select(p => Path.GetFileNameWithoutExtension(p)).Distinct()
                        .OrderBy(p => p)))
            {
                throw new ArgumentException("Not all spectra files searched were loaded");
            }
            if (psmTsvPath != "")
            {
                FilteredPsms = AllPsms.Where(p => p.QValue <= QValueFilter /*&& p.PEP <= PepFilter*/).ToList();
                if (!AllPsms.Select(p => p.FileNameWithoutExtension).Distinct().OrderBy(p => p)
                        .SequenceEqual(spectraPaths.Select(p => Path.GetFileNameWithoutExtension(p)).Distinct()
                            .OrderBy(p => p)))
                {
                    throw new ArgumentException("Not all spectra files searched were loaded");
                }
            }

            // load in scans
            //for (int i = 0; i < spectraPaths.Length; i++)
            //{
            //    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(spectraPaths[i]);
            //    AllScansByFileDict.Add(fileNameWithoutExtension, LoadAllScansFromFile(spectraPaths[i]));
            //    FileNameIndex.Add(fileNameWithoutExtension, i);
            //    FilteredProteoformsByFileDict.Add(fileNameWithoutExtension, FilteredProteoforms.Where(p => p.FileNameWithoutExtension == fileNameWithoutExtension).ToList());
            //    if (AllPsms.Any())
            //        FilteredPsmsByFileDict.Add(fileNameWithoutExtension, FilteredPsms.Where(p => p.FileNameWithoutExtension == fileNameWithoutExtension).ToList());
            //}

            AddDefaultColumnsToTable(DataTable);

            // construct table rows
            foreach (var fraction in AllScansByFileDict)
            {
                DataRow row = DataTable.NewRow();
                row["Fraction"] = fraction.Key;
                row["Proteoforms"] = AllProteoforms.Count(p => p.FileNameWithoutExtension == fraction.Key);
                row["Filtered Proteoforms"] = FilteredProteoforms.Count(p => p.FileNameWithoutExtension == fraction.Key);
                if (AllPsms.Any())
                {
                    row["Psms"] = AllPsms.Count(p => p.FileNameWithoutExtension == fraction.Key);
                    row["Filtered Psms"] = FilteredPsms.Count(p => p.FileNameWithoutExtension == fraction.Key);
                }
                DataTable.Rows.Add(row);
            }

            DataRow totalRow = DataTable.NewRow();
            totalRow["Fraction"] = "Total";
            DataTable.Rows.Add(totalRow);
            PopulateTotalRow();
        }

        public SearchResultAnalyzer(List<string> spectraPaths, string proteoformPath, string psmTsvPath = "", string databasePath = "")
        {
            // value initialization
            AllPsms = new();
            AllProteoforms = new();
            DataTable = new();
            AllScansByFileDict = new();
            FilteredProteoformsByFileDict = new();
            FilteredPsmsByFileDict = new();
            FileNameIndex = new();
            this.DatabasePath = databasePath;

            // load in proteoforms and psms if present
            AllProteoforms = PsmTsvReader.ReadTsv(proteoformPath, out List<string> warnings);
            FilteredProteoforms = AllProteoforms.Where(p => p.QValue <= QValueFilter /*&& p.PEP <= PepFilter*/).ToList();
            if (!AllProteoforms.Select(p => p.FileNameWithoutExtension).Distinct().OrderBy(p => p)
                    .SequenceEqual(spectraPaths.Select(p => Path.GetFileNameWithoutExtension(p)).Distinct()
                        .OrderBy(p => p)))
            {
                throw new ArgumentException("Not all spectra files searched were loaded");
            }
            if (psmTsvPath != "")
            {
                AllPsms = PsmTsvReader.ReadTsv(psmTsvPath, out warnings);
                FilteredPsms = AllPsms.Where(p => p.QValue <= QValueFilter /*&& p.PEP <= PepFilter*/).ToList();
                if (!AllPsms.Select(p => p.FileNameWithoutExtension).Distinct().OrderBy(p => p)
                        .SequenceEqual(spectraPaths.Select(p => Path.GetFileNameWithoutExtension(p)).Distinct()
                            .OrderBy(p => p)))
                {
                    throw new ArgumentException("Not all spectra files searched were loaded");
                }
            }

            // load in scans
            for (int i = 0; i < spectraPaths.Count; i++)
            {
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(spectraPaths[i]);
                AllScansByFileDict.Add(fileNameWithoutExtension, LoadAllScansFromFile(spectraPaths[i]));
                FileNameIndex.Add(fileNameWithoutExtension, i);
                FilteredProteoformsByFileDict.Add(fileNameWithoutExtension, FilteredProteoforms.Where(p => p.FileNameWithoutExtension == fileNameWithoutExtension).ToList());
                if (AllPsms.Any())
                    FilteredPsmsByFileDict.Add(fileNameWithoutExtension, FilteredPsms.Where(p => p.FileNameWithoutExtension == fileNameWithoutExtension).ToList());
            }

            AddDefaultColumnsToTable(DataTable);

            // construct table rows
            foreach (var fraction in AllScansByFileDict)
            {
                DataRow row = DataTable.NewRow();
                row["Fraction"] = fraction.Key;
                row["Proteoforms"] = AllProteoforms.Count(p => p.FileNameWithoutExtension == fraction.Key);
                row["Filtered Proteoforms"] = FilteredProteoforms.Count(p => p.FileNameWithoutExtension == fraction.Key);
                if (AllPsms.Any())
                {
                    row["Psms"] = AllPsms.Count(p => p.FileNameWithoutExtension == fraction.Key);
                    row["Filtered Psms"] = FilteredPsms.Count(p => p.FileNameWithoutExtension == fraction.Key);
                }
                DataTable.Rows.Add(row);
            }

            DataRow totalRow = DataTable.NewRow();
            totalRow["Fraction"] = "Total";
            DataTable.Rows.Add(totalRow);
            PopulateTotalRow();
        }

        #endregion

        #region Bulk Processing

        /// <summary>
        /// Performs All Bulk Processing Methods
        /// Caut
        /// </summary>
        public void PerformAllProcessing()
        {
            ScoreSpectraByMatchedIons();
            CalculateChimeraInformation();
            CalculateAmbiguityInformation();

        }

        /// <summary>
        /// Scores psmtsv results by the number of matched ions above the defined s/n cutoff in the spectra that made the ID
        /// </summary>
        public void ScoreSpectraByMatchedIons()
        {
            DataColumn column = new DataColumn()
            {
                DataType = typeof(double),
                ColumnName = "Filtered Proteoforms Score",
                Caption = "Filtered Proteoforms Score",
            };
            DataTable.Columns.Add(column);
            ScoreSpectraByFilteredProteoformMatchedIonsAboveSNRCutoff();
            if (AllPsms.Any())
            {
                column = new DataColumn()
                {
                    DataType = typeof(double),
                    ColumnName = "Filtered Psms Score",
                    Caption = "Filtered Psms Score",
                };
                DataTable.Columns.Add(column);
                ScoreSpectraByFilteredPsmMatchedIonsAboveSNRCutoff();
            }
            PopulateTotalRow();
        }

        public void CalculateChimeraInformation()
        {
            CalculateProteoformChimeraInfo();
            if (AllPsms.Any())
            {
                CalculatePsmChimeraInfo();
                if (DatabasePath != "")
                {
                    CalculatePSMChimeraAccuracy();
                }
            }
            PopulateTotalRow();
        }

        public void CalculateAmbiguityInformation()
        {
            foreach (var ambiguityLevel in ambiguityLevels)
            {
                string columnHeader = $"Proteoform Ambiguity Level {ambiguityLevel}";
                DataColumn column = new DataColumn()
                {
                    DataType = typeof(int),
                    ColumnName = columnHeader,
                    Caption = columnHeader,
                };
                DataTable.Columns.Add(column);
            }
            CalculateProteoformAmbiguityInfo();

            if (AllPsms.Any())
            {
                foreach (var ambiguityLevel in ambiguityLevels)
                {
                    string columnHeader = $"PSM Ambiguity Level {ambiguityLevel}";
                    DataColumn column = new DataColumn()
                    {
                        DataType = typeof(int),
                        ColumnName = columnHeader,
                        Caption = columnHeader,
                    };
                    DataTable.Columns.Add(column);
                }
                CalculatePsmAmbiguityInfo();
            }
            PopulateTotalRow();
        }

        #endregion

        #region Individual Processing Methods

        #region Scoring (Will definitely need to parallelized if these are ever implemented somewhere)

        private void ScoreSpectraByFilteredProteoformMatchedIonsAboveSNRCutoff()
        {

            foreach (var fraction in FilteredProteoformsByFileDict)
            {
                int allMatchedIonCount = 0;
                foreach (var proteoform in fraction.Value)
                {
                    MsDataScan ms2IdScan = AllScansByFileDict[proteoform.FileNameWithoutExtension]
                        .First(p => p.OneBasedScanNumber == proteoform.Ms2ScanNumber);
                    double signalToNoiseCutoff = ms2IdScan.GetNoiseLevel(SignalToNoiseLimit);
                    var matchedIonsAboveIntensityCutoff =
                        proteoform.MatchedIons.Where(p => p.Intensity > signalToNoiseCutoff).ToList();
                    allMatchedIonCount += matchedIonsAboveIntensityCutoff.Count;
                }

                DataTable.Rows[FileNameIndex[fraction.Key]]["Filtered Proteoforms Score"] =
                    allMatchedIonCount / fraction.Value.Count;
            }
        }

        private void ScoreSpectraByFilteredPsmMatchedIonsAboveSNRCutoff()
        {
            if (!AllPsms.Any())
            {
                return;
            }
            else
            {
                foreach (var fraction in FilteredPsmsByFileDict)
                {
                    int allMatchedIonCount = 0;
                    foreach (var psm in fraction.Value)
                    {
                        MsDataScan ms2IdScan = AllScansByFileDict[psm.FileNameWithoutExtension]
                            .First(p => p.OneBasedScanNumber == psm.Ms2ScanNumber);
                        double signalToNoiseCutoff = ms2IdScan.GetNoiseLevel(SignalToNoiseLimit);
                        var matchedIonsAboveIntensityCutoff =
                            psm.MatchedIons.Where(p => p.Intensity > signalToNoiseCutoff).ToList();
                        allMatchedIonCount += matchedIonsAboveIntensityCutoff.Count;
                    }
                    DataTable.Rows[FileNameIndex[fraction.Key]]["Filtered Psms Score"] = allMatchedIonCount / fraction.Value.Count;
                }
            }
        }

        #endregion

        #region Chimeras

        public void CalculatePSMChimeraAccuracy()
        {
            if (!AllPsms.Any() || DatabasePath == "")
            {
                return;
            }
            else
            {
                List<Protein> proteins = new();
                if (DatabasePath.Split(".").Last().Equals("fasta"))
                    proteins = ProteinDbLoader.LoadProteinFasta(DatabasePath, true, DecoyType.None, false,
                        out List<string> errors);
                else if (DatabasePath.Split(".").Last().Equals("xml"))
                    proteins = ProteinDbLoader.LoadProteinXML(DatabasePath, true, DecoyType.None,
                        GlobalVariables.AllModsKnown, false, null,
                        out Dictionary<string, Modification> unknownModifications);
                else
                    return;

                foreach (var psmsInOneFile in FilteredPsmsByFileDict)
                {
                    var groupedPsms = psmsInOneFile.Value.GroupBy(p => p.Ms2ScanNumber).Where(p => p.Count() > 1);
                    int validatedCount = 0;

                    // foreach chimeric spectra
                    foreach (var chimericSpectraPsms in groupedPsms)
                    {
                        var Ms1Scan = AllScansByFileDict[psmsInOneFile.Key].First(p =>
                            p.OneBasedScanNumber == chimericSpectraPsms.First().PrecursorScanNum);
                        var Ms2Scan = AllScansByFileDict[psmsInOneFile.Key].First(p =>
                            p.OneBasedScanNumber == chimericSpectraPsms.First().Ms2ScanNumber);

                        var precursorMz = chimericSpectraPsms.Select(p => p.PrecursorMz);
                        var isolationMin = Ms2Scan.SelectedIonMZ - (Ms2Scan.IsolationWidth / 2);
                        var isolationMax = Ms2Scan.SelectedIonMZ + (Ms2Scan.IsolationWidth / 2);

                        DoubleRange isolationRange = new((double)isolationMin, (double)isolationMax);

                        // if all precursor Mz's are within range
                        if (precursorMz.All(p => isolationRange.Contains(p)))
                        {
                            List<PeptideWithSetModifications> idProteins = new();
                            chimericSpectraPsms.ForEach(p => idProteins.Add(new(p.FullSequence.Split('|')[0], GlobalVariables.AllModsKnownDictionary)));

                            List<double> calculatedMz = new();
                            for (int i = 0; i < idProteins.Count; i++)
                            {
                                calculatedMz.Add(idProteins[i].MonoisotopicMass /
                                                 chimericSpectraPsms.ElementAt(i).PrecursorCharge);
                            }

                            // if the theoretical protein also has a charge within the range
                            if (calculatedMz.All(p => isolationRange.Contains(p)))
                            {
                                validatedCount++;
                            }
                            else
                            {

                            }

                        }
                    }

                    // add new row with number of validated IDs if not already a row
                    string columnHeader = $"Validated Chimera Count";
                    if (!DataTable.Columns.Contains(columnHeader))
                    {
                        DataColumn column = new DataColumn()
                        {
                            DataType = typeof(int),
                            ColumnName = columnHeader,
                            Caption = columnHeader,
                        };
                        DataTable.Columns.Add(column);
                    }

                    // add counts to column and row
                    DataTable.Rows[FileNameIndex[psmsInOneFile.Key]][columnHeader] =
                        validatedCount;
                }
            }
        }

        private void CalculateProteoformChimeraInfo()
        {
            foreach (var proteoformsInOneFile in FilteredProteoformsByFileDict)
            {
                Dictionary<int, int> chimeraInfo = new();
                var groupedProteoforms = proteoformsInOneFile.Value.GroupBy(p => p.Ms2ScanNumber);
                foreach (var group in groupedProteoforms)
                {
                    if (!chimeraInfo.TryAdd(group.Count(), 1))
                    {
                        chimeraInfo[group.Count()]++;
                    }
                }

                MaxProteoformChimerasFromOneSpectra = MaxProteoformChimerasFromOneSpectra > chimeraInfo.Last().Key ? MaxProteoformChimerasFromOneSpectra : chimeraInfo.Last().Key;
                foreach (var proteoformChimeraTotal in chimeraInfo)
                {
                    // add new row with number of IDs if not already a row
                    string columnHeader = $"{proteoformChimeraTotal.Key} Proteoform ID per MS2";
                    if (!DataTable.Columns.Contains(columnHeader))
                    {
                        DataColumn column = new DataColumn()
                        {
                            DataType = typeof(int),
                            ColumnName = columnHeader,
                            Caption = columnHeader,
                        };
                        DataTable.Columns.Add(column);
                    }

                    // add counts to column and row
                    DataTable.Rows[FileNameIndex[proteoformsInOneFile.Key]][columnHeader] =
                        proteoformChimeraTotal.Value;
                }
            }
        }

        private void CalculatePsmChimeraInfo()
        {
            if (!AllPsms.Any())
            {
                return;
            }
            else
            {
                foreach (var psmsInOneFile in FilteredPsmsByFileDict)
                {
                    Dictionary<int, int> chimeraInfo = new();
                    var groupedPsms = psmsInOneFile.Value.GroupBy(p => p.Ms2ScanNumber);
                    foreach (var group in groupedPsms)
                    {
                        if (!chimeraInfo.TryAdd(group.Count(), 1))
                        {
                            chimeraInfo[group.Count()]++;
                        }
                    }

                    MaxPsmChimerasFromOneSpectra = MaxPsmChimerasFromOneSpectra > chimeraInfo.Last().Key ? MaxPsmChimerasFromOneSpectra : chimeraInfo.Last().Key;
                    foreach (var psmsChimeraTotal in chimeraInfo)
                    {
                        // add new row with number of IDs if not already a row
                        string columnHeader = $"{psmsChimeraTotal.Key} PSM ID per MS2";
                        if (!DataTable.Columns.Contains(columnHeader))
                        {
                            DataColumn column = new DataColumn()
                            {
                                DataType = typeof(int),
                                ColumnName = columnHeader,
                                Caption = columnHeader,
                            };
                            DataTable.Columns.Add(column);
                        }

                        // add counts to column and row
                        DataTable.Rows[FileNameIndex[psmsInOneFile.Key]][columnHeader] =
                            psmsChimeraTotal.Value;
                    }
                }
            }
        }

        #endregion

        #region Ambiguity

        private void CalculateProteoformAmbiguityInfo()
        {
            foreach (var proteoformsFromOneFile in FilteredProteoformsByFileDict)
            {
                foreach (var ambiguityLevel in ambiguityLevels)
                {
                    DataTable.Rows[FileNameIndex[proteoformsFromOneFile.Key]][$"Proteoform Ambiguity Level {ambiguityLevel}"]
                        = proteoformsFromOneFile.Value.Count(p => p.AmbiguityLevel == ambiguityLevel);
                }
            }
        }

        private void CalculatePsmAmbiguityInfo()
        {
            if (!AllPsms.Any())
            {
                return;
            }
            else
            {
                foreach (var psmsFromOneFile in FilteredPsmsByFileDict)
                {
                    foreach (var ambiguityLevel in ambiguityLevels)
                    {
                        DataTable.Rows[FileNameIndex[psmsFromOneFile.Key]][$"PSM Ambiguity Level {ambiguityLevel}"]
                            = psmsFromOneFile.Value.Count(p => p.AmbiguityLevel == ambiguityLevel);
                    }
                }
            }
        }

        #endregion

        #endregion

        #region Helpers

        /// <summary>
        /// Called at the end of each processing method to populate the total row within the table
        /// </summary>
        private void PopulateTotalRow()
        {
            DataTable.Rows.Remove(DataTable.Rows[FileNameIndex.Count]);
            DataRow totalRow = DataTable.NewRow();
            totalRow["Fraction"] = "Total";
            DataTable.Rows.Add(totalRow);

            foreach (DataColumn column in DataTable.Columns)
            {
                if (column.DataType != typeof(string))
                    DataTable.Rows[FileNameIndex.Count][column.ToString()] = DataTable.Compute($"Sum([{column}])", "");
            }
        }

        /// <summary>
        /// Creates a List of MsDataScans from a spectra file. Currently supports MzML and raw
        /// </summary>
        /// <param name="filepath"></param>
        /// <returns></returns>
        /// <exception cref="MzLibException"></exception>
        private static List<MsDataScan> LoadAllScansFromFile(string filepath)
        {
            List<MsDataScan> scans = new();
            if (filepath.EndsWith(".mzML"))
                scans = Mzml.LoadAllStaticData(filepath).GetAllScansList();
            else if (filepath.EndsWith(".raw"))
                scans = ThermoRawFileReader.LoadAllStaticData(filepath).GetAllScansList();
            else
            {
                throw new MzLibException("Cannot load spectra");
            }
            return scans;
        }

        #endregion

    }

}
