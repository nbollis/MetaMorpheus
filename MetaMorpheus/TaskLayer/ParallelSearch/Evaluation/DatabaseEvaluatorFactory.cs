#nullable enable
using System;
using System.Collections.Generic;
using EngineLayer;

namespace TaskLayer.ParallelSearch.Evaluation;

/// <summary>
/// The evaluators a parallel search runs at the end, each in its own class implementing
/// <see cref="IDatabaseEvaluator"/>:
/// <list type="bullet">
/// <item>isolation forest over size-normalised raw metrics (always; twin-calibrated q-values when twins exist),</item>
/// <item>the statistical test suite ported from the ManySearchTask branch (always; twins give a calibration run),</item>
/// <item>target-vs-twin competition at database level (twins only; skipped by the runner without them),</item>
/// <item>proteome-level PEP, a cross-fitted targets-vs-twins classifier turned into local FDR (twins only).</item>
/// </list>
/// </summary>
public static class DatabaseEvaluatorFactory
{
    public static List<IDatabaseEvaluator> Create(ParallelSearchParameters parameters, CommonParameters commonParameters,
        Action<string>? warn = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(commonParameters);

        bool includeProteinGroups = parameters.DoParsimony;
        bool includePrecursorDeconvolution = commonParameters.DoPrecursorDeconvolution;

        return
        [
            new IsolationForestEvaluator(),
            new StatisticalTestEvaluator(
                () => StatisticalTestEvaluator.CreateDefaultTests(includeProteinGroups, includePrecursorDeconvolution),
                warn: warn),
            new TwinCompetitionEvaluator(),
            new ProteomePepEvaluator(),
        ];
    }
}
