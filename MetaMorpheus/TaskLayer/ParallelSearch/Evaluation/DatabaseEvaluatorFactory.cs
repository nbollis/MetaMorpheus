#nullable enable
using System;
using System.Collections.Generic;
using EngineLayer;
using TaskLayer.ParallelSearch.Statistics;

namespace TaskLayer.ParallelSearch.Evaluation;

/// <summary>
/// The evaluators a parallel search runs at the end, each in its own class implementing
/// <see cref="IDatabaseEvaluator"/>:
/// <list type="bullet">
/// <item>isolation forest over size-normalised raw metrics (always; twin-calibrated q-values when twins exist),</item>
/// <item>the statistical test suite ported from the ManySearchTask branch (always; twins give a calibration run),</item>
/// <item>target-vs-twin competition at database level (twins only; skipped by the runner without them).</item>
/// </list>
/// </summary>
public static class DatabaseEvaluatorFactory
{
    public static List<IDatabaseEvaluator> Create(ParallelSearchParameters parameters, CommonParameters commonParameters,
        Action<string>? warn = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(commonParameters);

        return
        [
            new IsolationForestEvaluator(),
            new StatisticalTestEvaluator(() => CreateStatisticalTests(parameters, commonParameters), warn: warn),
            new TwinCompetitionEvaluator(),
        ];
    }

    /// <summary>
    /// The statistical test suite for these settings: protein-group tests when parsimony runs, deconvolution tests
    /// when precursors are deconvoluted, and de novo tests when de novo predictions are given.
    /// </summary>
    public static IEnumerable<IStatisticalTest> CreateStatisticalTests(ParallelSearchParameters parameters, CommonParameters commonParameters) =>
        StatisticalTestEvaluator.CreateDefaultTests(
            includeProteinGroups: parameters.DoParsimony,
            includePrecursorDeconvolution: commonParameters.DoPrecursorDeconvolution,
            includeDeNovo: parameters.UseDeNovoMapping);
}
