#nullable enable
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using TaskLayer.ParallelSearch.Statistics;

namespace TaskLayer.ParallelSearch.IO;

public class TestSummaryResultFile : ParallelSearchResultFile<TestSummary>
{
    public static CsvConfiguration CsvConfiguration => new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        Encoding = Encoding.UTF8,
        HasHeaderRecord = true,
        Delimiter = ","
    };

    public TestSummaryResultFile(string filePath) : base(filePath) { }

    /// <summary>
    /// Constructor used to initialize from the factory method
    /// </summary>
    public TestSummaryResultFile() : base() { }

    public override void LoadResults()
    {
        using var csv = new CsvReader(new StreamReader(FilePath), CsvConfiguration);
        Results = csv.GetRecords<TestSummary>().ToList();
    }

    public override void WriteResults(string outputPath)
    {
        using var csv = new CsvWriter(new StreamWriter(File.Create(outputPath)), CsvConfiguration);

        csv.WriteHeader<TestSummary>();
        foreach (var result in Results
                      .OrderBy(p => p.EvidenceFamily)
                     .ThenBy(p => p.IsFamilySummary ? 0 : 1)
                     .ThenByDescending(p => p.ValidDatabases)
                     .ThenByDescending(p => p.PercentSignificantByP)
                     .ThenBy(p => p.TestName)
                     .ThenBy(p => p.MetricName))
        {
            csv.NextRecord();
            csv.WriteRecord(result);
        }
    }
}
