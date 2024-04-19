using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.ChimeraPaper.ResultFiles
{
    public class AllResults : IEnumerable<CellLineResults>
    {
        public string DirectoryPath { get; set; }
        public bool Override { get; set; } = false;
        public List<CellLineResults> CellLineResults { get; set; }

        public AllResults(string directoryPath)
        {
            DirectoryPath = directoryPath;
            CellLineResults = new List<CellLineResults>();
            foreach (var directory in Directory.GetDirectories(DirectoryPath).Where(p => !p.Contains("Figures"))) 
            {
                CellLineResults.Add(new CellLineResults(directory));
            }
        }

        public AllResults(string directoryPath, List<CellLineResults> cellLineResults)
        {
            DirectoryPath = directoryPath;
            CellLineResults = cellLineResults;
        }

        private string _chimeraCountingPath => Path.Combine(DirectoryPath, $"All_PSM_{FileIdentifiers.ChimeraCountingFile}");
        private ChimeraCountingFile _chimeraCountingFile;
        public ChimeraCountingFile ChimeraCountingFile => _chimeraCountingFile ??= CountChimericPsms();
        public ChimeraCountingFile CountChimericPsms()
        {
            if (!Override && File.Exists(_chimeraCountingPath))
                return new ChimeraCountingFile(_chimeraCountingPath);

            List<ChimeraCountingResult> results = new List<ChimeraCountingResult>();
            foreach (var cellLineResult in CellLineResults)
            {
                results.AddRange(cellLineResult.ChimeraCountingFile.Results);
            }

            var chimeraCountingFile = new ChimeraCountingFile(_chimeraCountingPath) { Results = results };
            chimeraCountingFile.WriteResults(_chimeraCountingPath);
            return chimeraCountingFile;
        }

        private string _chimeraPeptidePath => Path.Combine(DirectoryPath, $"All_Peptide_{FileIdentifiers.ChimeraCountingFile}");
        private ChimeraCountingFile _chimeraPeptideFile;
        public ChimeraCountingFile ChimeraPeptideFile => _chimeraPeptideFile ??= CountChimericPeptides();
        public ChimeraCountingFile CountChimericPeptides()
        {
            if (!Override && File.Exists(_chimeraPeptidePath))
                return new ChimeraCountingFile(_chimeraPeptidePath);

            List<ChimeraCountingResult> results = new List<ChimeraCountingResult>();
            foreach (var cellLineResult in CellLineResults)
            {
                results.AddRange(cellLineResult.ChimeraPeptideFile.Results);
            }

            var chimeraPeptideFile = new ChimeraCountingFile(_chimeraPeptidePath) { Results = results };
            chimeraPeptideFile.WriteResults(_chimeraPeptidePath);
            return chimeraPeptideFile;
        }

        public string _bulkResultCountComparisonPath => Path.Combine(DirectoryPath, $"All_{FileIdentifiers.BottomUpResultComparison}");
        private BulkResultCountComparisonFile _bulkResultCountComparisonFile;
        public BulkResultCountComparisonFile BulkResultCountComparisonFile => _bulkResultCountComparisonFile ??= GetBulkResultCountComparisonFile();

        public BulkResultCountComparisonFile GetBulkResultCountComparisonFile()
        {
            if (!Override && File.Exists(_bulkResultCountComparisonPath))
                return new BulkResultCountComparisonFile(_bulkResultCountComparisonPath);

            List<BulkResultCountComparison> results = new List<BulkResultCountComparison>();
            foreach (var cellLineResult in CellLineResults)
            {
                results.AddRange(cellLineResult.BulkResultCountComparisonFile.Results);
            }

            var bulkResultCountComparisonFile = new BulkResultCountComparisonFile(_bulkResultCountComparisonPath) { Results = results };
            bulkResultCountComparisonFile.WriteResults(_bulkResultCountComparisonPath);
            return bulkResultCountComparisonFile;
        }

        private string _individualFileComparisonPath => Path.Combine(DirectoryPath, $"All_{FileIdentifiers.IndividualFileComparison}");
        private BulkResultCountComparisonFile _individualFileComparison;
        public BulkResultCountComparisonFile IndividualFileComparisonFile => _individualFileComparison ??= IndividualFileComparison();

        public BulkResultCountComparisonFile IndividualFileComparison()
        {
            if (!Override && File.Exists(_individualFileComparisonPath))
                return new BulkResultCountComparisonFile(_individualFileComparisonPath);

            List<BulkResultCountComparison> results = new List<BulkResultCountComparison>();
            foreach (var cellLineResult in CellLineResults.Where(p => p.IndividualFileComparisonFile != null))
            {
                results.AddRange(cellLineResult.IndividualFileComparisonFile.Results);
            }

            var individualFileComparison = new BulkResultCountComparisonFile(_individualFileComparisonPath) { Results = results };
            individualFileComparison.WriteResults(_individualFileComparisonPath);
            return individualFileComparison;
        }

        public IEnumerator<CellLineResults> GetEnumerator()
        {
            return CellLineResults.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
