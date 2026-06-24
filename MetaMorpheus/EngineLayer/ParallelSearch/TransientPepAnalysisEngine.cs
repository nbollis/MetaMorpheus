#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Chromatography.RetentionTimePrediction;
using EngineLayer.FdrAnalysis;
using Microsoft.ML;
using Microsoft.ML.Data;

// The concrete trained-model type returned by the FastTree pipeline; aliased so the train-once / apply
// methods (this engine reuses one base-trained model across thousands of transient databases) stay readable.
using PepModel = Microsoft.ML.Data.TransformerChain<Microsoft.ML.Data.BinaryPredictionTransformer<Microsoft.ML.Calibrators.CalibratedModelParametersBase<Microsoft.ML.Trainers.FastTree.FastTreeBinaryModelParameters, Microsoft.ML.Calibrators.PlattCalibrator>>>;

namespace EngineLayer.ParallelSearch;

/// <summary>
/// ParallelSearch-specific PEP engine. The generic <see cref="PepAnalysisEngine"/> trains a fresh
/// cross-validated model per dataset; the transient databases searched by ParallelSearchTask are far too
/// small to support that. This derived engine trains ONE model on the large, decoy-rich base (human) search
/// and REUSES it — plus a snapshotted background PEP -&gt; PEP_QValue curve — to score every transient
/// database's hits out-of-sample, without retraining. Keeping this behavior here leaves the base engine
/// focused on the standard workflow (mirrors <see cref="TransientProteinParsimonyEngine"/> and
/// <see cref="TransientProteinScoringAndFdrEngine"/>).
/// </summary>
public sealed class TransientPepAnalysisEngine : PepAnalysisEngine
{
    private MLContext? _trainedContext;
    private PepModel? _trainedModel;

    // Background PEP -> PEP_QValue relationship, snapshotted from the base (human) search, which has
    // enough decoys to compute a real PEP-based q-value. Sorted by PEP ascending; PEP_QValue is monotone
    // non-decreasing along it. Transient peptides (far too small for their own PEP target/decoy) get their
    // PEP_QValue by looking up their MODEL PEP on this curve. This is deliberately PEP-based, NOT borrowed
    // from the score-based QValue: q-value and PEP_QValue rank matches differently and are not interchangeable.
    private double[] _bgPepAscPsm = Array.Empty<double>(), _bgQByPepPsm = Array.Empty<double>();
    private double[] _bgPepAscPep = Array.Empty<double>(), _bgQByPepPep = Array.Empty<double>();

    public TransientPepAnalysisEngine(List<SpectralMatch> psms, string searchType,
        List<(string fileName, CommonParameters fileSpecificParameters)> fileSpecificParameters,
        string outputFolder, IRetentionTimePredictor? rtPredictor = null)
        : base(psms, searchType, fileSpecificParameters, outputFolder, rtPredictor)
    {
    }

    /// <summary>True once <see cref="TrainSingleModelAndAssignBasePep"/> has produced a reusable model.</summary>
    public bool HasTrainedModel => _trainedModel != null;

    /// <summary>
    /// Snapshots the (PEP ascending, PEP_QValue) curve over <paramref name="matches"/>, which must already
    /// carry a PEP in GetFdrInfo(<paramref name="peptideLevel"/>).PEP and contain decoys. Computes the
    /// PEP-based q-value on this set, then captures the two monotone arrays. Mutates the matches' PEP_QValue
    /// and cumulative target/decoy (harmless — the score-based QValue lives in a different field).
    /// </summary>
    internal static (double[] pepAsc, double[] qByPep) BuildPepQValueCurve(List<SpectralMatch> matches, bool peptideLevel)
    {
        var ordered = matches.Where(m => m?.GetFdrInfo(peptideLevel) != null)
                             .OrderBy(m => m.GetFdrInfo(peptideLevel).PEP).ToList();
        if (ordered.Count == 0)
            return (Array.Empty<double>(), Array.Empty<double>());
        FdrAnalysisEngine.CalculateQValue(ordered, peptideLevelCalculation: peptideLevel, pepCalculation: true);
        var pepAsc = new double[ordered.Count];
        var qByPep = new double[ordered.Count];
        for (int i = 0; i < ordered.Count; i++)
        {
            pepAsc[i] = ordered[i].GetFdrInfo(peptideLevel).PEP;
            qByPep[i] = ordered[i].GetFdrInfo(peptideLevel).PEP_QValue;
        }
        return (pepAsc, qByPep);
    }

    /// <summary>
    /// Maps a single PEP onto the background curve: returns the PEP_QValue of the background match whose PEP
    /// is the smallest value &gt;= <paramref name="pep"/> (lower-bound). Because PEP_QValue is monotone in PEP,
    /// this is the q-value at that confidence level. Returns 2 (the FdrInfo sentinel) when no curve exists.
    /// </summary>
    internal static double LookupBackgroundPepQValue(double pep, double[] pepAsc, double[] qByPep)
    {
        if (pepAsc.Length == 0)
            return 2.0;
        int l = 0, r = pepAsc.Length;
        while (l < r)
        {
            int mid = (l + r) >> 1;
            if (pepAsc[mid] >= pep) r = mid; else l = mid + 1;
        }
        int idx = l < pepAsc.Length ? l : pepAsc.Length - 1;
        return qByPep[idx];
    }

    /// <summary>
    /// Trains ONE PEP model on all of this engine's PSMs (the base search) and assigns their PEP, then keeps
    /// the model + ML context so it can be REUSED — via <see cref="AssignPepFromTrainedModel"/> — to score
    /// out-of-sample PSMs (e.g. the per-database transient hits) WITHOUT retraining. The transient databases
    /// are far too small to train their own model and are out-of-sample relative to this one, so the
    /// cross-validation used by <see cref="PepAnalysisEngine.ComputePEPValuesForAllPSMs"/> is unnecessary for
    /// them. The feature dictionaries were already built from these PSMs in the constructor, so transient
    /// feature vectors are computed against the same (base-run) calibration. Returns false when the base PSMs
    /// lack both target and decoy training examples.
    /// </summary>
    public bool TrainSingleModelAndAssignBasePep()
    {
        List<SpectralMatchGroup> peptideGroups = UsePeptideLevelQValueForTraining
            ? SpectralMatchGroup.GroupByBaseSequence(AllPsms)
            : SpectralMatchGroup.GroupByIndividualPsm(AllPsms);
        var allIndices = Enumerable.Range(0, peptideGroups.Count).ToList();

        var psmData = CreatePsmData(SearchType, peptideGroups, allIndices).ToList();
        if (!psmData.Any(p => p.Label) || !psmData.Any(p => !p.Label))
            return false; // need both positive (target) and negative (decoy) examples

        var mlContext = new MLContext(seed: _randomSeed);
        var trainer = mlContext.BinaryClassification.Trainers.FastTree(BGDTreeOptions);
        var pipeline = mlContext.Transforms.Concatenate("Features", TrainingVariables).Append(trainer);
        PepModel model = pipeline.Fit(mlContext.Data.LoadFromEnumerable(psmData));
        _trainedContext = mlContext;
        _trainedModel = model;

        // Assign PEP to the base PSMs as well (the base assignment is for the base-search output only;
        // transient PSMs scored later are genuinely out-of-sample, so no out-of-fold concern applies).
        Compute_PSM_PEP(peptideGroups, allIndices, mlContext, model, SearchType, OutputFolder);

        // Snapshot the background PEP -> PEP_QValue curves used to assign transient PEP_QValue by lookup.
        // PSM-level: over all base PSMs. Peptide-level: over one representative (best-scoring) PSM per base
        // sequence, so the q-value reflects unique peptides. Both read PEP (identical in both FdrInfos).
        (_bgPepAscPsm, _bgQByPepPsm) = BuildPepQValueCurve(AllPsms, peptideLevel: false);
        var basePeptideReps = SpectralMatchGroup.GroupByBaseSequence(AllPsms)
            .Select(g => g.OrderByDescending(p => p.Score).First())
            .ToList();
        (_bgPepAscPep, _bgQByPepPep) = BuildPepQValueCurve(basePeptideReps, peptideLevel: false);
        return true;
    }

    /// <summary>
    /// Assigns PEP to a NEW set of PSMs (a transient database's hits) using the model trained on the base
    /// search by <see cref="TrainSingleModelAndAssignBasePep"/>. Reuses the base-trained model and the base
    /// feature dictionaries — no retraining. Safe to call from many databases concurrently. No-op until a
    /// model has been trained.
    /// </summary>
    public void AssignPepFromTrainedModel(List<SpectralMatch> psms, bool peptideLevel = false)
    {
        if (_trainedModel == null || psms == null)
            return;
        var scorable = psms.Where(p => p != null).ToList();
        if (scorable.Count == 0)
            return;
        // Compute_PSM_PEP writes BOTH PsmFdrInfo.PEP and PeptideFdrInfo.PEP. Transient PSMs may not have a
        // PeptideFdrInfo yet (peptide-level FDR isn't always run per database), so create whichever is
        // missing — otherwise PEP is silently never assigned (the old guard skipped these PSMs entirely).
        foreach (var p in scorable)
        {
            p.PsmFdrInfo ??= new FdrInfo();
            p.PeptideFdrInfo ??= new FdrInfo();
        }
        List<SpectralMatchGroup> groups = UsePeptideLevelQValueForTraining
            ? SpectralMatchGroup.GroupByBaseSequence(scorable)
            : SpectralMatchGroup.GroupByIndividualPsm(scorable);
        Compute_PSM_PEP(groups, Enumerable.Range(0, groups.Count).ToList(), _trainedContext!, _trainedModel, SearchType, OutputFolder);

        // Assign PEP_QValue by mapping each match's MODEL PEP through the BACKGROUND PEP->PEP_QValue curve.
        // The transient database is far too small for its own PEP target/decoy (few/no decoys), so we borrow
        // the relationship from the base search — but strictly on PEP, not on the score-based QValue, since
        // q-value and PEP_QValue rank matches differently and are not interchangeable.
        double[] pepAsc = peptideLevel ? _bgPepAscPep : _bgPepAscPsm;
        double[] qByPep = peptideLevel ? _bgQByPepPep : _bgQByPepPsm;
        foreach (var p in scorable)
        {
            var info = p.GetFdrInfo(peptideLevel);
            info.PEP_QValue = LookupBackgroundPepQValue(info.PEP, pepAsc, qByPep);
        }
    }
}
