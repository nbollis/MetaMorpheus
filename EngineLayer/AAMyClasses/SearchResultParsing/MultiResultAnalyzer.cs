using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;
using DataColumn = System.Data.DataColumn;

namespace EngineLayer
{
    public class MultiResultAnalyzer : ResultAnalyzer
    {
        private List<PsmFromTsv> FilteredProteoformsConcat { get; set; }
        private List<PsmFromTsv> ProteoformsConcat { get; set; }
        private List<PsmFromTsv> FilteredPsmsConcat { get; set; }
        private List<PsmFromTsv> PsmsConcat { get; set; }

        public Dictionary<string, SearchResultAnalyzer> ResultsDict { get; set; }
        public DataTable TotalTable { get; set; }

        #region Constructor

        public MultiResultAnalyzer()
        {
            TotalTable = new DataTable();
            ResultsDict = new Dictionary<string, SearchResultAnalyzer>();
            FileNameIndex = new();
            FilteredProteoformsConcat = new();
            ProteoformsConcat = new();
            FilteredPsmsConcat = new();
            PsmsConcat = new();
            AddDefaultColumnsToTable(TotalTable);
            TotalTable.Columns[0].ColumnName = "Name";
            TotalTable.Columns[0].Caption = "Name";
        }

        #endregion

        #region AddingSearchResults

        /// <summary>
        /// Adds a search result to the list being analyzed
        /// </summary>
        /// <param name="name"></param>
        /// <param name="spectraPaths"></param>
        /// <param name="proteoformPath"></param>
        /// <param name="psmPath"></param>
        public void AddSearchResult(string name, List<string> spectraPaths, string proteoformPath, string psmPath = "", string databasepath = "")
        {
            SearchResultAnalyzer analyzer = new SearchResultAnalyzer(spectraPaths, proteoformPath, psmPath, databasepath);
            ResultsDict.Add(name, analyzer);
            FileNameIndex.Add(name, FileNameIndex.Count);
            FilteredProteoformsConcat.AddRange(analyzer.FilteredProteoforms);
            ProteoformsConcat.AddRange(analyzer.AllProteoforms);
            FilteredPsmsConcat.AddRange(analyzer.AllPsms);
            PsmsConcat.AddRange(analyzer.AllPsms);

            DataRow row = TotalTable.NewRow();
            row["Name"] = name;
            row["Proteoforms"] = analyzer.DataTable.Rows[analyzer.FileNameIndex.Count]["Proteoforms"];
            row["Filtered Proteoforms"] = analyzer.DataTable.Rows[analyzer.FileNameIndex.Count]["Filtered Proteoforms"];
            if (analyzer.AllPsms.Any())
            {
                row["Psms"] = analyzer.DataTable.Rows[analyzer.FileNameIndex.Count]["Psms"];
                row["Filtered Psms"] = analyzer.DataTable.Rows[analyzer.FileNameIndex.Count]["Filtered Psms"];
            }

            TotalTable.Rows.Add(row);
        }

        public void AddSearchResult(string name, string proteoformPath, string psmPath = "")
        {
            SearchResultAnalyzer analyzer = new SearchResultAnalyzer(proteoformPath, psmPath);
            ResultsDict.Add(name, analyzer);
            FileNameIndex.Add(name, FileNameIndex.Count);
            FilteredProteoformsConcat.AddRange(analyzer.FilteredProteoforms);
            ProteoformsConcat.AddRange(analyzer.AllProteoforms);
            FilteredPsmsConcat.AddRange(analyzer.AllPsms);
            PsmsConcat.AddRange(analyzer.AllPsms);

            DataRow row = TotalTable.NewRow();
            row["Name"] = name;
            row["Proteoforms"] = analyzer.DataTable.Rows[analyzer.FileNameIndex.Count]["Proteoforms"];
            row["Filtered Proteoforms"] = analyzer.DataTable.Rows[analyzer.FileNameIndex.Count]["Filtered Proteoforms"];
            if (analyzer.AllPsms.Any())
            {
                row["Psms"] = analyzer.DataTable.Rows[analyzer.FileNameIndex.Count]["Psms"];
                row["Filtered Psms"] = analyzer.DataTable.Rows[analyzer.FileNameIndex.Count]["Filtered Psms"];
            }

            TotalTable.Rows.Add(row);
        }

        public void AddManySearchResults(string[] directorypaths)
        {
            foreach (var directorypath in directorypaths)
            {
                AddSearchResult(directorypath);
            }
        }

        public void AddSearchResult(string directoryPath)
        {
            string name = directoryPath.Split(@"\").Last();
            string proteoformPath = SearchResultParser.GetFilePath(directoryPath, FileTypes.AllProteoforms);
            string psmPath = SearchResultParser.GetFilePath(directoryPath, FileTypes.AllPSMs);

            SearchResultAnalyzer analyzer = new SearchResultAnalyzer(proteoformPath, psmPath);
            ResultsDict.Add(name, analyzer);
            FileNameIndex.Add(name, FileNameIndex.Count);
            FilteredProteoformsConcat.AddRange(analyzer.FilteredProteoforms);
            ProteoformsConcat.AddRange(analyzer.AllProteoforms);
            FilteredPsmsConcat.AddRange(analyzer.AllPsms);
            PsmsConcat.AddRange(analyzer.AllPsms);

            DataRow row = TotalTable.NewRow();
            row["Name"] = name;
            row["Proteoforms"] = analyzer.DataTable.Rows[analyzer.FileNameIndex.Count]["Proteoforms"];
            row["Filtered Proteoforms"] = analyzer.DataTable.Rows[analyzer.FileNameIndex.Count]["Filtered Proteoforms"];
            if (analyzer.AllPsms.Any())
            {
                row["Psms"] = analyzer.DataTable.Rows[analyzer.FileNameIndex.Count]["Psms"];
                row["Filtered Psms"] = analyzer.DataTable.Rows[analyzer.FileNameIndex.Count]["Filtered Psms"];
            }

            TotalTable.Rows.Add(row);
        }


        #endregion


        #region Bulk Processing

        /// <summary>
        /// Calculates every value from the search results
        /// </summary>
        public void PerformAllProcessing()
        {
            PerformAllWholeGroupProcessing();
            PerformAllIndividualGroupProcessing();
        }

        /// <summary>
        /// Performs all individual file processing steps and adds the total values to TotalTable
        /// </summary>
        public void PerformAllIndividualGroupProcessing()
        {
            PerformMatchedIonScoringProcessing();
            PerformChimericInfoProcessing();
            PerformAmbiguityInfoProcessing();
        }

        /// <summary>
        /// Performs all group processing steps and adds their columns to the table
        /// </summary>
        public void PerformAllWholeGroupProcessing()
        {
            GetTotalProteinCounts();
            PerformFilteredWholeGroupProcessing();
            PerformUnfilteredWholeGroupProcessing();
        }

        /// <summary>
        /// Finds count of distinct proteins, proteoforms, and psms in the filtered results
        /// </summary>
        public void PerformFilteredWholeGroupProcessing()
        {
            PerformFilteredDistinctProteinProcessing();
            PerformFilteredDistinctProteoformProcessing();
            if (ResultsDict.Values.Any(p => p.AllPsms.Any()))
            {
                PerformFilteredDistinctPsmProcessing();
            }
        }

        /// <summary>
        /// Finds count of distinct proteins, proteoforms, and psms in the unfiltered results
        /// </summary>
        public void PerformUnfilteredWholeGroupProcessing()
        {
            PerformUnfilteredDistinctProteinProcessing();
            PerformUnfilteredDistinctProteoformProcessing();
            if (ResultsDict.Values.Any(p => p.AllPsms.Any()))
            {
                PerformUnfilteredDistinctPsmProcessing();
            }
        }

        #endregion

        #region Individual Processing Methods

        public void GetTotalProteinCounts()
        {
            DataColumn column = new DataColumn()
            {
                DataType = typeof(int),
                ColumnName = "Proteins",
                Caption = "Proteins",
            };
            TotalTable.Columns.Add(column);

            column = new DataColumn()
            {
                DataType = typeof(int),
                ColumnName = "Filtered Proteins",
                Caption = "Filtered Proteins"
            };
            TotalTable.Columns.Add(column);

            foreach (var result in ResultsDict)
            {
                TotalTable.Rows[FileNameIndex[result.Key]]["Proteins"] = result.Value.AllProteoforms
                    .Select(p => p.ProteinAccession).Distinct().Count();
                TotalTable.Rows[FileNameIndex[result.Key]]["Filtered Proteins"] = result.Value.FilteredProteoforms
                    .Select(p => p.ProteinAccession).Distinct().Count();
            }
        }

        /// <summary>
        /// Determines the number of proteins in the filtered proteoform list that are unique to that search result
        /// </summary>
        public void PerformFilteredDistinctProteinProcessing()
        {
            DataColumn column = new DataColumn()
            {
                DataType = typeof(int),
                ColumnName = "Distinct Filtered Proteins",
                Caption = "Distinct Filtered Proteins",
            };
            TotalTable.Columns.Add(column);

            foreach (var result in ResultsDict)
            {
                IEnumerable<string> distinct = result.Value.FilteredProteoforms.Select(p => p.ProteinAccession);
                foreach (var otherResult in ResultsDict.Where(p => p.Key != result.Key))
                {
                    distinct = distinct.Except(otherResult.Value.FilteredProteoforms.Select(p => p.ProteinAccession));
                }
                TotalTable.Rows[FileNameIndex[result.Key]]["Distinct Filtered Proteins"] = distinct.Count();
            }
        }

        /// <summary>
        /// Determines the number of proteoforms in the filtered proteoform list that are unique to that search result
        /// </summary>
        public void PerformFilteredDistinctProteoformProcessing()
        {
            DataColumn column = new DataColumn()
            {
                DataType = typeof(int),
                ColumnName = "Distinct Filtered Proteoforms",
                Caption = "Distinct Filtered Proteoforms",
            };
            TotalTable.Columns.Add(column);

            foreach (var result in ResultsDict)
            {
                IEnumerable<string> distinct = result.Value.FilteredProteoforms.Select(p => p.FullSequence);
                foreach (var otherResult in ResultsDict.Where(p => p.Key != result.Key))
                {
                    distinct = distinct.Except(otherResult.Value.FilteredProteoforms.Select(p => p.FullSequence));
                }
                TotalTable.Rows[FileNameIndex[result.Key]]["Distinct Filtered Proteoforms"] = distinct.Count();
            }
        }

        /// <summary>
        /// Determines the number of psms in the filtered psms list that are unique to that search result
        /// </summary>
        public void PerformFilteredDistinctPsmProcessing()
        {
            DataColumn column = new DataColumn()
            {
                DataType = typeof(int),
                ColumnName = "Distinct Filtered Psms",
                Caption = "Distinct Filtered Psms",
            };
            TotalTable.Columns.Add(column);

            foreach (var result in ResultsDict)
            {
                IEnumerable<string> distinct = result.Value.FilteredPsms.Select(p => p.FullSequence);
                foreach (var otherResult in ResultsDict.Where(p => 
                             p.Key != result.Key && p.Value.AllPsms.Any())) 
                {
                    distinct = distinct.Except(otherResult.Value.FilteredPsms.Select(p => p.FullSequence));
                }
                TotalTable.Rows[FileNameIndex[result.Key]]["Distinct Filtered Psms"] = distinct.Count();
            }
        }

        /// <summary>
        /// Determines the number of proteins in the unfiltered proteoform list that are unique to that search result
        /// </summary>
        public void PerformUnfilteredDistinctProteinProcessing()
        {
            DataColumn column = new DataColumn()
            {
                DataType = typeof(int),
                ColumnName = "Distinct Unfiltered Proteins",
                Caption = "Distinct Unfiltered Proteins",
            };
            TotalTable.Columns.Add(column);

            foreach (var result in ResultsDict)
            {
                IEnumerable<string> distinct = result.Value.AllProteoforms.Select(p => p.BaseSeq);
                foreach (var otherResult in ResultsDict.Where(p => p.Key != result.Key))
                {
                    distinct = distinct.Except(otherResult.Value.AllProteoforms.Select(p => p.BaseSeq));
                }
                TotalTable.Rows[FileNameIndex[result.Key]]["Distinct Unfiltered Proteins"] = distinct.Count();
            }
        }

        /// <summary>
        /// Determines the number of proteoforms in the unfiltered proteoform list that are unique to that search result
        /// </summary>
        public void PerformUnfilteredDistinctProteoformProcessing()
        {
            DataColumn column = new DataColumn()
            {
                DataType = typeof(int),
                ColumnName = "Distinct Unfiltered Proteoforms",
                Caption = "Distinct Unfiltered Proteoforms",
            };
            TotalTable.Columns.Add(column);

            foreach (var result in ResultsDict)
            {
                IEnumerable<string> distinct = result.Value.AllProteoforms.Select(p => p.FullSequence);
                foreach (var otherResult in ResultsDict.Where(p => p.Key != result.Key))
                {
                    distinct = distinct.Except(otherResult.Value.AllProteoforms.Select(p => p.FullSequence));
                }
                TotalTable.Rows[FileNameIndex[result.Key]]["Distinct Unfiltered Proteoforms"] = distinct.Count();
            }
        }

        /// <summary>
        /// Determines the number of psms in the unfiltered psms list that are unique to that search result
        /// </summary>
        public void PerformUnfilteredDistinctPsmProcessing()
        {
            DataColumn column = new DataColumn()
            {
                DataType = typeof(int),
                ColumnName = "Distinct Unfiltered Psms",
                Caption = "Distinct Unfiltered Psms",
            };
            TotalTable.Columns.Add(column);

            foreach (var result in ResultsDict)
            {
                IEnumerable<string> distinct = result.Value.AllPsms.Select(p => p.FullSequence);
                foreach (var otherResult in ResultsDict.Where(p => 
                             p.Key != result.Key && p.Value.AllPsms.Any())) 
                {
                    distinct = distinct.Except(otherResult.Value.AllPsms.Select(p => p.FullSequence));
                }
                TotalTable.Rows[FileNameIndex[result.Key]]["Distinct Unfiltered Psms"] = distinct.Count();
            }
        }

        /// <summary>
        /// Scores proteoforms based on the number of matched ions above defined s/n cutoff
        /// This uses the filtered proteoforms and psms lists
        /// </summary>
        public void PerformMatchedIonScoringProcessing()
        {
            DataColumn column = new DataColumn()
            {
                DataType = typeof(double),
                ColumnName = "Filtered Proteoforms Score",
                Caption = "Filtered Proteoforms Score",
            };
            TotalTable.Columns.Add(column);

            if (ResultsDict.Values.Any(p => p.AllPsms.Any()))
            {
                column = new DataColumn()
                {
                    DataType = typeof(double),
                    ColumnName = "Filtered Psms Score",
                    Caption = "Filtered Psms Score",
                };
                TotalTable.Columns.Add(column);
            }

            foreach (var result in ResultsDict)
            {
                result.Value.ScoreSpectraByMatchedIons();

                TotalTable.Rows[FileNameIndex[result.Key]]["Filtered Proteoforms Score"] =
                    result.Value.DataTable.Rows[result.Value.FileNameIndex.Count]["Filtered Proteoforms Score"];
                if (result.Value.AllPsms.Any())
                {
                    TotalTable.Rows[FileNameIndex[result.Key]]["Filtered Psms Score"] =
                        result.Value.DataTable.Rows[result.Value.FileNameIndex.Count]["Filtered Psms Score"];
                }
            }
        }

        /// <summary>
        /// Gets a count of how ID's were made per MS2
        /// This uses the filtered proteoform and psms list
        /// </summary>
        public void PerformChimericInfoProcessing()
        {
            // run analysis
            foreach (var result in ResultsDict)
            {
                result.Value.CalculateChimeraInformation();
            }

            // add the appropriate amount of columns and add respective data
            MaxProteoformChimerasFromOneSpectra = ResultsDict.Values.Max(p => p.MaxProteoformChimerasFromOneSpectra);
            for (int i = 1; i < MaxProteoformChimerasFromOneSpectra + 1; i++)
            {
                string columnHeader = $"{i} Proteoform ID per MS2";
                DataColumn column = new DataColumn()
                {
                    DataType = typeof(int),
                    ColumnName = columnHeader,
                    Caption = columnHeader
                };
                TotalTable.Columns.Add(column);

                foreach (var result in ResultsDict)
                {
                    if (result.Value.DataTable.Columns.Contains(columnHeader))
                    {
                        TotalTable.Rows[FileNameIndex[result.Key]][columnHeader] =
                            result.Value.DataTable.Rows[result.Value.FileNameIndex.Count][columnHeader];
                    }
                }
            }

            if (ResultsDict.Values.Any(p => p.AllPsms.Any()))
            {
                MaxPsmChimerasFromOneSpectra = ResultsDict.Values.Max(p => p.MaxPsmChimerasFromOneSpectra);
                for (int i = 1; i < MaxPsmChimerasFromOneSpectra + 1; i++)
                {
                    string columnHeader = $"{i} PSM ID per MS2";
                    DataColumn column = new DataColumn()
                    {
                        DataType = typeof(int),
                        ColumnName = columnHeader,
                        Caption = columnHeader
                    };
                    TotalTable.Columns.Add(column);

                    foreach (var result in ResultsDict)
                    {
                        if (result.Value.DataTable.Columns.Contains(columnHeader))
                        {
                            TotalTable.Rows[FileNameIndex[result.Key]][columnHeader] =
                                result.Value.DataTable.Rows[result.Value.FileNameIndex.Count][columnHeader];
                        }
                    }
                }
            }

            if (ResultsDict.Values.Any(p => p.DatabasePath != ""))
            {
                string columnHeader = $"Validated Chimera Count";
                DataColumn column = new DataColumn()
                {
                    DataType = typeof(int),
                    ColumnName = columnHeader,
                    Caption = columnHeader
                };
                TotalTable.Columns.Add(column);

                foreach (var result in ResultsDict)
                {
                    if (result.Value.DataTable.Columns.Contains(columnHeader))
                    {
                        TotalTable.Rows[FileNameIndex[result.Key]][columnHeader] =
                            result.Value.DataTable.Rows[result.Value.FileNameIndex.Count][columnHeader];
                    }
                }
            }
        }

        /// <summary>
        /// Determine the count of each ambiguity level within the result files
        /// This uses the filtered proteoform and psm lists
        /// </summary>
        public void PerformAmbiguityInfoProcessing()
        {
            List<string> headers = new(); 
            List<string> psmHeaders = new();
            foreach (var ambiguityLevel in ambiguityLevels)
            {
                string columnHeader = $"Proteoform Ambiguity Level {ambiguityLevel}";
                headers.Add(columnHeader);
                DataColumn column = new DataColumn()
                {
                    DataType = typeof(int),
                    ColumnName = columnHeader,
                    Caption = columnHeader,
                };
                TotalTable.Columns.Add(column);
            }

            if (ResultsDict.Values.Any(p => p.AllPsms.Any()))
            {
                foreach (var ambiguityLevel in ambiguityLevels)
                {
                    string columnHeader = $"Psm Ambiguity Level {ambiguityLevel}";
                    psmHeaders.Add(columnHeader);
                    DataColumn column = new DataColumn()
                    {
                        DataType = typeof(int),
                        ColumnName = columnHeader,
                        Caption = columnHeader,
                    };
                    TotalTable.Columns.Add(column);
                }
            }

            foreach (var result in ResultsDict)
            {
                result.Value.CalculateAmbiguityInformation();

                foreach (var header in headers)
                {
                    TotalTable.Rows[FileNameIndex[result.Key]][header] =
                        result.Value.DataTable.Rows[result.Value.FileNameIndex.Count][header];
                }

                if (result.Value.AllPsms.Any())
                {
                    foreach (var header in psmHeaders)
                    {
                        TotalTable.Rows[FileNameIndex[result.Key]][header] =
                            result.Value.DataTable.Rows[result.Value.FileNameIndex.Count][header];
                    }
                }
            }
        }

        #endregion
    }
}
