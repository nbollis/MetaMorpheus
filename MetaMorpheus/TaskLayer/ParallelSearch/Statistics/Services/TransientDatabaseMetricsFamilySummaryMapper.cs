#nullable enable
using System;
using TaskLayer.ParallelSearch.Analysis;

namespace TaskLayer.ParallelSearch.Statistics;

/// <summary>
/// Provides explicit property mapping for family-level summary fields on
/// TransientDatabaseMetrics. Replaces the earlier reflection-based approach
/// with a clear switch-on-enum for both best-p/q and combined-p/q writeback.
/// </summary>
public static class TransientDatabaseMetricsFamilySummaryMapper
{
    public static void SetFamilyBestSummary(
        TransientDatabaseMetrics metrics,
        StatisticalEvidenceFamily family,
        int validTests,
        int passedTests,
        double bestPValue,
        double bestQValue)
    {
        switch (family)
        {
            case StatisticalEvidenceFamily.CountEnrichment:
                metrics.CountEnrichmentValidTests = validTests;
                metrics.CountEnrichmentPassedTests = passedTests;
                metrics.CountEnrichmentBestPValue = bestPValue;
                metrics.CountEnrichmentBestQValue = bestQValue;
                break;
            case StatisticalEvidenceFamily.AmbiguityOrTargetDecoy:
                metrics.AmbiguityOrTargetDecoyValidTests = validTests;
                metrics.AmbiguityOrTargetDecoyPassedTests = passedTests;
                metrics.AmbiguityOrTargetDecoyBestPValue = bestPValue;
                metrics.AmbiguityOrTargetDecoyBestQValue = bestQValue;
                break;
            case StatisticalEvidenceFamily.ScoreDistribution:
                metrics.ScoreDistributionValidTests = validTests;
                metrics.ScoreDistributionPassedTests = passedTests;
                metrics.ScoreDistributionBestPValue = bestPValue;
                metrics.ScoreDistributionBestQValue = bestQValue;
                break;
            case StatisticalEvidenceFamily.Fragmentation:
                metrics.FragmentationValidTests = validTests;
                metrics.FragmentationPassedTests = passedTests;
                metrics.FragmentationBestPValue = bestPValue;
                metrics.FragmentationBestQValue = bestQValue;
                break;
            case StatisticalEvidenceFamily.RetentionTime:
                metrics.RetentionTimeValidTests = validTests;
                metrics.RetentionTimePassedTests = passedTests;
                metrics.RetentionTimeBestPValue = bestPValue;
                metrics.RetentionTimeBestQValue = bestQValue;
                break;
            case StatisticalEvidenceFamily.ProteinGroup:
                metrics.ProteinGroupValidTests = validTests;
                metrics.ProteinGroupPassedTests = passedTests;
                metrics.ProteinGroupBestPValue = bestPValue;
                metrics.ProteinGroupBestQValue = bestQValue;
                break;
            case StatisticalEvidenceFamily.DeNovo:
                metrics.DeNovoValidTests = validTests;
                metrics.DeNovoPassedTests = passedTests;
                metrics.DeNovoBestPValue = bestPValue;
                metrics.DeNovoBestQValue = bestQValue;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(family), family, null);
        }
    }

    public static void SetFamilyCombinedSummary(
        TransientDatabaseMetrics metrics,
        StatisticalEvidenceFamily family,
        double combinedPValue,
        double combinedQValue)
    {
        switch (family)
        {
            case StatisticalEvidenceFamily.CountEnrichment:
                metrics.CountEnrichmentCombinedPValue = combinedPValue;
                metrics.CountEnrichmentCombinedQValue = combinedQValue;
                break;
            case StatisticalEvidenceFamily.AmbiguityOrTargetDecoy:
                metrics.AmbiguityOrTargetDecoyCombinedPValue = combinedPValue;
                metrics.AmbiguityOrTargetDecoyCombinedQValue = combinedQValue;
                break;
            case StatisticalEvidenceFamily.ScoreDistribution:
                metrics.ScoreDistributionCombinedPValue = combinedPValue;
                metrics.ScoreDistributionCombinedQValue = combinedQValue;
                break;
            case StatisticalEvidenceFamily.Fragmentation:
                metrics.FragmentationCombinedPValue = combinedPValue;
                metrics.FragmentationCombinedQValue = combinedQValue;
                break;
            case StatisticalEvidenceFamily.RetentionTime:
                metrics.RetentionTimeCombinedPValue = combinedPValue;
                metrics.RetentionTimeCombinedQValue = combinedQValue;
                break;
            case StatisticalEvidenceFamily.ProteinGroup:
                metrics.ProteinGroupCombinedPValue = combinedPValue;
                metrics.ProteinGroupCombinedQValue = combinedQValue;
                break;
            case StatisticalEvidenceFamily.DeNovo:
                metrics.DeNovoCombinedPValue = combinedPValue;
                metrics.DeNovoCombinedQValue = combinedQValue;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(family), family, null);
        }
    }
}
