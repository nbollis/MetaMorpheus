using EngineLayer;
using EngineLayer.FdrAnalysis;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Easy.Common.Extensions;
using Test.AveragingPaper;
using Proteomics.ProteolyticDigestion;
using Proteomics.RetentionTimePrediction;

namespace Test.ChimeraPaper.ResultFiles
{
    public class MetaMorpheusResult : BulkResult
    {
        public override BulkResultCountComparisonFile BaseSeqIndividualFileComparisonFile => _baseSeqIndividualFileComparison ??= CountIndividualFilesForFengChaoComparison();


        public MetaMorpheusResult(string directoryPath) : base(directoryPath)
        {
            _psmPath = Directory.GetFiles(directoryPath, "*PSMs.psmtsv", SearchOption.AllDirectories).First();
            _peptidePath = Directory.GetFiles(directoryPath, "*Peptides.psmtsv", SearchOption.AllDirectories).First();
            _proteinPath = Directory.GetFiles(directoryPath, "*ProteinGroups.tsv", SearchOption.AllDirectories).First();

            _individualFileComparison = null;
            _chimeraPsmFile = null;
        }

        public override BulkResultCountComparisonFile IndividualFileComparison(string path = null)
        {
            path ??= _IndividualFilePath;
            if (!Override && File.Exists(path))
                return new BulkResultCountComparisonFile(path);

            var indFileDir =
                Directory.GetDirectories(DirectoryPath, "Individual File Results", SearchOption.AllDirectories);
            if (indFileDir.Length == 0)
                return null;
            var indFileDirectory = indFileDir.First();

            var fileNames = Directory.GetFiles(indFileDirectory, "*tsv");
            List<BulkResultCountComparison> results = new List<BulkResultCountComparison>();
            foreach (var individualFile in fileNames.GroupBy(p => Path.GetFileNameWithoutExtension(p).Split('-')[0])
                         .ToDictionary(p => p.Key, p => p.ToList()))
            {
                string psm = individualFile.Value.First(p => p.Contains("PSM"));
                string peptide = individualFile.Value.First(p => p.Contains("Peptide"));
                string protein = individualFile.Value.First(p => p.Contains("Protein"));

                var spectralmatches = PsmTsvReader.ReadTsv(psm, out _)
                    .Where(p => p.DecoyContamTarget == "T").ToList();
                var peptides = PsmTsvReader.ReadTsv(peptide, out _)
                    .Where(p => p.DecoyContamTarget == "T")
                    .DistinctBy(p => p.BaseSeq).ToList();

                int count = 0;
                int onePercentCount = 0;
                using (var sw = new StreamReader(File.OpenRead(protein)))
                {
                    var header = sw.ReadLine();
                    var headerSplit = header.Split('\t');
                    var qValueIndex = Array.IndexOf(headerSplit, "Protein QValue");


                    while (!sw.EndOfStream)
                    {
                        var line = sw.ReadLine();
                        var values = line.Split('\t');
                        count++;
                        if (double.Parse(values[qValueIndex]) <= 0.01)
                            onePercentCount++;
                    }
                }

                int psmCount = spectralmatches.Count;
                int onePercentPsmCount = spectralmatches.Count(p => p.PEP_QValue <= 0.01);
                int peptideCount = peptides.Count;
                int onePercentPeptideCount = peptides.Count(p => p.PEP_QValue <= 0.01);

                results.Add(new BulkResultCountComparison()
                {
                    DatasetName = DatasetName,
                    Condition = Condition,
                    FileName = individualFile.Key,
                    PsmCount = psmCount,
                    PeptideCount = peptideCount,
                    ProteinGroupCount = count,
                    OnePercentPsmCount = onePercentPsmCount,
                    OnePercentPeptideCount = onePercentPeptideCount,
                    OnePercentProteinGroupCount = onePercentCount
                });
            }

            var bulkComparisonFile = new BulkResultCountComparisonFile(path)
            {
                Results = results
            };
            bulkComparisonFile.WriteResults(path);
            return bulkComparisonFile;
        }

        public override ChimeraCountingFile CountChimericPsms()
        {
            if (File.Exists(_chimeraPsmPath))
                return new ChimeraCountingFile(_chimeraPsmPath);

            var psms = PsmTsvReader.ReadTsv(_psmPath, out _).Where(p => p.DecoyContamTarget == "T").ToList();
            var allPsmCounts = psms.GroupBy(p => p, CustomComparer<PsmFromTsv>.ChimeraComparer)
                .GroupBy(m => m.Count()).ToDictionary(p => p.Key, p => p.Count());
            var onePercentFdrPsmCounts = psms.Where(p => p.PEP_QValue <= 0.01).GroupBy(p => p, CustomComparer<PsmFromTsv>.ChimeraComparer)
                .GroupBy(m => m.Count()).ToDictionary(p => p.Key, p => p.Count());
            var results = allPsmCounts.Keys.Select(count => new ChimeraCountingResult(count, allPsmCounts[count],
                onePercentFdrPsmCounts.TryGetValue(count, out var psmCount) ? psmCount : 0, DatasetName, Condition)).ToList();

            var chimeraCountingFile = new ChimeraCountingFile(_chimeraPsmPath) { Results = results };
            chimeraCountingFile.WriteResults(_chimeraPsmPath);
            return chimeraCountingFile;
        }

        private string _chimeraPeptidePath => Path.Combine(DirectoryPath, $"{DatasetName}_{Condition}_Peptide_{FileIdentifiers.ChimeraCountingFile}");
        private ChimeraCountingFile _chimeraPeptideFile;
        public ChimeraCountingFile ChimeraPeptideFile => _chimeraPeptideFile ??= CountChimericPeptides();
        public ChimeraCountingFile CountChimericPeptides()
        {
            if (!Override && File.Exists(_chimeraPeptidePath))
                return new ChimeraCountingFile(_chimeraPeptidePath);

            var peptides = PsmTsvReader.ReadTsv(_peptidePath, out _).Where(p => p.DecoyContamTarget == "T").ToList();
            var allPeptideCounts = peptides.GroupBy(p => p, CustomComparer<PsmFromTsv>.ChimeraComparer)
                .GroupBy(m => m.Count()).ToDictionary(p => p.Key, p => p.Count());
            var onePercentFdrPeptideCounts = peptides.Where(p => p.PEP_QValue <= 0.01).GroupBy(p => p, CustomComparer<PsmFromTsv>.ChimeraComparer)
                .GroupBy(m => m.Count()).ToDictionary(p => p.Key, p => p.Count());
            var results = allPeptideCounts.Keys.Select(count => new ChimeraCountingResult(count, allPeptideCounts[count],
                               onePercentFdrPeptideCounts.TryGetValue(count, out var peptideCount) ? peptideCount : 0, DatasetName, Condition)).ToList();

            var chimeraCountingFile = new ChimeraCountingFile(_chimeraPeptidePath) { Results = results };
            chimeraCountingFile.WriteResults(_chimeraPeptidePath);
            return chimeraCountingFile;
        }

        public BulkResultCountComparisonFile CountIndividualFilesForFengChaoComparison()
        {
            if (!Override && File.Exists(_baseSeqIndividualFilePath))
                return new BulkResultCountComparisonFile(_baseSeqIndividualFilePath);

            var indFileDir =
                Directory.GetDirectories(DirectoryPath, "Individual File Results", SearchOption.AllDirectories);
            if (indFileDir.Length == 0)
                return null;
            
            var indFileDirectory = indFileDir.First();
                
            var fileNames = Directory.GetFiles(indFileDirectory, "*tsv");
            List<BulkResultCountComparison> results = new List<BulkResultCountComparison>();
            foreach (var individualFile in fileNames.GroupBy(p => Path.GetFileNameWithoutExtension(p).Split('-')[0])
                         .ToDictionary(p => p.Key, p => p.ToList()))
            {
                string psm = individualFile.Value.First(p => p.Contains("PSM"));
                string peptide = individualFile.Value.First(p => p.Contains("Peptide"));
                string protein = individualFile.Value.First(p => p.Contains("Protein"));

                var spectralmatches = PsmTsvReader.ReadTsv(psm, out _)
                    .Where(p => p.DecoyContamTarget == "T").ToList();
                var peptides = PsmTsvReader.ReadTsv(peptide, out _)
                    .Where(p => p.DecoyContamTarget == "T")
                    .DistinctBy(p => p.BaseSeq).ToList();

                int count = 0;
                int onePercentCount = 0;
                using (var sw = new StreamReader(File.OpenRead(protein)))
                {
                    var header = sw.ReadLine();
                    var headerSplit = header.Split('\t');
                    var qValueIndex = Array.IndexOf(headerSplit, "Protein QValue");
                    

                    while (!sw.EndOfStream)
                    {
                        var line = sw.ReadLine();
                        var values = line.Split('\t');
                        count++;
                        if (double.Parse(values[qValueIndex]) <= 0.01)
                            onePercentCount++;
                    }
                }

                int psmCount = spectralmatches.Count;
                int onePercentPsmCount = spectralmatches.Count(p => p.PEP_QValue <= 0.01);
                int peptideCount = peptides.Count;
                int onePercentPeptideCount = peptides.Count(p => p.PEP_QValue <= 0.01);
                int onePercentPeptideCountQ = peptides.Count(p => p.QValue <= 0.01);

                results.Add( new BulkResultCountComparison()
                {
                    DatasetName = DatasetName,
                    Condition = Condition,
                    FileName = individualFile.Key,
                    PsmCount = psmCount,
                    PeptideCount = peptideCount,
                    ProteinGroupCount = count,
                    OnePercentPsmCount = onePercentPsmCount,
                    OnePercentPeptideCount = onePercentPeptideCountQ,
                    OnePercentProteinGroupCount = onePercentCount
                });
            }
            var bulkComparisonFile = new BulkResultCountComparisonFile(_baseSeqIndividualFilePath)
            {
                Results = results
            };
            bulkComparisonFile.WriteResults(_baseSeqIndividualFilePath);
            return bulkComparisonFile;
        }

        public override BulkResultCountComparisonFile GetBulkResultCountComparisonFile(string path = null)
        {
            path ??= _bulkResultCountComparisonPath;
            if (!Override && File.Exists(path))
                return new BulkResultCountComparisonFile(path);

            var psms = path.Contains("BaseS") ?
                PsmTsvReader.ReadTsv(_psmPath, out _).Where(p => p.DecoyContamTarget == "T").DistinctBy(p => p.BaseSeq).ToList() 
                : PsmTsvReader.ReadTsv(_psmPath, out _).Where(p => p.DecoyContamTarget == "T").ToList();
            var peptides = path.Contains("BaseS") ?
                PsmTsvReader.ReadTsv(_peptidePath, out _).Where(p => p.DecoyContamTarget == "T").DistinctBy(p => p.BaseSeq).ToList()
                : PsmTsvReader.ReadTsv(_peptidePath, out _).Where(p => p.DecoyContamTarget == "T").ToList();

            int psmsCount = psms.Count;
            int peptidesCount = peptides.Count;
            int onePercentPsmCount = psms.Count(p => p.PEP_QValue <= 0.01);
            int onePercentPeptideCount = peptides.Count(p => p.PEP_QValue <= 0.01);
            int onePercentUnambiguousPsmCount = psms.Count(p => p.PEP_QValue <= 0.01 && p.AmbiguityLevel == "1");
            int onePercentUnambiguousPeptideCount = peptides.Count(p => p.PEP_QValue <= 0.01 && p.AmbiguityLevel == "1");


            int proteingCount = 0;
            int onePercentProteinCount = 0;

            using (var sw = new StreamReader(File.OpenRead(_proteinPath)))
            {
                var header = sw.ReadLine();
                var headerSplit = header.Split('\t');
                var qValueIndex = Array.IndexOf(headerSplit, "Protein QValue");
                int count = 0;
                int onePercentCount = 0;

                while (!sw.EndOfStream)
                {
                    var line = sw.ReadLine();
                    var values = line.Split('\t');
                    proteingCount++;
                    if (double.Parse(values[qValueIndex]) <= 0.01)
                        onePercentProteinCount++;
                }
            }

            var bulkResultCountComparison = new BulkResultCountComparison
            {
                DatasetName = DatasetName,
                Condition = Condition,
                FileName = "Combined",
                PsmCount = psmsCount,
                PeptideCount = peptidesCount,
                ProteinGroupCount = proteingCount,
                OnePercentPsmCount = onePercentPsmCount,
                OnePercentPeptideCount = onePercentPeptideCount,
                OnePercentProteinGroupCount = onePercentProteinCount,
                OnePercentUnambiguousPsmCount = onePercentUnambiguousPsmCount,
                OnePercentUnambiguousPeptideCount = onePercentUnambiguousPeptideCount
            };

            var bulkComparisonFile = new BulkResultCountComparisonFile(path)
            {
                Results = new List<BulkResultCountComparison> { bulkResultCountComparison }
            };
            bulkComparisonFile.WriteResults(path);
            return bulkComparisonFile;
        }

        #region Retention Time Predictions

        private string _retentionTimePredictionPath => Path.Combine(DirectoryPath, $"{DatasetName}_MM_{FileIdentifiers.RetentionTimePredictionReady}");
        private string _chronologerRunningFilePath => Path.Combine(DirectoryPath, $"{DatasetName}_{FileIdentifiers.ChronologerReadyFile}");
        private RetentionTimePredictionFile _retentionTimePredictionFile;

        public RetentionTimePredictionFile RetentionTimePredictionFile
        {
            get
            {
                if (File.Exists(_retentionTimePredictionPath))
                {
                    _retentionTimePredictionFile ??= new RetentionTimePredictionFile() { FilePath = _retentionTimePredictionPath };
                    return _retentionTimePredictionFile;
                }
                else
                {
                    CreateRetentionTimePredictionReadyFile();
                    _retentionTimePredictionFile ??= new RetentionTimePredictionFile() { FilePath = _retentionTimePredictionPath };
                    return _retentionTimePredictionFile;
                }
            }
        }

        public void CreateRetentionTimePredictionReadyFile()
        {
            string outpath = _retentionTimePredictionPath;
            if (File.Exists(outpath) || !DirectoryPath.Contains("MetaMorpheusWithLibrary"))
                return;
            var modDict = GlobalVariables.AllModsKnown.ToDictionary(p => p.IdWithMotif, p => p.MonoisotopicMass.Value);
            var peptides = PsmTsvReader.ReadTsv(_peptidePath, out _)
                .Where(p => p.DecoyContamTarget == "T" && p.PEP_QValue <= 0.01)
                .ToList();
            var calc = new SSRCalc3("SSRCalc 3.0 (300A)", SSRCalc3.Column.A300);
            List<RetentionTimePredictionEntry> retentionTimePredictions = new List<RetentionTimePredictionEntry>();
            foreach (var group in peptides.GroupBy(p => p, CustomComparer<PsmFromTsv>.ChimeraComparer))
            {
                bool isChimeric = group.Count() > 1;
                retentionTimePredictions.AddRange(group.Select(p =>
                    new RetentionTimePredictionEntry(p.FileNameWithoutExtension, p.Ms2ScanNumber, p.PrecursorScanNum,
                        p.RetentionTime.Value, p.BaseSeq, p.FullSequence, p.PeptideModSeq(modDict), p.QValue,
                        p.PEP_QValue, p.PEP, p.SpectralAngle ?? -1, isChimeric)
                    { SSRCalcPrediction = calc.ScoreSequence(new PeptideWithSetModifications(p.FullSequence.Split('|')[0], GlobalVariables.AllModsKnownDictionary)) }));
            }
            var retentionTimePredictionFile = new RetentionTimePredictionFile() { FilePath = outpath, Results = retentionTimePredictions };
            retentionTimePredictionFile.WriteResults(outpath);

            var chronologerReady = new RetentionTimePredictionFile() { FilePath = _chronologerRunningFilePath, Results = retentionTimePredictions.Where(p => p.PeptideModSeq != "").ToList() };
            chronologerReady.WriteResults(_chronologerRunningFilePath);
        }

        public void AppendChronologerPrediction()
        {
            var chronologerResultFile = Directory
                .GetFiles(DirectoryPath, FileIdentifiers.ChoronologerResults, SearchOption.AllDirectories)
                .First();


            foreach (var line in File.ReadAllLines(chronologerResultFile).Skip(1))
            {
                var split = line.Split('\t');
                var fileName = split[0];
                var scanNum = int.Parse(split[1]);
                var precursorScanNumber = int.Parse(split[2]);
                var fullSequence = split[6];
                var prediction = double.Parse(split.Last());
                var result = RetentionTimePredictionFile.Results.First(p => p.ScanNumber == scanNum && p.PrecursorScanNumber == precursorScanNumber && p.FileNameWithoutExtension == fileName && p.FullSequence == fullSequence);
                if (result.PeptideModSeq == "")
                    continue;
                if (result.ChronologerPrediction != 0)
                    continue;
                result.ChronologerPrediction = prediction;
            }

            RetentionTimePredictionFile.WriteResults(_retentionTimePredictionPath);
        }

        #endregion
    }
}
