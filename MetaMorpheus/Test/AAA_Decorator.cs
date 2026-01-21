using Chromatography.RetentionTimePrediction;
using Chromatography.RetentionTimePrediction.Chronologer;
using Easy.Common.Extensions;
using EngineLayer;
using MathNet.Numerics.Statistics;
using NUnit.Framework;
using Proteomics.ProteolyticDigestion;
using Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskLayer;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Analysis.Collectors;

namespace Test;

[TestFixture]
public class AAA_Decorator
{
    static string ProteomeDir = @"B:\Users\Nic\BacterialProteomics\Uniprot\Bacteria_Reviewed";
    private record RunInfo(string SearchDir, string proteomeDir, string TOMLPath);

    static RunInfo BigRun_EV = new RunInfo(
        @"D:\Projects\BacterialProteomics\BigRun_EV\Task1-ManySearchTask",
        ProteomeDir,
        @"D:\Projects\BacterialProteomics\BigRun_EV\Task Settings\Task1-ManySearchTaskconfig.toml"
    );

    static RunInfo BigRun_NotEV = new RunInfo(
        @"D:\Projects\BacterialProteomics\BigRun_notEV\Task1-ManySearchTask",
        ProteomeDir,
        @"D:\Projects\BacterialProteomics\BigRun_notEV\Task Settings\Task1-ManySearchTaskconfig.toml"
    );

    static RunInfo VaginalSpike_Control = new RunInfo(
        @"D:\Projects\BacterialProteomics\VaginalSpike_BacterialControls\Task1-ManySearchTask",
        ProteomeDir,
        @"D:\Projects\BacterialProteomics\VaginalSpike_BacterialControls\Task Settings\Task1-ManySearchTaskconfig.toml");


    static RunInfo VaginalSpike_Ascites = new RunInfo(
        @"D:\Projects\BacterialProteomics\VaginalSpike_AllAscitesAllProteomes\Task1-ManySearchTask",
        ProteomeDir,
        @"D:\Projects\BacterialProteomics\VaginalSpike_AllAscitesAllProteomes\Task Settings\Task1-ManySearchTaskconfig.toml");

    [Test]
    public void TestMethod()
    {
        RunInfo info = VaginalSpike_Control;

        //var decorator = new PostHocDecorator(info.SearchDir, info.proteomeDir, info.TOMLPath);

        //decorator.DecorateAndWrite();
    }

    [Test]
    public void FixPsmOutput()
    {
        bool makeBackup = false;
        string rootDir = @"D:\Projects\BacterialProteomics\CovidSpikedIn_Bulk_FixPepWriterAndFragmentLengthNormalization\Task1-ParallelSearchTask";

        var fixedFiles = PsmFixer.TraverseAndFix(rootDir, makeBackup);


        rootDir = @"D:\Projects\BacterialProteomics\CovidSpikedIn_Bulk\Task1-ParallelSearchTask";
        fixedFiles = PsmFixer.TraverseAndFix(rootDir, makeBackup);
    }

    [Test]
    public void AddRtInformation()
    {
        bool makeBackup = false;
        string rootDir = @"D:\Projects\BacterialProteomics\CovidSpikedIn_Bulk_FixPepWriterAndFragmentLengthNormalization\Task1-ParallelSearchTask";
        //string rootDir = @"D:\Projects\BacterialProteomics\VaginalSpike_AllAscitesAllProteomes\Task1-ManySearchTask";

        var decorator = new RetentionTimeDecorator(rootDir, false);
        decorator.Decorate();


        //rootDir = @"D:\Projects\BacterialProteomics\CovidSpikedIn_Bulk\Task1-ParallelSearchTask";
        //fixedFiles = PsmFixer.TraverseAndFix(rootDir, makeBackup);
    }
}



public static class PsmFixer
{
    public static List<string> TraverseAndFix(string rootDir, bool makeBackup)
    {
        var fixedFiles = new List<string>();

        foreach (var file in Directory.EnumerateFiles(
                     rootDir,
                     "*.psmtsv",
                     SearchOption.AllDirectories))
        {
            if (file.Contains("- Copy"))
                continue;

            try
            {
                if (DetectAndFixPsmTsv(file, makeBackup))
                    fixedFiles.Add(file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {file}: {ex.Message}");
            }
        }

        return fixedFiles;
    }

    public static bool DetectAndFixPsmTsv(string path, bool makeBackup)
    {
        using var reader = new StreamReader(path);

        string? headerLine = reader.ReadLine();
        if (headerLine == null)
            return false;

        var headerCols = headerLine.Split('\t');
        int expectedCols = headerCols.Length;
        int organismColIndex = Array.IndexOf(headerCols, SpectrumMatchFromTsvHeader.OrganismName);

        var outputLines = new List<string> { headerLine };
        bool modified = false;

        string? line;
        int lineNumber = 1;

        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            var cols = line.Split('\t');

            if (cols.Length != expectedCols)
            {
                modified = true;

                if (cols.Length > expectedCols)
                {
                    if (organismColIndex >= 0 && cols[organismColIndex+1].IsNullOrEmpty() && cols[organismColIndex+2].IsNullOrEmpty())
                    {
                        // Shift left to remove two empty columns after OrganismName
                        for (int i = organismColIndex + 1; i < cols.Length - 2; i++)
                        {
                            cols[i] = cols[i + 2];
                        }
                        // Resize to expected columns
                        Array.Resize(ref cols, expectedCols);
                    }
                    else
                        cols = cols.Take(expectedCols).ToArray();
                }
                else
                {
                    Array.Resize(ref cols, expectedCols);
                }
            }

            outputLines.Add(string.Join('\t', cols));
        }

        if (!modified)
            return false;

        reader.Dispose();

        if (makeBackup)
        {
            string backupPath = path + ".bak";
            if (!File.Exists(backupPath))
                File.Move(path, backupPath);
        }

        File.WriteAllLines(path, outputLines);
        return true;
    }
}

public abstract class Decorator(string directoryPath, bool reRunStats)
{
    public void Decorate()
    {
        string statsOutputPath = Path.Combine(directoryPath, "StatisticalAnalysis_Results.csv");
        var resultManager = TaskLayer.ParallelSearch.ParallelSearchTask.CreateResultsManager(directoryPath, true);

        Parallel.ForEach(resultManager.AllAnalysisResults.Values, result =>
        {
            var innerDirPath = Path.Combine(directoryPath, result.DatabaseName);
            Decorate(result, innerDirPath);
        });

        resultManager.WriteSearchSummaryCacheResults(resultManager.SearchSummaryFilePath);

        if (reRunStats)
        {
            var statResults = resultManager.FinalizeStatisticalAnalysis();
            resultManager.WriteStatisticalResults(statResults, statsOutputPath);
        }
    }

    protected abstract void Decorate(TransientDatabaseMetrics result, string innerResultPath);
}

public class RetentionTimeDecorator(string directoryPath, bool reRunStats) : Decorator(directoryPath, reRunStats)
{
    private static RetentionTimePredictor _predictor = new ChronologerRetentionTimePredictor();
    private Dictionary<string, double> _predictionCache = new();

    protected override void Decorate(TransientDatabaseMetrics result, string innerResultPath)
    {
        const double qValueThreshold = 0.05; // TODO: Update when needed

        List<double> allPsmRtErrors = new();
        List<double> psmObservedRts = new();
        List<double> psmPredictedRts = new();
        var psmFilepath = Directory.GetFiles(innerResultPath, "*PSMs.psmtsv").FirstOrDefault();
        if (psmFilepath is not null)
        {
            // Perform decoration using the PSM file
            var psms = SpectrumMatchTsvReader.ReadPsmTsv(psmFilepath, out var warnings);
            if (warnings.Count > 0)
                Console.WriteLine($"Warnings while reading PSM TSV for {result.DatabaseName}:");

            foreach (var psm in psms.Where(p => p is { IsDecoy: false, QValue: <= qValueThreshold }))
            {
                double observedRt = psm.RetentionTime;
                foreach (var fullSeq in psm.FullSequence.Split('|'))
                {
                    if (!_predictionCache.TryGetValue(fullSeq, out double predictedRt))
                    {
                        double? predicted = _predictor.PredictRetentionTime(new PeptideWithSetModifications(fullSeq, GlobalVariables.AllModsKnownDictionary), out var failureReason);

                        if (predicted is null)
                            continue;

                        predictedRt = predicted.Value;
                        _predictionCache[fullSeq] = predictedRt;
                    }


                    if (predictedRt > 0) // Only include valid predictions
                    {
                        double rtError = observedRt - predictedRt;
                        allPsmRtErrors.Add(rtError);
                        psmObservedRts.Add(observedRt);
                        psmPredictedRts.Add(predictedRt);
                    }

                    break; // Only use first hypothesis for RT prediction
                }
            }
        }
        List<double> allPeptideRtErrors = new();
        List<double> peptideObservedRts = new();
        List<double> peptidePredictedRts = new();
        var peptideFilePath = Directory.GetFiles(innerResultPath, "*Peptides.psmtsv").FirstOrDefault();
        if (peptideFilePath is not null)
        {
            // Perform decoration using the peptide file
            var peptides = SpectrumMatchTsvReader.ReadPsmTsv(peptideFilePath, out var warnings);
            if (warnings.Count > 0)
                Console.WriteLine($"Warnings while reading PSM TSV for {result.DatabaseName}:");

            foreach (var peptide in peptides.Where(p => p is { IsDecoy: false, QValue: <= qValueThreshold }))
            {
                double observedRt = peptide.RetentionTime;
                foreach (var fullSeq in peptide.FullSequence.Split('|'))
                {
                    if (!_predictionCache.TryGetValue(fullSeq, out double predictedRt))
                    {
                        double? predicted = _predictor.PredictRetentionTime(new PeptideWithSetModifications(fullSeq, GlobalVariables.AllRnaModsKnownDictionary), out var failureReason);

                        if (predicted is null)
                            continue;

                        predictedRt = predicted.Value;
                        _predictionCache[fullSeq] = predictedRt;
                    }


                    if (predictedRt > 0) // Only include valid predictions
                    {
                        double rtError = observedRt - predictedRt;
                        allPeptideRtErrors.Add(rtError);
                        peptideObservedRts.Add(observedRt);
                        peptidePredictedRts.Add(predictedRt);
                    }

                    break; // Only use first hypothesis for RT prediction
                }
            }
        }

        // Calculate statistics
        double psmMeanAbsoluteError = allPsmRtErrors.Any() ? allPsmRtErrors.Select(Math.Abs).Mean() : 0;
        double psmCorrelation = psmObservedRts.Count > 1 ? Correlation.Pearson(psmObservedRts, psmPredictedRts) : 0;

        double peptideMeanAbsoluteError = allPeptideRtErrors.Any() ? allPeptideRtErrors.Select(Math.Abs).Mean() : 0;
        double peptideCorrelation = peptideObservedRts.Count > 1 ? Correlation.Pearson(peptideObservedRts, peptidePredictedRts) : 0;

        result.Results[RetentionTimeCollector.PsmMeanAbsoluteRtError] = psmMeanAbsoluteError;
        result.Results[RetentionTimeCollector.PsmRtCorrelationCoefficient] = psmCorrelation;
        result.Results[RetentionTimeCollector.PsmAllRtErrors] = allPsmRtErrors.ToArray();
        result.Results[RetentionTimeCollector.PeptideMeanAbsoluteRtError] = peptideMeanAbsoluteError;
        result.Results[RetentionTimeCollector.PeptideRtCorrelationCoefficient] = peptideCorrelation;
        result.Results[RetentionTimeCollector.PeptideAllRtErrors] = allPeptideRtErrors.ToArray();
        result.PopulatePropertiesFromResults();
    }
}