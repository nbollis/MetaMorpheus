#nullable enable
using EngineLayer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskLayer.ParallelSearchTask.Analysis;
using TaskLayer.ParallelSearchTask.Util;

namespace TaskLayer.ParallelSearchTask.Statistics;

/// <summary>
/// Orchestrates statistical analysis across all transient databases
/// Calculates p-values on-demand from aggregated results at the end of the run
/// No per-database caching of p-values; all calculations deferred until finalization
/// </summary>
public class StatisticalAnalysisAggregator(List<IStatisticalTest> tests, bool applyCombinedPValue = true)
{
    private readonly List<IStatisticalTest> _tests = tests ?? throw new ArgumentNullException(nameof(tests));
    public int TestCount => _tests.Count;

    /// <summary>
    /// Finalize analysis by computing p-values and q-values across ALL databases
    /// This is called after all databases have been analyzed and results are cached
    /// </summary>
    public List<StatisticalResult> FinalizeAnalysis(List<AggregatedAnalysisResult> allResults, double alpha = 0.05)
    {
        if (allResults == null || allResults.Count == 0)
        {
            Console.WriteLine("Warning: No results to finalize statistical analysis");
            return new List<StatisticalResult>();
        }

        Console.WriteLine($"Finalizing statistical analysis for {allResults.Count} databases...");

        // Compute p-values by running all tests
        var statisticalResults = ComputePValuesForAllDatabases(allResults, alpha);

        // Apply Benjamini-Hochberg correction across ALL results
        Console.WriteLine($"Applying Benjamini-Hochberg FDR correction to {statisticalResults.Count} test results");
        MultipleTestingCorrection.ApplyBenjaminiHochberg(statisticalResults);

        // Optionally compute combined p-values 
        if (applyCombinedPValue)
        {
            Console.WriteLine("Computing combined p-values");
            var combinedPValues = MetaAnalysis.CombinePValuesAcrossTests(statisticalResults);
            var combinedQValues = MultipleTestingCorrection.BenjaminiHochberg(combinedPValues);

            // Add combined results
            foreach (var dbName in combinedPValues.Keys)
            {
                statisticalResults.Add(new StatisticalResult
                {
                    DatabaseName = dbName,
                    TestName = "Combined",
                    MetricName = "All",
                    PValue = combinedPValues[dbName],
                    QValue = combinedQValues[dbName]
                });
            }
        }

        return statisticalResults;
    }

    /// <summary>
    /// Compute p-values for all tests across all databases
    /// Tests are only run if they can execute on the provided data
    /// </summary>
    private List<StatisticalResult> ComputePValuesForAllDatabases(List<AggregatedAnalysisResult> allResults, double alpha = 0.05)
    {
        int resultCount = allResults.Count;
        var statisticalResults = new ConcurrentBag<StatisticalResult>();
        var toRemove = new ConcurrentBag<IStatisticalTest>();

        // Run each test on all databases
        Parallel.ForEach(_tests, test =>
        {
            if (!test.CanRun(allResults))
            {
                Console.WriteLine($"Skipping {test.TestName} - {test.MetricName}: insufficient data");
                toRemove.Add(test);
                return;
            }

            try
            {
                Console.WriteLine($"Running {test.TestName} - {test.MetricName} on {resultCount} databases...");
                var pValues = test.RunTest(allResults);

                if (resultCount != pValues.Count)
                    Debugger.Break();

                // Reject tests if they are bad (many sig findings). 
                if (test.SignificantResults >= resultCount / 2)
                {
                    toRemove.Add(test);
                    Warn($"Removing {test.TestName} - {test.MetricName} due to excessive (>=50%) significant p-values.");
                    return;
                }

                // Reject tests if they are bad (many non-significant findings).
                if (resultCount - test.SignificantResults >= resultCount * 0.99999)
                {
                    toRemove.Add(test);
                    Warn($"Removing {test.TestName} - {test.MetricName} due to excessive (>=99.999%) 1 p-values.");
                    return;
                }

                // Convert p-values to StatisticalResult format
                HashSet<AggregatedAnalysisResult> unmapped = allResults.ToHashSet();
                foreach (var (dbName, pValue) in pValues)
                {
                    var result = unmapped.First(r => r.DatabaseName == dbName);
                    unmapped.Remove(result);

                    var testStat = test.GetTestValue(result);
                    statisticalResults.Add(new StatisticalResult
                    {
                        DatabaseName = dbName,
                        TestName = test.TestName,
                        MetricName = test.MetricName,
                        PValue = pValue,
                        QValue = double.NaN, // Will be filled by Benjamini-Hochberg
                        TestStatistic = testStat
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running {test.TestName} - {test.MetricName}: {ex.Message}");
            }
        });

        foreach (var test in toRemove)
        {
            _tests.Remove(test);
        }

        return statisticalResults.ToList();
    }

    #region Events

    public static event EventHandler<StringEventArgs>? WarnHandler;

    public static event EventHandler<StringEventArgs>? LogHandler;

    protected static void Warn(string v)
    {
        WarnHandler?.Invoke(null, new StringEventArgs(v, null));
    }

    protected void Log(string v, List<string> nestedIds)
    {
        LogHandler?.Invoke(this, new StringEventArgs(v, nestedIds));
    }

    #endregion

    /// <summary>
    /// Export detailed statistical results to CSV in wide format (one row per database)
    /// Includes taxonomy information if available from embedded taxonomy resources
    /// </summary>
    public static void WriteResultsToCsv(List<StatisticalResult> results, string outputPath, double alpha = 0.05)
    {
        if (!results.Any())
        {
            Console.WriteLine("No statistical results to write.");
            return;
        }

        // Group results by database
        var resultsByDatabase = results
            .GroupBy(r => r.DatabaseName)
            .OrderBy(g => g.Key)
            .ToList();

        // Get all unique test-metric combinations for column headers (excluding Combined)
        var testMetricCombos = results
            .Where(r => r.TestName != "Combined")
            .Select(r => (r.TestName, r.MetricName))
            .Distinct()
            .OrderBy(x => x.TestName)
            .ThenBy(x => x.MetricName)
            .ToList();

        // Check if there's a Combined test
        bool hasCombined = results.Any(r => r.TestName == "Combined");

        using (var writer = new StreamWriter(outputPath))
        {
            // Write header
            var header = new StringBuilder("DatabaseName,StatisticalTestsPassed");

            // Add taxonomy columns
            header.Append(",Organism,Kingdom,Phylum,Class,Order,Family,Genus,Species,ProteinCount");

            // Add Combined test columns first (if present)
            if (hasCombined)
            {
                header.Append(",pValue_Combined_All,qValue_Combined_All,isSignificant_Combined_All");
                if (results.Any(r => r.TestName == "Combined" && r.TestStatistic.HasValue))
                {
                    header.Append(",testStatistic_Combined_All");
                }
            }

            // Add columns for individual test-metric combinations
            foreach (var (testName, metricName) in testMetricCombos)
            {
                header.Append($",pValue_{testName}_{metricName},qValue_{testName}_{metricName},isSignificant_{testName}_{metricName}");
                if (results.Any(r => r.TestName == testName && r.MetricName == metricName && r.TestStatistic.HasValue))
                {
                    header.Append($",testStatistic_{testName}_{metricName}");
                }
            }

            writer.WriteLine(header.ToString());

            // Write data rows
            foreach (var dbGroup in resultsByDatabase)
            {
                string databaseName = dbGroup.Key;
                var dbResults = dbGroup.ToList();

                // Count tests passed (excluding Combined)
                int testsPassed = dbResults.Count(r => r.TestName != "Combined" && r.IsSignificant());

                var row = new StringBuilder(databaseName);
                row.Append(',');
                row.Append(testsPassed);

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
                        if (results.Any(r => r.TestName == "Combined" && r.TestStatistic.HasValue))
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
                        if (results.Any(r => r.TestName == testName && r.MetricName == metricName && r.TestStatistic.HasValue))
                        {
                            row.Append(",0");
                        }
                    }
                }

                writer.WriteLine(row.ToString());
            }
        }

        Console.WriteLine($"Wrote {resultsByDatabase.Count} database results to {outputPath}");
    }

    /// <summary>
    /// Escape CSV fields that contain special characters
    /// </summary>
    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // If the value contains comma, quote, or newline, wrap in quotes and escape internal quotes
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}