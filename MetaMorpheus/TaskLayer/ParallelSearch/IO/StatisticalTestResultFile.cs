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
using TaskLayer.ParallelSearch.Analysis;

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

                // Read summary columns
                var passedTestCount = csv.GetField<int>("PassedTestCount");
                var validTestCount = csv.GetField<int>("ValidTestCount");
                var passedFamilyCount = csv.GetField<int>("PassedFamilyCount");
                var validFamilyCount = csv.GetField<int>("ValidFamilyCount");
                var testPassedRatio = csv.GetField<double>("TestPassedRatio");
                var statisticalTestsPassed = csv.GetField<int>("StatisticalTestsPassed");
                var statisticalTestsRun = csv.GetField<int>("StatisticalTestsRun");
                var summaryAnomalyScore = csv.GetField<double>("SummaryAnomalyScore");
                var fullAnomalyScore = csv.GetField<double>("FullAnomalyScore");
                var anomalyRank = csv.GetField<int>("AnomalyRank");

            // Read each test result
            foreach (var testName in testColumns)
            {
                var pValueField = $"pValue_{testName}";
                var qValueField = $"qValue_{testName}";
                var isSignificantField = $"isSignificant_{testName}";
                var isDefinedField = $"isDefined_{testName}";
                var evidenceFamilyField = $"evidenceFamily_{testName}";
                var effectSizeField = $"effectSize_{testName}";
                var eligibilityReasonField = $"eligibilityReason_{testName}";
                var testStatField = $"testStatistic_{testName}";

                // Read values safely
                var pValueStr = csv.GetField(pValueField);
                var qValueStr = csv.GetField(qValueField);
                var isDefinedStr = headers.Contains(isDefinedField) ? csv.GetField(isDefinedField) : null;
                var evidenceFamilyStr = headers.Contains(evidenceFamilyField) ? csv.GetField(evidenceFamilyField) : null;
                var effectSizeStr = headers.Contains(effectSizeField) ? csv.GetField(effectSizeField) : null;
                var eligibilityReasonStr = headers.Contains(eligibilityReasonField) ? csv.GetField(eligibilityReasonField) : null;
                var statStr = csv.GetField(testStatField);

                if (string.IsNullOrWhiteSpace(pValueStr) || string.IsNullOrWhiteSpace(qValueStr))
                    continue;

                if (!double.TryParse(pValueStr, out var pValue) ||
                    !double.TryParse(qValueStr, out var qValue))
                    continue;

                double stat = double.NaN;
                double.TryParse(statStr, out stat);

                double effectSizeValue;
                bool hasEffectSize = double.TryParse(effectSizeStr, out effectSizeValue);

                var result = new StatisticalTestResult
                {
                    DatabaseName = databaseName,
                    TestName = testName,
                    MetricName = ExtractMetricName(testName),
                    EvidenceFamily = TryParseEvidenceFamily(evidenceFamilyStr),
                    IsDefined = string.IsNullOrWhiteSpace(isDefinedStr)
                        ? !double.IsNaN(pValue)
                        : bool.TryParse(isDefinedStr, out var isDefined) && isDefined,
                    EligibilityReason = string.IsNullOrWhiteSpace(eligibilityReasonStr) ? null : eligibilityReasonStr,
                    PValue = pValue,
                    QValue = qValue,
                    EffectSize = hasEffectSize ? effectSizeValue : null,
                    TestStatistic = stat,
                    AdditionalMetrics = new Dictionary<string, object>(),
                    PassedTestCount = passedTestCount,
                    ValidTestCount = validTestCount,
                    PassedFamilyCount = passedFamilyCount,
                    ValidFamilyCount = validFamilyCount,
                    TestPassedRatio = testPassedRatio,
                    StatisticalTestsPassed = statisticalTestsPassed,
                    StatisticalTestsRun = statisticalTestsRun,
                    SummaryAnomalyScore = summaryAnomalyScore,
                    FullAnomalyScore = fullAnomalyScore,
                    AnomalyRank = anomalyRank,
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

    private static StatisticalEvidenceFamily? TryParseEvidenceFamily(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Enum.TryParse<StatisticalEvidenceFamily>(value, out var family)
            ? family
            : null;
    }

    public override void WriteResults(string outputPath)
    {
        WriteResults(outputPath, null);
    }

    public void WriteResults(string outputPath, IReadOnlyDictionary<string, TransientDatabaseMetrics>? metricsDict)
    {
        // Group Results by database
        var resultsByDatabase = Results
            .GroupBy(r => r.DatabaseName)
            .OrderBy(g => g.Key)
            .ToList();

        // Get all unique test-metric combinations for column headers (excluding Combined)
        var testMetricCombos = Results
            .Where(r => !r.IsCombinedResult)
            .Select(r => (r.TestName, r.MetricName))
            .Distinct()
            .OrderBy(x => x.MetricName)
            .ThenBy(x => x.TestName)
            .ToList();

        var combinedMetricCombos = Results
            .Where(r => r.IsCombinedResult)
            .Select(r => (r.TestName, r.MetricName))
            .Distinct()
            .OrderBy(x => x.MetricName == "All" ? 1 : 0)
            .ThenBy(x => x.MetricName)
            .ToList();

        using (var writer = new StreamWriter(outputPath))
        {
            // Write header
            var header = new StringBuilder("DatabaseName,PassedTestCount,ValidTestCount,PassedFamilyCount,ValidFamilyCount,TestPassedRatio,StatisticalTestsPassed,StatisticalTestsRun");

            if (metricsDict != null)
                header.Append(",SummaryAnomalyScore,FullAnomalyScore,AnomalyRank");

            // Add taxonomy columns
            header.Append(",Organism,Kingdom,Phylum,Class,Order,Family,Genus,Species,ProteinCount");

            // Add Combined test columns first (if present)
            foreach (var (testName, metricName) in combinedMetricCombos)
            {
                header.Append($",isDefined_{testName}_{metricName},evidenceFamily_{testName}_{metricName},resultState_{testName}_{metricName},effectSize_{testName}_{metricName},eligibilityReason_{testName}_{metricName},pValue_{testName}_{metricName},qValue_{testName}_{metricName},isSignificant_{testName}_{metricName}");
                if (Results.Any(r => r.TestName == testName && r.MetricName == metricName && r.TestStatistic.HasValue))
                {
                    header.Append($",testStatistic_{testName}_{metricName}");
                }
            }

            // Add columns for individual test-metric combinations
            foreach (var (testName, metricName) in testMetricCombos)
            {
                header.Append($",isDefined_{testName}_{metricName},evidenceFamily_{testName}_{metricName},resultState_{testName}_{metricName},effectSize_{testName}_{metricName},eligibilityReason_{testName}_{metricName},pValue_{testName}_{metricName},qValue_{testName}_{metricName},isSignificant_{testName}_{metricName}");
                if (Results.Any(r => r.TestName == testName && r.MetricName == metricName && r.TestStatistic.HasValue))
                {
                    header.Append($",testStatistic_{testName}_{metricName}");
                }
            }

            writer.WriteLine(header.ToString());

            // Write data rows
            foreach (var dbGroup in resultsByDatabase.OrderByDescending(p => p.Where(t => t.EvidenceFamily.HasValue && t.IsSignificant(Alpha)).Select(t => t.EvidenceFamily!.Value).Distinct().Count())
                         .ThenByDescending(p => p.Count(t => t.IsSignificant(Alpha))))
            {
                string databaseName = dbGroup.Key;
                var dbResults = dbGroup.ToList();


                int testsRun = dbResults.Count(p => p.IsDefined && !p.IsCombinedResult);
                int testsPassed = dbResults.Count(r => !r.IsCombinedResult && r.IsSignificant(Alpha));
                int validFamilyCount = dbResults.Where(r => !r.IsCombinedResult && r.IsDefined && r.EvidenceFamily.HasValue)
                    .Select(r => r.EvidenceFamily!.Value)
                    .Distinct()
                    .Count();
                int passedFamilyCount = dbResults.Where(r => !r.IsCombinedResult && r.IsSignificant(Alpha) && r.EvidenceFamily.HasValue)
                    .Select(r => r.EvidenceFamily!.Value)
                    .Distinct()
                    .Count();
                double testPassedRatio = testsRun > 0 ? testsPassed / (double)testsRun : 0.0;

                var row = new StringBuilder(databaseName);
                row.Append($",{testsPassed},{testsRun},{passedFamilyCount},{validFamilyCount},{testPassedRatio},{testsPassed},{testsRun}");

                // Add anomaly scores if metrics dictionary provided
                if (metricsDict != null && metricsDict.TryGetValue(databaseName, out var metrics))
                {
                    row.Append(',');
                    row.Append(double.IsNaN(metrics.SummaryAnomalyScore) ? "NaN" : metrics.SummaryAnomalyScore.ToString());
                    row.Append(',');
                    row.Append(double.IsNaN(metrics.FullAnomalyScore) ? "NaN" : metrics.FullAnomalyScore.ToString());
                    row.Append(',');
                    row.Append(metrics.AnomalyRank >= 0 ? metrics.AnomalyRank.ToString() : "NaN");
                }

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
                foreach (var (testName, metricName) in combinedMetricCombos)
                {
                    var combinedResult = dbResults.FirstOrDefault(r =>
                        r.TestName == testName && r.MetricName == metricName);

                    if (combinedResult != null)
                    {
                        row.Append(',');
                        row.Append(combinedResult.IsDefined ? "TRUE" : "FALSE");
                        row.Append(',');
                        row.Append(EscapeCsv(combinedResult.EvidenceFamily?.ToString() ?? string.Empty));
                        row.Append(',');
                        row.Append(combinedResult.GetState(Alpha));
                        row.Append(',');
                        row.Append(combinedResult.EffectSize?.ToString() ?? string.Empty);
                        row.Append(',');
                        row.Append(EscapeCsv(combinedResult.EligibilityReason ?? string.Empty));
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
                        row.Append(",FALSE,,Undefined,,,0,0,FALSE");
                        if (Results.Any(r => r.TestName == testName && r.MetricName == metricName && r.TestStatistic.HasValue))
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
                        row.Append(result.IsDefined ? "TRUE" : "FALSE");
                        row.Append(',');
                        row.Append(EscapeCsv(result.EvidenceFamily?.ToString() ?? string.Empty));
                        row.Append(',');
                        row.Append(result.GetState(Alpha));
                        row.Append(',');
                        row.Append(result.EffectSize?.ToString() ?? string.Empty);
                        row.Append(',');
                        row.Append(EscapeCsv(result.EligibilityReason ?? string.Empty));
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
                        row.Append(",FALSE,,Undefined,,,0,0,FALSE");
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
