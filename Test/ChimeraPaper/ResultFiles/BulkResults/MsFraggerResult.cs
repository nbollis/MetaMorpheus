using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using Test.AveragingPaper;

namespace Test.ChimeraPaper.ResultFiles
{
    public class MsFraggerResult : BulkResult
    {
        public List<MsFraggerIndividualFileResult> IndividualFileResults { get; set; }

        
        private MsFraggerPsmFile _psmFile;
        public MsFraggerPsmFile CombinedPsms => _psmFile ??= CombinePsmFiles();

        private MsFraggerPeptideFile _peptideFile;
        public MsFraggerPeptideFile CombinedPeptides => _peptideFile ??= new MsFraggerPeptideFile(_peptidePath);

        private MsFraggerProteinFile _proteinFile;
        public MsFraggerProteinFile CombinedProteins => _proteinFile ??= new MsFraggerProteinFile(_proteinPath);

        #region Base Sequence Only Filtering

        private string _peptideBaseSeqPath;

        private MsFraggerPeptideFile _peptideBaseSeqFile;
        public MsFraggerPeptideFile CombinedPeptideBaseSeq => _peptideBaseSeqFile ??= CombinePeptideFiles(_peptideBaseSeqPath);

        #endregion

        public MsFraggerResult(string directoryPath) : base(directoryPath)
        {
            _psmPath = Path.Combine(DirectoryPath, "Combined_psm.tsv");
            _peptidePath = Path.Combine(DirectoryPath, "combined_peptide.tsv");
            _peptideBaseSeqPath = Path.Combine(DirectoryPath, "Combined_BaseSequence_peptide.tsv");
            _proteinPath = Path.Combine(DirectoryPath, "combined_protein.tsv");

            IndividualFileResults = new List<MsFraggerIndividualFileResult>();
            foreach (var directory in System.IO.Directory.GetDirectories(DirectoryPath).Where(p => !p.Contains("shepherd") && !p.Contains("meta")))
            {
                IndividualFileResults.Add(new MsFraggerIndividualFileResult(directory));
            }

            _individualFileComparison = null;
            _chimeraPsmFile = null;
        }

        /// <summary>
        /// Combine psm files by aggregating all individual psm files
        /// </summary>
        /// <returns></returns>
        public MsFraggerPsmFile CombinePsmFiles()
        {
            if (!Override && File.Exists(_psmPath))
                return new MsFraggerPsmFile(_psmPath);

            var msFraggerResultFiles =
                Directory.GetFiles(DirectoryPath, "*psm.tsv", SearchOption.AllDirectories)
                    .Where(p => !p.Contains("Combined"));

            var results = new List<MsFraggerPsm>();
            foreach (var file in IndividualFileResults.Select(p => p.PsmFile))
            {
                file.LoadResults();
                results.AddRange(file.Results);
            }

            var combinedMsFraggerPsmFile = new MsFraggerPsmFile(_psmPath) { Results = results };
            combinedMsFraggerPsmFile.WriteResults(_psmPath);
            return combinedMsFraggerPsmFile;
        }

        public MsFraggerPeptideFile CombinePeptideFiles(string path = null)
        {
            path ??= _peptidePath;
            if (!Override && File.Exists(path))
                return new MsFraggerPeptideFile(path);

            var results = new List<MsFraggerPeptide>();
            foreach (var file in IndividualFileResults.Select(p => p.PeptideFile))
            {
                file.LoadResults();
                results.AddRange(file.Results);
            }

            var distinct = path.Contains("BaseS") 
                ? results.GroupBy(p => p.BaseSequence)
                .Select(p => p.MaxBy(result => result.Probability))
                .ToList()
                : results.GroupBy(p => p, CustomComparer<MsFraggerPeptide>.MsFraggerPeptideDistinctComparer)
                .Select(p => p.MaxBy(result => result.Probability))
                .ToList();

            var combinedMsFraggerPeptideFile = new MsFraggerPeptideFile(path) { Results = distinct };
            combinedMsFraggerPeptideFile.WriteResults(path);
            return combinedMsFraggerPeptideFile;
        }

        /// <summary>
        /// Compare individual file results. If the file already exists, return it. Otherwise, create it.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public override BulkResultCountComparisonFile IndividualFileComparison(string path = null)
        {
            path ??= _IndividualFilePath;
            if (!Override && File.Exists(path))
                return new BulkResultCountComparisonFile(path);

            List<BulkResultCountComparison> bulkResultCountComparisonFiles = new List<BulkResultCountComparison>();
            foreach (var file in IndividualFileResults)
            {
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.PsmFile.First().FileNameWithoutExtension);
                string fileName = fileNameWithoutExtension!.Replace("interact-", "");

                var uniquePeptides = path.Contains("BaseS")
                    ? file.PeptideFile.Results.GroupBy(p => p.BaseSequence).Count()
                    : file.PeptideFile.Results.Count;
                var uniquePsms = file.PsmFile.Results.Count;

                var uniquePeptidesProb = path.Contains("BaseS")
                    ? file.PeptideFile.Results.GroupBy(p => p.BaseSequence)
                        .Select(p => p.MaxBy(m => m.Probability))
                        .Count()
                    : file.PeptideFile.Results.Count();
                var uniquePsmsProb = file.PsmFile.Results.Count(p => p.PeptideProphetProbability >= 0.99);

                int proteinCount;
                int onePercentProteinCount;
                using (var sr = new StreamReader(_proteinPath))
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

                bulkResultCountComparisonFiles.Add(new BulkResultCountComparison
                {
                    DatasetName = DatasetName,
                    Condition = Condition,
                    FileName = fileName,
                    PsmCount = uniquePsms,
                    PeptideCount = uniquePeptides,
                    ProteinGroupCount = proteinCount,
                    OnePercentPsmCount = uniquePsmsProb,
                    OnePercentPeptideCount = uniquePeptidesProb,
                    OnePercentProteinGroupCount = onePercentProteinCount
                });
            }
            
            var bulkComparisonFile = new BulkResultCountComparisonFile(path)
            {
                Results = bulkResultCountComparisonFiles
            };
            bulkComparisonFile.WriteResults(path);
            return bulkComparisonFile;
        }

        public override ChimeraCountingFile CountChimericPsms()
        {
            if (!Override && File.Exists(_chimeraPsmPath))
                return new ChimeraCountingFile(_chimeraPsmPath);

            var allPSms = CombinedPsms.Results
                .GroupBy(p => p, CustomComparer<MsFraggerPsm>.MsFraggerChimeraComparer)
                .GroupBy(m => m.Count())
                .ToDictionary(p => p.Key, p => p.Count());
            var filtered = CombinedPsms.Results
                .Where(p => p.PeptideProphetProbability >= 0.99)
                .GroupBy(p => p, CustomComparer<MsFraggerPsm>.MsFraggerChimeraComparer)
                .GroupBy(m => m.Count())
                .ToDictionary(p => p.Key, p => p.Count());

            var results = allPSms.Keys.Select(count => new ChimeraCountingResult(count, allPSms[count],
                filtered.TryGetValue(count, out var psmCount) ? psmCount : 0, DatasetName, Condition )).ToList();
            _chimeraPsmFile = new ChimeraCountingFile() { FilePath = _chimeraPsmPath, Results = results };
            _chimeraPsmFile.WriteResults(_chimeraPsmPath);
            return _chimeraPsmFile;
        }

        public override BulkResultCountComparisonFile GetBulkResultCountComparisonFile(string path = null)
        {
            path ??= _bulkResultCountComparisonPath;
            if (!Override && File.Exists(path))
                return new BulkResultCountComparisonFile(path);

            var peptideFile = path.Contains("BaseS") ? CombinedPeptideBaseSeq : CombinedPeptides;
            var psmsCount = CombinedPsms.Results.Count;
            var peptidesCount = peptideFile.Results.Count;

            var psmsProbCount = CombinedPsms.Results.Count(p => p.PeptideProphetProbability > 0.99);
            var peptidesProbCount = peptideFile.Results.Count(p => p.Probability > 0.99);

            int proteinCount;
            int onePercentProteinCount;
            using (var sr = new StreamReader(_proteinPath))
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

            var bulkResultCountComparison = new BulkResultCountComparison
            {
                DatasetName = DatasetName,
                Condition = Condition,
                FileName = "c",
                PsmCount = psmsCount,
                PeptideCount = peptidesCount,
                ProteinGroupCount = proteinCount,
                OnePercentPsmCount = psmsProbCount,
                OnePercentPeptideCount = peptidesProbCount,
                OnePercentProteinGroupCount = onePercentProteinCount
            };

            var bulkComparisonFile = new BulkResultCountComparisonFile(path)
            {
                Results = new List<BulkResultCountComparison> { bulkResultCountComparison }
            };
            bulkComparisonFile.WriteResults(path);
            return bulkComparisonFile;
        }
    }

    public class MsFraggerIndividualFileResult
    {
        public string DirectoryPath { get; set; }

        private string _psmPath;
        private MsFraggerPsmFile _psmFile;
        public MsFraggerPsmFile PsmFile => _psmFile ??= new MsFraggerPsmFile(_psmPath);

        private string _peptidePath;
        private MsFraggerPeptideFile _peptideFile;
        public MsFraggerPeptideFile PeptideFile => _peptideFile ??= new MsFraggerPeptideFile(_peptidePath);

        private string _proteinPath;
        private MsFraggerProteinFile _proteinFile;
        public MsFraggerProteinFile ProteinFile => _proteinFile ??= new MsFraggerProteinFile(_proteinPath);


        public MsFraggerIndividualFileResult(string directoryPath)
        {
            DirectoryPath = directoryPath;
            _psmPath = System.IO.Path.Combine(DirectoryPath, "psm.tsv");
            _peptidePath = System.IO.Path.Combine(DirectoryPath, "peptide.tsv");
            _proteinPath = System.IO.Path.Combine(DirectoryPath, "protein.tsv");
        }
    }
}
