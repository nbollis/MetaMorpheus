using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Easy.Common.Extensions;
using Easy.Common.Interfaces;
using Test.AveragingPaper;

namespace Test.ChimeraPaper.ResultFiles
{
    public class MsPathFinderTResults : BulkResult, IEnumerable<MsPathFinderTIndividualFileResult>
    {
        private string _combinedResultFilePath;

        public List<MsPathFinderTIndividualFileResult> IndividualFileResults { get; set; }
        public MsPathFinderTResults(string directoryPath) : base(directoryPath)
        {
            IsTopDown = true;
            IndividualFileResults = new List<MsPathFinderTIndividualFileResult>();

            // combined file if ProMexAlign was ran
            _combinedResultFilePath = Directory.GetFiles(DirectoryPath).FirstOrDefault(p => p.Contains("crosstab.tsv")); 

            // sorting out the individual result files
            var files = Directory.GetFiles(DirectoryPath)
                .Where(p => !p.Contains(".txt") && !p.Contains(".png") && !p.Contains(".db") && !p.Contains("Dataset"))
                .GroupBy(p => string.Join("_", Path.GetFileNameWithoutExtension(
                    p.Replace("_IcDecoy", "").Replace("_IcTarget", "").Replace("_IcTda", ""))))
                .ToDictionary(p => p.Key, p => p.ToList());
            foreach (var resultFile in files.Where(p => p.Value.Count == 6))
            {
                var key = resultFile.Key;
                var decoyPath = resultFile.Value.First(p => p.Contains("Decoy"));
                var targetPath = resultFile.Value.First(p => p.Contains("Target"));
                var combinedPath = resultFile.Value.First(p => p.Contains("IcTda"));
                var rawFilePath = resultFile.Value.First(p => p.Contains(".pbf"));
                var paramsPath = resultFile.Value.First(p => p.Contains(".param"));
                var ftFilepath = resultFile.Value.First(p => p.Contains(".ms1ft"));

                IndividualFileResults.Add(new MsPathFinderTIndividualFileResult(decoyPath, targetPath, combinedPath, key, ftFilepath, paramsPath, rawFilePath));
            }
            // TODO: Add case for the with mods search where not all items will be in the same directory
            foreach (var resultFile in files.Where(p => p.Value.Count == 4))
            {
                var key = resultFile.Key;
                var decoyPath = resultFile.Value.First(p => p.Contains("Decoy"));
                var targetPath = resultFile.Value.First(p => p.Contains("Target"));
                var combinedPath = resultFile.Value.First(p => p.Contains("IcTda"));
                var paramsPath = resultFile.Value.First(p => p.Contains(".param"));
                var rawFilePath = Directory.GetParent(directoryPath).GetDirectories("MsPathFinderT").First()
                    .GetFiles($"{key}.pbf").First().FullName;
                var ftPath = Directory.GetParent(directoryPath).GetDirectories("MsPathFinderT").First()
                    .GetFiles($"{key}.ms1ft").First().FullName;
                IndividualFileResults.Add(new MsPathFinderTIndividualFileResult(decoyPath, targetPath, combinedPath, key, ftPath, paramsPath, rawFilePath));
            }
        }

        public override BulkResultCountComparisonFile IndividualFileComparison(string path = null)
        {
            if (!Override && File.Exists(_IndividualFilePath))
                return new BulkResultCountComparisonFile(_IndividualFilePath);

            var results = new List<BulkResultCountComparison>();
            foreach (var file in IndividualFileResults)
            {
                var proteoformCount = file.TargetResults.Results.Count;
                var onePercentProteoformCount = file.TargetResults.FilteredResults.Count;

                results.Add(new BulkResultCountComparison()
                {
                    Condition = Condition,
                    DatasetName =DatasetName,
                    FileName = file.Name,
                    OnePercentPsmCount = onePercentProteoformCount,
                    PsmCount = proteoformCount,
                });
            }

            var bulkComparisonFile = new BulkResultCountComparisonFile(_IndividualFilePath)
            {
                Results = results
            };
            bulkComparisonFile.WriteResults(_IndividualFilePath);
            return bulkComparisonFile;
        }

        public override ChimeraCountingFile CountChimericPsms()
        {
            if (!Override && File.Exists(_chimeraPsmPath))
                return new ChimeraCountingFile(_chimeraPsmPath);

            var prsms = CombinedTargetResults.GroupBy(p => p, CustomComparer<MsPathFinderTResult>.MsPathFinderTChimeraComparer)
                .GroupBy(m => m.Count()).ToDictionary(p => p.Key, p => p.Count());
            var filtered = CombinedTargetResults.FilteredResults.GroupBy(p => p, CustomComparer<MsPathFinderTResult>.MsPathFinderTChimeraComparer)
                .GroupBy(m => m.Count()).ToDictionary(p => p.Key, p => p.Count());

            var results = prsms.Keys.Select(count => new ChimeraCountingResult(count, prsms[count],
                filtered.TryGetValue(count, out var psmCount) ? psmCount : 0, DatasetName, Condition)).ToList();
            _chimeraPsmFile = new ChimeraCountingFile() { FilePath = _chimeraPsmPath, Results = results };
            _chimeraPsmFile.WriteResults(_chimeraPsmPath);
            return _chimeraPsmFile;
        }

        public override BulkResultCountComparisonFile GetBulkResultCountComparisonFile(string path = null)
        {
            if (!Override && File.Exists(_bulkResultCountComparisonPath))
                return new BulkResultCountComparisonFile(_bulkResultCountComparisonPath);

            int proteoformCount = 0;
            int onePercentProteoformCount = 0;
            List<string> accessions = new();
            if (!_combinedResultFilePath.IsNullOrEmpty()) // if ProMexAlign was ran
            {
                using (var sw = new StreamReader(_combinedResultFilePath))
                {
                    var header = sw.ReadLine();
                    var eValueIndex = header!.Split('\t').ToList().IndexOf("BestEValue");
                    var nameIndex = header!.Split('\t').ToList().IndexOf("ProteinName");

                    while (!sw.EndOfStream)
                    {
                        var line = sw.ReadLine();
                        var splits = line.Split('\t');
                        if (splits[eValueIndex].IsNullOrEmpty())
                            continue;
                        proteoformCount++;
                        if (double.TryParse(splits[eValueIndex], out double eValue) && eValue < 0.01)
                            onePercentProteoformCount++;
                        try
                        {
                            accessions.Add(splits[nameIndex].Split('|')[1].Trim());
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                            Debugger.Break();
                        }
                    }
                }
            }


            var distinctProteins = accessions.Distinct().Count();
            var result = new BulkResultCountComparison()
            {
                Condition = Condition,
                DatasetName = DatasetName,
                FileName = "Combined",
                OnePercentPsmCount = CombinedTargetResults.FilteredResults.Count,
                PsmCount = CombinedTargetResults.Results.Count,
                PeptideCount = proteoformCount,
                OnePercentPeptideCount = onePercentProteoformCount,
                ProteinGroupCount = distinctProteins,
                OnePercentProteinGroupCount = distinctProteins
            };

            var bulkComparisonFile = new BulkResultCountComparisonFile(_bulkResultCountComparisonPath)
            {
                Results = new List<BulkResultCountComparison> { result }
            };
            bulkComparisonFile.WriteResults(_bulkResultCountComparisonPath);
            return bulkComparisonFile;
        }

        private string _combinedTargetResultFilePath => Path.Combine(DirectoryPath, "CombinedTargetResults_IcTarget.tsv");
        private MsPathFinderTResultFile _combinedTargetResults;
        public MsPathFinderTResultFile CombinedTargetResults => _combinedTargetResults ??= CombinePrSMFiles();
        public MsPathFinderTResultFile CombinePrSMFiles()
        {
            if (!Override && File.Exists(_combinedTargetResultFilePath))
                return new MsPathFinderTResultFile(_combinedTargetResultFilePath);

            var results = IndividualFileResults.SelectMany(p => p.TargetResults.Results).ToList();
            var file = new MsPathFinderTResultFile(_combinedTargetResultFilePath) { Results = results };
            file.WriteResults(_combinedTargetResultFilePath);
            return file;
        }

        private string _datasetInfoFilePath => Path.Combine(DirectoryPath, "DatasetInfoFile.tsv");
        public void CreateDatasetInfoFile()
        {
            if (File.Exists(_datasetInfoFilePath))
                return;
            using var sw = new StreamWriter(_datasetInfoFilePath);
            sw.WriteLine("Label\tRawFilePath\tMs1FtFilePath\tMsPathfinderIdFilePath");
            foreach (var individualFile in IndividualFileResults)
            {
                sw.WriteLine($"{individualFile.Name}\t{individualFile.RawFilePath}\t{individualFile.Ms1FtFilePath}\t{individualFile.CombinedPath}");
            }
            sw.Dispose();
        }

        public IEnumerator<MsPathFinderTIndividualFileResult> GetEnumerator()
        {
            return IndividualFileResults.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public class MsPathFinderTIndividualFileResult
    {
        public string Name { get; set; }
        private string _targetPath;
        private MsPathFinderTResultFile _targetResults;
        public MsPathFinderTResultFile TargetResults => _targetResults ??= new MsPathFinderTResultFile(_targetPath);

        private string _decoyPath;
        private MsPathFinderTResultFile _decoyResults;
        public MsPathFinderTResultFile DecoyResults => _decoyResults ??= new MsPathFinderTResultFile(_decoyPath);

        internal string CombinedPath;
        private MsPathFinderTResultFile _combinedResults;
        public MsPathFinderTResultFile CombinedResults => _combinedResults ??= new MsPathFinderTResultFile(CombinedPath);

        public string Ms1FtFilePath { get; set; }
        public string ParamPath { get; set; }
        public string RawFilePath { get; set; }

        public MsPathFinderTIndividualFileResult(string decoyPath, string targetPath, string combinedPath, string name, string ms1FtFilePath, string paramPath, string rawFilePath)
        {
            _decoyPath = decoyPath;
            _targetPath = targetPath;
            CombinedPath = combinedPath;
            Name = name;
            Ms1FtFilePath = ms1FtFilePath;
            ParamPath = paramPath;
            RawFilePath = rawFilePath;
        }

        public void ReWriteParamFile()
        {
            var lines = File.ReadAllLines(ParamPath);
            var newLines = new List<string>();
            foreach (var line in lines)
            {
                if (line.StartsWith("ActivationMethod"))
                    newLines.Add("ActivationMethod\tHCD");
                else if (line.StartsWith("MaxDynamicModificationsPerSequence"))
                    newLines.Add("MaxDynamicModificationsPerSequence\t2");
                else
                    newLines.Add(line);
            }
            File.WriteAllLines(ParamPath, newLines);
        }
        
    }
}
