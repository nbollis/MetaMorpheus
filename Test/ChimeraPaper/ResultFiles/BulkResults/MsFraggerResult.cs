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
        public MsFraggerPeptideFile CombinedPeptides => _peptideFile ??= CombinePeptideFiles();

        private MsFraggerProteinFile _proteinFile;
        public MsFraggerProteinFile CombinedProteins => _proteinFile ??= new MsFraggerProteinFile(_proteinPath);

        public MsFraggerResult(string directoryPath) : base(directoryPath)
        {
            _psmPath = Path.Combine(DirectoryPath, "Combined_psm.tsv");
            _peptidePath = Path.Combine(DirectoryPath, "Combined_peptide.tsv");
            _proteinPath = Path.Combine(DirectoryPath, "Combined_protein.tsv");

            IndividualFileResults = new List<MsFraggerIndividualFileResult>();
            foreach (var directory in System.IO.Directory.GetDirectories(DirectoryPath).Where(p => !p.Contains("shepherd")))
            {
                IndividualFileResults.Add(new MsFraggerIndividualFileResult(directory));
            }

            _individualFileComparison = null;
            _chimeraPsmFile = null;
        }

        public MsFraggerPsmFile CombinePsmFiles()
        {
            if (!Override && File.Exists(_psmPath))
                return new MsFraggerPsmFile(_psmPath);

            var msFraggerResultFiles =
                Directory.GetFiles(DirectoryPath, "*psm.tsv", SearchOption.AllDirectories);

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

        public MsFraggerPeptideFile CombinePeptideFiles()
        {
            if (!Override && File.Exists(_peptidePath))
                return new MsFraggerPeptideFile(_peptidePath);

            var results = new List<MsFraggerPeptide>();
            foreach (var file in IndividualFileResults.Select(p => p.PeptideFile))
            {
                file.LoadResults();
                results.AddRange(file.Results);
            }

            var distinct = results.GroupBy(p => p, CustomComparer<MsFraggerPeptide>.MsFraggerPeptideDistinctComparer)
                .Select(p => p.MaxBy(result => result.Probability))
                .ToList();
            var combinedMsFraggerPeptideFile = new MsFraggerPeptideFile(_peptidePath) { Results = distinct };
            combinedMsFraggerPeptideFile.WriteResults(_peptidePath);
            return combinedMsFraggerPeptideFile;
        }

        public override BulkResultCountComparisonFile IndividualFileComparison()
        {
            if (!Override && _individualFileComparison != null)
                return _individualFileComparison;

            if (!Override && File.Exists(_IndividualFilePath))
                return _individualFileComparison = new BulkResultCountComparisonFile(_IndividualFilePath);

            List<BulkResultCountComparison> bulkResultCountComparisonFiles = new List<BulkResultCountComparison>();
            foreach (var file in IndividualFileResults)
            {
                file.PsmFile.LoadResults();
                file.PeptideFile.LoadResults();

                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.PsmFile.First().FileNameWithoutExtension);
                string fileName = fileNameWithoutExtension!.Replace("interact-", "");

                var uniquePeptides = file.PeptideFile.Results.Count;
                var uniquePsms = file.PsmFile.Results.Count;

                var uniquePeptidesProb = file.PeptideFile.Results.Count(p => p.Probability >= 0.99);
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

            var bulkComparisonFile = new BulkResultCountComparisonFile(_IndividualFilePath)
            {
                Results = bulkResultCountComparisonFiles
            };
            bulkComparisonFile.WriteResults(_IndividualFilePath);
            return bulkComparisonFile;
        }

        public override ChimeraCountingFile CountChimericPsms()
        {
            if (!Override && File.Exists(_chimeraPsmPath))
                return new ChimeraCountingFile(_chimeraPsmPath);

            var allPSms = CombinedPsms.Results.GroupBy(p => p, CustomComparer<MsFraggerPsm>.MsFraggerChimeraComparer)
                .GroupBy(m => m.Count()).ToDictionary(p => p.Key, p => p.Count());
            var filtered = CombinedPsms.Results.Where(p => p.PeptideProphetProbability >= 0.99).GroupBy(p => p, CustomComparer<MsFraggerPsm>.MsFraggerChimeraComparer)
                .GroupBy(m => m.Count()).ToDictionary(p => p.Key, p => p.Count());

            var results = allPSms.Keys.Select(count => new ChimeraCountingResult(count, allPSms[count],
                filtered.TryGetValue(count, out var psmCount) ? psmCount : 0, DatasetName, Condition )).ToList();
            _chimeraPsmFile = new ChimeraCountingFile() { FilePath = _chimeraPsmPath, Results = results };
            _chimeraPsmFile.WriteResults(_chimeraPsmPath);
            return _chimeraPsmFile;
        }

        public override BulkResultCountComparisonFile GetBulkResultCountComparisonFile()
        {
            if (!Override && File.Exists(_bulkResultCountComparisonPath))
                return new BulkResultCountComparisonFile(_bulkResultCountComparisonPath);

            var psmsCount = CombinedPsms.Results.Count;
            var peptidesCount = CombinedPeptides.Results.Count;

            var psmsProbCount = CombinedPsms.Results.Count(p => p.PeptideProphetProbability > 0.99);
            var peptidesProbCount = CombinedPeptides.Results.Count(p => p.Probability > 0.99);

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
                FileName = "Combined",
                PsmCount = psmsCount,
                PeptideCount = peptidesCount,
                ProteinGroupCount = proteinCount,
                OnePercentPsmCount = psmsProbCount,
                OnePercentPeptideCount = peptidesProbCount,
                OnePercentProteinGroupCount = onePercentProteinCount
            };

            var bulkComparisonFile = new BulkResultCountComparisonFile(_bulkResultCountComparisonPath)
            {
                Results = new List<BulkResultCountComparison> { bulkResultCountComparison }
            };
            bulkComparisonFile.WriteResults(_bulkResultCountComparisonPath);
            return bulkComparisonFile;
        }
    }
}
