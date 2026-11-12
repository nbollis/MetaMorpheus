#nullable enable
using System.Collections.Generic;

namespace TaskLayer.ParallelSearch.Evaluation;

/// <summary>
/// The evaluators a parallel search runs at the end. Planned, each in its own class implementing
/// <see cref="IDatabaseEvaluator"/>:
/// <list type="bullet">
/// <item>isolation forest over size-normalised metrics (always; twin-calibrated when twins exist),</item>
/// <item>the statistical test suite ported from the ManySearchTask branch (always; twins give a calibration run),</item>
/// <item>target-vs-twin competition at database level (twins only),</item>
/// <item>proteome-level PEP, a targets-vs-twins classifier (twins only).</item>
/// </list>
/// None is implemented yet, so the list is empty and no evaluation files are written.
/// </summary>
public static class DatabaseEvaluatorFactory
{
    public static List<IDatabaseEvaluator> Create(ParallelSearchParameters parameters)
    {
        var evaluators = new List<IDatabaseEvaluator>();
        return evaluators;
    }
}
