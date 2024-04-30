using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test.AveragingPaper;
using Test.ChimeraPaper.ResultFiles;

namespace Test.ChimeraPaper
{
    internal class ProsightPDResult : BulkResult
    {
        private PSPSPrSMFile _psmFile;
        private PSPSProteoformFile _peptideFile;
        private PSPSProteinFile _proteinFile;
        private string _inputFilePath;
        private PSPDInputFileFile _inputFile;
        private Dictionary<string, string> _idToFileNameDictionary;

        public PSPSPrSMFile PrsmFile => _psmFile ??= new PSPSPrSMFile(_psmPath);
        public PSPSProteoformFile ProteoformFile => _peptideFile ??= new PSPSProteoformFile(_peptidePath);
        public PSPSProteinFile ProteinFile => _proteinFile ??= new PSPSProteinFile(_proteinPath);
        public PSPDInputFileFile InputFile => _inputFile ??= new PSPDInputFileFile(_inputFilePath);
        public Dictionary<string,string> IdToFileNameDictionary => _idToFileNameDictionary ??= InputFile.ToDictionary(p => p.FileID, p => Path.GetFileNameWithoutExtension(p.FileName));

        public ProsightPDResult(string directoryPath) : base(directoryPath)
        {
            _psmPath = Directory.GetFiles(directoryPath, "*PrSMs.txt").First();
            _peptidePath = Directory.GetFiles(directoryPath, "*Proteoforms.txt").First();
            _proteinPath = Directory.GetFiles(directoryPath, "*Proteins.txt").First();
            _inputFilePath = Directory.GetFiles(directoryPath, "*InputFiles.txt").First();
        }

        public override BulkResultCountComparisonFile IndividualFileComparison(string path = null)
        {
            if (!Override && File.Exists(_IndividualFilePath))
                return new BulkResultCountComparisonFile(_IndividualFilePath);

            // set up result dictionary
            var results = PrsmFile
                .Select(p => p.FileID).Distinct()
                .ToDictionary(fileID => fileID,
                    fileID => new BulkResultCountComparison()
                    {
                        DatasetName = DatasetName, 
                        Condition = Condition, 
                        FileName = IdToFileNameDictionary[fileID],
                    });

            // foreach psm, if the proteoform shares accession and mods, count it. If the protein shares accession, count it.
            foreach (var fileGroupedPrsms in PrsmFile.GroupBy(p => p.FileID))
            {
                results[fileGroupedPrsms.Key].PsmCount = fileGroupedPrsms.Count();
                results[fileGroupedPrsms.Key].OnePercentPsmCount = fileGroupedPrsms.Count(p => p.NegativeLogEValue >= 5);

                foreach (var prsm in fileGroupedPrsms)
                {
                    var proteoforms = ProteoformFile.Where(p =>
                            p.Modifications == prsm.Modifications && p.ProteinAccessions == prsm.ProteinAccessions)
                        .ToArray();
                    if (proteoforms.Any())
                    {
                        results[fileGroupedPrsms.Key].PeptideCount++;
                        if (proteoforms.Any(p => p.QValue <= 0.01))
                            results[fileGroupedPrsms.Key].OnePercentPeptideCount++;
                    }

                    var proteins = ProteinFile.Where(p => p.Accession == prsm.ProteinAccessions).ToArray();
                    if (proteins.Any())
                    {
                        results[fileGroupedPrsms.Key].ProteinGroupCount++;
                        if (proteins.Any(p => p.QValue <= 0.01))
                            results[fileGroupedPrsms.Key].OnePercentProteinGroupCount++;
                    }
                }
            }

            var bulkResultComparisonFile = new BulkResultCountComparisonFile(_IndividualFilePath)
            {
                Results = results.Values.ToList()
            };
            bulkResultComparisonFile.WriteResults(_IndividualFilePath);
            return bulkResultComparisonFile;
        }

        public override ChimeraCountingFile CountChimericPsms()
        {
            if (!Override && File.Exists(_chimeraPsmPath))
                return new ChimeraCountingFile(_chimeraPsmPath);

            var allPsms = PrsmFile
                .GroupBy(p => p, CustomComparer<PSPDPrSMRecord>.PSPDPrSMChimeraComparer)
                .GroupBy(p => p.Count())
                .ToDictionary(p => p.Key, p => p.Count());
            var filtered = PrsmFile
                .Where(p => p.NegativeLogEValue >= 5)
                .GroupBy(p => p, CustomComparer<PSPDPrSMRecord>.PSPDPrSMChimeraComparer)
                .GroupBy(p => p.Count())
                .ToDictionary(p => p.Key, p => p.Count());

            var results = allPsms.Keys.Select(count => new ChimeraCountingResult(count, allPsms[count],
                filtered.TryGetValue(count, out var psmCount) ? psmCount : 0, DatasetName, Condition)).ToList();
            _chimeraPsmFile = new ChimeraCountingFile() { FilePath = _chimeraPsmPath, Results = results };
            _chimeraPsmFile.WriteResults(_chimeraPsmPath);
            return _chimeraPsmFile;
        }

        public override BulkResultCountComparisonFile GetBulkResultCountComparisonFile(string path = null)
        {
            if (!Override && File.Exists(_bulkResultCountComparisonPath))
                return new BulkResultCountComparisonFile(_bulkResultCountComparisonPath);

            var psmCount = PrsmFile.Count();
            var proteoformCount = ProteoformFile.Count();
            var proteinCount = ProteinFile.Count();

            var onePercentPsmCount = PrsmFile.Count(p => p.NegativeLogEValue >= 5);
            var onePercentProteoformCount = ProteoformFile.Count(p => p.QValue <= 0.01);
            var onePercentProteinCount = ProteinFile.Count(p => p.QValue <= 0.01);

            var bulkResultCountComparison = new BulkResultCountComparison()
            {
                DatasetName = DatasetName,
                Condition = Condition,
                FileName = "Combined",
                PsmCount = psmCount,
                PeptideCount = proteoformCount,
                ProteinGroupCount = proteinCount,
                OnePercentPsmCount = onePercentPsmCount,
                OnePercentPeptideCount = onePercentProteoformCount,
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
