#nullable enable
using System.Collections.Generic;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Staged builder for StatisticalTestResult. Accumulates fields across the
/// test-execution and correction lifecycle, then produces an immutable result
/// via Build(). This replaces direct mutation of public setters, making the
/// data-flow contract between services explicit.
/// </summary>
public sealed class StatisticalTestResultBuilder
{
    private string _databaseName = string.Empty;
    private string _testName = string.Empty;
    private string _metricName = string.Empty;
    private StatisticalEvidenceFamily? _evidenceFamily;
    private bool _isDefined = true;
    private string? _eligibilityReason;
    private double _pValue = double.NaN;
    private double _qValue = double.NaN;
    private double? _testStatistic;
    private double? _effectSize;
    private Dictionary<string, object>? _additionalMetrics;

    public StatisticalTestResultBuilder WithDatabaseName(string databaseName)
    {
        _databaseName = databaseName;
        return this;
    }

    public StatisticalTestResultBuilder WithTestName(string testName)
    {
        _testName = testName;
        return this;
    }

    public StatisticalTestResultBuilder WithMetricName(string metricName)
    {
        _metricName = metricName;
        return this;
    }

    public StatisticalTestResultBuilder WithEvidenceFamily(StatisticalEvidenceFamily? evidenceFamily)
    {
        _evidenceFamily = evidenceFamily;
        return this;
    }

    public StatisticalTestResultBuilder WithIsDefined(bool isDefined)
    {
        _isDefined = isDefined;
        return this;
    }

    public StatisticalTestResultBuilder WithEligibilityReason(string? eligibilityReason)
    {
        _eligibilityReason = eligibilityReason;
        return this;
    }

    public StatisticalTestResultBuilder WithPValue(double pValue)
    {
        _pValue = pValue;
        return this;
    }

    public StatisticalTestResultBuilder WithQValue(double qValue)
    {
        _qValue = qValue;
        return this;
    }

    public StatisticalTestResultBuilder WithTestStatistic(double? testStatistic)
    {
        _testStatistic = testStatistic;
        return this;
    }

    public StatisticalTestResultBuilder WithEffectSize(double? effectSize)
    {
        _effectSize = effectSize;
        return this;
    }

    public StatisticalTestResultBuilder WithAdditionalMetrics(Dictionary<string, object>? additionalMetrics)
    {
        _additionalMetrics = additionalMetrics;
        return this;
    }

    public StatisticalTestResult Build()
    {
        return new StatisticalTestResult
        {
            DatabaseName = _databaseName,
            TestName = _testName,
            MetricName = _metricName,
            EvidenceFamily = _evidenceFamily,
            IsDefined = _isDefined,
            EligibilityReason = _eligibilityReason,
            PValue = _pValue,
            QValue = _qValue,
            TestStatistic = _testStatistic,
            EffectSize = _effectSize,
            AdditionalMetrics = _additionalMetrics ?? new Dictionary<string, object>()
        };
    }
}
