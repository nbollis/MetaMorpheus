#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskLayer.ParallelSearch.Evaluation;

/// <summary>
/// Proteome-level PEP: MetaMorpheus's PEP idea moved from PSMs to whole databases. A classifier learns to tell targets
/// from their twins on the raw-metric features (<see cref="DatabasePepModel"/>, cross-fitted). Twins are all null,
/// while targets are a mix of null and present (positive-unlabelled), so the classifier score is turned into a local
/// FDR (<see cref="LocalFdr"/>): PEP = π₀ · f_twin(s) / f_target(s). A database's q-value is the mean PEP of all
/// targets at least as good, and a target is called at q ≤ alpha.
/// <para>
/// The Percolator-style retraining in the plan (retrain on targets the competition calls) is not implemented.
/// </para>
/// </summary>
public sealed class ProteomePepEvaluator : IDatabaseEvaluator
{
    public const string EvaluatorName = "ProteomePep";
    public const double DefaultAlpha = 0.05;

    public const string PepColumn = "PEP";
    public const string ProbabilityColumn = "ClassifierProbability";
    public const string FoldColumn = "Fold";
    public const string Pi0Column = "Pi0";

    private readonly DatabasePepModel _model;

    public ProteomePepEvaluator(double alpha = DefaultAlpha, DatabasePepModel? model = null)
    {
        Alpha = alpha;
        _model = model ?? new DatabasePepModel();
    }

    public string Name => EvaluatorName;
    public bool RequiresTwins => true;
    public double Alpha { get; }

    public IReadOnlyList<DatabaseEvaluationResult> Evaluate(DatabaseEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var (targetFits, twinFits) = _model.CrossFit(input);
        var targetScores = targetFits.Select(f => f.Score).ToList();
        var twinScores = twinFits.Select(f => f.Score).ToList();

        double pi0 = LocalFdr.EstimatePi0(targetScores, twinScores);
        double[] peps = LocalFdr.PosteriorErrorProbabilities(targetScores, twinScores, pi0);
        double[] qValues = LocalFdr.QValuesFromPeps(peps);

        var results = new List<DatabaseEvaluationResult>(input.Targets.Count + input.Twins.Count);
        for (int i = 0; i < input.Targets.Count; i++)
        {
            var details = new Dictionary<string, double>
            {
                [PepColumn] = peps[i],
                [ProbabilityColumn] = targetFits[i].Probability,
                [FoldColumn] = targetFits[i].Fold,
                [Pi0Column] = pi0,
            };
            results.Add(new DatabaseEvaluationResult(input.Targets[i].DatabaseName, string.Empty, targetScores[i],
                double.NaN, qValues[i], qValues[i] <= Alpha, details));
        }

        for (int i = 0; i < input.Twins.Count; i++)
        {
            results.Add(DatabaseEvaluationResult.ScoreOnly(input.Twins[i].DatabaseName, input.Twins[i].TwinOf, twinScores[i],
                new Dictionary<string, double>
                {
                    [ProbabilityColumn] = twinFits[i].Probability,
                    [FoldColumn] = twinFits[i].Fold,
                }));
        }

        return results;
    }
}
