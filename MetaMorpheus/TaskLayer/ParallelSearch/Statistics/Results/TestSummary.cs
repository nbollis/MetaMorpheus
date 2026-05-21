#nullable enable
using CsvHelper.Configuration.Attributes;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Per-test summary statistics counting how many databases had valid (defined)
/// results, how many were undefined, and how many were significant by p-value
/// or by q-value. Also used for synthetic family-summary rows to roll up
/// evidence-family-level counts.
/// </summary>
public class TestSummary
{
    public string TestName { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public StatisticalEvidenceFamily? EvidenceFamily { get; set; }
    public bool IsFamilySummary { get; set; }
    public int ValidDatabases { get; set; }
    public int UndefinedDatabases { get; set; }
    public int SignificantByP { get; set; }
    public int SignificantByQ { get; set; }

    [Ignore]
    public double PercentSignificantByP => ValidDatabases > 0 ? SignificantByP * 100.0 / ValidDatabases : 0;

    [Ignore]
    public double PercentSignificantByQ => ValidDatabases > 0 ? SignificantByQ * 100.0 / ValidDatabases : 0;
}
