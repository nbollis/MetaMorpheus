#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TaskLayer.ParallelSearchTask.Analysis.Statistics;

namespace TaskLayer.ParallelSearchTask.Analysis;

/// <summary>
/// Orchestrates statistical analysis across all transient databases
/// </summary>
public class StatisticalAnalysisAggregator
{
    private readonly List<IStatisticalTest> _tests;
    private readonly bool _applyCombinedPValue;

    public StatisticalAnalysisAggregator(List<IStatisticalTest> tests, bool applyCombinedPValue = true)
    {
        _tests = tests ?? throw new ArgumentNullException(nameof(tests));
        _applyCombinedPValue = applyCombinedPValue;
    }

    /// <summary>
    /// Run all statistical tests on the collected results and return the statistical results
    /// </summary>
    public List<StatisticalResult> RunAnalysis(List<AggregatedAnalysisResult> allResults)
    {
        var statisticalResults = new List<StatisticalResult>();

        // Run each test
        foreach (var test in _tests)
        {
            if (!test.CanRun(allResults))
            {
                Console.WriteLine($"Skipping {test.TestName} - {test.MetricName}: insufficient data");
                continue;
            }

            try
            {
                var pValues = test.ComputePValues(allResults);

                // Create result objects
                foreach (var kvp in pValues)
                {
                    statisticalResults.Add(new StatisticalResult
                    {
                        DatabaseName = kvp.Key,
                        TestName = test.TestName,
                        MetricName = test.MetricName,
                        PValue = kvp.Value,
                        QValue = double.NaN // Will be computed after all tests
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running {test.TestName} - {test.MetricName}: {ex.Message}");
            }
        }

        // Apply Benjamini-Hochberg correction
        MultipleTestingCorrection.ApplyBenjaminiHochberg(statisticalResults);

        // Optionally compute combined p-values using Fisher's method
        if (_applyCombinedPValue)
        {
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
    /// Count how many tests each database passed (q < alpha), excluding the combined test
    /// </summary>
    public Dictionary<string, int> CountTestsPassed(List<StatisticalResult> results, double alpha = 0.05)
    {
        return results
            .Where(r => r.TestName != "Combined" && r.IsSignificant(alpha))
            .GroupBy(r => r.DatabaseName)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Export detailed statistical results to CSV in wide format (one row per database)
    /// </summary>
    public void WriteResultsToCsv(List<StatisticalResult> results, string outputPath)
    {
        // Group results by database
        var resultsByDatabase = results
            .GroupBy(r => r.DatabaseName)
            .OrderBy(g => g.Key)
            .ToList();

        if (resultsByDatabase.Count == 0)
        {
            Console.WriteLine("No statistical results to write.");
            return;
        }

        // Get all unique test-metric combinations for column headers
        var testMetricCombos = results
            .Select(r => (TestName: r.TestName, MetricName: r.MetricName))
            .Distinct()
            .OrderBy(x => x.TestName)
            .ThenBy(x => x.MetricName)
            .ToList();

        using var writer = new StreamWriter(outputPath);

        // Build header
        var header = new StringBuilder();
        header.Append("DatabaseName,StatisticalTestsPassed");

        foreach (var (testName, metricName) in testMetricCombos)
        {
            header.Append($",pValue_{testName}_{metricName}");
            header.Append($",qValue_{testName}_{metricName}");
            header.Append($",testStatistic_{testName}_{metricName}");
            header.Append($",isSignificant_{testName}_{metricName}");
        }

        writer.WriteLine(header.ToString());

        // Write data rows
        foreach (var dbGroup in resultsByDatabase)
        {
            string databaseName = dbGroup.Key;
            var dbResults = dbGroup.ToList();

            // Count tests passed (excluding Combined)
            int testsPassed = dbResults.Count(r => r.TestName != "Combined" && r.IsSignificant());

            var row = new StringBuilder();
            row.Append(databaseName);
            row.Append(',');
            row.Append(testsPassed);

            // Add columns for each test-metric combination
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
                    row.Append(result.TestStatistic?.ToString() ?? "NA");
                    row.Append(',');
                    row.Append(result.IsSignificant() ? "TRUE" : "FALSE");
                }
                else
                {
                    // No result for this test-metric combo
                    row.Append(",NA,NA,NA,FALSE");
                }
            }

            writer.WriteLine(row.ToString());
        }
    }
}