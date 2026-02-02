using CsvHelper;
using CsvHelper.Configuration;
using Omics.Fragmentation;
using Readers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsvHelper.Configuration.Attributes;
using CsvHelper.TypeConversion;

namespace EngineLayer.FragmentTypeDetection;

/// <summary>
/// Statistics for an individual fragment type
/// </summary>
public class FragmentTypeStatistics
{
    public ProductType FragmentType { get; set; }

    /// <summary>
    /// The number of PSMs at 1% FDR that have matches for this fragment type
    /// </summary>
    public int PsmsWithMatches { get; set; }
    /// <summary>
    /// The percentage of PSMs at 1% FDR that have matches for this fragment type
    /// </summary>
    public double PercentOfPsmsWithMatches { get; set; }

    /// <summary>
    /// The average number of matched ions for this fragment type when it is present in a PSM
    /// </summary>
    public double AverageMatchesWhenPresent { get; set; }

    // TODO: Add more statistics as needed:
    // - Average score contribution
    // - Correlation with high-confidence PSMs
    // - Intensity-weighted metrics

    /// <summary>
    /// The total intensity contribution of this fragment type across all PSMs
    /// </summary>
    public double TotalIntensityContribution { get; set; }

    /// <summary>
    /// The percentage of the total spectral intensity contributed by this fragment type
    /// </summary>
    public double PercentSpectralIntensityContribution { get; set; }

    /// <summary>
    /// The percentage of the total identification intensity contributed by this fragment type
    /// </summary>
    public double PercentIdentificationIntensityContribution { get; set; }

    /// <summary>
    /// E(type >= k) = number of decoy hits of type >= k / number of target hits of type >= k
    /// KEY: k is the number of ions of this fragment type matched in the PSM 
    /// VALUE: E(type >= k)
    /// </summary>
    [TypeConverter(typeof(DictionaryToDecoyHitRateConverter))]
    public Dictionary<int, double> DecoyHitRate { get; set; } = new();

    [TypeConverter(typeof(DictionaryToDecoyHitRateConverter))]
    public Dictionary<int, double> TargetHitRate { get; set; } = new();
}

public class FragmentTypeStatisticsResultFile : ResultFile<FragmentTypeStatistics>, IResultFile
{
    public static CsvConfiguration CsvConfiguration => new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
    {
        Delimiter = "\t",
        HasHeaderRecord = true,
    };

    public FragmentTypeStatisticsResultFile() : base() {}

    public FragmentTypeStatisticsResultFile(string filePath) : base(filePath, Software.MetaMorpheus) { }

    public override void LoadResults()
    {
        if (!System.IO.File.Exists(FilePath))
            throw new System.IO.FileNotFoundException($"The file {FilePath} does not exist.");

        using var csv = new CsvReader(new StreamReader(FilePath), CsvConfiguration);
        Results = csv.GetRecords<FragmentTypeStatistics>().ToList();
    }

    public override void WriteResults(string outputPath)
    {
        using var csv = new CsvWriter(new StreamWriter(File.Create(outputPath)), CsvConfiguration);

        csv.WriteHeader<FragmentTypeStatistics>();
        foreach (var result in Results)
        {
            csv.NextRecord();
            csv.WriteRecord(result);
        }
    }

    public override SupportedFileType FileType { get; }
    public override Software Software { get; set; } = Software.MetaMorpheus;
}

public class DictionaryToDecoyHitRateConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        var dict = new Dictionary<int, double>();
        var entries = text.Split(';');
        foreach (var entry in entries)
        {
            var kvp = entry.Split(':');
            if (kvp.Length == 2 &&
                int.TryParse(kvp[0], out int key) &&
                double.TryParse(kvp[1], out double value))
            {
                dict[key] = value;
            }
        }
        return dict;
    }
    public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
    {
        if (value is Dictionary<int, double> dict)
        {
            return string.Join(";", dict.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
        }
        return base.ConvertToString(value, row, memberMapData);
    }
}