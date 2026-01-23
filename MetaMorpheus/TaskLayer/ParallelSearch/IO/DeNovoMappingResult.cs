
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TaskLayer.ParallelSearch.Util.Converters;

namespace TaskLayer.ParallelSearch.IO;
public class DeNovoMappingResult
{
    // Database Information 
    public string DatabaseIdentifier { get; set; } = string.Empty;
    public int DatabaseProteinCount { get; set; } = -1;
    public int DatabasePeptideCount { get; set; } = -1;

    // Raw Casanovo Prediction Metrics
    public int TotalPredictions { get; set; } 
    public int TargetPredictions { get; set; }
    public int DecoyPredictions { get; set; }
    public int UniquePeptidesMapped { get; set; } 
    public int UniqueProteinsMapped { get; set; }
    public double MeanRtError { get; set; } = double.NaN;
    public double MedianRtError { get; set; } = double.NaN;
    public double StdDevRtError { get; set; } = double.NaN;
    public double MeanPredictionScore { get; set; } = double.NaN;
    public double MedianPredictionScore { get; set; } = double.NaN;
    public double StdDevPredictionScore { get; set; } = double.NaN;


    [TypeConverter(typeof(CommaDelimitedToDoubleConcurrentBagTypeConverter))]
    public ConcurrentBag<double> RetentionTimeErrors { get; set; } = new();

    [TypeConverter(typeof(CommaDelimitedToDoubleConcurrentBagTypeConverter))]
    public ConcurrentBag<double> PredictionScores { get; set; } = new();

    [TypeConverter(typeof(CommaDelimitedToDoubleConcurrentBagTypeConverter))]
    public ConcurrentBag<double> TargetPredictionScores { get; set; } = new();

    [TypeConverter(typeof(CommaDelimitedToDoubleConcurrentBagTypeConverter))]
    public ConcurrentBag<double> DecoyPredictionScores { get; set; } = new();

    [Ignore] 
    public Dictionary<string, int> PredictionCountsByFile { get; set; } = new();

    public void FinalizeValues()
    {
        // Finalize RT Error statistics
        if (RetentionTimeErrors.Count > 0)
        {
            var rtErrorsList = RetentionTimeErrors.ToList();
            MeanRtError = rtErrorsList.Average();
            MedianRtError = rtErrorsList.OrderBy(x => x).ElementAt(rtErrorsList.Count / 2);
            StdDevRtError = Math.Sqrt(rtErrorsList.Select(x => Math.Pow(x - MeanRtError, 2)).Average());
        }

        // Finalize Prediction Score statistics
        TotalPredictions = TargetPredictions + DecoyPredictions;
        PredictionScores.Clear();
        foreach (var score in TargetPredictionScores)
            PredictionScores.Add(score);
        foreach (var score in DecoyPredictionScores)
            PredictionScores.Add(score);

        if (PredictionScores.Count > 0)
        {
            var predictionScoresList = PredictionScores.ToList();
            MeanPredictionScore = predictionScoresList.Average();
            MedianPredictionScore = predictionScoresList.OrderBy(x => x).ElementAt(predictionScoresList.Count / 2);
            StdDevPredictionScore = Math.Sqrt(predictionScoresList.Select(x => Math.Pow(x - MeanPredictionScore, 2)).Average());
        }
    }
}

public class DeNovoMappingResultFile : ParallelSearchResultFile<DeNovoMappingResult>
{
    public string DefaultFileName => "MappingSummary.tsv";
    public static CsvConfiguration CsvConfiguration => new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        Encoding = Encoding.UTF8,
        HasHeaderRecord = true,
        Delimiter = "\t"
    };

    public DeNovoMappingResultFile(string filePath) : base(filePath) { }

    /// <summary>
    /// Constructor used to initialize from the factory method
    /// </summary>
    public DeNovoMappingResultFile() : base() { }

    public override void LoadResults()
    {
        using var csv = new CsvReader(new StreamReader(FilePath), CsvConfiguration);
        Results = csv.GetRecords<DeNovoMappingResult>().ToList();
    }

    public override void WriteResults(string outputPath)
    {
        using var csv = new CsvWriter(new StreamWriter(File.Create(outputPath)), CsvConfiguration);
        var additionalHeaders = Results
            .SelectMany(r => r.PredictionCountsByFile.Keys)
            .Distinct()
            .OrderBy(k => k)
            .ToList();

        csv.WriteHeader<DeNovoMappingResult>();
        foreach (var header in additionalHeaders)
            csv.WriteField(header);

        foreach (var result in Results.OrderByDescending(p => p.TotalPredictions).ThenByDescending(p => p.TargetPredictions))
        {
            result.FinalizeValues();

            csv.NextRecord();
            csv.WriteRecord(result);

            foreach (var header in additionalHeaders)
            {
                result.PredictionCountsByFile.TryGetValue(header, out int count);
                csv.WriteField(count);
            }
        }
    }
}
