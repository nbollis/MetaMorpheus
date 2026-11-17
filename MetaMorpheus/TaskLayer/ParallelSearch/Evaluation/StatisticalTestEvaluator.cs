#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.IO;
using TaskLayer.ParallelSearch.Statistics;
using TaskLayer.ParallelSearch.Statistics.Calibration;

namespace TaskLayer.ParallelSearch.Evaluation;

/// <summary>
/// The statistical test suite from the ManySearchTask branch as an evaluator. Each database's verdict is the overall
/// combined p-value (<c>Combined | All</c>) and its Benjamini–Hochberg q-value; the call is q ≤ alpha.
/// <para>
/// The official run uses targets only, so its results are the same whether or not twins were searched. With twins, a
/// second run over targets and twins together calibrates the tests: the twins' p-values go to the calibration report,
/// each twin gets its combined p-value from that run, and each target gets a twin-calibrated q-value from the same
/// run in <c>TwinCalibratedQValue</c>. Nothing else uses the twin rows.
/// </para>
/// </summary>
public sealed class StatisticalTestEvaluator : IDatabaseEvaluator, IWritesEvaluationFiles
{
    public const string EvaluatorName = "StatisticalTests";
    public const double DefaultAlpha = 0.05;

    public const string StatResultFileName = "StatisticalAnalysis_Results.csv";
    public const string TestSummaryFileName = "Test_Results.csv";
    public const string CalibrationReportFileName = "CalibrationReport.txt";

    public const string TestsRunColumn = "TestsRun";
    public const string TestsPassedColumn = "TestsPassed";
    public const string FamiliesRunColumn = "FamiliesRun";
    public const string FamiliesPassedColumn = "FamiliesPassed";
    public const string CalibrationRunPValueColumn = "CalibrationRunPValue";
    public const string TwinCalibratedQValueColumn = "TwinCalibratedQValue";
    public static string FamilyQValueColumn(StatisticalEvidenceFamily family) => $"{family}_CombinedQValue";

    private readonly StatisticalAnalysisPipeline _pipeline;
    private readonly Action<string> _warn;

    private StatisticalAnalysisRun? _officialRun;
    private List<TransientDatabaseMetrics>? _officialMetrics;
    private List<TwinPValueProfile>? _twinProfiles;

    /// <param name="createTests">Builds a fresh test suite for each run.</param>
    public StatisticalTestEvaluator(Func<IEnumerable<IStatisticalTest>> createTests, double alpha = DefaultAlpha,
        Action<string>? warn = null)
        : this(new StatisticalAnalysisPipeline(createTests, alpha), warn)
    {
    }

    public StatisticalTestEvaluator(StatisticalAnalysisPipeline pipeline, Action<string>? warn = null)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _warn = warn ?? (_ => { });
    }

    public string Name => EvaluatorName;
    public bool RequiresTwins => false;
    public double Alpha => _pipeline.Alpha;

    /// <summary>The official targets-only run from the last <see cref="Evaluate"/>; null before the first.</summary>
    public StatisticalAnalysisRun? OfficialRun => _officialRun;

    /// <summary>Per-test twin p-value profiles from the last <see cref="Evaluate"/>; null when there were no twins.</summary>
    public IReadOnlyList<TwinPValueProfile>? TwinProfiles => _twinProfiles;

    /// <summary>
    /// The suite the task runs: the same families as on the branch, minus de novo. Protein-group tests need
    /// parsimony and precursor-deconvolution tests need deconvolution, as their metrics are empty otherwise.
    /// </summary>
    public static IEnumerable<IStatisticalTest> CreateDefaultTests(bool includeProteinGroups, bool includePrecursorDeconvolution,
        bool includeDeNovo = false)
    {
        var builder = new TestSuiteBuilder()
            .AddCountEnrichmentTests()
            .AddAmbiguityOrTargetDecoyTests()
            .AddScoreDistributionTests()
            .AddRetentionTimeTests()
            .AddFragmentationTests();

        if (includeProteinGroups)
            builder.AddProteinGroupTests();
        if (includePrecursorDeconvolution)
            builder.AddPrecursorDeconvolutionTests();
        if (includeDeNovo)
            builder.AddDeNovoTests();

        return builder.Build();
    }

    public IReadOnlyList<DatabaseEvaluationResult> Evaluate(DatabaseEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _officialRun = null;
        _officialMetrics = null;
        _twinProfiles = null;

        var targets = input.Targets.ToList();
        var officialRun = _pipeline.Run(targets, _warn, updateMetrics: true);
        var officialCombined = officialRun.OverallCombinedByDatabase();
        var familyQ = officialRun.Results
            .Where(r => r.IsCombinedResult && r.IsDefined && r.EvidenceFamily.HasValue)
            .GroupBy(r => r.DatabaseName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToDictionary(r => r.EvidenceFamily!.Value, r => r.QValue), StringComparer.Ordinal);

        // Twin calibration run: same suite over targets and twins together, never written into the metrics
        Dictionary<string, StatisticalTestResult>? calibrationCombined = null;
        if (input.HasTwins)
        {
            var calibrationRun = _pipeline.Run(targets.Concat(input.Twins).ToList(), WarnForCalibrationRun, updateMetrics: false);
            calibrationCombined = calibrationRun.OverallCombinedByDatabase();
            var twinNames = new HashSet<string>(input.Twins.Select(t => t.DatabaseName), StringComparer.Ordinal);
            _twinProfiles = TwinPValueCalibration.Build(calibrationRun.Results, twinNames, Alpha);
        }

        double[]? twinCalibratedQ = null;
        if (calibrationCombined != null)
        {
            twinCalibratedQ = TwinCalibration.EmpiricalQValues(
                targets.Select(t => ScoreFrom(calibrationCombined, t.DatabaseName)).ToList(),
                input.Twins.Select(t => ScoreFrom(calibrationCombined, t.DatabaseName)).ToList());
        }

        var results = new List<DatabaseEvaluationResult>(targets.Count + input.Twins.Count);
        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            double p = PValueFrom(officialCombined, target.DatabaseName);
            double q = officialCombined.TryGetValue(target.DatabaseName, out var row) && row.IsDefined ? row.QValue : double.NaN;

            var details = new Dictionary<string, double>
            {
                [TestsRunColumn] = target.StatisticalTestsRun,
                [TestsPassedColumn] = target.StatisticalTestsPassed,
                [FamiliesRunColumn] = target.ValidFamilyCount,
                [FamiliesPassedColumn] = target.PassedFamilyCount,
            };
            if (familyQ.TryGetValue(target.DatabaseName, out var byFamily))
                foreach (var (family, familyQValue) in byFamily)
                    details[FamilyQValueColumn(family)] = familyQValue;
            if (calibrationCombined != null)
            {
                details[CalibrationRunPValueColumn] = PValueFrom(calibrationCombined, target.DatabaseName);
                details[TwinCalibratedQValueColumn] = twinCalibratedQ![i];
            }

            results.Add(new DatabaseEvaluationResult(target.DatabaseName, string.Empty, ToScore(p), p, q,
                !double.IsNaN(q) && q <= Alpha, details));
        }

        if (calibrationCombined != null)
        {
            foreach (var twin in input.Twins)
            {
                double p = PValueFrom(calibrationCombined, twin.DatabaseName);
                results.Add(new DatabaseEvaluationResult(twin.DatabaseName, twin.TwinOf, ToScore(p), p, double.NaN, false,
                    new Dictionary<string, double> { [CalibrationRunPValueColumn] = p }));
            }
        }

        _officialRun = officialRun;
        _officialMetrics = targets;
        return results;
    }

    public IReadOnlyList<string> WriteFiles(string outputFolder)
    {
        if (_officialRun == null || _officialMetrics == null)
            return [];

        Directory.CreateDirectory(outputFolder);
        var written = new List<string>();

        string statPath = Path.Combine(outputFolder, StatResultFileName);
        new StatisticalTestResultFile(Alpha) { Results = _officialRun.Results }
            .WriteResults(statPath, _officialMetrics.ToDictionary(m => m.DatabaseName, StringComparer.Ordinal));
        written.Add(statPath);

        string summaryPath = Path.Combine(outputFolder, TestSummaryFileName);
        new TestSummaryResultFile { Results = _officialRun.TestSummaries.Values.ToList() }.WriteResults(summaryPath);
        written.Add(summaryPath);

        string calibrationPath = Path.Combine(outputFolder, CalibrationReportFileName);
        string report = CalibrationReportWriter.FormatReport(_officialRun.Calibration);
        if (_twinProfiles != null)
            report += Environment.NewLine + TwinPValueCalibration.FormatSection(_twinProfiles, Alpha);
        File.WriteAllText(calibrationPath, report);
        written.Add(calibrationPath);

        return written;
    }

    /// <summary>Only errors from the calibration run are worth reporting; its notes repeat the official run's.</summary>
    private void WarnForCalibrationRun(string message)
    {
        if (message.StartsWith("Error", StringComparison.Ordinal))
            _warn($"Twin calibration run: {message}");
    }

    private static double PValueFrom(Dictionary<string, StatisticalTestResult> combined, string databaseName) =>
        combined.TryGetValue(databaseName, out var row) && row.IsDefined ? row.PValue : double.NaN;

    private static double ScoreFrom(Dictionary<string, StatisticalTestResult> combined, string databaseName) =>
        ToScore(PValueFrom(combined, databaseName));

    /// <summary>−log10 p, so a higher score is stronger evidence; p = 0 maps to the largest finite score.</summary>
    private static double ToScore(double pValue)
    {
        if (double.IsNaN(pValue))
            return double.NaN;
        return pValue <= 0 ? -Math.Log10(double.Epsilon) : -Math.Log10(pValue);
    }
}
