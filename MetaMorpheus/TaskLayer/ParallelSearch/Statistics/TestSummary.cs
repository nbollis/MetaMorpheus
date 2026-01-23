#nullable enable
using CsvHelper.Configuration.Attributes;

namespace TaskLayer.ParallelSearch.Statistics;

public class TestSummary
{
    public string TestName { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public int ValidDatabases { get; set; }
    public int SignificantByP { get; set; }
    public int SignificantByQ { get; set; }

    [Ignore]
    public double PercentSignificantByP => ValidDatabases > 0 ? SignificantByP * 100.0 / ValidDatabases : 0;

    [Ignore]
    public double PercentSignificantByQ => ValidDatabases > 0 ? SignificantByQ * 100.0 / ValidDatabases : 0;
}