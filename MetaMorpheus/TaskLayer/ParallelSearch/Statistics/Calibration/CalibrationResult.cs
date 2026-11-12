#nullable enable
using System;
using System.Collections.Generic;

namespace TaskLayer.ParallelSearch.Statistics.Calibration;

public sealed class CalibrationResult
{
    public int TotalDatabases { get; init; }
    public int NullBulkDatabaseCount { get; init; }
    public int DatabasesRemovedAsOutliers { get; init; }
    public int IterationsUsed { get; init; }
    public double Alpha { get; init; }

    public NullDistributionProfile? OverallTestPassCountProfile { get; init; }
    public NullDistributionProfile? OverallFamilyPassCountProfile { get; init; }
    public NullDistributionProfile? CombinedPValueProfile { get; init; }
    public NullDistributionProfile? CombinedQValueProfile { get; init; }

    public IReadOnlyDictionary<string, NullDistributionProfile> PerTestPValueProfiles { get; init; }
        = new Dictionary<string, NullDistributionProfile>();

    public IReadOnlyDictionary<string, NullDistributionProfile> PerTestEffectSizeProfiles { get; init; }
        = new Dictionary<string, NullDistributionProfile>();

    public IReadOnlyDictionary<string, NullDistributionProfile> PerTestStatisticProfiles { get; init; }
        = new Dictionary<string, NullDistributionProfile>();

    public IReadOnlyDictionary<StatisticalEvidenceFamily, NullDistributionProfile> PerFamilyTestPassCountProfiles { get; init; }
        = new Dictionary<StatisticalEvidenceFamily, NullDistributionProfile>();

    public NullDistributionProfile? PerFamilyPassCountProfile { get; init; }
}
