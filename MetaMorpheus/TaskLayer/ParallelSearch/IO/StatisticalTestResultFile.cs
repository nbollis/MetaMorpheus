#nullable enable
using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TaskLayer.ParallelSearch.Statistics;
using TaskLayer.ParallelSearch.Util;

namespace TaskLayer.ParallelSearch.IO;

public class StatisticalTestResultFile : ParallelSearchResultFile<StatisticalTestResult>
{
    public double Alpha { get; set; }

    public StatisticalTestResultFile(string filePath, double alpha = 0.05) : base(filePath) 
    {
        Alpha = alpha;
    }

    /// <summary>
    /// Constructor used to initialize from the factory method
    /// </summary>
    public StatisticalTestResultFile(double alpha = 0.05) : base() 
    { 
        Alpha = alpha;
    }

    public override void LoadResults()
    {
        using var reader = new StreamReader(FilePath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null
        });

        // Read header to get all column names
        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? throw new InvalidOperationException("CSV file has no header");

        var results = new List<StatisticalTestResult>();

        // Find columns with p-values and q-values
        var testColumns = new HashSet<string>();
        foreach (var header in headers)
        {
            if (header.StartsWith("pValue_"))
            {
                // Extract test name from pValue_TestName
                var testName = header.Substring("pValue_".Length);
                testColumns.Add(testName);
            }
        }

        // Read each row
        while (csv.Read())
        {
            var databaseName = csv.GetField<string>("DatabaseName");

            if (string.IsNullOrWhiteSpace(databaseName))
                continue;

            // Read each test result
            foreach (var testName in testColumns)
            {
                var pValueField = $"pValue_{testName}";
                var qValueField = $"qValue_{testName}";
                var isSignificantField = $"isSignificant_{testName}";
                var testStatField = $"testStatistic_{testName}";

                // Read values safely
                var pValueStr = csv.GetField(pValueField);
                var qValueStr = csv.GetField(qValueField);
                var statStr = csv.GetField(testStatField);

                if (string.IsNullOrWhiteSpace(pValueStr) || string.IsNullOrWhiteSpace(qValueStr))
                    continue;

                if (!double.TryParse(pValueStr, out var pValue) ||
                    !double.TryParse(qValueStr, out var qValue))
                    continue;

                double stat = double.NaN;
                double.TryParse(statStr, out stat);

                var result = new StatisticalTestResult
                {
                    DatabaseName = databaseName,
                    TestName = testName,
                    MetricName = ExtractMetricName(testName),
                    PValue = pValue,
                    QValue = qValue,
                    TestStatistic = stat,
                    AdditionalMetrics = new Dictionary<string, object>()
                };

                results.Add(result);
            }
        }

        Results = results;
    }

    /// <summary>
    /// Extract the metric name from test name
    /// e.g., "FisherExact_Peptide" -> "Peptide"
    /// </summary>
    private string ExtractMetricName(string testName)
    {
        var parts = testName.Split('_');
        return parts.Length > 1 ? string.Join("_", parts.Skip(1)) : testName;
    }

    public override void WriteResults(string outputPath)
    {
        // Group Results by database
        var resultsByDatabase = Results
            .GroupBy(r => r.DatabaseName)
            .OrderBy(g => g.Key)
            .ToList();

        // Get all unique test-metric combinations for column headers (excluding Combined)
        var testMetricCombos = Results
            .Where(r => r.TestName != "Combined")
            .Select(r => (r.TestName, r.MetricName))
            .Distinct()
            .OrderBy(x => x.MetricName)
            .ThenBy(x => x.TestName)
            .ToList();

        // Check if there's a Combined test
        bool hasCombined = Results.Any(r => r.TestName == "Combined");

        using (var writer = new StreamWriter(outputPath))
        {
            // Write header
            var header = new StringBuilder("DatabaseName,StatisticalTestsPassed,StatisticalTestsRun,TestPassedRatio");

            // Add taxonomy columns
            header.Append(",Organism,Kingdom,Phylum,Class,Order,Family,Genus,Species,ProteinCount");

            // Add Combined test columns first (if present)
            if (hasCombined)
            {
                header.Append(",pValue_Combined_All,qValue_Combined_All,isSignificant_Combined_All");
                if (Results.Any(r => r.TestName == "Combined" && r.TestStatistic.HasValue))
                {
                    header.Append(",testStatistic_Combined_All");
                }
            }

            // Add columns for individual test-metric combinations
            foreach (var (testName, metricName) in testMetricCombos)
            {
                header.Append($",pValue_{testName}_{metricName},qValue_{testName}_{metricName},isSignificant_{testName}_{metricName}");
                if (Results.Any(r => r.TestName == testName && r.MetricName == metricName && r.TestStatistic.HasValue))
                {
                    header.Append($",testStatistic_{testName}_{metricName}");
                }
            }

            writer.WriteLine(header.ToString());

            // Write data rows
            foreach (var dbGroup in resultsByDatabase.OrderByDescending(p => p.Count(t => t.IsSignificant(Alpha))))
            {
                string databaseName = dbGroup.Key;
                var dbResults = dbGroup.ToList();


                int testsRun = dbResults.Count(p => !double.IsNaN(p.PValue));
                int testsPassed = dbResults.Count(r => r.IsSignificant(Alpha));
                double testPassedRatio = testsRun > 0 ? testsPassed / (double)testsRun : 0.0;

                // Count tests passed (excluding Combined)
                var row = new StringBuilder(databaseName);
                row.Append($",{testsPassed},{testsRun},{testPassedRatio}");

                // Add taxonomy information
                var taxInfo = TaxonomyMapping.GetTaxonomyInfo(databaseName);
                if (taxInfo != null)
                {
                    row.Append(',').Append(EscapeCsv(taxInfo.Organism));
                    row.Append(',').Append(EscapeCsv(taxInfo.Kingdom));
                    row.Append(',').Append(EscapeCsv(taxInfo.Phylum));
                    row.Append(',').Append(EscapeCsv(taxInfo.Class));
                    row.Append(',').Append(EscapeCsv(taxInfo.Order));
                    row.Append(',').Append(EscapeCsv(taxInfo.Family));
                    row.Append(',').Append(EscapeCsv(taxInfo.Genus));
                    row.Append(',').Append(EscapeCsv(taxInfo.Species));
                    row.Append(',').Append(EscapeCsv(taxInfo.ProteinCount));
                }
                else
                {
                    // Empty taxonomy columns if not found
                    row.Append(",,,,,,,,,");
                }

                // Write Combined test columns first (if present)
                if (hasCombined)
                {
                    var combinedResult = dbResults.FirstOrDefault(r =>
                        r.TestName == "Combined" && r.MetricName == "All");

                    if (combinedResult != null)
                    {
                        row.Append(',');
                        row.Append(combinedResult.PValue);
                        row.Append(',');
                        row.Append(combinedResult.QValue);
                        row.Append(',');
                        row.Append(combinedResult.IsSignificant() ? "TRUE" : "FALSE");
                        if (combinedResult.TestStatistic.HasValue)
                        {
                            row.Append(',');
                            row.Append(combinedResult.TestStatistic.Value);
                        }
                    }
                    else
                    {
                        row.Append(",0,0,FALSE");
                        if (Results.Any(r => r.TestName == "Combined" && r.TestStatistic.HasValue))
                        {
                            row.Append(",0");
                        }
                    }
                }

                // Write columns for each individual test-metric combination
                foreach (var (testName, metricName) in testMetricCombos)
                {
                    var result = dbResults.FirstOrDefault(r =>
                        r.TestName == testName && r.MetricName == metricName);

                    if (result != null)
                    {
                        row.Append(',');
                        row.Append(result.PValue);
                        row.Append(',');
                        row.Append(result.QValue);
                        row.Append(',');
                        row.Append(result.IsSignificant() ? "TRUE" : "FALSE");
                        if (result.TestStatistic.HasValue)
                        {
                            row.Append(',');
                            row.Append(result.TestStatistic.Value);
                        }
                    }
                    else
                    {
                        row.Append(",0,0,FALSE");
                        if (Results.Any(r => r.TestName == testName && r.MetricName == metricName && r.TestStatistic.HasValue))
                        {
                            row.Append(",0");
                        }
                    }
                }

                writer.WriteLine(row.ToString());
            }
        }
    }
}