using System;
using System.Collections.Generic;
using System.Linq;
using Chromatography.RetentionTimePrediction;
using Chromatography.RetentionTimePrediction.Chronologer;
using EngineLayer;
using MathNet.Numerics.Statistics;

namespace TaskLayer.ParallelSearch.Analysis.Analyzers;

public class RetentionTimeAnalyzer : ITransientDatabaseAnalyzer
{
    private static RetentionTimePredictor _predictor = new ChronologerRetentionTimePredictor();

    public const string PsmMeanAbsoluteRtError = "PsmMeanAbsoluteRtError";
    public const string PsmRtCorrelationCoefficient = "PsmRtCorrelationCoefficient";
    public const string PsmAllRtErrors = "PsmAllRtErrors";
    public const string PeptideMeanAbsoluteRtError = "PeptideMeanAbsoluteRtError";
    public const string PeptideRtCorrelationCoefficient = "PeptideRtCorrelationCoefficient";
    public const string PeptideAllRtErrors = "PeptideAllRtErrors";

    public string AnalyzerName => "RetentionTime";
    public IEnumerable<string> GetOutputColumns()
    {
        yield return PsmMeanAbsoluteRtError;
        yield return PsmRtCorrelationCoefficient;
        yield return PsmAllRtErrors;
        yield return PeptideMeanAbsoluteRtError;
        yield return PeptideRtCorrelationCoefficient;
        yield return PeptideAllRtErrors;
    }

    public Dictionary<string, object> Analyze(TransientDatabaseAnalysisContext context)
    {
        double qValueThreshold = Math.Min(context.CommonParameters.QValueThreshold, context.CommonParameters.PepQValueThreshold);
        var cache = new Dictionary<string, double>();

        // Psms
        var confidentPsms = context.TransientPsms
            .Where(p => p.FdrInfo.QValue <= qValueThreshold)
            .ToList();

        List<double> allPsmRtErrors = new();
        List<double> psmObservedRts = new();
        List<double> psmPredictedRts = new();

        foreach (var psm in confidentPsms)
        {
            double observedRt = psm.ScanRetentionTime;
            
            foreach (var hypothesis in psm.BestMatchingBioPolymersWithSetMods)
            {
                if (hypothesis.IsDecoy)
                    continue;

                if (!cache.TryGetValue(hypothesis.SpecificBioPolymer.FullSequence, out double predictedRt))
                {
                    double? predicted = _predictor.PredictRetentionTime(hypothesis.SpecificBioPolymer as IRetentionPredictable, out var failureReason);

                    if (predicted is null)
                        continue;

                    predictedRt = predicted.Value;
                    cache[hypothesis.SpecificBioPolymer.FullSequence] = predictedRt;
                }

                if (predictedRt > 0) // Only include valid predictions
                {
                    double rtError = observedRt - predictedRt;
                    allPsmRtErrors.Add(rtError);
                    psmObservedRts.Add(observedRt);
                    psmPredictedRts.Add(predictedRt);
                }
                
                break; // Only use first hypothesis for RT prediction
            }
        }

        // Peptides
        var confidentPeptides = context.TransientPeptides
            .Where(p => p.FdrInfo.QValue <= qValueThreshold)
            .ToList();

        List<double> allPeptideRtErrors = new();
        List<double> peptideObservedRts = new();
        List<double> peptidePredictedRts = new();

        foreach (var peptide in confidentPeptides)
        {
            double observedRt = peptide.ScanRetentionTime;
            
            foreach (var hypothesis in peptide.BestMatchingBioPolymersWithSetMods)
            {
                if (hypothesis.IsDecoy)
                    continue;
                if (!cache.TryGetValue(hypothesis.SpecificBioPolymer.FullSequence, out double predictedRt))
                {
                    double? predicted = _predictor.PredictRetentionTime(hypothesis.SpecificBioPolymer as IRetentionPredictable, out var failureReason);

                    if (predicted is null)
                        continue;

                    predictedRt = predicted.Value;
                    cache[hypothesis.SpecificBioPolymer.FullSequence] = predictedRt;
                }

                if (predictedRt > 0) // Only include valid predictions
                {
                    double rtError = observedRt - predictedRt;
                    allPeptideRtErrors.Add(rtError);
                    peptideObservedRts.Add(observedRt);
                    peptidePredictedRts.Add(predictedRt);
                }
                
                break; // Only use first hypothesis for RT prediction
            }
        }

        // Calculate statistics
        double psmMeanAbsoluteError = allPsmRtErrors.Any() ? allPsmRtErrors.Select(Math.Abs).Mean() : 0;
        double psmCorrelation = psmObservedRts.Count > 1 ? Correlation.Pearson(psmObservedRts, psmPredictedRts) : 0;
        
        double peptideMeanAbsoluteError = allPeptideRtErrors.Any() ? allPeptideRtErrors.Select(Math.Abs).Mean() : 0;
        double peptideCorrelation = peptideObservedRts.Count > 1 ? Correlation.Pearson(peptideObservedRts, peptidePredictedRts) : 0;

        return new Dictionary<string, object>
        {
            [PsmMeanAbsoluteRtError] = psmMeanAbsoluteError,
            [PsmRtCorrelationCoefficient] = psmCorrelation,
            [PsmAllRtErrors] = allPsmRtErrors.ToArray(),
            [PeptideMeanAbsoluteRtError] = peptideMeanAbsoluteError,
            [PeptideRtCorrelationCoefficient] = peptideCorrelation,
            [PeptideAllRtErrors] = allPeptideRtErrors.ToArray()
        };
    }

    public bool CanAnalyze(TransientDatabaseAnalysisContext context)
    {
        return context.AllPsms != null
               && context.TransientPsms != null
               && context.TransientPsms.All(p => p is PeptideSpectralMatch)
               && context.AllPeptides != null
               && context.TransientPeptides != null
               && context.TransientPeptides.All(p => p is PeptideSpectralMatch);
    }
}
