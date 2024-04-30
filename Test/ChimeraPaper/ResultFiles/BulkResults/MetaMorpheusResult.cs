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
using MassSpectrometry;
using System.Text.RegularExpressions;
using Chemistry;
using Readers;

namespace Test.ChimeraPaper.ResultFiles
{
    public class MetaMorpheusResult : BulkResult
    {
        public override BulkResultCountComparisonFile BaseSeqIndividualFileComparisonFile => _baseSeqIndividualFileComparison ??= CountIndividualFilesForFengChaoComparison();
        public string[] DataFilePaths { get; set; } // set by CellLineResults constructor
        public MetaMorpheusResult(string directoryPath) : base(directoryPath)
        {
            _psmPath = Directory.GetFiles(directoryPath, "*PSMs.psmtsv", SearchOption.AllDirectories).First();
            _peptidePath = Directory.GetFiles(directoryPath, "*Peptides.psmtsv", SearchOption.AllDirectories).FirstOrDefault();
            if (_peptidePath is null)
            {
                IsTopDown = true;
                _peptidePath = Directory.GetFiles(directoryPath, "*Proteoforms.psmtsv", SearchOption.AllDirectories).First();
            }
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
            foreach (var individualFile in fileNames.GroupBy(p =>
                             Path.GetFileNameWithoutExtension(p).Replace("-calib", "").Replace("-averaged", "")
                                 .Replace("_Peptides", "").Replace("_PSMs", "").Replace("_ProteinGroups", "")
                                 .Replace("_Proteoforms", ""))
                         .ToDictionary(p => p.Key, p => p.ToList()))
            {
                string psm = individualFile.Value.First(p => p.Contains("PSM"));
                string peptide = individualFile.Value.First(p => p.Contains("Peptide") || p.Contains("Proteoform"));
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

        private string _chimeraPeptidePath => Path.Combine(DirectoryPath, $"{DatasetName}_{Condition}_{ResultType}_{FileIdentifiers.ChimeraCountingFile}");
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

        private string _chimeraBreakDownPath => Path.Combine(DirectoryPath, $"{DatasetName}_{Condition}_{FileIdentifiers.ChimeraBreakdownComparison}");
        private ChimeraBreakdownFile _chimeraBreakdownFile;
        public ChimeraBreakdownFile ChimeraBreakdownFile => _chimeraBreakdownFile ??= GetChimeraBreakdownFile();
        public ChimeraBreakdownFile GetChimeraBreakdownFile()
        {
            if (!Override && File.Exists(_chimeraBreakDownPath))
                return new ChimeraBreakdownFile(_chimeraBreakDownPath);

            bool useIsolation;
            List<ChimeraBreakdownRecord> chimeraBreakDownRecords = new();
            // PSMs or PrSMs
            foreach (var fileGroup in PsmTsvReader.ReadTsv(_psmPath, out _)
                         .Where(p => p.PEP_QValue <= 0.01)
                         .GroupBy(p => p.FileNameWithoutExtension))
            {
                useIsolation = true;
                MsDataFile dataFile = null;
                var dataFilePath = DataFilePaths.FirstOrDefault(p => p.Contains(fileGroup.Key));
                if (dataFilePath == null)
                    useIsolation = false;
                else
                {
                    try
                    {
                        dataFile = MsDataFileReader.GetDataFile(dataFilePath);
                        dataFile.InitiateDynamicConnection();
                    }
                    catch
                    {
                        useIsolation = false;
                    }
                }
                foreach (var chimeraGroup in fileGroup.GroupBy(p => p, CustomComparer<PsmFromTsv>.ChimeraComparer)
                             .Select(p => p.ToArray()))
                {
                    var record = new ChimeraBreakdownRecord()
                    {
                        Dataset = DatasetName,
                        FileName = chimeraGroup.First().FileNameWithoutExtension.Replace("-calib", "").Replace("-averaged", ""),
                        Condition = Condition,
                        Ms2ScanNumber = chimeraGroup.First().Ms2ScanNumber,
                        Type = ChimeraBreakdownType.Psm,
                        IdsPerSpectra = chimeraGroup.Length,
                        TargetCount = chimeraGroup.Count(p => p.DecoyContamTarget == "T"),
                        DecoyCount = chimeraGroup.Count(p => p.DecoyContamTarget == "D")
                    };

                    PsmFromTsv parent = null;
                    if (chimeraGroup.Length != 1)
                    {
                        PsmFromTsv[] orderedChimeras;
                        if (useIsolation) // use the precursor with the closest mz to the isolation mz
                        {
                            var ms2Scan = dataFile.GetOneBasedScan(chimeraGroup.First().Ms2ScanNumber);
                            var isolationMz = ms2Scan.IsolationMz;
                            if (isolationMz is null) // if this fails, order by score
                                orderedChimeras = chimeraGroup
                                    .Where(p => p.DecoyContamTarget == "T")
                                    .OrderByDescending(p => p.Score)
                                    .ThenBy(p => Math.Abs(p.DeltaMass))
                                    .ToArray();
                            else
                                orderedChimeras = chimeraGroup
                                    .Where(p => p.DecoyContamTarget == "T")
                                    .OrderBy(p => Math.Abs(p.PrecursorMz - (double)isolationMz))
                                    .ThenByDescending(p => p.Score)
                                    .ToArray();
                            record.IsolationMz = isolationMz ?? -1;
                        }
                        else // use the precursor with the highest score
                        {
                            orderedChimeras = chimeraGroup
                                .Where(p => p.DecoyContamTarget == "T")
                                .OrderByDescending(p => p.Score)
                                .ThenBy(p => Math.Abs(p.DeltaMass))
                                .ToArray();
                        }

                        foreach (var chimericPsm in orderedChimeras)
                            if (parent is null)
                                parent = chimericPsm;
                            else if (parent.BaseSeq == chimericPsm.BaseSeq)
                                record.UniqueForms++;
                            else
                                record.UniqueProteins++;
                    }
                    chimeraBreakDownRecords.Add(record);
                }
                if (useIsolation)
                    dataFile.CloseDynamicConnection();
            }


            // Peptides or Proteoforms
            foreach (var fileGroup in PsmTsvReader.ReadTsv(_peptidePath, out _)
                         .Where(p => p.PEP_QValue <= 0.01)
                         .GroupBy(p => p.FileNameWithoutExtension))
            {
                useIsolation = true;
                MsDataFile dataFile = null;
                var dataFilePath = DataFilePaths.FirstOrDefault(p => p.Contains(fileGroup.Key));
                if (dataFilePath == null)
                    useIsolation = false;
                else
                {
                    try
                    {
                        dataFile = MsDataFileReader.GetDataFile(dataFilePath);
                        dataFile.InitiateDynamicConnection();
                    }
                    catch
                    {
                        useIsolation = false;
                    }
                }
                foreach (var chimeraGroup in fileGroup.GroupBy(p => p, CustomComparer<PsmFromTsv>.ChimeraComparer)
                             .Select(p => p.ToArray()))
                {
                    var record = new ChimeraBreakdownRecord()
                    {
                        Dataset = DatasetName,
                        FileName = chimeraGroup.First().FileNameWithoutExtension.Replace("-calib", "").Replace("-averaged", ""),
                        Condition = Condition,
                        Ms2ScanNumber = chimeraGroup.First().Ms2ScanNumber,
                        Type = ChimeraBreakdownType.Peptide,
                        IdsPerSpectra = chimeraGroup.Length,
                        TargetCount = chimeraGroup.Count(p => p.DecoyContamTarget == "T"),
                        DecoyCount = chimeraGroup.Count(p => p.DecoyContamTarget == "D")
                    };

                    PsmFromTsv parent = null;
                    if (chimeraGroup.Length != 1)
                    {
                        PsmFromTsv[] orderedChimeras;
                        if (useIsolation) // use the precursor with the closest mz to the isolation mz
                        {
                            var ms2Scan = dataFile.GetOneBasedScan(chimeraGroup.First().Ms2ScanNumber);
                            var isolationMz = ms2Scan.IsolationMz;
                            if (isolationMz is null) // if this fails, order by score
                                orderedChimeras = chimeraGroup
                                    .Where(p => p.DecoyContamTarget == "T")
                                    .OrderByDescending(p => p.Score)
                                    .ThenBy(p => Math.Abs(p.DeltaMass)).ToArray();
                            else
                                orderedChimeras = chimeraGroup
                                    .Where(p => p.DecoyContamTarget == "T")
                                    .OrderBy(p => Math.Abs(p.PrecursorMz - (double)isolationMz))
                                    .ThenByDescending(p => p.Score).ToArray();
                            record.IsolationMz = isolationMz ?? -1;
                        }
                        else // use the precursor with the highest score
                        {
                            orderedChimeras = chimeraGroup
                                .Where(p => p.DecoyContamTarget == "T")
                                .OrderByDescending(p => p.Score)
                                .ThenBy(p => Math.Abs(p.DeltaMass)).ToArray();
                        }

                        foreach (var chimericPsm in orderedChimeras)
                            if (parent is null)
                                parent = chimericPsm;
                            else if (parent.BaseSeq == chimericPsm.BaseSeq)
                                record.UniqueForms++;
                            else
                                record.UniqueProteins++;
                    }
                    chimeraBreakDownRecords.Add(record);
                }
                if (useIsolation)
                    dataFile.CloseDynamicConnection();
            }

            var file = new ChimeraBreakdownFile(_chimeraBreakDownPath) { Results = chimeraBreakDownRecords };
            file.WriteResults(_chimeraBreakDownPath);
            return file;
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

    public static class Extensions
    {
        public static MsDataScan CloneWithNewPrecursor(this MsDataScan scan, double precursorMz, int precursorCharge,
            double precursorIntensity)
        {
            return new MsDataScan(scan.MassSpectrum, scan.OneBasedScanNumber, scan.MsnOrder, scan.IsCentroid,
                scan.Polarity, scan.RetentionTime, scan.ScanWindowRange, scan.ScanFilter, scan.MzAnalyzer,
                scan.TotalIonCurrent, scan.InjectionTime, scan.NoiseData, scan.NativeId, precursorMz,
                precursorCharge, precursorIntensity, scan.IsolationMz, scan.IsolationWidth,
                scan.DissociationType, scan.OneBasedPrecursorScanNumber, scan.SelectedIonMonoisotopicGuessMz,
                scan.HcdEnergy);
        }

        public static MsDataScan Clone(this MsDataScan scan)
        {
            return new MsDataScan(scan.MassSpectrum, scan.OneBasedScanNumber, scan.MsnOrder, scan.IsCentroid,
                scan.Polarity, scan.RetentionTime, scan.ScanWindowRange, scan.ScanFilter, scan.MzAnalyzer,
                scan.TotalIonCurrent, scan.InjectionTime, scan.NoiseData, scan.NativeId, scan.SelectedIonMZ,
                scan.SelectedIonChargeStateGuess, scan.SelectedIonIntensity, scan.IsolationMz, scan.IsolationWidth,
                scan.DissociationType, scan.OneBasedPrecursorScanNumber, scan.SelectedIonMonoisotopicGuessMz,
                scan.HcdEnergy);
        }



        public static string[] AcceptableMods = new[]
        {
            "Oxidation on M",
            "Acetylation on X",
            "Acetylation on K",
            "Phosphorylation on S",
            "Phosphorylation on T",
            "Phosphorylation on Y",
            "Succinylation on K",
            "Methylation on K",
            "Methylation on R",
            "Dimethylation on K",
            "Dimethylation on R",
            "Trimethylation on K",
            "Ammonia loss on N",
            "Deamidation on Q",
            "Carbamidomethyl on C",
            "Hydroxylation on M",
        };
        public static string PeptideModSeq(this PsmFromTsv psm, Dictionary<string, double> modDictionary)
        {
            // Regex pattern to match words in brackets
            string pattern = @"\[(.*?)\]";

            // Replace words in brackets with numerical values
            string output = Regex.Replace(psm.FullSequence.Split('|')[0], pattern, match =>
            {
                string[] parts = match.Groups[1].Value.Split(':');
                var mod = modDictionary.TryGetValue(parts[1], out double value);
                if (parts.Length == 2 && modDictionary.ContainsKey(parts[1]))
                {
                    if (AcceptableMods.Contains(parts[1]))
                    {
                        var symbol = modDictionary[parts[1]] > 0 ? "+" : "";
                        return $"[{symbol}{modDictionary[parts[1]]:N6}]";
                    }

                    return "-1";
                }
                return "-1";
            });

            return output.Contains("-1") ? "" : output;
        }
    }
}
