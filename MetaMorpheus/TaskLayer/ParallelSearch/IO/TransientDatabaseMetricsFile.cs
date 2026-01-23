#nullable enable
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.IO;

public class TransientDatabaseMetricsFile : ParallelSearchResultFile<TransientDatabaseMetrics>
{
    public static CsvConfiguration CsvConfiguration => new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        Encoding = Encoding.UTF8,
        HasHeaderRecord = true,
        Delimiter = ","     
    };

    public TransientDatabaseMetricsFile(string filePath) : base(filePath) { }

    public TransientDatabaseMetricsFile() : base() { }

    public override void LoadResults()
    {
        using var reader = new StreamReader(FilePath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null
        });

        var records = csv.GetRecords<TransientDatabaseMetrics>().ToList();

        foreach (var record in records)
        {
            record.PopulateResultsFromProperties();
        }
        
        Results = records;
    }

    public override void WriteResults(string outputPath)
    {
        using var csv = new CsvWriter(new StreamWriter(File.Create(outputPath)), CsvConfiguration);

        csv.WriteHeader<TransientDatabaseMetrics>();
        csv.NextRecord();

        foreach (var result in Results.OrderByDescending(p => p.StatisticalTestsPassed))
        {
            result.PopulatePropertiesFromResults();

            csv.WriteRecord(result);
            csv.NextRecord();
        }
        csv.Flush();
    }
}