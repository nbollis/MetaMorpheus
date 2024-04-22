using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using EngineLayer;
using Microsoft.VisualStudio.TestPlatform.Common.DataCollection;
using Org.BouncyCastle.Math.EC.Multiplier;
using Readers;
using Test.ChimeraPaper.ResultFiles;

namespace Test.ChimeraPaper;

public static class DatasetOperations
{

    #region Files

    private static string _internalComparisonMetaMorpheusPath => Path.Combine(BURunner.DirectoryPath, $"MetaMorpheus_{FileIdentifiers.InternalChimeraComparison}");
    private static BulkResultCountComparisonFile _internalComparisonMetaMorpheusFile;
    public static BulkResultCountComparisonFile InternalComparisonMetaMorpheusFile
    {
        get
        {
            if (File.Exists(_internalComparisonMetaMorpheusPath))
            {
                _internalComparisonMetaMorpheusFile = new BulkResultCountComparisonFile() {FilePath = _internalComparisonMetaMorpheusPath};
            }
            return _internalComparisonMetaMorpheusFile;
        }
    }

    private static string _internalComparisonFraggerPath => Path.Combine(BURunner.DirectoryPath, $"Fragger_{FileIdentifiers.InternalChimeraComparison}");
    private static BulkResultCountComparisonFile _internalComparisonFraggerFile;
    public static BulkResultCountComparisonFile InternalComparisonFraggerFile
    {
        get
        {
            if (File.Exists(_internalComparisonFraggerPath))
            {
                _internalComparisonFraggerFile = new BulkResultCountComparisonFile() { FilePath = _internalComparisonFraggerPath};
            }
            return _internalComparisonFraggerFile;
        }
    }

    private static string _chimeraCountingPath => Path.Combine(BURunner.DirectoryPath, $"BulkComparison_{FileIdentifiers.ChimeraCountingFile}");
    private static ChimeraCountingFile _chimeraCountingFile;
    public static ChimeraCountingFile ChimeraCountingFile
    {
        get
        {
            if (File.Exists(_chimeraCountingPath))
            {
                _chimeraCountingFile = new ChimeraCountingFile() { FilePath = _chimeraCountingPath};
            }
            return _chimeraCountingFile;
        }
    }

    #endregion

    public static void MergeAllResultComparisons(this List<Dataset> datasets)
    {
        var results = new List<BulkResultCountComparison>();
        foreach (var dataset in datasets)
        {
            var outPath = Path.Combine(dataset._directoryPath, FileIdentifiers.BottomUpResultComparison);
            if (File.Exists(outPath)) // if this has already been crunched for the dataset, just load it in
            {
                results.AddRange(new BulkResultCountComparisonFile() { FilePath = outPath}.Results);
            }
            else
            {
                var fileSpecificResults = new List<BulkResultCountComparison>();
                foreach (var resultType in Enum.GetValues<BottomUpResultType>())
                {
                    var result = dataset.GetBulkComparisonIndividual(resultType);
                    fileSpecificResults.Add(result);
                    results.Add(result);
                }

                var individualFile = new BulkResultCountComparisonFile() { FilePath = outPath, Results = fileSpecificResults};
                individualFile.WriteResults(outPath);
            }

        }

        var outpath = Path.Combine(BURunner.DirectoryPath, FileIdentifiers.BottomUpResultComparison);
        var file = new BulkResultCountComparisonFile() { FilePath = outpath, Results = results};
        file.WriteResults(outpath);
    }


    public static BulkResultCountComparison GetBulkComparisonIndividual(this Dataset dataset, BottomUpResultType resultType)
    {
        string psmPath;
        string peptidePath;
        string proteinPath;
        string datasetName = dataset.DatasetName;
        string condition;
        switch (resultType)
        {
            case BottomUpResultType.MetaMorpheus:
                psmPath = dataset._psmResultPath;
                peptidePath = dataset._peptideResultPath;
                proteinPath = dataset._proteinGroupsResultPath;
                condition = "MetaMorpheus";
                break;

            case BottomUpResultType.MetaMorpheusNoChimeras:
                psmPath = dataset._psmResultNoChimerasPath;
                peptidePath = dataset._peptideResultNoChimerasPath;
                proteinPath = dataset._proteinGroupsNoChimerasResultPath;
                condition = "MetaMorpheusNoChimeras";
                break;

            case BottomUpResultType.MetaMorpheusFraggerLike:
                var dirPath = dataset._metaMorpheusFraggerEquivalentDirectoryPath;
                psmPath = Path.Combine(dirPath, "AllPSMs.psmtsv");
                peptidePath = Path.Combine(dirPath, "AllPeptides.psmtsv");
                proteinPath = Path.Combine(dirPath, "AllProteinGroups.psmtsv");
                condition = "MetaMorpheusFraggerLike";
                break;

            case BottomUpResultType.MetaMorpheusFraggerLikeNoChimeras:
                var dirPathNoChimeras = dataset._metaMorpheusFraggerEquivalentNoChimerasDirectoryPath;
                psmPath = Path.Combine(dirPathNoChimeras, "AllPSMs.psmtsv");
                peptidePath = Path.Combine(dirPathNoChimeras, "AllPeptides.psmtsv");
                proteinPath = Path.Combine(dirPathNoChimeras, "AllProteinGroups.psmtsv");
                condition = "MetaMorpheusFraggerLikeNoChimeras";
                break;

            case BottomUpResultType.MsFraggerDDAPlus:
                psmPath = dataset._combinedMsFraggerDDAPlusPSMResultsPath;
                peptidePath = dataset._combinedMsFraggerDDAPlusPeptideResultsPath;
                proteinPath = dataset._combinedMsFraggerDDAPlusProteinResultsPath;
                condition = "MsFraggerDDAPlus";
                break;

            case BottomUpResultType.MsFragger:
                psmPath = dataset._combinedMsFraggerPSMResultsPath;
                peptidePath = dataset._combinedMsFraggerPeptideResultsPath;
                proteinPath = dataset._combinedMsFraggerProteinResultsPath;
                condition = "MsFragger";
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(resultType), resultType, null);
        }


        int psmCount, onePercentPsmCount, onePercentUnambiguousPsmCount, peptideCount, 
            onePercentPeptideCount, onePercentUnambiguousPeptideCount, proteinGroupCount, 
            onePercentProteinGroupCount;
        if (resultType.ToString().Contains("Fragger") && !resultType.ToString().Contains("Meta"))
        {
            var psms = new MsFraggerPsmFile() { FilePath = psmPath };
            psms.LoadResults();
            psmCount = psms.Results.Count;
            var filtered = psms.Results.Where(p => p.PeptideProphetProbability >= 0.99).ToList();
            onePercentPsmCount = filtered.Count;

            using (var sr = new StreamReader(peptidePath))
            {
                var header = sr.ReadLine();
                var headerSplit = header.Split('\t');
                var qValueIndex = Array.IndexOf(headerSplit, "Probability");
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
                peptideCount = count;
                onePercentPeptideCount = onePercentCount;
            }

            using (var sr = new StreamReader(proteinPath))
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

                proteinGroupCount = count;
                onePercentProteinGroupCount = onePercentCount;
            }

            var bulkResult = new BulkResultCountComparison()
            {
                DatasetName = datasetName,
                Condition = condition,
                PsmCount = psmCount,
                OnePercentPsmCount = onePercentPsmCount,
                PeptideCount = peptideCount,
                OnePercentPeptideCount = onePercentPeptideCount,
                ProteinGroupCount = proteinGroupCount,
                OnePercentProteinGroupCount = onePercentProteinGroupCount
            };
            return bulkResult;
        }
        else // MetaMorpheus Results
        {
            
            using (var sw = new StreamReader(File.OpenRead(dataset._proteinGroupsResultPath)))
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
                    count++;
                    if (double.Parse(values[qValueIndex]) <= 0.01)
                        onePercentCount++;
                }
                proteinGroupCount = count;
                onePercentProteinGroupCount = onePercentCount;
            }

            var spectralmatches = PsmTsvReader.ReadTsv(psmPath, out _);
            spectralmatches = spectralmatches.Where(p => p.DecoyContamTarget == "T").ToList();
            psmCount = spectralmatches.Count;
            var filtered = spectralmatches.Where(p => p.PEP_QValue <= 0.01).ToList();
            onePercentPsmCount = filtered.Count;
            onePercentUnambiguousPsmCount = filtered.Count(p => p.AmbiguityLevel == "1");

            var peptideResults = PsmTsvReader.ReadTsv(peptidePath, out _);
            peptideResults = peptideResults.Where(p => p.DecoyContamTarget == "T").ToList();
            peptideCount = peptideResults.Count;
            var filteredPeptides = peptideResults.Where(p => p.PEP_QValue <= 0.01).ToList();
            onePercentPeptideCount = filteredPeptides.Count;
            onePercentUnambiguousPeptideCount = filteredPeptides.Count(p => p.AmbiguityLevel == "1");

            var bulkResult = new BulkResultCountComparison()
            {
                DatasetName = datasetName,
                Condition = condition,
                PsmCount = psmCount,
                OnePercentPsmCount = onePercentPsmCount,
                OnePercentUnambiguousPsmCount = onePercentUnambiguousPsmCount,
                PeptideCount = peptideCount,
                OnePercentPeptideCount = onePercentPeptideCount,
                OnePercentUnambiguousPeptideCount = onePercentUnambiguousPeptideCount,
                ProteinGroupCount = proteinGroupCount,
                OnePercentProteinGroupCount = onePercentProteinGroupCount
            };
            return bulkResult;
        }
    }
    
    public static List<BulkResultCountComparison> GetMMResultsForInternalComparison(List<Dataset> datasets)
    {
        int multiplier = 2;
        int[] psms = new int[datasets.Count * multiplier];
        int[] peptides = new int[datasets.Count * multiplier];
        int[] proteinGroup = new int[datasets.Count * multiplier];
        int[] onePercentPsms = new int[datasets.Count * multiplier];
        int[] onePercentPeptides = new int[datasets.Count * multiplier];
        int[] onePercentProteinGroups = new int[datasets.Count * multiplier];
        int[] onePercentUnambiguousPsms = new int[datasets.Count * multiplier];
        int[] onePercentUnambiguousPeptides = new int[datasets.Count * multiplier];
        string[] datasetNames = new string[datasets.Count * multiplier];
        string[] conditions = new string[datasets.Count * multiplier];
        

        // collect data
        int i = 0;
        foreach (var dataset in datasets)
        {
            datasetNames[i] = dataset.DatasetName;
            datasetNames[i + 1] = dataset.DatasetName;
            conditions[i] = "Chimeras";
            conditions[i + 1] = "No Chimeras";

            using (var sw = new StreamReader(File.OpenRead(dataset._proteinGroupsResultPath)))
            {
                var header = sw.ReadLine();
                var headerSplit = header.Split('\t');
                var qValueIndex = Array.IndexOf(headerSplit, "Protein QValue");
                int proteinGroupCount = 0;
                int onePercentProteinGroupCount = 0;

                while (!sw.EndOfStream)
                {
                    var line = sw.ReadLine();
                    var values = line.Split('\t');
                    proteinGroupCount++;
                    if (double.Parse(values[qValueIndex]) <= 0.01)
                        onePercentProteinGroupCount++;
                }
                proteinGroup[i] = proteinGroupCount;
                onePercentProteinGroups[i] = onePercentProteinGroupCount;
            }

            using (var sw = new StreamReader(File.OpenRead(dataset._proteinGroupsNoChimerasResultPath)))
            {
                var header = sw.ReadLine();
                var headerSplit = header.Split('\t');
                var qValueIndex = Array.IndexOf(headerSplit, "Protein QValue");
                int proteinGroupCount = 0;
                int onePercentProteinGroupCount = 0;

                while (!sw.EndOfStream)
                {
                    var line = sw.ReadLine();
                    var values = line.Split('\t');
                    proteinGroupCount++;
                    if (double.Parse(values[qValueIndex]) <= 0.01)
                        onePercentProteinGroupCount++;
                }
                proteinGroup[i + 1] = proteinGroupCount;
                onePercentProteinGroups[i + 1] = onePercentProteinGroupCount;
            }

            var spectralmatches = PsmTsvReader.ReadTsv(dataset._psmResultPath, out _);
            spectralmatches = spectralmatches.Where(p => p.DecoyContamTarget == "T").ToList();
            psms[i] = spectralmatches.Count;
            var filtered = spectralmatches.Where(p => p.PEP_QValue <= 0.01).ToList();
            onePercentPsms[i] = filtered.Count;
            onePercentUnambiguousPsms[i] = filtered.Count(p => p.AmbiguityLevel == "1");

            spectralmatches = PsmTsvReader.ReadTsv(dataset._psmResultNoChimerasPath, out _);
            spectralmatches = spectralmatches.Where(p => p.DecoyContamTarget == "T").ToList();
            psms[i + 1] = spectralmatches.Count;
            filtered = spectralmatches.Where(p => p.PEP_QValue <= 0.01).ToList();
            onePercentPsms[i + 1] = filtered.Count;
            onePercentUnambiguousPsms[i + 1] = filtered.Count(p => p.AmbiguityLevel == "1");

            var peptideResults = PsmTsvReader.ReadTsv(dataset._peptideResultPath, out _);
            peptideResults = peptideResults.Where(p => p.DecoyContamTarget == "T").ToList();
            peptides[i] = peptideResults.Count;
            var filteredPeptides = peptideResults.Where(p => p.PEP_QValue <= 0.01).ToList();
            onePercentPeptides[i] = filteredPeptides.Count;
            onePercentUnambiguousPeptides[i] = filteredPeptides.Count(p => p.AmbiguityLevel == "1");

            peptideResults = PsmTsvReader.ReadTsv(dataset._peptideResultNoChimerasPath, out _);
            peptideResults = peptideResults.Where(p => p.DecoyContamTarget == "T").ToList();
            peptides[i + 1] = peptideResults.Count;
            filteredPeptides = peptideResults.Where(p => p.PEP_QValue <= 0.01).ToList();
            onePercentPeptides[i + 1] = filteredPeptides.Count;
            onePercentUnambiguousPeptides[i + 1] = filteredPeptides.Count(p => p.AmbiguityLevel == "1");

            i += 2;
        }

        var results = new List<BulkResultCountComparison>();
        for (i = 0; i < psms.Length; i++)
        {
            results.Add(new BulkResultCountComparison()
            {
                DatasetName = datasetNames[i],
                Condition = conditions[i],
                PsmCount = psms[i],
                OnePercentPsmCount = onePercentPsms[i],
                OnePercentUnambiguousPsmCount = onePercentUnambiguousPsms[i],
                PeptideCount = peptides[i],
                OnePercentPeptideCount = onePercentPeptides[i],
                OnePercentUnambiguousPeptideCount = onePercentUnambiguousPeptides[i],
                ProteinGroupCount = proteinGroup[i],
                OnePercentProteinGroupCount = onePercentProteinGroups[i]
            });
        }

        return results;

        

        //// output data
        //var outpath = Path.Combine(BURunner.DirectoryPath, FileIdentifiers.InternalChimeraComparison);
        //using (StreamWriter output = new StreamWriter(outpath))
        //{
        //    output.WriteLine("Dataset,Condition,PSMs,1% PSMs,1% Unambiguous PSMs,Peptides,1% Peptides,1% Unambiguous Peptides,Protein Groups,1% ProteinGroups");
        //    for (i = 0; i < psms.Length; i++)
        //    {
        //        output.WriteLine($"{datasetNames[i]},{conditions[i]},{psms[i]},{onePercentPsms[i]},{onePercentUnambiguousPsms[i]},{peptides[i]},{onePercentPeptides[i]},{onePercentUnambiguousPeptides[i]},{proteinGroup[i]},{onePercentProteinGroups[i]}");
        //    }
        //}
    }


    public static void MergeChimeraCountingData(List<Dataset> datasets)
    {
        string outpath = Path.Combine(BURunner.DirectoryPath, $"BulkComparison_{FileIdentifiers.ChimeraCountingFile}");
        
        var results = new List<ChimeraCountingResult>();
        foreach (var dataset in datasets)
        {
            results.AddRange(dataset.ChimeraCountingFile.Results);
            results.AddRange(dataset.FraggerChimeraCountingFile);
            results.AddRange(dataset.ChimeraCountingMetaMorpheusFraggerEquivalentFile);
        }

        var file = new ChimeraCountingFile() {FilePath = outpath, Results = results};
        file.WriteResults(outpath);
    }
}