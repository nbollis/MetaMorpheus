using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Test.ChimeraPaper.ResultFiles
{
    public abstract class BulkResult
    {
        public string DirectoryPath { get; set; }
        public string DatasetName { get; set; }
        public string Condition { get; set; }
        public bool Override { get; set; } = false;

        protected string _psmPath;
        protected string _peptidePath;
        protected string _proteinPath;

        protected string _IndividualFilePath => Path.Combine(DirectoryPath, $"{DatasetName}_{Condition}_{FileIdentifiers.IndividualFileComparison}");
        protected BulkResultCountComparisonFile _individualFileComparison;
        public BulkResultCountComparisonFile IndividualFileComparisonFile => _individualFileComparison ??= IndividualFileComparison();

        protected string _chimeraPsmPath => Path.Combine(DirectoryPath,
            $"{DatasetName}_{Condition}_PSM_{FileIdentifiers.ChimeraCountingFile}");
        protected ChimeraCountingFile _chimeraPsmFile;
        public ChimeraCountingFile ChimeraPsmFile => _chimeraPsmFile ??= CountChimericPsms();

        protected string _bulkResultCountComparisonPath => Path.Combine(DirectoryPath,
                       $"{DatasetName}_{Condition}_{FileIdentifiers.BottomUpResultComparison}");
        protected BulkResultCountComparisonFile _bulkResultCountComparisonFile;
        public BulkResultCountComparisonFile BulkResultCountComparisonFile => _bulkResultCountComparisonFile ??= GetBulkResultCountComparisonFile();

        public BulkResult(string directoryPath)
        {
            DirectoryPath = directoryPath;
            if (DirectoryPath.Contains("Task"))
            {
                DatasetName = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(DirectoryPath))));
                Condition = Path.GetFileName(Path.GetDirectoryName(DirectoryPath)) + Path.GetFileName(DirectoryPath).Split('-')[1];
            }
            else
            {
                Condition = Path.GetFileName(DirectoryPath);
                DatasetName = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(DirectoryPath)));
            }
        }

        public abstract BulkResultCountComparisonFile IndividualFileComparison();
        public abstract ChimeraCountingFile CountChimericPsms();
        public abstract BulkResultCountComparisonFile GetBulkResultCountComparisonFile();

        public override string ToString()
        {
            return $"{DatasetName}_{Condition}";
        }
    }
}
