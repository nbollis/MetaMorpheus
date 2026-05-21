#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Executes all configured IStatisticalTest instances across a list of
/// TransientDatabaseMetrics results. Handles CanRun checks, parallel execution,
/// exception capture, warning-log output, and packages results into a
/// StatisticalTestExecutionResult for downstream processing.
/// </summary>
public sealed class StatisticalTestExecutor
{
    private readonly double _alpha;
    private readonly Action<string> _warn;

    public StatisticalTestExecutor(double alpha, Action<string> warn)
    {
        _alpha = alpha;
        _warn = warn ?? throw new ArgumentNullException(nameof(warn));
    }

    public StatisticalTestExecutionResult Execute(List<IStatisticalTest> tests, List<TransientDatabaseMetrics> searchResults)
    {
        int resultCount = searchResults.Count;
        var statisticalResults = new ConcurrentBag<StatisticalTestResult>();
        var testsToRemove = new ConcurrentBag<IStatisticalTest>();
        var resultsByDatabase = searchResults.ToDictionary(p => p.DatabaseName, p => p);

        Parallel.ForEach(tests, test =>
        {
            if (!test.CanRun(searchResults))
            {
                _warn($"Skipping {test.TestName} - {test.MetricName}: insufficient data");
                testsToRemove.Add(test);
                return;
            }

            try
            {
                Console.WriteLine($"Running {test.TestName} - {test.MetricName} on {resultCount} databases...");
                var pValues = test.RunTest(searchResults, _alpha);

                if (resultCount != pValues.Count)
                {
                    Debugger.Break();
                }

                if (test.SignificantResults >= resultCount / 10)
                {
                    _warn($"Warning: {test.TestName} - {test.MetricName} has excessive (>=10%) significant p-values.");
                    return;
                }

                if (test.SignificantResults == 0)
                {
                    _warn($"Warning: {test.TestName} - {test.MetricName} has no significant values.");
                    return;
                }

                foreach (var (dbName, pValue) in pValues)
                {
                    if (!resultsByDatabase.TryGetValue(dbName, out var result))
                    {
                        continue;
                    }

                    var isDefined = test.IsDefinedFor(result);
                    statisticalResults.Add(new StatisticalTestResult
                    {
                        DatabaseName = dbName,
                        TestName = test.TestName,
                        MetricName = test.MetricName,
                        EvidenceFamily = test.EvidenceFamily,
                        IsDefined = isDefined,
                        EligibilityReason = isDefined ? null : test.GetUndefinedReason(result),
                        PValue = pValue,
                        QValue = double.NaN,
                        TestStatistic = test.GetTestValue(result),
                        EffectSize = test.GetEffectSize(result, searchResults)
                    });
                }
            }
            catch (Exception ex)
            {
                var stackTrace = new StackTrace(ex, true);
                var frame = stackTrace.GetFrame(0);
                var lineNumber = frame?.GetFileLineNumber() ?? 0;
                var fileName = frame?.GetFileName() ?? "Unknown";

                _warn($"Error running {test.TestName} - {test.MetricName}: {ex.Message} at {fileName}:line {lineNumber}");
                testsToRemove.Add(test);
            }
        });

        return new StatisticalTestExecutionResult(statisticalResults.ToList(), testsToRemove.Distinct().ToList());
    }
}
