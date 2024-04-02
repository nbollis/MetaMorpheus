using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EngineLayer;
using Microsoft.VisualStudio.TestPlatform.Common.DataCollection;

namespace Test.ChimeraPaper;

public static class DatasetOperations
{
    public static void MergeMMResultsForInternalComparison(List<Dataset> datasets)
    {
        int[] psms = new int[datasets.Count*2];
        int[] peptides = new int[datasets.Count*2];
        int[] proteinGroup = new int[datasets.Count*2];
        int[] onePercentPsms = new int[datasets.Count*2];
        int[] onePercentPeptides = new int[datasets.Count*2];
        int[] onePercentProteinGroups = new int[datasets.Count*2];
        int[] onePercentUnambiguousPsms = new int[datasets.Count*2];
        int[] onePercentUnambiguousPeptides = new int[datasets.Count*2];
        string[] datasetNames = new string[datasets.Count*2];
        string[] conditions = new string[datasets.Count*2];
        

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

        // output data
        var outpath = Path.Combine(Runner.DirectoryPath, FileIdentifiers.InternalChimeraComparison);
        using (StreamWriter output = new StreamWriter(outpath))
        {
            output.WriteLine("Dataset,Condition,PSMs,1% PSMs,1% Unambiguous PSMs,Peptides,1% Peptides,1% Unambiguous Peptides,Protein Groups,1% ProteinGroups");
            for (i = 0; i < psms.Length; i++)
            {
                output.WriteLine($"{datasetNames[i]},{conditions[i]},{psms[i]},{onePercentPsms[i]},{onePercentUnambiguousPsms[i]},{peptides[i]},{onePercentPeptides[i]},{onePercentUnambiguousPeptides[i]},{proteinGroup[i]},{onePercentProteinGroups[i]}");
            }
        }
    }


    public static void MergeChimeraCountingData(List<Dataset> datasets)
    {
        string outpath = Path.Combine(Runner.DirectoryPath, $"BulkComparison_{FileIdentifiers.ChimeraCountingFile}");
        
        var results = new List<ChimeraCountingResult>();
        foreach (var dataset in datasets)
        {
            results.AddRange(dataset.ChimeraCountingFile.Results);
            results.AddRange(dataset.FraggerChimeraCountingFile);
        }

        var file = new ChimeraCountingFile() {FilePath = outpath, Results = results};
        file.WriteResults(outpath);
    }
}